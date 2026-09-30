using System.Text.Json;
using D4Memory.Scan;
using D4Memory.SceneRoute;
using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// Where this game build keeps its conversation records (research/client-memory.md §5). Each part can only be found
/// while its state is live (the playing list while someone talks, the dialogue menu while it is open), so the search
/// runs in the background every few seconds until both are known. Found addresses are saved per build next to the exe,
/// so the search runs once per game update.
/// </summary>
public sealed class ConversationAddresses
{
    // A search reads every pointer in the module's data (about half a second, live 2026-09-30).
    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly Func<int, bool> _isConversation;
    private readonly Action<string>? _log;
    private uint _build;
    private long _playingRva, _menuRva;
    private bool _searching;
    private DateTime _nextSearch = DateTime.MinValue;

    /// <param name="filePath">The address file (JSON, build → RVAs); created on the first find.</param>
    /// <param name="isConversation">True for a ConversationDefinition SNO; called from the search thread.</param>
    public ConversationAddresses(string filePath, Func<int, bool> isConversation, Action<string>? log = null)
    {
        _filePath = filePath;
        _isConversation = isConversation;
        _log = log;
    }

    /// <summary>The known addresses for the build, or null when neither part is found yet.</summary>
    public ConversationOffsets? For(uint build)
    {
        lock (_gate)
        {
            if (build != _build) Load(build);
            if (_playingRva == 0 && _menuRva == 0) return null;
            return GameBuildOffsetsTable.ConversationLayout with { PlayingListRva = _playingRva, OptionsPointerRva = _menuRva };
        }
    }

    /// <summary>Starts a background search when a part is unknown and none ran within the interval; never blocks.</summary>
    public void SearchIfDue(IMemoryReader memory, IReadOnlyList<MemoryRegion> data, ModuleRange module, uint build)
    {
        bool needPlaying, needMenu;
        lock (_gate)
        {
            if (build != _build) Load(build);
            if (_searching || (_playingRva != 0 && _menuRva != 0) || DateTime.UtcNow < _nextSearch) return;
            _searching = true;
            (needPlaying, needMenu) = (_playingRva == 0, _menuRva == 0);
        }
        Task.Run(() =>
        {
            var layout = GameBuildOffsetsTable.ConversationLayout;
            long? playing = needPlaying ? ConversationDiscovery.FindPlayingList(memory, data, module, layout, _isConversation) : null;
            long? menu = needMenu ? ConversationDiscovery.FindMenuOptions(memory, data, module, layout, _isConversation) : null;
            lock (_gate)
            {
                _searching = false;
                _nextSearch = DateTime.UtcNow + SearchInterval;
                if (build != _build) return;
                bool found = false;
                if (playing is { } p && _playingRva == 0) { _playingRva = p - module.Base; found = true; _log?.Invoke($"TALK playing list found at RVA 0x{_playingRva:X}"); }
                if (menu is { } m && _menuRva == 0) { _menuRva = m - module.Base; found = true; _log?.Invoke($"TALK dialogue menu found at RVA 0x{_menuRva:X}"); }
                if (found) Save();
            }
        });
    }

    private void Load(uint build)
    {
        (_build, _playingRva, _menuRva, _nextSearch) = (build, 0, 0, DateTime.MinValue);
        if (ReadFile().TryGetValue(Key(build), out var saved)) (_playingRva, _menuRva) = (saved.PlayingListRva, saved.OptionsPointerRva);
    }

    private void Save()
    {
        var all = ReadFile();
        all[Key(_build)] = new Saved(_playingRva, _menuRva);
        try { File.WriteAllText(_filePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true })); }
        catch (IOException e) { _log?.Invoke($"TALK addresses not saved: {e.Message}"); }
        catch (UnauthorizedAccessException e) { _log?.Invoke($"TALK addresses not saved: {e.Message}"); }
    }

    private Dictionary<string, Saved> ReadFile()
    {
        try { return File.Exists(_filePath) ? JsonSerializer.Deserialize<Dictionary<string, Saved>>(File.ReadAllText(_filePath)) ?? [] : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }

    private static string Key(uint build) => $"0x{build:X8}";

    private sealed record Saved(long PlayingListRva, long OptionsPointerRva);
}
