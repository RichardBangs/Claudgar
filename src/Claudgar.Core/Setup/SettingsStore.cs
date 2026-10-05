using System.Text.Json;

namespace Claudgar.Core.Setup;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string FilePath { get; }

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Claudgar", "settings.json");
    }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(new());
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(SafeFiles.ReadLimited(FilePath, 1024 * 1024), JsonOptions);
            if (settings is null || settings.SchemaVersion != 1 || settings.Port is < 1024 or > 65535 ||
                settings.GameDirectories is null || settings.GameDirectories.Count > 32)
                return new(new(), "Settings are invalid or from an unsupported version. The original file was preserved.");
            return new(settings);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(new(), "Settings could not be read. The original file was preserved: " + error.Message);
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Port is < 1024 or > 65535 || settings.GameDirectories is null || settings.GameDirectories.Count > 32)
            throw new ArgumentException("Choose a port from 1024 through 65535 and at most 32 game folders.", nameof(settings));
        if (File.Exists(FilePath))
        {
            // Never turn a failed read into an automatic reset of the user's configuration.
            var previous = Load();
            if (previous.Warning is not null) throw new IOException(previous.Warning);
            settings = settings with { AdditionalSettings = previous.Settings.AdditionalSettings };
        }
        SafeFiles.WriteAtomic(FilePath, JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions), backup: true);
    }
}
