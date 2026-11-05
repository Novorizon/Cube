using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Ability;
using UnityEngine;

internal static class Program
{
    private const float Delta = 1f / 60f;
    private static double checksum;

    public static int Main(string[] args)
    {
        int frames = Integer(args, "--frames", 120, 20, 1000);
        int warmup = Integer(args, "--warmup", 10, 1, 100);
        int rounds = Integer(args, "--rounds", 3, 1, 10);
        string root = Argument(args, "--source-root", Directory.GetCurrentDirectory());
        Report report = new Report
        {
            Label = Argument(args, "--label", "current"),
            Utc = DateTime.UtcNow,
            SourceSha256 = SourceHash(root),
            SourceReference = Argument(args, "--source-reference", "working-tree snapshot"),
            Framework = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Processor = ProcessorName(),
            AvailableProcessors = Environment.ProcessorCount,
            ServerGc = GCSettings.IsServerGC,
            GcLatencyMode = GCSettings.LatencyMode.ToString(),
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default",
            WarmupFramesPerRound = warmup,
            MeasuredFramesPerRound = frames,
            Rounds = rounds
        };
        Console.WriteLine("scenario,units,round,p50_ms,p95_ms,p99_ms,max_ms,p50_bytes,damage_events,heal_events,aborts,setup_aborts,valid,cleanup_objects");
        foreach (int count in new[] { 100, 500, 1000 })
            foreach (string scenario in new[] { "damage_plain", "damage_lifesteal_reflect", "fresh_tracking_impacts", "fresh_linear_impacts", "modifier_churn_expire", "clear_and_rebuild", "dynamic_property_queries", "find_units_all_in_radius", "find_units_broad_wrappers" })
                for (int round = 1; round <= rounds; round++) report.Results.Add(Measure(scenario, count, round, warmup, frames));

        string output = Argument(args, "--output", string.Empty);
        if (!string.IsNullOrEmpty(output))
        {
            string full = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, JsonSerializer.Serialize(report, new JsonSerializerOptions { IncludeFields = true }));
            Console.WriteLine("Saved " + full);
        }
        GC.KeepAlive(checksum);
        return report.Results.All(value => value.WorkComplete && value.ExecutionAborts == 0 && value.SetupExecutionAborts == 0 && value.CleanupObjects == 0) ? 0 : 1;
    }

    private static Result Measure(string scenario, int count, int round, int warmup, int frames)
    {
        Scene scene = null;
        Result result = new Result { Scenario = scenario, Units = count, ModifiersPerUnit = 3, Round = round, WorkComplete = true, Frames = new List<FrameSample>(frames) };
        try
        {
            scene = new Scene(count, scenario);
            result.SetupExecutionAborts = scene.Counters.Aborts;
            for (int frame = 0; frame < warmup; frame++) { scene.Prepare(); scene.Frame(); scene.VerifyFrame(); }
            Counters before = scene.Counters;
            long queries = scene.World.Queries, candidates = scene.World.Candidates;
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            for (int frame = 0; frame < frames; frame++)
            {
                scene.Prepare();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                scene.Frame();
                double milliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                scene.VerifyFrame();
                result.Frames.Add(new FrameSample { Milliseconds = milliseconds, AllocatedBytes = bytes });
            }
            result.DamageEvents = scene.Counters.DamageEvents - before.DamageEvents;
            result.HealEvents = scene.Counters.HealEvents - before.HealEvents;
            result.DamageApplied = scene.Counters.Damage - before.Damage;
            result.HealingApplied = scene.Counters.Healing - before.Healing;
            result.ExecutionAborts = scene.Counters.Aborts - before.Aborts;
            result.WorldQueries = scene.World.Queries - queries;
            result.WorldCandidates = scene.World.Candidates - candidates;
            result.Gen0Collections = GC.CollectionCount(0) - gen0;
            result.Gen1Collections = GC.CollectionCount(1) - gen1;
            result.Gen2Collections = GC.CollectionCount(2) - gen2;
        }
        catch (Exception error)
        {
            result.WorkComplete = false;
            result.Failure = error.GetType().Name + ": " + error.Message;
            if (scene != null) result.ExecutionAborts = scene.Counters.Aborts;
        }
        finally
        {
            if (scene != null)
            {
                long start = Stopwatch.GetTimestamp();
                scene.Engine.ClearRuntime();
                result.FinalCleanupMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                AbilityRuntimeSnapshot snapshot = scene.Engine.CreateRuntimeSnapshot();
                result.CleanupObjects = snapshot.Modifiers.Count + snapshot.Projectiles.Count + snapshot.Thinkers.Count + snapshot.Units.Sum(value => value.Abilities.Count);
                result.BudgetMessages.AddRange(scene.BudgetMessages);
            }
        }
        if (result.Frames.Count > 0)
        {
            double[] time = result.Frames.Select(value => value.Milliseconds).OrderBy(value => value).ToArray();
            long[] bytes = result.Frames.Select(value => value.AllocatedBytes).OrderBy(value => value).ToArray();
            result.P50Ms = Percentile(time, 0.5);
            result.P95Ms = Percentile(time, 0.95);
            result.P99Ms = Percentile(time, 0.99);
            result.MaxMs = time[time.Length - 1];
            result.P50AllocatedBytes = bytes[(int)Math.Ceiling(bytes.Length * 0.5) - 1];
            result.MaxAllocatedBytes = bytes[bytes.Length - 1];
        }
        result.MeasurementValid = result.WorkComplete && result.ExecutionAborts == 0 && result.SetupExecutionAborts == 0 && result.CleanupObjects == 0;
        Console.WriteLine($"{scenario},{count},{round},{result.P50Ms:F3},{result.P95Ms:F3},{result.P99Ms:F3},{result.MaxMs:F3},{result.P50AllocatedBytes},{result.DamageEvents},{result.HealEvents},{result.ExecutionAborts},{result.SetupExecutionAborts},{result.MeasurementValid},{result.CleanupObjects}");
        if (!result.WorkComplete) Console.WriteLine("FAIL " + result.Failure);
        return result;
    }

    private sealed class Scene
    {
        public readonly AbilitySystem Engine = new AbilitySystem();
        public readonly BenchUnit[] Units;
        public readonly IndexedWorld World;
        public readonly List<string> BudgetMessages = new List<string>();
        public Counters Counters;
        private readonly string scenario;
        private readonly Ability[] abilities;
        private readonly DamageInfo[] damage;
        private readonly ProjectileDefinition tracking = new ProjectileDefinition { Speed = 240f, Radius = 0.05f, Distance = 20f, MaxLifetime = 1f, TargetTeam = TargetTeam.Enemy, TrackingTargetPolicy = TrackingTargetPolicy.RevalidateTarget };
        private readonly ProjectileDefinition linear = new ProjectileDefinition { Speed = 120f, Radius = 0.1f, Distance = 1.25f, MaxLifetime = 0.05f, TargetTeam = TargetTeam.Enemy, DeleteOnHit = true };
        private Counters frameStart;
        private float queryTotal;
        private int queryGeneration;
        private readonly List<IUnit> queryResults;
        private readonly HashSet<int> verifiedIds = new HashSet<int>();
        private readonly TargetQuery broadQuery;
        private readonly float queryRadius;
        private readonly int expectedQueryUnits;

        public Scene(int count, string scenario)
        {
            this.scenario = scenario;
            Units = new BenchUnit[count];
            for (int index = 0; index < count; index++)
            {
                int pair = index / 2;
                Units[index] = new BenchUnit(index + 1, index % 2 + 1, new Vector3(pair % 25 * 4 + index % 2, 0, pair / 25 * 4));
            }
            World = new IndexedWorld(Units, scenario.StartsWith("find_units_", StringComparison.Ordinal), scenario == "find_units_broad_wrappers");
            Engine.Initialize(World);
            queryRadius = scenario == "find_units_broad_wrappers" ? 30f : 10000f;
            expectedQueryUnits = Units.Count(unit => unit.Position.sqrMagnitude <= queryRadius * queryRadius);
            queryResults = new List<IUnit>(count * 2);
            broadQuery = new TargetQuery { Engine = Engine, Team = TargetTeam.Both, Types = UnitType.All, Flags = TargetFlags.IncludeSelf };
            Engine.EventRaised += value =>
            {
                if (value.EventType == RuntimeEventType.ExecutionAborted)
                {
                    Counters.Aborts++;
                    if (BudgetMessages.Count < 8) BudgetMessages.Add(value.Message);
                }
                if (value.EventType == RuntimeEventType.DamageApplied) { Counters.DamageEvents++; Counters.Damage += value.Value; }
                if (value.EventType == RuntimeEventType.HealApplied) { Counters.HealEvents++; Counters.Healing += value.Value; }
            };
            DefineModifiers();
            Seed();
            damage = new DamageInfo[count / 2];
            abilities = new Ability[count / 2];
            Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "impact", OnProjectileHitActions = { new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, DamageType = DamageType.Pure, Value = LevelValue.Constant(25f) } } });
            for (int pair = 0; pair < count / 2; pair++)
            {
                damage[pair] = new DamageInfo { Attacker = Units[pair * 2], Victim = Units[pair * 2 + 1], Amount = 100f, DamageType = DamageType.Pure };
                if (scenario == "fresh_tracking_impacts" || scenario == "fresh_linear_impacts")
                    abilities[pair] = Engine.AddAbility(Units[pair * 2], "impact");
            }
            Require(Engine.Modifiers.Count == count * 3, "Three modifiers per unit must be seeded.");
        }

        private void DefineModifiers()
        {
            for (int index = 0; index < 3; index++)
            {
                ModifierDefinition definition = new ModifierDefinition { Name = "base_" + index, Duration = -1 };
                definition.Properties.Add(ModifierProperty.ArmorBonus, 1f);
                Engine.RegisterModifierDefinition(definition);
                if (scenario == "dynamic_property_queries") Engine.RegisterModifierScript(definition.Name, () => new DynamicProperty());
            }
            ModifierDefinition leech = new ModifierDefinition { Name = "leech", Duration = -1, TriggerEventType = ModifierEventType.DamageDealt, TriggerExcludedDamageFlags = DamageFlags.Reflected };
            leech.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Parent, ValueSource = ActionValueSource.EventDamage, Value = LevelValue.Constant(0.25f) });
            Engine.RegisterModifierDefinition(leech);
            ModifierDefinition reflect = new ModifierDefinition { Name = "reflect", Duration = -1, TriggerEventType = ModifierEventType.DamageTaken, TriggerExcludedDamageFlags = DamageFlags.Reflected };
            reflect.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.EventSource, ValueSource = ActionValueSource.EventDamage, Value = LevelValue.Constant(0.2f), DamageType = DamageType.Pure, DamageFlags = DamageFlags.Reflected | DamageFlags.NoSpellAmplification });
            Engine.RegisterModifierDefinition(reflect);
            Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "short_lived", Duration = 0.005f });
        }

        private void Seed()
        {
            foreach (BenchUnit unit in Units)
            {
                // Query-wrapper scenes deliberately use the valid unregistered-world boundary;
                // explicit RegisterUnit would classify an alternate wrapper as a stale owner.
                if (!scenario.StartsWith("find_units_", StringComparison.Ordinal)) Engine.RegisterUnit(unit);
                Require(Engine.AddModifier(unit, unit, "base_0") != null, "Seed base_0 failed.");
                Require(Engine.AddModifier(unit, unit, scenario == "damage_lifesteal_reflect" ? "leech" : "base_1") != null, "Seed second modifier failed.");
                Require(Engine.AddModifier(unit, unit, scenario == "damage_lifesteal_reflect" ? "reflect" : "base_2") != null, "Seed third modifier failed.");
            }
        }

        public void Prepare()
        {
            foreach (BenchUnit unit in Units) unit.Reset();
            queryResults.Clear();
            frameStart = Counters;
        }

        public void Frame()
        {
            if (scenario == "damage_plain" || scenario == "damage_lifesteal_reflect")
            {
                foreach (DamageInfo request in damage) Engine.ApplyDamage(request);
            }
            else if (scenario == "fresh_tracking_impacts" || scenario == "fresh_linear_impacts")
            {
                for (int pair = 0; pair < damage.Length; pair++)
                {
                    if (scenario == "fresh_tracking_impacts") Engine.CreateTrackingProjectile(abilities[pair], Units[pair * 2], Units[pair * 2 + 1], tracking);
                    else Engine.CreateLinearProjectile(abilities[pair], Units[pair * 2], Units[pair * 2].Position, Vector3.right, linear);
                }
                Tick();
            }
            else if (scenario == "modifier_churn_expire")
            {
                foreach (BenchUnit unit in Units) Engine.AddModifier(unit, unit, "short_lived");
                Tick();
            }
            else if (scenario == "clear_and_rebuild")
            {
                Engine.ClearRuntime();
                Seed();
            }
            else if (scenario.StartsWith("find_units_", StringComparison.Ordinal))
                Engine.FindUnits(Vector3.zero, queryRadius, broadQuery, queryResults);
            else
            {
                queryGeneration++;
                queryTotal = 0f;
                foreach (BenchUnit unit in Units)
                {
                    unit.DynamicArmor = queryGeneration % 2 == 0 ? 2f : 4f;
                    queryTotal += Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null);
                }
                checksum = queryTotal;
            }
        }

        private void Tick() { World.Time += Delta; Engine.Update(Delta); }

        public void VerifyFrame()
        {
            long pairs = Units.Length / 2;
            if (scenario == "damage_plain")
            {
                Require(Counters.DamageEvents - frameStart.DamageEvents == pairs, "The plain workload must commit one damage event per pair.");
                Near(Counters.Damage - frameStart.Damage, pairs * 100f, "Plain applied HP");
            }
            else if (scenario == "damage_lifesteal_reflect")
            {
                Require(Counters.DamageEvents - frameStart.DamageEvents == pairs * 2, "Every base hit must also reflect once.");
                Require(Counters.HealEvents - frameStart.HealEvents == pairs, "Every base hit must produce one effective leech heal.");
                Near(Counters.Damage - frameStart.Damage, pairs * 120f, "Damage and reflection HP");
                Near(Counters.Healing - frameStart.Healing, pairs * 25f, "Leech HP");
                foreach (BenchUnit unit in Units) Near(unit.CurrentHealth, unit.TeamId == 1 ? 500005f : 499900f, "HP must agree with committed proc facts");
            }
            else if (scenario == "fresh_tracking_impacts" || scenario == "fresh_linear_impacts")
            {
                Require(Counters.DamageEvents - frameStart.DamageEvents == pairs, "Every newly spawned projectile must hit exactly one target.");
                Near(Counters.Damage - frameStart.Damage, pairs * 25f, "Fresh projectile applied HP");
                Require(Engine.Projectiles.Count == 0, "The same persistent projectile must not be reused across measured frames.");
            }
            else if (scenario == "dynamic_property_queries") Near(queryTotal, Units.Length * 3f * (queryGeneration % 2 == 0 ? 2f : 4f), "Dynamic properties must remain live");
            else if (scenario.StartsWith("find_units_", StringComparison.Ordinal))
            {
                verifiedIds.Clear();
                foreach (IUnit unit in queryResults)
                {
                    Require(verifiedIds.Add(unit.EntityId), "Broad queries must collapse duplicate collider wrappers by entity ID.");
                    Require(unit.Position.sqrMagnitude <= queryRadius * queryRadius, "Broad candidates outside the requested radius must not leak into final results.");
                }
                Require(queryResults.Count == expectedQueryUnits, "Broad queries must return every unique in-radius entity exactly once.");
            }
            Require(Engine.Modifiers.Count == Units.Length * 3, "Every frame must return to its three-modifier baseline.");
            Require(Counters.Aborts == frameStart.Aborts, "Normal workloads must not consume the configured execution budget.");
        }
    }

    private sealed class DynamicProperty : ModifierScript
    {
        public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) => property == ModifierProperty.ArmorBonus ? ((BenchUnit)Parent).DynamicArmor : 0f;
    }

    private sealed class BenchUnit : ICombatUnit
    {
        public BenchUnit(int id, int team, Vector3 position) { EntityId = id; TeamId = team; Position = position; Reset(); }
        public int EntityId { get; }
        public int TeamId { get; }
        public Vector3 Position { get; }
        public UnitType UnitType => UnitType.Basic;
        public bool IsAlive => CurrentHealth > 0f;
        public bool IsMagicImmune => false;
        public bool IsInvulnerable => false;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => 1000000f;
        public float BaseArmor => 0f;
        public float BaseMagicResistancePercent => 0f;
        public float BasePhysicalDamageBlock => 0f;
        public float DynamicArmor;
        public void Reset() => CurrentHealth = 500000f;
        public bool IsVisibleToTeam(int teamId) => true;
        public DamageApplicationResult ApplyFinalDamage(DamageResult result)
        {
            float before = CurrentHealth;
            CurrentHealth = Math.Max(0f, CurrentHealth - result.FinalAmount);
            return new DamageApplicationResult(before - CurrentHealth, CurrentHealth == 0f);
        }
        public HealApplicationResult ApplyFinalHeal(HealInfo info)
        {
            float before = CurrentHealth;
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + info.Amount);
            return new HealApplicationResult(CurrentHealth - before);
        }
        public void ApplyDamage(DamageResult result) => ApplyFinalDamage(result);
        public void Heal(HealInfo info) => ApplyFinalHeal(info);
    }

    private sealed class IndexedWorld : IWorld
    {
        private readonly Dictionary<(int, int), List<BenchUnit>> grid = new Dictionary<(int, int), List<BenchUnit>>();
        private readonly List<BenchUnit> broadCandidates;
        public IndexedWorld(IEnumerable<BenchUnit> units, bool broad, bool distinctWrappers)
        {
            if (broad) broadCandidates = new List<BenchUnit>();
            foreach (BenchUnit unit in units)
            {
                var key = Cell(unit.Position.x, unit.Position.z);
                if (!grid.TryGetValue(key, out List<BenchUnit> bucket)) grid.Add(key, bucket = new List<BenchUnit>());
                bucket.Add(unit);
                if (broad)
                {
                    broadCandidates.Add(unit);
                    broadCandidates.Add(distinctWrappers ? new BenchUnit(unit.EntityId, unit.TeamId, unit.Position) : unit);
                }
            }
        }
        public float Time { get; set; }
        public long Queries, Candidates;
        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            Queries++;
            if (broadCandidates != null)
            {
                foreach (BenchUnit unit in broadCandidates) { results.Add(unit); Candidates++; }
                return;
            }
            var min = Cell(center.x - radius, center.z - radius);
            var max = Cell(center.x + radius, center.z + radius);
            for (int x = min.Item1; x <= max.Item1; x++)
                for (int z = min.Item2; z <= max.Item2; z++)
                    if (grid.TryGetValue((x, z), out List<BenchUnit> units))
                        foreach (BenchUnit unit in units)
                            if ((unit.Position - center).sqrMagnitude <= radius * radius) { results.Add(unit); Candidates++; }
        }
        public bool HasLineOfSight(IUnit viewer, Vector3 position) => true;
        private static (int, int) Cell(float x, float z) => ((int)MathF.Floor(x / 4), (int)MathF.Floor(z / 4));
    }

    private struct Counters { public long DamageEvents, HealEvents, Aborts; public double Damage, Healing; }
    public struct FrameSample { public double Milliseconds; public long AllocatedBytes; }
    public sealed class Result
    {
        public string Scenario, Failure;
        public int Units, ModifiersPerUnit, Round, CleanupObjects;
        public bool WorkComplete, MeasurementValid;
        public double P50Ms, P95Ms, P99Ms, MaxMs, FinalCleanupMs, DamageApplied, HealingApplied;
        public long P50AllocatedBytes, MaxAllocatedBytes, DamageEvents, HealEvents, ExecutionAborts, SetupExecutionAborts, WorldQueries, WorldCandidates;
        public int Gen0Collections, Gen1Collections, Gen2Collections;
        public List<FrameSample> Frames = new List<FrameSample>();
        public List<string> BudgetMessages = new List<string>();
    }
    public sealed class Report
    {
        public string Label, SourceSha256, SourceReference, Framework, Os, Architecture, Processor, GcLatencyMode, TieredCompilation;
        public DateTime Utc;
        public int AvailableProcessors, WarmupFramesPerRound, MeasuredFramesPerRound, Rounds;
        public bool ServerGc;
        public List<Result> Results = new List<Result>();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) >= 0.05) throw new InvalidOperationException(message + ": " + actual + " != " + expected);
    }
    private static double Percentile(double[] sorted, double q) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * q) - 1)];
    private static string Argument(string[] args, string name, string fallback) { for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return fallback; }
    private static int Integer(string[] args, string name, int fallback, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, int.Parse(Argument(args, name, fallback.ToString()))));
    private static string ProcessorName()
    {
        if (!File.Exists("/proc/cpuinfo")) return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        string line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(value => value.StartsWith("model name", StringComparison.Ordinal));
        return line == null ? "unknown" : line.Substring(line.IndexOf(':') + 1).Trim();
    }
    private static string SourceHash(string root)
    {
        string directory = Path.Combine(root, "Assets/Scripts/Ability");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Run from the source root or supply --source-root.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/') + "\n"));
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
