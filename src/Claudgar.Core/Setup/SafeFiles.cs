using System.Security.Cryptography;

namespace Claudgar.Core.Setup;

internal static class SafeFiles
{
    public static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    public static string HashFile(string path) => Hash(ReadLimited(path, 16 * 1024 * 1024));

    public static byte[] ReadLimited(string path, int maximumBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes) throw new IOException("The file is too large: " + path);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (output.Length + read > maximumBytes) throw new IOException("The file grew beyond its size limit: " + path);
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    public static void RejectReparsePoints(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Setup will not write through a symbolic link or directory junction: " + current);
        }
    }

    public static string UnderDirectory(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new ArgumentException("A packaged filename must be relative.", nameof(relativePath));
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A packaged filename escapes its installation folder.", nameof(relativePath));
        return full;
    }

    public static void WriteAtomic(string path, byte[] content, bool backup)
    {
        RejectReparsePoints(path);
        var parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, ".claudgar-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            if (backup && File.Exists(path))
                File.Copy(path, path + ".claudgar-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N"), false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
