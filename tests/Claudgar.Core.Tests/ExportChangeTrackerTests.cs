using Claudgar.Core.Data;
using Claudgar.Core.Exports;

internal static class ExportChangeTrackerTests
{
    public static void MetadataChanges() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = WriteExport(folder, "ACCOUNT-A");
        var tracker = new ExportChangeTracker();
        Assert(tracker.HasChanges([folder]), "The first check did not request a refresh.");
        tracker.AcceptChanges();
        using (var lockedExport = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert(!tracker.HasChanges([folder]), "An unchanged export was reopened or appeared changed.");

        var timestamp = File.GetLastWriteTimeUtc(path).AddMinutes(1);
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert(tracker.HasChanges([folder]), "A timestamp-only change was ignored.");
        tracker.AcceptChanges();
        File.AppendAllText(path, "more saved data");
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert(tracker.HasChanges([folder]), "A size change with the same timestamp was ignored.");
        tracker.AcceptChanges();
        Assert(!tracker.HasChanges([folder, folder + Path.DirectorySeparatorChar]), "Equivalent configured paths changed the stamp.");
    });

    public static void DiscoveryChanges() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var tracker = new ExportChangeTracker();
        tracker.HasChanges([folder]);
        tracker.AcceptChanges();
        Assert(!tracker.HasChanges([folder]), "An unchanged missing account root requested another refresh.");
        var path = WriteExport(folder, "ACCOUNT-A");
        Assert(tracker.HasChanges([folder]), "A new account export was ignored.");
        tracker.AcceptChanges();

        var characterDirectory = Path.Combine(folder, "WTF", "Account", "ACCOUNT-A", "Realm", "Character");
        var characterExport = WriteExport(characterDirectory, null);
        Assert(tracker.HasChanges([folder]), "A nested character export was ignored.");
        tracker.AcceptChanges();
        File.Delete(characterExport);
        Assert(tracker.HasChanges([folder]), "An export deletion was ignored.");
        tracker.AcceptChanges();

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "OtherAddon.lua"), "unrelated");
        File.WriteAllText(Path.Combine(folder, "WTF", "Account", "ACCOUNT-A", ExportContract.ExportFileName), "wrong parent");
        Assert(!tracker.HasChanges([folder]), "An unrelated file triggered refresh.");
        var emptyAccount = Path.Combine(folder, "WTF", "Account", "ACCOUNT-B");
        Directory.CreateDirectory(emptyAccount);
        Assert(tracker.HasChanges([folder]), "A new account was ignored.");
        tracker.AcceptChanges();
        Directory.Delete(emptyAccount);
        Assert(tracker.HasChanges([folder]), "An account removal was ignored.");
        tracker.AcceptChanges();
        Assert(tracker.HasChanges([]), "Removing a configured installation was ignored.");
    });

    public static void UnacceptedReadsAreRetried() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        var path = WriteExport(folder, "ACCOUNT-A");
        var tracker = new ExportChangeTracker();
        tracker.HasChanges([folder]);
        Assert(tracker.HasChanges([folder]), "An unaccepted refresh suppressed a retry.");
        tracker.AcceptChanges();

        File.AppendAllText(path, "first change");
        tracker.HasChanges([folder]);
        File.AppendAllText(path, "changed while being read");
        tracker.AcceptChanges();
        Assert(tracker.HasChanges([folder]), "A change during a refresh was committed without being observed.");
        tracker.AcceptChanges();
        Assert(!tracker.HasChanges([folder]), "A successfully accepted refresh was repeated.");
        tracker.Invalidate();
        Assert(tracker.HasChanges([folder]), "Invalidation did not force a retry.");
    });

    public static void IncompleteDiscoveryIsRetried() => SetupRegressionTests.InTemporaryFolder(folder =>
    {
        Directory.CreateDirectory(Path.Combine(folder, "WTF"));
        // A file in place of Account makes directory enumeration fail deterministically,
        // without relying on platform-specific access-control or symlink privileges.
        File.WriteAllText(Path.Combine(folder, "WTF", "Account"), "not a directory");
        var tracker = new ExportChangeTracker();
        Assert(tracker.HasChanges([folder]), "An incomplete scan did not request a refresh.");
        tracker.AcceptChanges();
        Assert(tracker.HasChanges([folder]), "An incomplete scan was accepted and suppressed retry.");
    });

    private static string WriteExport(string folder, string? account)
    {
        var savedVariables = account is null ? Path.Combine(folder, "SavedVariables") :
            Path.Combine(folder, "WTF", "Account", account, "SavedVariables");
        Directory.CreateDirectory(savedVariables);
        var path = Path.Combine(savedVariables, ExportContract.ExportFileName);
        File.WriteAllText(path, "saved export data");
        return path;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
