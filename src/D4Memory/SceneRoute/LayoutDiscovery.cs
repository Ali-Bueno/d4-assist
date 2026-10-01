using System.Buffers.Binary;
using System.Numerics;
using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.SceneRoute;

/// <summary>One occupied pool slot: its id and its bytes.</summary>
public sealed record SlotBytes(uint Id, byte[] Bytes);

/// <summary>
/// Checks the structure layout against the live game and finds a field that moved (research/client-memory.md §6). Each
/// field is tested by what must hold across structures, never by its value alone:
/// - the player's RActor holds the player record's position and actor SNO; the SNO field holds actor SNOs in every slot;
/// - an RActor's ACD id leads to an ACD slot whose RActor id leads back;
/// - a linked ACD holds the same actor SNO and position; the player's ACD points to a container of named attributes;
/// - a scene slot's CScene record passes CSceneRecordMatcher.
/// The known value is kept when it passes; another is searched only when it fails, so a working layout never changes.
/// </summary>
public static class LayoutDiscovery
{
    // The player record and the RActor are written at different moments: within a unit of each other.
    private const float SamePositionUnits = 1f;
    // Share of slots that must agree for a field to count.
    private const double Agreement = 0.8;
    // A container of the player's attributes holds hundreds of entries; a stray pointer none.
    private const int MinNamedAttributes = 5;
    // Each pool slot starts with its own id (PoolContainer); a link to it would only match the slot to itself.
    private const int OwnIdOffset = PoolContainer.IdOffsetInSlot;
    // A slot is read up to this many bytes: RActor 0xC30 and ACD 0x6A0 in build 3.2.x.
    private const int MaxSlotBytes = 0x2000;

    /// <summary>The checked fields and one line per field for the log.</summary>
    public sealed record ActorLayout(RActorFields Actor, AcdFields Acd, string Report, bool Conclusive);

    /// <summary>Checks the RActor and ACD fields on slots already read.</summary>
    /// <param name="isActorSno">True for an actor SNO the game files hold.</param>
    /// <param name="namedAttributes">How many attributes with a name the container at an address holds (0 when none).</param>
    public static ActorLayout CheckActors(
        IReadOnlyList<SlotBytes> ractors, IReadOnlyList<SlotBytes> acds, Vector3 player, int playerSno, Func<int, bool> isActorSno,
        Func<long, int> namedAttributes, RActorFields knownActor, AcdFields knownAcd)
    {
        var report = new List<string>();
        var acdById = new Dictionary<uint, byte[]>();
        foreach (var acd in acds) acdById.TryAdd(acd.Id, acd.Bytes);

        // 1. The player's RActor: its position and actor SNO.
        var (self, position, sno) = FindPlayer(ractors, player, playerSno, isActorSno, knownActor);
        if (self is null)
            return new ActorLayout(knownActor, knownAcd, "player's RActor not found: layout kept as known", Conclusive: false);
        report.Add(Line("RActor position", knownActor.Position, position));
        report.Add(Line("RActor actor SNO", knownActor.ActorSno, sno));

        // 2. The RActor ↔ ACD link.
        var (acdIdField, rActorIdField) = FindLink(ractors, acdById, self, knownActor.AcdId, knownAcd.RActorId);
        if (acdIdField is not { } acdId || rActorIdField is not { } rActorId)
        {
            report.Add("RActor-ACD link not found: ACD fields kept as known");
            var actorOnly = knownActor with { Position = position, ActorSno = sno };
            return new ActorLayout(actorOnly, knownAcd, string.Join("; ", report), Conclusive: false);
        }
        report.Add(Line("RActor ACD id", knownActor.AcdId, acdId));
        report.Add(Line("ACD RActor id", knownAcd.RActorId, rActorId));

        var pairs = ractors.Where(r => Fits(r.Bytes, acdId, 4) && acdById.TryGetValue(U32(r.Bytes, acdId), out var a)
                && Fits(a, rActorId, 4) && U32(a, rActorId) == r.Id)
            .Select(r => (R: r.Bytes, A: acdById[U32(r.Bytes, acdId)])).ToList();
        var selfAcd = acdById[U32(self.Bytes, acdId)];

        // 3. The ACD's copies of the actor SNO and position.
        int acdSno = Field(knownAcd.ActorSno, 4, pairs.Count, o => pairs.Count(p => Fits(p.A, o, 4) && I32(p.A, o) == I32(p.R, sno)),
            MaxBytes(acds), out bool snoFound);
        report.Add(snoFound ? Line("ACD actor SNO", knownAcd.ActorSno, acdSno) : "ACD actor SNO not found, kept");
        int acdPosition = Field(knownAcd.Position, 12, pairs.Count,
            o => pairs.Count(p => Fits(p.A, o, 12) && Near(V3(p.A, o), V3(p.R, position))), MaxBytes(acds), out bool positionFound);
        report.Add(positionFound ? Line("ACD position", knownAcd.Position, acdPosition) : "ACD position not found, kept");

        // 4. The player's attribute container.
        int attributes = knownAcd.Attributes;
        bool attributesFound = Fits(selfAcd, attributes, 8) && namedAttributes(I64(selfAcd, attributes)) >= MinNamedAttributes;
        if (!attributesFound)
        {
            var best = Enumerable.Range(0, (selfAcd.Length - 8) / 8 + 1).Select(i => i * 8)
                .Select(o => (Offset: o, Count: namedAttributes(I64(selfAcd, o)))).MaxBy(c => c.Count);
            if (best.Count >= MinNamedAttributes) (attributes, attributesFound) = (best.Offset, true);
        }
        report.Add(attributesFound ? Line("ACD attributes", knownAcd.Attributes, attributes) : "ACD attributes not found, kept");

        return new ActorLayout(
            knownActor with { AcdId = acdId, ActorSno = sno, Position = position },
            new AcdFields(rActorId, acdSno, acdPosition, attributes),
            string.Join("; ", report) + "; RActor gizmo state not checked", Conclusive: true);
    }

