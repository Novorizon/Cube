# Ability runtime performance probe

This executable compiles the production `Assets/Scripts/Ability` sources through `CoreSources.csproj`. It uses a small .NET world/unit adapter and the existing portable Unity math substitutes. It does not compile into the Unity game and needs no benchmark NuGet packages.

Run from the repository root with .NET 8:

```sh
dotnet build Tools/AbilityVerification/Performance/AbilityPerformance.csproj -c Release
DOTNET_TieredCompilation=0 dotnet Tools/AbilityVerification/Performance/bin/Release/net8.0/AbilityPerformance.dll --label current --output Tools/AbilityVerification/Performance/results/current.json
```

In the managed workspace, the executable is `/workspace/.tools/dotnet/dotnet`; set `DOTNET_CLI_HOME=/workspace/.tools/dotnet-home` if needed. `--source-root` can identify the repository when running from another directory. Keep the same build mode, JIT configuration, machine and parameters for comparisons. Tiered compilation is disabled to avoid optimization-tier transitions in these short samples; this is not a recommendation for game runtime configuration.

Each scenario warms up for 3 operations and records 7 samples of 3 operations. The result is the median of those sample means, using `Stopwatch.GetTimestamp`. `P95Ms` is the nearest-rank 95th percentile of **seven sample means** (their maximum), not a statistically established per-frame tail latency. The runner does not force GC between samples: collection costs belong in the workload. Allocation comes from `GC.GetAllocatedBytesForCurrentThread` and includes only managed allocation on the calling thread. Setup, logging and report serialization are excluded. Results contain GC collection deltas, execution-budget aborts, world query/candidate counts, runtime/CPU metadata, and a SHA-256 fingerprint of Ability production source paths and contents.

The scenes cover:

- 100, 500 and 1000 units, with either 1 or 3 permanent static armor modifiers each. The query operation performs one absent-state query and one armor query per unit. Separate rows measure queries, a 1/60-second update, and their combination.
- Ten attack-landed event broadcasts per operation, including large scenes where most modifiers have no event callback. The configured execution budget stays at its production default; the probe reports aborts instead of silently increasing it.
- Five aura sources with a radius of 4 and a 250 ms refresh interval, with 3 base modifiers per unit. Each operation advances **250 ms and includes an aura pulse**, rather than averaging the pulse into ordinary frames.
- A supplemental ordinary-frame aura scene advances 1/60 second with a 100 ms refresh interval and 500 ms child duration. It includes boundary coverage checks and periodic refresh cost. This row was added after the baseline capture and has no historical comparison.
- 100 persistent piercing linear projectiles, with 3 base modifiers per unit. Each operation advances 1/60 second. Projectiles move slowly through a fixed dense field so candidate density remains stable. This measures movement, broad-phase queries and target validation after initial contacts, rather than continuous new impacts, damage, or presentation.

The world uses a fixed 4-unit spatial grid over a 2-unit-spaced field. It returns radius-filtered broad candidates; the production engine applies target rules. It is not a full-world scan. Units implement `IUnit` without HP simulation, Unity transforms, physics or presentation. The setup verifies seeded state/property values. Gameplay regression tests separately cover damage, event scoping, callback mutation, nested queries, lifecycle cleanup, and the budget behavior.

`results/baseline.json` was captured before the modifier lookup/event-dispatch optimization. It is intentionally retained with its abort diagnostics: truncated event chains are correctness failures, not valid fast results. Some baseline aura operations also abort and therefore do not complete equivalent work. Compare these rows as diagnostics, rather than claiming a precise aura speedup from incomplete work.

The baseline host was Debian 13 x64, .NET 8.0.33, AMD EPYC 9V74, 4 CPUs available to the container, workstation GC / Interactive latency. It is a shared virtualized host; timing can vary with contention. Managed-byte counts and scaling trends are more reliable than sub-millisecond timing differences.

For the common query-plus-update operation, the captured medians are:

| Units | Modifiers per unit | Baseline → optimized (ms) | Baseline → optimized (managed bytes) |
| --- | --- | --- | --- |
| 100 | 1 | 0.261 → 0.022 | 184,104 → 24 |
| 100 | 3 | 0.383 → 0.040 | 532,904 → 24 |
| 500 | 1 | 2.557 → 0.129 | 4,120,104 → 24 |
| 500 | 3 | 9.652 → 0.225 | 12,264,104 → 24 |
| 1000 | 1 | 10.265 → 0.231 | 16,240,104 → 24 |
| 1000 | 3 | 36.849 → 0.543 | 48,528,104 → 24 |

The engine now copies only a queried unit's modifiers, reuses bounded snapshots, checks runtime membership with hash sets, and dispatches only subscribed configured events / custom listeners. Custom callbacks retain their global event contract, registration order, fresh mutable property contexts and independent damage snapshots. The sealed built-in stat/configured scripts skip unused property contexts while still reading live definition values and stack counts. No dynamic state or property value is cached.

At 1000 units / 3000 base modifiers, the optimized query-only operation allocates 0 managed bytes after warmup. Update retains a small fixed 24-byte cost in this probe. The projectile row allocates 11,224 bytes per operation; the 250 ms aura pulse allocates 13,680, and the ordinary aura frame median allocates 4,736. These paths still create query/event/options data; callback-heavy gameplay, damage payloads, new/expired instances and pool growth will allocate. The ten-event row has no actual modifier listeners, so its very small cost does not represent ten damage/proc chains. The regression suite verifies thousands of unrelated configured listeners do not consume the execution budget, while custom recursive chains remain bounded.

These are synthetic .NET measurements, **not Unity Editor, Mono, IL2CPP, target-device FPS, rendering, asset loading or physics measurements**. The portable `ListPool<T>` substitute used by configured action execution allocates a list, unlike Unity's pool; action-heavy scenes need measurements in Unity. The production runtime snapshot pool used by the optimized queries and updates runs unchanged here. A 60 Hz frame provides 16.67 ms for the entire game, not just the skill system. Capture Unity Profiler CPU/GC data in representative scenes on the intended devices before choosing a shipping unit/projectile budget. Follow-up optimizations should target measured work; dynamic script state/property results must not be cached as if static.
