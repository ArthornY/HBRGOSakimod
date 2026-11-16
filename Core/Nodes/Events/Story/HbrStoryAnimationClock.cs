using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HBRGOSakiMod.Core.Nodes.Events.Story;

// A local, explicitly ticked clock. It never uses combat time, game RNG or timers
// which could complete a visual while its reader is covered by a pause screen.
internal sealed class HbrStoryAnimationClock : IDisposable
{
    private sealed class Job
    {
        internal string Key;
        internal double Elapsed, Duration;
        internal Action<float> Update;
        internal readonly TaskCompletionSource<bool> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationTokenRegistration Cancellation;
    }
    private readonly List<Job> _jobs = new();
    private bool _disposed;
    internal bool Paused { get; set; }
    internal Task Animate(string key, double seconds, Action<float> update, CancellationToken token = default)
    {
        if (_disposed || token.IsCancellationRequested) return Task.FromCanceled(token.IsCancellationRequested ? token : new CancellationToken(true));
        if (key != null) Cancel(key);
        if (seconds <= 0) { update?.Invoke(1); return Task.CompletedTask; }
        Job job = new() { Key = key, Duration = seconds, Update = update };
        if (token.CanBeCanceled) job.Cancellation = token.Register(() => job.Done.TrySetCanceled(token));
        _jobs.Add(job); update?.Invoke(0); return job.Done.Task;
    }
    internal Task Wait(double seconds, CancellationToken token = default) => Animate(null, seconds, null, token);
    internal void Cancel(string key)
    {
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            Job job = _jobs[i]; if (job.Key != key) continue;
            _jobs.RemoveAt(i); job.Cancellation.Dispose(); job.Done.TrySetCanceled();
        }
    }
    internal void Tick(double delta)
    {
        if (_disposed || Paused) return;
        List<Job> complete = new();
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            Job job = _jobs[i];
            if (job.Done.Task.IsCompleted) { _jobs.RemoveAt(i); job.Cancellation.Dispose(); continue; }
            job.Elapsed += Math.Max(0, delta);
            job.Update?.Invoke((float)Math.Clamp(job.Elapsed / job.Duration, 0, 1));
            if (job.Elapsed >= job.Duration) { _jobs.RemoveAt(i); complete.Add(job); }
        }
        foreach (Job job in complete) { job.Cancellation.Dispose(); job.Done.TrySetResult(true); }
    }
    internal void CompleteAll()
    {
        Job[] jobs = _jobs.ToArray(); _jobs.Clear();
        foreach (Job job in jobs)
        {
            if (!job.Done.Task.IsCompleted) { job.Update?.Invoke(1); job.Done.TrySetResult(true); }
            job.Cancellation.Dispose();
        }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (Job job in _jobs) { job.Cancellation.Dispose(); job.Done.TrySetCanceled(); }
        _jobs.Clear();
    }
}
