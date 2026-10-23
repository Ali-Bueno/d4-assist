using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using D4Assist.Navigation;
using D4Data.Format;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Safety;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `effects`: logs, every few tens of milliseconds, the client-only actors (effects, no ACD) appearing and leaving around the
/// player, and the effects every boss shows on its ACD (Buff_Visual_Effect parameters, effect groups such as
/// Horadric_Guardian_360_Sweep_Warning) as they come and go: what warns of a boss's attack, where, and how long before it lands
/// (research/boss-telegraphs.md).
/// </summary>
internal static partial class EffectsCommand
{
    private const float DefaultRangeUnits = 60f;
    private const int DefaultIntervalMs = 50;
    private const uint NoAcdId = 0xFFFFFFFF;
    private const uint MaxAttributeId = 0x1000;
    private const string BuffVisualEffect = "Buff_Visual_Effect";

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        float range = float.TryParse(reader.Get("--range"), CultureInfo.InvariantCulture, out var r) ? r : DefaultRangeUnits;
        int intervalMs = int.TryParse(reader.Get("--interval-ms"), out var i) ? i : DefaultIntervalMs;
        int seconds = int.TryParse(reader.Get("--watch"), out var w) ? w : 120;
        var match = reader.Get("--match") is { } pattern ? new Regex(pattern, RegexOptions.IgnoreCase) : null;
        string? outPath = reader.Get("--out");

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var (worldSno, worldName) = WorldResolver.Resolve(store, schema, install);
            using var process = ProcessReader.OpenDiabloIv();
            if (!PlayerRecordLocator.TryLocateLive(process, store, schema, exePath, worldSno, out _, out var player)) return 1;
            (var discovered, var module) = Offsets.DiscoverAny(process, exePath);
            if (discovered is not { Acd: not null } offsets || !AcdReader.TryOpen(process, module.Base, offsets, out var acds))
            {
                Console.Error.WriteLine("ACD route unavailable for this build.");
                return 1;
            }
            var names = new AttributeNameTable(process, module.Base, offsets.Acd);
            uint? buffId = null;
            for (uint id = 0; id < MaxAttributeId && buffId is null; id++)
                if (names.NameOf(id) == BuffVisualEffect) buffId = id;
            var defs = new ActorDefinitionReader(store, schema);
            using var log = outPath is null ? null : new StreamWriter(outPath, append: false) { AutoFlush = true };
            void Write(string line)
            {
                Console.WriteLine(line);
                log?.WriteLine(line);
            }
            Write($"# {worldName}, every {intervalMs} ms for {seconds} s, range {range}; ms;event;sno;name;etype;x;y;z;yaw;playerDist;playerDir;bossDist;bossAngle");

            var present = new Dictionary<uint, (long Since, RActorRecord Actor)>();
            var bossBuffs = new Dictionary<uint, HashSet<int>>();
            var bossAttributes = new Dictionary<uint, Dictionary<string, string>>();
            var clock = Stopwatch.StartNew();
            bool first = true;
            Vector3 here = player.Position;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (!RActorReader.TryRead(process, module.Base, offsets, out var actors)) { Thread.Sleep(intervalMs); continue; }
                long now = clock.ElapsedMilliseconds;
                if (actors.Where(a => a.IsPlaced && a.ActorSno == player.ActorSno).OrderBy(a => Vector3.Distance(a.Position, here)).FirstOrDefault() is { Id: not 0 } self)
                    here = self.Position;
                var bosses = actors.Where(a => a.IsPlaced && a.AcdId != NoAcdId && defs.Read(a.ActorSno)?.MonsterRarity == D4Data.Format.Conventions.MonsterRarityBoss
                    && Vector3.Distance(a.Position, here) <= range).ToList();
                var nearestBoss = bosses.OrderBy(b => Vector3.Distance(b.Position, here)).FirstOrDefault();

