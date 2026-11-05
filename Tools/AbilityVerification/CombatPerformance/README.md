# Ability combat and lifecycle probe

This executable measures production Ability code through the portable CoreSources assembly. It extends the older static-modifier probe with actual HP application, damage reactions, new projectile hits, object churn and broad-query results. It does not use Unity Editor, transforms, physics, rendering or asset loading.

Run with .NET 8 from the source root:

~~~sh
dotnet build Tools/AbilityVerification/CombatPerformance/CombatPerformance.csproj -c Release -m:1
DOTNET_TieredCompilation=0 dotnet Tools/AbilityVerification/CombatPerformance/bin/Release/net8.0/CombatPerformance.dll --label current --output Tools/AbilityVerification/CombatPerformance/results/current.json
~~~

Optional arguments are --frames, --warmup, --rounds, --source-root and --source-reference. Defaults are 10 warmup operations followed by 120 individually timed operations in each of three fresh rounds. Keep build mode, JIT configuration, hardware and all arguments identical for comparisons. Disabling tiered compilation stabilizes these short measurements; it is not a game runtime recommendation.

The process returns nonzero if any scene has incorrect work, setup/runtime budget aborts or failed cleanup. Such rows remain in the report with MeasurementValid=false. Do not discard them or compare their time as if they completed equivalent work.

## What one sample means

A sample is one **fixed operation batch**, called a frame in the code. It is not a measured Unity game frame or a claim that this workload occurs every 1/60 second in a real battle. Damage batches do not include a separate Engine.Update; projectile and expiry batches do. All scenes use 100, 500 and 1000 units with exactly three baseline modifiers per unit.

| Scenario | Work in one timed batch |
| --- | --- |
| damage_plain | N/2 separate pure damage applications of 100 HP, one attacker/victim pair each |
| damage_lifesteal_reflect | N/2 applications of 100 HP; each reflects 20 pure HP once and heals the initial attacker for 25 HP. Configured reflected-flag filters prevent repeated reflection/leech |
| fresh_tracking_impacts | Create N/2 new tracking projectiles, then advance 1/60 second; every projectile hits once for 25 HP and is removed |
| fresh_linear_impacts | Create N/2 new short, narrow linear projectiles, advance 1/60 second, apply 25 HP per newly hit target and remove the projectiles |
| modifier_churn_expire | Add one short modifier per unit, advance 1/60 second, expire all of them and return to the three-modifier baseline |
| clear_and_rebuild | Clear the complete runtime, re-register all units and create their three baseline modifiers again |
| dynamic_property_queries | Query three custom dynamic property scripts per unit. Values alternate between operations and are checked, so caching a stale result would fail |
| find_units_all_in_radius | The broad world returns every in-radius unit twice using the same object. The final query must return N objects; this is an equivalent-work comparison for the old List.Contains path and the new identity set |
| find_units_broad_wrappers | Broad candidates contain out-of-radius units and alternate objects with duplicate entity IDs. Final results must contain each in-radius entity once. A wrong historical implementation is invalid, not a fast comparison |

Damage and projectile scenes use actual ICombatUnit health adapters, with HP reset to 500,000 before timing each batch. This prevents deaths or a full-health cap from silently reducing damage or healing work. HP changes, damage/heal event counts, dynamic values, object counts and query identity/radius rules are checked after each batch. Counters must show two damage facts and one effective heal per leech/reflection pair.

In the query-wrapper scenes, units are world-provided and deliberately not explicitly registered with RegisterUnit: canonical registration would correctly classify a different wrapper as an obsolete owner before identity deduplication could be exercised. Their modifiers are still seeded normally. The ordinary spatial world uses a fixed four-unit grid; paired units are one unit apart and pairs are four units apart. Query-only scenes return their fixed broad candidate array without grid pruning, so grid traversal cannot hide the engine's filtering/deduplication cost. None of these adapters models Unity physics.

## Measurement and validity

Raw Milliseconds and AllocatedBytes are retained for every measured batch in compact JSON. P50/P95/P99 use nearest-rank percentiles of those individual samples, rather than percentiles of sample averages. With 120 observations, high-tail estimates remain limited and correlated; MaxMs is reported separately. Results for the three rounds remain separate. A summary table uses the median of round P50 values and does not silently pool samples or omit a slow round.

Timing uses Stopwatch.GetTimestamp. Managed allocation uses GC.GetAllocatedBytesForCurrentThread around the operation only. Scene construction, unit HP reset, assertions, result storage, final cleanup and report serialization are outside that interval. Raw sample storage is preallocated and uses value types. GC collection deltas span the measured loop; no forced collection occurs between samples. Runtime listeners and their real event-copy overhead are included.

The report records actual damage/heal counts and amounts, world queries/candidates, collection counts, setup and runtime aborts, cleanup time and remaining runtime objects. Final cleanup always calls the real ClearRuntime and checks its snapshot. SourceSha256 hashes production C# paths/contents; SourceReference labels the source snapshot. The previous probe and these scenes are different workloads and their absolute timings must not be compared as equivalent operations.