    /// <summary>Where the CScene record starts in a scene slot: the known offset when a record matches there, else the one
    /// where most slots match; null when none does (no DRLG scene loaded, or the record itself changed).</summary>
    public static int? CheckSceneRecord(IReadOnlyList<byte[]> sceneSlots, int known, Func<byte[], int, bool> matchesAt)
    {
        if (sceneSlots.Any(s => matchesAt(s, known))) return known;
        int longest = sceneSlots.Count == 0 ? 0 : sceneSlots.Max(s => s.Length);
        var best = Enumerable.Range(0, Math.Max(0, longest / 4)).Select(i => i * 4)
            .Select(o => (Offset: o, Count: sceneSlots.Count(s => matchesAt(s, o)))).Where(c => c.Count > 0)
            .DefaultIfEmpty((Offset: -1, Count: 0)).MaxBy(c => c.Count);
        return best.Count > 0 ? best.Offset : null;
    }

    /// <summary>Reads the live pools and checks the RActor and ACD fields; returns the offsets with them.</summary>
    public static GameBuildOffsets CheckLive(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, Vector3 player, int playerSno, Func<int, bool> isActorSno,
        out string report, out bool conclusive)
    {
        conclusive = false;
        if (offsets.Acd is not { } acd) { report = "no ACD layout"; return offsets; }
        if (!TryReadPool(reader, moduleBase, offsets, offsets.Actor, out var ractors)
            || !TryReadPool(reader, moduleBase, offsets, acd.Pool, out var acds))
        {
            report = "pools unreadable: layout kept as known";
            return offsets;
        }

        var names = new AttributeNameTable(reader, moduleBase, acd);
        int NamedAttributes(long container) =>
            container is < 0x10000 or > 0x7FFF_FFFF_0000 ? 0
                : AcdAttributeReader.Read(reader, container).Count(a => names.NameOf(a.AttributeId) is not null);

        var layout = CheckActors(ractors, acds, player, playerSno, isActorSno, NamedAttributes, offsets.ActorFields, acd.Fields);
        (report, conclusive) = ($"{layout.Report} ({ractors.Count} RActors, {acds.Count} ACDs)", layout.Conclusive);
        return offsets with { ActorFields = layout.Actor, Acd = acd with { Fields = layout.Acd } };
    }

    /// <summary>Reads the live scene slots and checks where their CScene record starts; null when no record matches.</summary>
    public static int? CheckSceneRecordLive(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, CandidateIndex candidates)
    {
        if (!TryReadPool(reader, moduleBase, offsets, offsets.Scene, out var slots)) return null;
        return CheckSceneRecord([.. slots.Select(s => s.Bytes)], offsets.SceneRecordOffset, (slot, a) =>
            a >= CSceneRecordMatcher.RecordBytesBefore && a + CSceneRecordMatcher.RecordBytesAfter <= slot.Length
            && CSceneRecordMatcher.TryMatchAt(slot, a, candidates, out _));
    }

    private static bool TryReadPool(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, PoolParameters pool, out List<SlotBytes> slots)
    {
        slots = [];
        if (!PoolContainer.TryOpen(reader, moduleBase, offsets, pool, out var container)) return false;
        int length = (int)Math.Min(container.Stride, MaxSlotBytes);
        foreach (var (address, id) in container.OccupiedSlots())
        {
            var bytes = new byte[length];
            if (reader.TryRead(address, bytes, length)) slots.Add(new SlotBytes(id, bytes));
        }
        return true;
    }