                string Row(string what, RActorRecord a)
                {
                    var delta = a.Position - here;
                    string boss = nearestBoss.Id == 0 ? ";" : $"{Vector2.Distance(new(a.Position.X, a.Position.Y), new(nearestBoss.Position.X, nearestBoss.Position.Y)):F1};{BossAngle(nearestBoss, a.Position):F0}";
                    return $"{now};{what};{a.ActorSno};{store.Name(a.ActorSno)};{defs.Read(a.ActorSno)?.EType};{a.Position.X:F1};{a.Position.Y:F1};{a.Position.Z:F1};" +
                           $"{Yaw(a.Rotation) * 180 / MathF.PI:F0};{new Vector2(delta.X, delta.Y).Length():F1};{ScreenDirections.Bin(delta.X, delta.Y)};{boss}";
                }

                var seen = new HashSet<uint>();
                foreach (var a in actors.Where(a => a.IsPlaced && a.AcdId == NoAcdId && Vector3.Distance(a.Position, here) <= range))
                {
                    if (match is not null && !(store.Name(a.ActorSno) is { } n && match.IsMatch(n))) continue;
                    seen.Add(a.Id);
                    if (present.ContainsKey(a.Id)) continue;
                    present[a.Id] = (now, a);
                    if (!first) Write(Row("+", a));
                }
                foreach (var (id, (since, a)) in present.Where(p => !seen.Contains(p.Key)).ToList())
                {
                    present.Remove(id);
                    Write(Row($"-{now - since}ms", a));
                }

                foreach (var boss in bosses)
                {
                    if (buffId is null || !acds.TryGet(boss.AcdId, out var acd)) continue;
                    var buffs = AcdAttributeReader.Read(process, acd.AttributesAddress)
                        .Where(a => a.AttributeId == buffId && a.RawValue != 0 && a.Parameter != AcdAttribute.NoParameter).Select(a => a.Parameter).ToHashSet();
                    var before = bossBuffs.GetValueOrDefault(boss.Id) ?? [];
                    foreach (int added in buffs.Except(before))
                        Write($"{now};boss+{store.Name(added) ?? added.ToString()};{boss.ActorSno};{store.Name(boss.ActorSno)};;{boss.Position.X:F1};{boss.Position.Y:F1};;{Yaw(boss.Rotation) * 180 / MathF.PI:F0};{Vector3.Distance(boss.Position, here):F1};;;");
                    foreach (int removed in before.Except(buffs))
                        Write($"{now};boss-{store.Name(removed) ?? removed.ToString()};{boss.ActorSno};{store.Name(boss.ActorSno)};;;;;;;;;");
                    bossBuffs[boss.Id] = buffs;
                    // Every other attribute that changes: which one names the power being cast, or its timing.
                    var attributes = AcdWatch.Attributes(process, names, acd)
                        .Where(kv => !NoisyAttribute().IsMatch(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                    if (bossAttributes.TryGetValue(boss.Id, out var old))
                    {
                        foreach (var (key, value) in attributes)
                            if (!old.TryGetValue(key, out var was) || was != value) Write($"{now};attr {key}={was ?? "-"}->{value};{boss.ActorSno};{store.Name(boss.ActorSno)};;;;;;;;;");
                        foreach (var key in old.Keys.Where(k => !attributes.ContainsKey(k))) Write($"{now};attr {key} gone;{boss.ActorSno};{store.Name(boss.ActorSno)};;;;;;;;;");
                    }
                    bossAttributes[boss.Id] = attributes;
                }
                first = false;
                Thread.Sleep(intervalMs);
            }
            Write("# done");
            return 0;
        }
    }

    // The angle (degrees) of a point seen from the boss, against the way the boss faces: 0 ahead, ±180 behind.
    private static float BossAngle(RActorRecord boss, Vector3 at)
    {
        float toward = MathF.Atan2(at.Y - boss.Position.Y, at.X - boss.Position.X);
        float diff = (toward - Yaw(boss.Rotation)) * 180 / MathF.PI;
        return (diff % 360 + 540) % 360 - 180;
    }

    // Timers, damage bookkeeping and the effects already logged above.
    [GeneratedRegex("^(Last_|DOT_|Hitpoints|Buff_Visual_Effect|Combat_Bloodiness|Damage|Stagger)")]
    private static partial Regex NoisyAttribute();

        private static float Yaw(Quaternion q) => MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
}
