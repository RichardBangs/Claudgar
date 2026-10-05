using Claudgar.Core.Data;

namespace Claudgar.Core.Exports;

/// <summary>
/// Compares export file metadata without opening or parsing saved game data. Call from one
/// thread, then accept the captured state only after the corresponding refresh succeeds.
/// </summary>
public sealed class ExportChangeTracker
{
    private Dictionary<string, EntryStamp>? accepted;
    private Dictionary<string, EntryStamp>? pending;
    private bool scanIncomplete;
    private bool refreshRequired;

    /// <summary>
    /// Checks configured installations, accounts, and exports for additions, removals, size
    /// changes, or timestamp changes. An unreadable directory always requests another refresh.
    /// </summary>
    public bool HasChanges(IEnumerable<string> gameDirectories)
    {
        ArgumentNullException.ThrowIfNull(gameDirectories);
        pending = new Dictionary<string, EntryStamp>(StringComparer.OrdinalIgnoreCase);
        scanIncomplete = false;
        foreach (var configuredDirectory in gameDirectories)
        {
            string gameDirectory;
            try { gameDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredDirectory)); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                scanIncomplete = true;
                continue;
            }
            if (!pending.TryAdd(gameDirectory, new EntryStamp(EntryKind.Installation))) continue;
            ScanAccounts(Path.Combine(gameDirectory, "WTF", "Account"));
        }
        refreshRequired |= scanIncomplete || accepted is null || pending.Count != accepted.Count ||
            pending.Any(entry => !accepted.TryGetValue(entry.Key, out var previous) || previous != entry.Value);
        return refreshRequired;
    }

    /// <summary>
    /// Commits the metadata captured by HasChanges, not a second scan after the refresh.
    /// Files changed during a read will therefore request another refresh on the next check.
    /// Do not accept a refresh that reported a failed or incomplete export read.
    /// </summary>
    public void AcceptChanges()
    {
        if (pending is null) throw new InvalidOperationException("Check for changes before accepting them.");
        if (!scanIncomplete)
        {
            accepted = pending;
            refreshRequired = false;
        }
    }

    /// <summary>Requests another refresh even when file metadata has not changed.</summary>
    public void Invalidate() => accepted = null;

    private void ScanAccounts(string accountRoot)
    {
        try
        {
            // Directory.Exists hides access failures. Reading attributes distinguishes an
            // absent folder from an inaccessible folder that needs to be retried.
            if ((File.GetAttributes(accountRoot) & FileAttributes.Directory) == 0)
            {
                scanIncomplete = true;
                return;
            }
            pending!.Add(accountRoot, new EntryStamp(EntryKind.AccountRoot));
            var accountOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false, IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot, "*", accountOptions))
            {
                pending.TryAdd(accountDirectory, new EntryStamp(EntryKind.Account));
                ScanExports(accountDirectory);
            }
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            scanIncomplete = true;
        }
    }

    private void ScanExports(string accountDirectory)
    {
        try
        {
            // Keep the same bounded, non-link-following discovery rules as ExportRepository.
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true, IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 8,
                MatchCasing = MatchCasing.CaseInsensitive
            };
            foreach (var path in Directory.EnumerateFiles(accountDirectory, ExportContract.ExportFileName, options))
            {
                if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "SavedVariables", StringComparison.OrdinalIgnoreCase)) continue;
                var file = new FileInfo(path);
                file.Refresh();
                if (!file.Exists) { scanIncomplete = true; continue; }
                pending![file.FullName] = new EntryStamp(EntryKind.Export, file.Length, file.LastWriteTimeUtc);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            scanIncomplete = true;
        }
    }

    private enum EntryKind { Installation, AccountRoot, Account, Export }
    private readonly record struct EntryStamp(EntryKind Kind, long Length = 0, DateTime ModifiedAt = default);
}
