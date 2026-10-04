using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Update;

/// <summary>
/// Looks for a newer release on GitHub, asks the player and, on Yes, replaces the app's files and asks for a restart.
/// Everything runs on background tasks: nothing here may hold up the game loop. Network trouble is only logged on the
/// automatic check and spoken on a manual one.
/// </summary>
public sealed class UpdateService(
    ISpeechSink speech, LanguageSetting language, Action<string> log, Action restart, string appFolder, string exeName, bool canReplaceItself)
{
    private int _busy;
    private Version? _askedFor;

    /// <summary>The check at start: silent unless a newer version exists.</summary>
    public void CheckAtStart() => Start(manual: false);

    /// <summary>The tray menu's check: also tells when there is nothing new or the check failed.</summary>
    public void CheckNow() => Start(manual: true);

    /// <summary>The service for this running app: its own folder and exe, able to replace itself only as the single-file build.</summary>
    public static UpdateService ForThisApp(ISpeechSink speech, LanguageSetting language, Action<string> log, Action restart) => new(
        speech, language, log, restart, AppContext.BaseDirectory, Path.GetFileName(Environment.ProcessPath!),
        canReplaceItself: string.IsNullOrEmpty(typeof(UpdateService).Assembly.Location)); // empty only inside a single-file exe

    private void Start(bool manual)
    {
        if (!canReplaceItself)
        {
            log("Update check skipped: only the downloaded build can replace itself.");
            if (manual) speech.Speak(Lines.UpdateOnlyDownloaded, interrupt: true);
            return;
        }
        if (Interlocked.Exchange(ref _busy, 1) == 1) return; // a check or an update is already running
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
        if (!manual && _askedFor == release.Version) return;
        _askedFor = release.Version;
        if (!Ask(release, current)) return;
        await InstallAsync(release, client);
    }

    private bool Ask(ReleaseInfo release, Version current)
    {
        log($"Update available: {release.Version.ToString(3)} (this is {current.ToString(3)}).");
        speech.Speak(string.Format(Lines.UpdateAvailableFormat, release.Version.ToString(3)), interrupt: true);
        var text = SettingsStrings.For(language.Current);
        return UpdatePrompt.AskYesNo(UpdatePrompt.Text(text.UpdateDialogFormat, release.Version, current, release.Notes), text.AppName);
    }

    private async Task InstallAsync(ReleaseInfo release, ReleaseClient client)
    {
        speech.Speak(Lines.UpdateDownloading, interrupt: true);
        try
        {
            var files = await UpdatePackage.PrepareAsync(release, client);
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
