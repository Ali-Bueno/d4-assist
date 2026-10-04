using System.Runtime.InteropServices;

namespace D4Assist.Update;

/// <summary>The Yes/No question that offers an update: a native message box that comes to the front over the game.</summary>
public static class UpdatePrompt
{
    // winuser.h: MB_YESNO | MB_ICONQUESTION | MB_SETFOREGROUND | MB_TOPMOST; IDYES.
    private const uint MessageBoxStyle = 0x4 | 0x20 | 0x10000 | 0x40000;
    private const int AnswerYes = 6;
    private const int MaxNotesLength = 1500;
    private const string Ellipsis = "…";

    /// <summary>The dialog text: <paramref name="format"/> filled with both versions and the release notes.</summary>
    public static string Text(string format, Version available, Version current, string notes) =>
        string.Format(format, available.ToString(3), current.ToString(3), CleanNotes(notes));

    /// <summary>The release notes with markdown emphasis marks removed, cut to a readable length.</summary>
    public static string CleanNotes(string notes)
    {
        var plain = notes.Replace("**", "").Replace("`", "").Replace("\r\n", "\n").Trim();
        return plain.Length <= MaxNotesLength ? plain : plain[..MaxNotesLength].TrimEnd() + Ellipsis;
    }

    /// <summary>Shows the question and waits; true on Yes. Blocks the calling thread, so never call it from a game poll thread.</summary>
    public static bool AskYesNo(string text, string title) => MessageBoxW(IntPtr.Zero, text, title, MessageBoxStyle) == AnswerYes;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