The portable Unity ListPool substitute allocates lists. Configured action-heavy allocation includes that substitute cost; production Unity pooling may reduce part of it. Runtime snapshot pools and all gameplay logic remain the production implementation. The shared host can be contended, so allocation counts and scaling are more stable than small timing differences. These results do not establish target-device CPU budgets, per-frame GC, throughput limits or FPS.

## Fixed baseline

results/baseline.json was compiled in a separate snapshot of the source at remote commit 38d27c5e5ee654901a402d078d6fbf84d38c5dc6 (local task commit d6ac9ad), before this audit's runtime edits. Its C# fingerprint is:

~~~text
6b80812289eba38805aee121bfdb79f5d2f1ef6325cc94dcdc639d4a32e4f186
~~~

The same new harness was copied to that snapshot and compiled there. It is not a baseline built from the concurrently edited working tree.

At 1000 units, medians of three round P50 batches were:

| Batch | Baseline ms | Managed bytes / batch |
| --- | ---: | ---: |
| 500 plain damage applications | 0.867 | 936,000 |
| 500 damage + reflection + leech chains | 18.101 | 2,832,000 |
| 500 new tracking impacts | 2.183 | 1,888,024 |
| 500 new linear impacts | 2.960 | 1,992,024 |
| 1000 modifier additions and expirations | 8.095 | 1,144,024 |
| Clear and rebuild 3000 modifiers | 3.099 | 3,424,240 |
| Wide query over 2000 same-object candidates | 3.388 | 0 |

There are 81 baseline rows; 69 are valid. Three 1000-unit dynamic-property rows each record 957 setup budget aborts because custom scripts inheriting a no-op OnEvent were still dispatched globally as modifiers were added. Nine broad-wrapper rows fail identity/radius correctness and have no accepted timing samples. They cannot be used for an equivalent-work speedup. Both problems remain visible in the baseline report and the process exits nonzero.

The combat batch intentionally applies N/2 hits per operation. For 1000 units this is a burst of 500 hits, not an assertion that an ordinary 1000-unit game performs 30,000 attacks per second. Translate measured work to the actual project's attack and proc rates before assigning a frame budget.

## Current audit result

results/current.json was captured after all audit fixes, including the pooled value-type frame-start ability timing snapshot, with the same harness, arguments and source layout. Its production C# fingerprint is:

~~~text
d25ae62cb10ceec1f995fa4f82f53961aa744664064836696880e3d7e4fffa50
~~~

All **81 current rows are valid**, with no setup or runtime budget aborts and no remaining runtime objects after cleanup. The original baseline remains unchanged. An intermediate attempt exposed a legitimate wide TargetQuery incorrectly exhausting the operation budget; it was not accepted as a successful result. The final query keeps custom callback limits while avoiding charging pure built-in state checks as script operations. Small recursive custom-state checks remain bounded by the separate correctness tests.

At 1000 units, medians of the three rounds are:

| Batch | Baseline P50 → current P50 (ms) | Current P95 (ms) | Managed bytes baseline → current |
| --- | ---: | ---: | ---: |
| 500 plain damage applications | 0.867 → 0.974 | 1.137 | 936,000 → 792,000 |
| 500 damage + reflection + leech chains | 18.101 → 2.498 | 2.818 | 2,832,000 → 2,544,000 |
| 500 new tracking impacts | 2.183 → 2.631 | 3.174 | 1,888,024 → 1,744,024 |
| 500 new linear impacts | 2.960 → 3.366 | 4.028 | 1,992,024 → 1,848,024 |
| 1000 modifier additions and expirations | 8.095 → 8.207 | 9.177 | 1,144,024 → 1,144,024 |
| Clear and rebuild 3000 modifiers | 3.099 → 3.041 | 3.928 | 3,424,240 → 3,424,272 |
| Wide query over 2000 same-object candidates | 3.388 → 0.637 | 0.733 | 0 → 0 |

The proc workload benefits from configured event listeners being indexed by event and related entity, rather than scanning all same-event listeners for every hit. The equivalent-work query batch benefits from identity-set deduplication. These are about 7.2× and 5.3× improvements in the captured batch P50; they do not mean all gameplay paths got faster.

Plain damage and fresh impacts are slower in this capture while allocating less. Modifier churn and clear/rebuild are nearly unchanged within the scale of shared-host variation. The audit adds source/lifecycle and other correctness checks; shared-host noise also affects timing. Do not hide these rows or attribute every difference to a single code change without profiling. Modifier expiry/churn and action-heavy payload allocation remain material costs: the current data does not establish that a game can perform these peak batches every frame.

Dynamic property queries now complete setup without budget aborts and still allocate 168,000 bytes per 1000-unit batch because custom callbacks receive fresh property contexts. Their historical timing row is invalid, so no precise speedup is claimed. Broad-wrapper queries now return correct results (1000-unit case P50 0.107 ms / 0 bytes), but the baseline's incorrect empty sample set is likewise not a timing comparator.
