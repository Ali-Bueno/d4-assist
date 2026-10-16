using System.Buffers.Binary;
using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;
using D4Memory.Scan;

namespace D4Assist.Tests;

public class ActiveQuestsTests
{
    // research/client-memory.md §4: QST_Scos_Blycroft_01 "Fields of Ruin", phase 8 picks up the wardstones, phase 14 places
    // them on three Wardstone_base gizmos named by markers.
    private const int FieldsOfRuin = 991256, CampaignQuest = 486210;
    private const uint PickUpPhase = 8, PlacePhase = 14, CampaignPhase = 271;
    private const int CollectionSno = 100, BaseSno = 200, PetalSno = 300;
    private const long ModuleData = 0x10000;

    private static readonly IReadOnlyList<PlacedMarker> Bases =
        [Marker(BaseSno, -1767.8f, -487.6f), Marker(BaseSno, -1779.8f, -438.4f), Marker(BaseSno, -1800.7f, -456.4f)];

    // Live 2026-10-04: S15_QST_Main_03_TriadC_01 phase 81 waits on the Travincal hub's quest, whose phase 8 asks for the
    // Compelling Orb; the game then holds the hub quest's pair, not the tracked quest's.
    private const int MainQuest = 2651255, HubQuest = 2605635, PortalSno = 2647347, OrbSno = 2597583;
    private const uint HandOverPhase = 81, PortalsPhase = 0, OrbPhase = 8;

    // research/dungeon-objectives.md §24: Wrack & Ruin (QST_Hawe_Marsh_03) phase 24 waits on the Right Eye quest, whose phase 21
    // names the right eye socket; the quest is found by the villager near the stone door, untracked.
    private const int WrackAndRuin = 594326, RightEyeQuest = 1222498, RightSocketSno = 1302768, VillagerSno = 400;
    private const uint FindTheEyesPhase = 24, ReturnTheEyePhase = 21;
    private static readonly PlacedMarker RightSocket = Marker(RightSocketSno, -777, 825);

    private static readonly Dictionary<int, IReadOnlyList<QuestPhaseObjects>> Phases = new()
    {
        [WrackAndRuin] = [new QuestPhaseObjects(FindTheEyesPhase, [], new HashSet<int>(), [], new HashSet<int> { RightEyeQuest })],
        [RightEyeQuest] = [Phase(ReturnTheEyePhase, [RightSocket])],
        [FieldsOfRuin] = [Phase(PickUpPhase, [Marker(CollectionSno, -1700, -400)]), Phase(PlacePhase, Bases)],
        [CampaignQuest] = [new QuestPhaseObjects(CampaignPhase, [], new HashSet<int> { PetalSno }, [])],
        [MainQuest] = [new QuestPhaseObjects(HandOverPhase, [], new HashSet<int>(), [], new HashSet<int> { HubQuest })],
        [HubQuest] =
        [
            new QuestPhaseObjects(PortalsPhase, [], new HashSet<int> { PortalSno }, []),
            new QuestPhaseObjects(OrbPhase, [], new HashSet<int> { OrbSno }, []),
        ],
    };

    [Fact]
    public void A_tracked_quest_that_waits_on_another_names_that_quests_current_phase()
    {
        var quests = Create(new ModuleReader((HubQuest, OrbPhase)));

        var named = quests.Update(tracked: [MainQuest], AreaWith(OrbSno)); // nothing here names the hub quest

        Assert.Equal([OrbSno], named.ActorSnos);
    }

    [Fact]
    public void AnUntrackedQuestInProgressHereNamesItsCurrentPhase()
    {
        var quests = Create(new ModuleReader((FieldsOfRuin, PlacePhase)));

        var named = quests.Update(tracked: [], AreaWith(BaseSno));

        Assert.Equal(Bases, named.Markers);
    }

