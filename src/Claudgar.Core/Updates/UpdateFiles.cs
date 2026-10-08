using System.Security.Cryptography;
using Claudgar.Core.Setup;

namespace Claudgar.Core.Updates;

public static class UpdateFiles
{
    public static bool IsSha256(string? digest) => digest is { Length: 64 } && digest.All(Uri.IsHexDigit);

    public static (string Sha256, long Size) Hash(string path)
    {
        SafeFiles.RejectReparsePoints(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > GitHubReleaseSource.MaximumAssetBytes) throw new IOException("The update executable has an invalid size.");
        return (Convert.ToHexStringLower(SHA256.HashData(stream)), stream.Length);
    }

    public static void Verify(string path, string sha256, long size)
    {
        if (!IsSha256(sha256)) throw new IOException("The update digest is invalid.");
        var actual = Hash(path);
        if (actual.Size != size || !actual.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The update did not pass its SHA-256 verification. The current app was preserved.");
    }

    public static void RejectLinks(string path) => SafeFiles.RejectReparsePoints(path);

    internal static void WriteAtomic(string path, byte[] content) => SafeFiles.WriteAtomic(path, content, backup: false);
}
