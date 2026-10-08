using System.Diagnostics;
using Claudgar.Core.Updates;

namespace Claudgar.App.Updates;

internal sealed class WindowsUpdateProcessRuntime(string mutexName) : IUpdateProcessRuntime
{
    public async Task WaitForParentExitAsync(int processId, long startedUtcTicks, CancellationToken cancellationToken)
    {
        Process process;
        try { process = Process.GetProcessById(processId); }
        catch (ArgumentException) { return; }
        using (process)
        {
            // A reused process id belongs to somebody else and must never be waited on or stopped.
            if (process.StartTime.ToUniversalTime().Ticks != startedUtcTicks) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
        }
    }

    public Task<IDisposable> AcquireAppMutexAsync(CancellationToken cancellationToken)
    {
        // Mutex ownership is thread-affine. Keep its owner on a dedicated thread across async file/process work.
        var completion = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim(false);
        var owner = new Thread(() =>
        {
            try
            {
                using var mutex = new Mutex(false, mutexName);
                var acquired = false;
                try
                {
                    try
                    {
                        var signaled = WaitHandle.WaitAny([mutex, cancellationToken.WaitHandle], TimeSpan.FromSeconds(30));
                        if (signaled == WaitHandle.WaitTimeout) throw new IOException("Another Claudgar instance prevented the update.");
                        if (signaled != 0) throw new OperationCanceledException(cancellationToken);
                        acquired = true;
                    }
                    catch (AbandonedMutexException) { acquired = true; }
                    completion.TrySetResult(new MutexLease(release, Thread.CurrentThread));
                    release.Wait();
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
            catch (Exception error) { completion.TrySetException(error); }
            finally { release.Dispose(); }
        }) { IsBackground = true, Name = "Claudgar updater mutex" };
        owner.Start();
        return completion.Task;
    }

    public IUpdateChildProcess Start(string executable, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)!,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return new ChildProcess(Process.Start(start) ?? throw new IOException("Claudgar could not restart after updating."));
    }

    private sealed class MutexLease(ManualResetEventSlim release, Thread owner) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            release.Set();
            owner.Join();
        }
    }

    private sealed class ChildProcess(Process process) : IUpdateChildProcess
    {
        public bool HasExited => process.HasExited;
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (process.HasExited) return;
            // Stop only the unacknowledged app started by this helper. Claude's separate bridge is untouched.
            process.Kill(entireProcessTree: false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
        }
        public void Dispose() => process.Dispose();
    }
}
