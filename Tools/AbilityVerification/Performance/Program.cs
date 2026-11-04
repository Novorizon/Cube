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
    private const int WarmupFrames = 3;
    private const int SampleCount = 7;
    private const int FramesPerSample = 3;
    private static float checksum;

    public static int Main(string[] args)
    {
        string label = Argument(args, "--label", "current");
        string output = Argument(args, "--output", "");
        string sourceRoot = Argument(args, "--source-root", Directory.GetCurrentDirectory());
        var report = new Report
        {
            Label = label,
            Utc = DateTime.UtcNow,
            Framework = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            Processor = ProcessorName(),
            ServerGc = GCSettings.IsServerGC,
            GcLatencyMode = GCSettings.LatencyMode.ToString(),
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default",
            SourceSha256 = SourceHash(sourceRoot),
            WarmupFrames = WarmupFrames,
            Samples = SampleCount,
            FramesPerSample = FramesPerSample
        };
        Console.WriteLine($"{label}: {report.Framework}, {report.Architecture}, {report.Processor}, {report.ProcessorCount} available CPUs, server GC={report.ServerGc}");
        Console.WriteLine("scenario,units,base_modifiers_per_unit,total_modifiers,median_ms_per_operation,p95_ms_per_operation,bytes_per_operation,execution_aborts");

        foreach (int count in new[] { 100, 500, 1000 })
        {
            foreach (int modifiersPerUnit in new[] { 1, 3 })
            {
                var scene = new Scene(count, modifiersPerUnit);
                Measure(report, scene, "queries_per_unit", scene.QueryFrame);
                Measure(report, scene, "update_60hz", () => scene.Tick(1f / 60f));
                Measure(report, scene, "queries_and_update_60hz", () => { scene.QueryFrame(); scene.Tick(1f / 60f); });
                Measure(report, scene, "ten_attack_landed_events", scene.TenAttackEvents);
            }
        }

        foreach (int count in new[] { 100, 500, 1000 })
        {
            var aura = new Scene(count, 3);
            aura.AddAuras();
            Measure(report, aura, "five_aura_sources_250ms_pulse", () => aura.Tick(0.25f));

            var auraFrames = new Scene(count, 3);
            auraFrames.AddAuras(0.1f, 0.5f);
            Measure(report, auraFrames, "five_aura_sources_60hz", () => auraFrames.Tick(1f / 60f));

            var projectiles = new Scene(count, 3);
            projectiles.AddProjectiles();
            Measure(report, projectiles, "100_linear_projectiles_60hz", () => projectiles.Tick(1f / 60f));
        }

        if (!string.IsNullOrEmpty(output))
        {
            string path = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            Console.WriteLine("Saved " + path);
        }
        GC.KeepAlive(checksum);
        return 0;
    }

    private static void Measure(Report report, Scene scene, string name, Action operation)
    {
        for (int frame = 0; frame < WarmupFrames; frame++) operation();
        var milliseconds = new double[SampleCount];
        var allocated = new long[SampleCount];
        long initialAborts = scene.ExecutionAborts;
        long initialCandidates = scene.World.CandidatesReturned;
        long initialQueries = scene.World.Queries;
        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        for (int sample = 0; sample < SampleCount; sample++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int frame = 0; frame < FramesPerSample; frame++) operation();
            milliseconds[sample] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency / FramesPerSample;
            allocated[sample] = (GC.GetAllocatedBytesForCurrentThread() - bytes) / FramesPerSample;
        }
        Array.Sort(milliseconds);
        Array.Sort(allocated);
        var result = new Result
        {
            Scenario = name,
            Units = scene.Units.Length,
            BaseModifiersPerUnit = scene.BaseModifiersPerUnit,
            TotalModifiers = scene.Engine.Modifiers.Count,
            ActiveProjectiles = scene.Engine.Projectiles.Count,
            MedianMs = milliseconds[SampleCount / 2],
            P95Ms = milliseconds[(int)Math.Ceiling(SampleCount * 0.95) - 1],
            MedianAllocatedBytes = allocated[SampleCount / 2],
            ExecutionAborts = scene.ExecutionAborts - initialAborts,
            SetupExecutionAborts = scene.SetupExecutionAborts,
            WorldQueriesPerOperation = (scene.World.Queries - initialQueries) / (double)(SampleCount * FramesPerSample),
            WorldCandidatesPerOperation = (scene.World.CandidatesReturned - initialCandidates) / (double)(SampleCount * FramesPerSample),
            Gen0Collections = GC.CollectionCount(0) - initialGen0,
            Gen1Collections = GC.CollectionCount(1) - initialGen1,
            Gen2Collections = GC.CollectionCount(2) - initialGen2
        };
        report.Results.Add(result);
        Console.WriteLine($"{name},{result.Units},{result.BaseModifiersPerUnit},{result.TotalModifiers},{result.MedianMs:F3},{result.P95Ms:F3},{result.MedianAllocatedBytes},{result.ExecutionAborts}");
    }

    private static string Argument(string[] args, string name, string fallback)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        return fallback;
    }

    private static string ProcessorName()
    {
        if (!File.Exists("/proc/cpuinfo")) return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        string line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(value => value.StartsWith("model name", StringComparison.Ordinal));
        return line == null ? "unknown" : line.Substring(line.IndexOf(':') + 1).Trim();
    }

    private static string SourceHash(string root)
    {
        string directory = Path.Combine(root, "Assets/Scripts/Ability");
        if (!Directory.Exists(directory)) return "unknown (run from repository root or pass --source-root)";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace('\\', '/') + "\n"));
            hash.AppendData(File.ReadAllBytes(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private sealed class Scene
    {
        public readonly AbilitySystem Engine = new AbilitySystem();
        public readonly BenchUnit[] Units;
        public readonly IndexedWorld World;
        public readonly int BaseModifiersPerUnit;
        public long ExecutionAborts;
        public long SetupExecutionAborts;

        public Scene(int count, int modifiersPerUnit)
        {
            BaseModifiersPerUnit = modifiersPerUnit;
            Units = new BenchUnit[count];
            for (int i = 0; i < count; i++) Units[i] = new BenchUnit(i + 1, new Vector3((i % 50) * 2, 0, (i / 50) * 2));
            World = new IndexedWorld(Units);
            Engine.Initialize(World);
            Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) ExecutionAborts++; };
            for (int type = 0; type < modifiersPerUnit; type++)
            {
                var definition = new ModifierDefinition { Name = "armor_" + type, Duration = -1 };
                definition.Properties.Add(ModifierProperty.ArmorBonus, 1f);
                Engine.RegisterModifierDefinition(definition);
            }
            foreach (BenchUnit unit in Units)
            {
                Engine.RegisterUnit(unit);
                for (int type = 0; type < modifiersPerUnit; type++)
                    if (Engine.AddModifier(unit, unit, "armor_" + type) == null) throw new InvalidOperationException("Could not seed modifier.");
            }
            SetupExecutionAborts = ExecutionAborts;
            if (Engine.Modifiers.Count != count * modifiersPerUnit) throw new InvalidOperationException("Unexpected modifier count.");
            foreach (BenchUnit unit in Units)
                if (Engine.HasState(unit, UnitState.Stunned) || Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null) != modifiersPerUnit)
                    throw new InvalidOperationException("Seeded state/property behavior is incorrect.");
        }

        public void QueryFrame()
        {
            float total = 0;
            foreach (BenchUnit unit in Units)
            {
                if (Engine.HasState(unit, UnitState.Stunned)) total++;
                total += Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null);
            }
            checksum = total;
        }

        public void Tick(float delta)
        {
            World.Time += delta;
            Engine.Update(delta);
        }

        public void TenAttackEvents()
        {
            for (int i = 0; i < 10; i++) Engine.NotifyAttackLanded(Units[0], Units[Units.Length - 1]);
        }

        public void AddAuras(float interval = 0.25f, float duration = 2)
        {
            Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "aura_child", Duration = 2 });
            Engine.RegisterModifierDefinition(new ModifierDefinition
            {
                Name = "aura_source", Duration = -1, AuraModifierName = "aura_child", AuraRadius = 4,
                AuraThinkInterval = interval, AuraDuration = duration, AuraTargetTeam = TargetTeam.Friendly
            });
            for (int i = 0; i < 5; i++)
            {
                BenchUnit source = Units[(i * Units.Length / 5) + 10];
                if (Engine.AddModifier(source, source, "aura_source") == null) throw new InvalidOperationException("Could not seed aura.");
            }
            SetupExecutionAborts = ExecutionAborts;
        }

        public void AddProjectiles()
        {
            var definition = new ProjectileDefinition
            {
                Name = "piercing", Speed = 0.1f, Radius = 1.1f, Distance = 1000000, MaxLifetime = 1000000,
                DeleteOnHit = false, TargetTeam = TargetTeam.Both
            };
            for (int i = 0; i < 100; i++)
            {
                // Move very slowly so measured frames retain the same candidate density.
                BenchUnit near = Units[(i * 17) % Units.Length];
                Vector3 origin = near.Position + new Vector3(0, 0, 1);
                if (Engine.CreateLinearProjectile(null, Units[0], origin, Vector3.right, definition) == null)
                    throw new InvalidOperationException("Could not seed projectile.");
            }
            SetupExecutionAborts = ExecutionAborts;
        }
    }

    private sealed class BenchUnit : IUnit
    {
        public int EntityId { get; }
        public int TeamId => 1;
        public UnitType UnitType => UnitType.Basic;
        public bool IsAlive => true;
        public bool IsMagicImmune => false;
        public bool IsInvulnerable => false;
        public Vector3 Position { get; }
        public BenchUnit(int entityId, Vector3 position) { EntityId = entityId; Position = position; }
        public bool IsVisibleToTeam(int teamId) => true;
        public void ApplyDamage(DamageResult result) { }
        public void Heal(HealInfo info) { }
    }

    private sealed class IndexedWorld : IWorld
    {
        private const int CellSize = 4;
        private readonly Dictionary<(int, int), List<IUnit>> cells = new Dictionary<(int, int), List<IUnit>>();
        public float Time { get; set; }
        public long Queries;
        public long CandidatesReturned;

        public IndexedWorld(IEnumerable<BenchUnit> units)
        {
            foreach (BenchUnit unit in units)
            {
                var key = Cell(unit.Position.x, unit.Position.z);
                if (!cells.TryGetValue(key, out List<IUnit> bucket)) cells.Add(key, bucket = new List<IUnit>());
                bucket.Add(unit);
            }
        }

        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            Queries++;
            var min = Cell(center.x - radius, center.z - radius);
            var max = Cell(center.x + radius, center.z + radius);
            float radiusSquared = radius * radius;
            for (int x = min.Item1; x <= max.Item1; x++)
            {
                for (int z = min.Item2; z <= max.Item2; z++)
                {
                    if (!cells.TryGetValue((x, z), out List<IUnit> bucket)) continue;
                    foreach (IUnit unit in bucket)
                    {
                        if ((unit.Position - center).sqrMagnitude > radiusSquared) continue;
                        results.Add(unit);
                        CandidatesReturned++;
                    }
                }
            }
        }

        public bool HasLineOfSight(IUnit viewer, Vector3 position) => true;
        private static (int, int) Cell(float x, float z) => ((int)MathF.Floor(x / CellSize), (int)MathF.Floor(z / CellSize));
    }

    public sealed class Report
    {
        public string Label;
        public DateTime Utc;
        public string Framework;
        public string Os;
        public string Architecture;
        public string Processor;
        public int ProcessorCount;
        public bool ServerGc;
        public string GcLatencyMode;
        public string TieredCompilation;
        public string SourceSha256;
        public int WarmupFrames;
        public int Samples;
        public int FramesPerSample;
        public List<Result> Results = new List<Result>();
    }

    public sealed class Result
    {
        public string Scenario;
        public int Units;
        public int BaseModifiersPerUnit;
        public int TotalModifiers;
        public int ActiveProjectiles;
        public double MedianMs;
        public double P95Ms;
        public long MedianAllocatedBytes;
        public long ExecutionAborts;
        public long SetupExecutionAborts;
        public double WorldQueriesPerOperation;
        public double WorldCandidatesPerOperation;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
    }
}
