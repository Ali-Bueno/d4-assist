using System.Windows.Forms;
using D4Assist.Strings;
using D4Data.Casc;

namespace D4Assist.Ui;

/// <summary>Lets the player choose the Diablo IV folder: at startup when nothing found it, or from the settings window.</summary>
internal static class InstallFolderPicker
{
    /// <summary>At startup: says the game wasn't found, then <see cref="Browse"/>; null when the player cancels.</summary>
    public static string? AskAtStartup(SettingsStrings text)
    {
        string? picked = null;
        // The folder dialog needs a single-threaded apartment; the main thread isn't one.
        var thread = new Thread(() =>
        {
            if (MessageBox.Show(text.InstallNotFound, text.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                picked = Browse(null, text, null);
        }) { Name = "Install picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return picked;
    }

    /// <summary>The install the player picks (the folder or one up to three levels under it); null when they cancel.</summary>
    public static string? Browse(IWin32Window? owner, SettingsStrings text, string? current)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = text.InstallPickerTitle, UseDescriptionForTitle = true, ShowNewFolderButton = false, SelectedPath = current ?? "",
        };
        while (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            if (InstallFolderSearch.AtOrUnder(dialog.SelectedPath, SnoStore.IsInstall) is { } install) return install;
            if (MessageBox.Show(owner, string.Format(text.WrongInstallFolderFormat, dialog.SelectedPath), text.AppName,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return null;
        }
        return null;
    }
}