    [Fact]
    public void TheTrackedQuestAndTheQuestInProgressHereAreFollowedTogether()
    {
        var quests = Create(new ModuleReader((CampaignQuest, CampaignPhase), (FieldsOfRuin, PlacePhase)));

        var named = quests.Update(tracked: [CampaignQuest], AreaWith(BaseSno));

        Assert.Equal(Bases, named.Markers);
        Assert.Equal([PetalSno], named.ActorSnos);
    }

    [Fact]
    public void AQuestNamedHereButNotInProgressNamesNothing()
    {
        var quests = Create(new ModuleReader());

        Assert.Equal(0, quests.Update(tracked: [], AreaWith(BaseSno)).Count);
    }

    [Fact]
    public void AnUntrackedQuestInProgressHereNamesTheCurrentPhaseOfTheQuestItWaitsOn()
    {
        var quests = Create(new ModuleReader((WrackAndRuin, FindTheEyesPhase), (RightEyeQuest, ReturnTheEyePhase)));

        var named = quests.Update(tracked: [], AreaWith(VillagerSno));

        Assert.Equal([RightSocket], named.Markers);
    }

    [Fact]
    public void AQuestFoundInProgressStaysFollowedAwayFromItsObjects()
    {
        var now = DateTime.UtcNow;
        var quests = Create(new ModuleReader((FieldsOfRuin, PlacePhase)), () => now);
        quests.Update(tracked: [], AreaWith(BaseSno));

        now += ActiveQuests.PhaseSearchInterval * 2;
        var named = quests.Update(tracked: [], Nowhere);

        Assert.Equal(Bases, named.Markers);
    }

    // Live 2026-10-16: tracked quest 1931517, phase never found, led the guide in the Light's Watch to Neyrelle's archive spots
    // in another world; its phases' actors may still sound.
    [Fact]
    public void A_tracked_quest_whose_phase_is_unknown_names_its_actors_but_no_world_marker()
    {
        var quests = Create(new ModuleReader());

        var named = quests.Update(tracked: [FieldsOfRuin, CampaignQuest], AreaWith(PetalSno));

        Assert.Empty(named.Markers);
        Assert.Equal([PetalSno], named.ActorSnos);
    }

    // Finds a quest by one of its objects, as QuestScriptMarkerReader.QuestsNaming does.
    private static ActiveQuests Create(ModuleReader memory, Func<DateTime>? clock = null) =>
        new(memory, memory.Regions(), q => Phases.GetValueOrDefault(q) ?? [],
            area => [.. area.Actors.Select(a => a.ActorSno switch { BaseSno => FieldsOfRuin, VillagerSno => WrackAndRuin, _ => 0 })
                .Where(q => q != 0).Distinct()],
            clock);

    private static Func<QuestSearchArea> AreaWith(int actorSno) => () => new QuestSearchArea(0, [], [(actorSno, Vector3.Zero)]);

    private static readonly Func<QuestSearchArea> Nowhere = () => new QuestSearchArea(0, [], []);

    private static PlacedMarker Marker(int sno, float x, float y) => new(Group: 1, sno, new Vector3(x, y, 0));

    private static QuestPhaseObjects Phase(uint uid, IReadOnlyList<PlacedMarker> markers) => new(uid, markers, new HashSet<int>(), []);

    // The module's data with one {quest SNO, phase UID} pair per quest in progress (research/client-memory.md §4, quest phases).
    private sealed class ModuleReader : IMemoryReader
    {
        private readonly byte[] _data = new byte[0x1000];

        public ModuleReader(params (int Quest, uint Phase)[] pairs)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(0x100 + i * 0x40), pairs[i].Quest);
                BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(0x104 + i * 0x40), pairs[i].Phase);
            }
        }

        public IReadOnlyList<MemoryRegion> Regions() => [new MemoryRegion(ModuleData, _data.Length)];

        public bool TryRead(long address, byte[] buffer, int count)
        {
            if (address < ModuleData || address + count > ModuleData + _data.Length) return false;
            Array.Copy(_data, address - ModuleData, buffer, 0, count);
            return true;
        }
    }
}