    // The slot whose fields at the known offsets hold the player's SNO and position; else every slot holding the player's
    // position, with the SNO offset most slots agree on and the position whose transform block looks right.
    private static (SlotBytes? Self, int Position, int Sno) FindPlayer(
        IReadOnlyList<SlotBytes> slots, Vector3 player, int playerSno, Func<int, bool> isActorSno, RActorFields known)
    {
        var self = slots.FirstOrDefault(s => Fits(s.Bytes, known.ActorSno, 4) && Fits(s.Bytes, known.Position, 12)
            && I32(s.Bytes, known.ActorSno) == playerSno && Near(V3(s.Bytes, known.Position), player));
        if (self is not null) return (self, known.Position, known.ActorSno);

        (SlotBytes? Self, int Position, int Sno, double Score) best = (null, 0, 0, 0);
        foreach (var slot in slots)
        {
            var positions = Offsets(slot.Bytes, 12).Where(o => Near(V3(slot.Bytes, o), player)).ToList();
            if (positions.Count == 0) continue;
            foreach (int sno in Offsets(slot.Bytes, 4).Where(o => I32(slot.Bytes, o) == playerSno))
            {
                double score = slots.Count(s => Fits(s.Bytes, sno, 4) && isActorSno(I32(s.Bytes, sno))) / (double)slots.Count;
                if (score < Agreement || score <= best.Score) continue;
                int position = positions.FirstOrDefault(o => LooksLikeTransform(slot.Bytes, o), positions[0]);
                best = (slot, position, sno, score);
            }
        }
        return (best.Self, best.Position, best.Sno);
    }

    // The block around the position: a unit quaternion 16 bytes before it and the placement word (0 = in the world) after it.
    private static bool LooksLikeTransform(byte[] slot, int position)
    {
        int rotation = position - 0x10, placement = position + 0x4C;
        if (rotation < 0 || !Fits(slot, placement, 4) || I32(slot, placement) != 0) return false;
        var q = new Quaternion(F32(slot, rotation), F32(slot, rotation + 4), F32(slot, rotation + 8), F32(slot, rotation + 12));
        return MathF.Abs(q.Length() - 1) < 0.02f;
    }

    // The RActor field holding an ACD id and the ACD field holding the RActor id back; the player's pair must link.
    private static (int? AcdId, int? RActorId) FindLink(
        IReadOnlyList<SlotBytes> ractors, Dictionary<uint, byte[]> acdById, SlotBytes self, int knownAcdId, int knownRActorId)
    {
        int Linked(int acdId, int rActorId) => ractors.Count(r => Fits(r.Bytes, acdId, 4)
            && acdById.TryGetValue(U32(r.Bytes, acdId), out var a) && Fits(a, rActorId, 4) && U32(a, rActorId) == r.Id);
        bool SelfLinks(int acdId, int rActorId) => Fits(self.Bytes, acdId, 4)
            && acdById.TryGetValue(U32(self.Bytes, acdId), out var a) && Fits(a, rActorId, 4) && U32(a, rActorId) == self.Id;

        if (SelfLinks(knownAcdId, knownRActorId)) return (knownAcdId, knownRActorId);

        int acdLength = acdById.Count == 0 ? 0 : acdById.Values.Max(a => a.Length);
        var candidates = Offsets(self.Bytes, 4).Where(o => o != OwnIdOffset && acdById.ContainsKey(U32(self.Bytes, o)))
            .OrderByDescending(o => ractors.Count(r => Fits(r.Bytes, o, 4) && acdById.ContainsKey(U32(r.Bytes, o)))).Take(8);
        (int? AcdId, int? RActorId, int Count) best = (null, null, 0);
        foreach (int acdId in candidates)
            foreach (int rActorId in Enumerable.Range(0, acdLength / 4).Select(i => i * 4).Where(o => o != OwnIdOffset))
            {
                if (!SelfLinks(acdId, rActorId)) continue;
                int count = Linked(acdId, rActorId);
                if (count > best.Count) best = (acdId, rActorId, count);
            }
        return (best.AcdId, best.RActorId);
    }

    // The known offset when enough pairs agree on it, else the offset most agree on; the known one when none reaches the bar.
    private static int Field(int known, int width, int pairs, Func<int, int> agreeing, int length, out bool found)
    {
        int needed = Math.Max(1, (int)Math.Ceiling(pairs * Agreement));
        found = true;
        if (agreeing(known) >= needed) return known;
        var best = Enumerable.Range(0, Math.Max(0, (length - width) / 4 + 1)).Select(i => i * 4)
            .Select(o => (Offset: o, Count: agreeing(o))).MaxBy(c => c.Count);
        if (best.Count >= needed) return best.Offset;
        found = false;
        return known;
    }

    private static string Line(string field, int known, int found) =>
        found == known ? $"{field} at 0x{found:X} as known" : $"{field} MOVED 0x{known:X} -> 0x{found:X}";

    private static IEnumerable<int> Offsets(byte[] slot, int width) => Enumerable.Range(0, Math.Max(0, (slot.Length - width) / 4 + 1)).Select(i => i * 4);
    private static int MaxBytes(IReadOnlyList<SlotBytes> slots) => slots.Count == 0 ? 0 : slots.Max(s => s.Bytes.Length);
    private static bool Fits(byte[] slot, int offset, int width) => offset >= 0 && offset + width <= slot.Length;
    private static bool Near(Vector3 a, Vector3 b) => float.IsFinite(a.X) && float.IsFinite(a.Y) && float.IsFinite(a.Z)
        && Vector3.Distance(a, b) <= SamePositionUnits;
    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
    private static int I32(byte[] b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o, 4));
    private static long I64(byte[] b, int o) => BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(o, 8));
    private static float F32(byte[] b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(o, 4));
    private static Vector3 V3(byte[] b, int o) => new(F32(b, o), F32(b, o + 4), F32(b, o + 8));
}
