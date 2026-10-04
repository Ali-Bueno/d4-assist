namespace D4Assist.Update;

/// <summary>Holds an automatic update question back while the game window is in the foreground; <see cref="Release"/> lets it through at once.</summary>
public sealed class ForegroundGate(Func<bool> isGameForeground, TimeSpan pollInterval)
{
    private TaskCompletionSource? _release;

    /// <summary>Completes when the game is not in the foreground, or when <see cref="Release"/> is called meanwhile.</summary>
    public async Task WaitUntilQuietAsync()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _release, release);
        try
        {
            while (isGameForeground() && !release.Task.IsCompleted)
                await Task.WhenAny(release.Task, Task.Delay(pollInterval));
        }
        finally { Volatile.Write(ref _release, null); }
    }

    /// <summary>Ends a running wait now (the player asked by hand); does nothing when nobody waits.</summary>
    public void Release() => Volatile.Read(ref _release)?.TrySetResult();
}
