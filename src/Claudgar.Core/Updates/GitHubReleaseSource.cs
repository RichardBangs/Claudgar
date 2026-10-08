using System.Net;
using System.Text.Json;

namespace Claudgar.Core.Updates;

public sealed class GitHubReleaseSource(HttpClient httpClient) : IReleaseSource
{
    public const string Repository = "RichardBangs/Claudgar";
    public const string AssetName = "Claudgar.exe";
    public const long MaximumAssetBytes = 512L * 1024 * 1024;
    public static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Repository}/releases/latest");

    public async Task<ReleaseAsset?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.UserAgent.ParseAdd("Claudgar-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) != 0)
        {
            if (buffer.Length + count > 1024 * 1024) throw new IOException("The release response is too large.");
            buffer.Write(bytes, 0, count);
        }
        using var document = JsonDocument.Parse(buffer.ToArray());
        return ParseRelease(document.RootElement);
    }

    public static ReleaseAsset? ParseRelease(JsonElement release)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean() ||
            release.GetProperty("published_at").ValueKind != JsonValueKind.String ||
            !ReleaseVersion.TryParse(release.GetProperty("tag_name").GetString(), out var version)) return null;
        var assets = release.GetProperty("assets").EnumerateArray()
            .Where(asset => asset.GetProperty("name").GetString() == AssetName).ToArray();
        if (assets.Length != 1) throw new IOException("This release does not contain one Windows download named Claudgar.exe.");
        var asset = assets[0];
        if (asset.GetProperty("state").GetString() != "uploaded") throw new IOException("The release download is not ready.");
        var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !UpdateFiles.IsSha256(digest[7..]))
            throw new IOException("The release is missing GitHub's SHA-256 digest. The current app will keep running.");
        var size = asset.GetProperty("size").GetInt64();
        if (size is <= 0 or > MaximumAssetBytes) throw new IOException("The release download has an invalid size.");
        var expectedUrl = $"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(release.GetProperty("tag_name").GetString()!)}/{AssetName}";
        if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.AbsoluteUri, expectedUrl, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The release download does not belong to Claudgar's GitHub repository.");
        return new(version, uri, digest[7..].ToLowerInvariant(), size);
    }

    public async Task<Stream> OpenDownloadAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUri);
        request.Headers.UserAgent.ParseAdd("Claudgar-Updater/1.0");
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != asset.Size)
                throw new IOException("The release download has an unexpected length.");
            return new ResponseStream(await response.Content.ReadAsStreamAsync(cancellationToken), response);
        }
        catch { response.Dispose(); throw; }
    }

    private sealed class ResponseStream(Stream stream, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => stream.ReadAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { stream.Dispose(); response.Dispose(); }
            base.Dispose(disposing);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
