namespace Claudgar.Core.Setup;

/// <summary>Stages and verifies a configuration update, then preserves the replaced file as its backup.</summary>
internal static class GuardedConfigurationWriter
{
    public static void Write(string path, byte[] content, byte[]? expectedOriginal, int maximumBytes)
    {
        SafeFiles.RejectReparsePoints(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".claudgar-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var staged = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 8192, FileOptions.WriteThrough))
            {
                staged.Write(content);
                staged.Flush(flushToDisk: true);
            }
            if (!SafeFiles.ReadLimited(temporary, maximumBytes).AsSpan().SequenceEqual(content))
                throw new IOException("The staged configuration did not match the intended bytes. The existing file was not replaced.");

            SafeFiles.RejectReparsePoints(path);
            if (expectedOriginal is null)
            {
                // Do not overwrite a file created by the chat app after our initial read.
                File.Move(temporary, path, overwrite: false);
                return;
            }

            // Hold a read handle that denies in-place writes while checking and replacing.
            // Other programs can still replace files by rename, so do not claim a cross-process transaction.
            using var current = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (current.Length != expectedOriginal.Length)
                throw Changed();
            var currentBytes = new byte[expectedOriginal.Length];
            current.ReadExactly(currentBytes);
            if (!currentBytes.AsSpan().SequenceEqual(expectedOriginal))
                throw Changed();

            var backup = path + ".claudgar-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
            // Backup and replacement are one filesystem operation, rather than a copy followed by an overwrite.
            File.Replace(temporary, path, backup);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static IOException Changed() => new("The configuration changed during setup. It was preserved; try setup again.");
}
