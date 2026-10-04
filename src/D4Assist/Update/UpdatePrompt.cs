using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace D4Assist.Update;

/// <summary>What the player chose in the update question; closing the dialog counts as <see cref="Later"/>.</summary>
public enum UpdateAnswer { Install, Later, Skip }

/// <summary>The labels of the update question, already in the player's language.</summary>
public sealed record UpdatePromptText(string Title, string Heading, string Body, string Install, string Later, string Skip);

/// <summary>The update question: a native task dialog with three buttons that comes to the front over the game.</summary>
public static class UpdatePrompt
{
    // winuser.h: HWND_TOPMOST is (HWND)-1; SWP_NOSIZE 0x1, SWP_NOMOVE 0x2.
    private static readonly IntPtr TopMost = new(-1);
    private const uint NoSizeNoMove = 0x1 | 0x2;
    private const int MaxNotesLength = 1500;
    private const string Ellipsis = "…";

    /// <summary>The dialog heading: <paramref name="format"/> filled with both versions.</summary>
    public static string Heading(string format, Version available, Version current) =>
        string.Format(format, available.ToString(3), current.ToString(3));

    /// <summary>The dialog text: <paramref name="format"/> filled with the cleaned release notes.</summary>
    public static string Text(string format, string notes) => string.Format(format, CleanNotes(notes));

    /// <summary>The release notes with markdown emphasis marks removed, cut to a readable length.</summary>
    public static string CleanNotes(string notes)
    {
        var plain = notes.Replace("**", "").Replace("`", "").Replace("\r\n", "\n").Trim();
        return plain.Length <= MaxNotesLength ? plain : plain[..MaxNotesLength].TrimEnd() + Ellipsis;
    }

    /// <summary>Shows the question on its own STA thread; the returned task completes with the answer without blocking the caller.</summary>
    public static Task<UpdateAnswer> AskAsync(UpdatePromptText text)
    {
        var answer = new TaskCompletionSource<UpdateAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles(); // the task dialog needs comctl32 v6; the tray thread may not exist yet
                answer.SetResult(Show(text));
            }
            catch (Exception e) { answer.SetException(e); }
        }) { IsBackground = true, Name = "UpdatePrompt" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return answer.Task;
    }

    private static UpdateAnswer Show(UpdatePromptText text)
    {
        var install = new TaskDialogButton(text.Install);
        var later = new TaskDialogButton(text.Later);
        var skip = new TaskDialogButton(text.Skip);
        var page = new TaskDialogPage
        {
            Caption = text.Title,
            Heading = text.Heading,
            Text = text.Body,
            Icon = TaskDialogIcon.Information,
            AllowCancel = true,
            Buttons = { install, later, skip },
            DefaultButton = install,
        };
        page.Created += (_, _) => BringToFront(page.BoundDialog!.Handle);
        var chosen = TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen);
        return chosen == install ? UpdateAnswer.Install : chosen == skip ? UpdateAnswer.Skip : UpdateAnswer.Later;
    }

    private static void BringToFront(IntPtr dialog)
    {
        SetWindowPos(dialog, TopMost, 0, 0, 0, 0, NoSizeNoMove);
        SetForegroundWindow(dialog);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
