using System.Globalization;

namespace Claudgar.Core.Updates;

public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (text is null) return false;
        if (text.StartsWith('v')) text = text[1..];
        var parts = text.Split('.');
        if (parts.Length != 3 || parts.Any(part => part.Length == 0 ||
                (part.Length > 1 && part[0] == '0') || part.Any(c => c is < '0' or > '9')))
            return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) return false;
        version = new(major, minor, patch);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result == 0) result = Minor.CompareTo(other.Minor);
        return result == 0 ? Patch.CompareTo(other.Patch) : result;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record ReleaseAsset(ReleaseVersion Version, Uri DownloadUri, string Sha256, long Size);
public enum UpdateState { Idle, Checking, Downloading, Ready, Failed }
public sealed record UpdateStatus(UpdateState State, string? Version = null, string? Message = null)
{
    public bool Ready => State == UpdateState.Ready;
}

public enum UpdatePhase { Ready, Applying, AwaitingHealth }

// Each portable executable has its own journal. No game or chatbot settings live here.
public sealed record PendingUpdate
{
    public int SchemaVersion { get; init; } = 1;
    public required string Token { get; init; }
    public required string TargetPath { get; init; }
    public required string PreviousVersion { get; init; }
    public required string Version { get; init; }
    public required string Sha256 { get; init; }
    public required long Size { get; init; }
    public UpdatePhase Phase { get; init; }
    public string? PreviousSha256 { get; init; }
    public long PreviousSize { get; init; }
    public bool StartInTray { get; init; }
}

public interface IReleaseSource
{
    Task<ReleaseAsset?> GetLatestAsync(CancellationToken cancellationToken);
    Task<Stream> OpenDownloadAsync(ReleaseAsset asset, CancellationToken cancellationToken);
}

public interface IUpdateExecutableValidator
{
    void Validate(string path, ReleaseVersion expectedVersion);
}
