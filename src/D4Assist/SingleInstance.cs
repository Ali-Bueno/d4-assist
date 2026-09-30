namespace D4Assist;

/// <summary>One running assistant per Windows session: a second copy would double every cue and announcement.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\D4DungeonAssist";

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Takes the session's slot, waiting up to <paramref name="wait"/> (a restart waits for the old copy to exit);
    /// null when another copy keeps it.</summary>
    public static SingleInstance? TryAcquire(TimeSpan wait)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            if (mutex.WaitOne(wait)) return new SingleInstance(mutex);
        }
        catch (AbandonedMutexException)
        {
            return new SingleInstance(mutex); // the previous copy ended without releasing it: the slot is ours
        }
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
