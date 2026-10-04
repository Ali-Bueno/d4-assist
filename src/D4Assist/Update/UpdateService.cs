using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Update;

/// <summary>
/// Looks for a newer release on GitHub at start and every few hours, asks the player (never while the game is in the
/// foreground, unless asked by hand) and, on Update, replaces the app's files and asks for a restart.
/// Everything runs on background tasks: nothing here may hold up the game loop. Network trouble is only logged on the
/// automatic check and spoken on a manual one.
/// </summary>
public sealed class UpdateService(
    ISpeechSink speech, LanguageSetting language, Action<string> log, Action restart, string appFolder, string exeName, bool canReplaceItself,
    Func<bool> isGameForeground)
{
    private const int ProgressStep = 25;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(5);

    private readonly ForegroundGate _gate = new(isGameForeground, ForegroundPollInterval);
    private readonly string _statePath = Path.Combine(appFolder, UpdateState.FileName);
    private int _busy;

    /// <summary>The check at start: silent unless a newer version exists.</summary>
    public void CheckAtStart() => Start(manual: false);

    /// <summary>The tray menu's check: also tells when there is nothing new or the check failed.</summary>
    public void CheckNow() => Start(manual: true);

    /// <summary>Repeats the automatic check every <see cref="CheckInterval"/> for as long as the app runs.</summary>
    public void CheckPeriodically()
    {
        if (!canReplaceItself) return;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(CheckInterval);
            while (await timer.WaitForNextTickAsync()) Start(manual: false);
        });
    }

    /// <summary>The service for this running app: its own folder and exe, able to replace itself only as the single-file build.</summary>
    public static UpdateService ForThisApp(ISpeechSink speech, LanguageSetting language, Action<string> log, Action restart) => new(
        speech, language, log, restart, AppContext.BaseDirectory, Path.GetFileName(Environment.ProcessPath!),
        canReplaceItself: string.IsNullOrEmpty(typeof(UpdateService).Assembly.Location), // empty only inside a single-file exe
        GameWindowFocus.IsGameForeground);

    private void Start(bool manual)
    {
        if (!canReplaceItself)
        {
            log("Update check skipped: only the downloaded build can replace itself.");
            if (manual) speech.Speak(Lines.UpdateOnlyDownloaded, interrupt: true);
            return;
        }
        if (Interlocked.Exchange(ref _busy, 1) == 1) // a check or an update is already running
        {
            if (manual) _gate.Release(); // an automatic one waiting for the game to leave the foreground asks right away
            return;
        }
        _ = Task.Run(async () =>
        {
            try { await RunAsync(manual); }
            catch (Exception e) { log($"Update check failed: {e.Message}"); if (manual) speech.Speak(Lines.UpdateCheckFailed, interrupt: true); }
            finally { Volatile.Write(ref _busy, 0); }
        });
    }

    private AppStrings Lines => AppStrings.For(language.Current);

    private async Task RunAsync(bool manual)
    {
        var current = UpdateVersion.Current;
        using var client = new ReleaseClient(current);
        var release = await client.GetLatestAsync();
        if (release is null)
        {
            log("Update check: the latest release has no readable version or zip.");
            if (manual) speech.Speak(Lines.UpdateCheckFailed, interrupt: true);
            return;
        }
        if (release.Version <= current)
        {
            log($"Update check: {current.ToString(3)} is the latest version.");
            if (manual) speech.Speak(Lines.UpToDate, interrupt: true);
            return;
        }
        if (!UpdateState.ShouldAsk(release.Version, current, UpdateState.Load(_statePath).Skipped, manual)) return;
        log($"Update available: {release.Version.ToString(3)} (this is {current.ToString(3)}).");
        if (!manual) await _gate.WaitUntilQuietAsync();
        switch (await AskAsync(release, current))
        {
            case UpdateAnswer.Install: await InstallAsync(release, client); break;
            case UpdateAnswer.Skip: SaveSkipped(release.Version); break;
        }
    }

    private async Task<UpdateAnswer> AskAsync(ReleaseInfo release, Version current)
    {
        speech.Speak(string.Format(Lines.UpdateAvailableFormat, release.Version.ToString(3)), interrupt: true);
        var text = SettingsStrings.For(language.Current);
        return await UpdatePrompt.AskAsync(new UpdatePromptText(
            text.AppName, UpdatePrompt.Heading(text.UpdateHeadingFormat, release.Version, current),
            UpdatePrompt.Text(text.UpdateTextFormat, release.Notes), text.UpdateInstall, text.UpdateLater, text.UpdateSkip));
    }

    private void SaveSkipped(Version version)
    {
        try { new UpdateState(version.ToString(3)).Save(_statePath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log($"Could not save the skipped version: {e.Message}"); }
    }

    // "25 %", "50 %", "75 %": the zip is ~50 MB, a slow line takes a while and the player can't see a bar.
    private Action<int> SpeakProgress() =>
        ProgressMilestones.Every(ProgressStep, percent => speech.Speak(string.Format(Lines.UpdateProgressFormat, percent), interrupt: false));

    private async Task InstallAsync(ReleaseInfo release, ReleaseClient client)
    {
        speech.Speak(Lines.UpdateDownloading, interrupt: true);
        try
        {
            var files = await UpdatePackage.PrepareAsync(release, client, SpeakProgress());
            var report = FileReplacement.Apply(appFolder, files, exeName, UpdatePackage.BackupPath(release.Version));
            log($"Updated to {release.Version.ToString(3)}: copied {string.Join(", ", report.Copied)}; kept {string.Join(", ", report.Kept)}.");
        }
        catch (UnauthorizedAccessException e)
        {
            log($"Update failed, {appFolder} is not writable: {e.Message}");
            speech.Speak(Lines.UpdateFolderNotWritable, interrupt: true);
            return;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException or TaskCanceledException)
        {
            log($"Update failed: {e}");
            speech.Speak(Lines.UpdateFailed, interrupt: true);
            return;
        }
        speech.Speak(Lines.UpdateRestarting, interrupt: true);
        restart();
    }
}
