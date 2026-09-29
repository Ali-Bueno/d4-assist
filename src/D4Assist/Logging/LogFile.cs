using System.Text;

namespace D4Assist.Logging;

/// <summary>Copies everything the console prints into d4assist-log.txt next to the exe, so testers can send it.</summary>
public sealed class LogFile : IDisposable
{
    public const string FileName = "d4assist-log.txt";
    public const string PreviousFileName = "d4assist-log.old.txt";

    private readonly TextWriter _consoleOut;
    private readonly TextWriter _consoleError;
    private readonly TextWriter? _file;

    private LogFile(TextWriter consoleOut, TextWriter consoleError, TextWriter? file)
    {
        (_consoleOut, _consoleError, _file) = (consoleOut, consoleError, file);
    }

    /// <summary>Starts the log in <paramref name="directory"/>, keeping the previous run's log as the .old file.</summary>
    public static LogFile Start(string directory)
    {
        var (consoleOut, consoleError) = (Console.Out, Console.Error);
        string path = Path.Combine(directory, FileName);
        TextWriter file;
        try
        {
            if (File.Exists(path)) File.Move(path, Path.Combine(directory, PreviousFileName), overwrite: true);
            file = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            consoleError.WriteLine($"No log file ({e.Message}).");
            return new LogFile(consoleOut, consoleError, null);
        }

        var stamped = TextWriter.Synchronized(new TimestampWriter(file, () => DateTime.Now));
        Console.SetOut(new TeeWriter(consoleOut, stamped));
        Console.SetError(new TeeWriter(consoleError, stamped));
        Console.WriteLine($"Log started {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        return new LogFile(consoleOut, consoleError, stamped);
    }

    public void Dispose()
    {
        if (_file is null) return;
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _file.Dispose();
    }
}

/// <summary>Writes every character to both writers.</summary>
public sealed class TeeWriter(TextWriter first, TextWriter second) : TextWriter
{
    public override Encoding Encoding => first.Encoding;

    public override void Write(char value) { first.Write(value); second.Write(value); }

    public override void Write(string? value) { first.Write(value); second.Write(value); }

    public override void Flush() { first.Flush(); second.Flush(); }
}

/// <summary>Prefixes each line with the time it started, to line the log up with FenrisDebug.txt.</summary>
public sealed class TimestampWriter(TextWriter inner, Func<DateTime> now) : TextWriter
{
    private bool _atLineStart = true;

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (_atLineStart && value != '\r') inner.Write(now().ToString("HH:mm:ss.fff "));
        inner.Write(value);
        _atLineStart = value == '\n';
    }

    public override void Write(string? value)
    {
        foreach (char c in value ?? "") Write(c);
    }

    public override void Flush() => inner.Flush();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
