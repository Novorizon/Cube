# Portable Ability verification

Runtime 公开接口快照与独立编辑器依赖已补齐；运行时 0.2.2-preview.1，编辑器 0.1.1-preview.1。

Run with the .NET 8 SDK from the repository root:

```sh
dotnet run --project Tools/AbilityVerification/ApiContract -c Release
dotnet run --project Tools/AbilityVerification/Core/AbilityTests.csproj
dotnet run --project Tools/AbilityVerification/Business/BusinessTests.csproj
bash Tools/AbilityVerification/PackageSmoke/run.sh
```

No external packages are restored. These tools are outside `Assets` and do not enter the Unity player.

`PackageSmoke` physically copies only the Ability directory into an empty temporary project, then builds an external consumer with its own units/world/resources/presentation/custom scripts. It verifies one-way package API usage without any project gameplay sources. Set `DOTNET` to a full SDK executable path if `dotnet` is not on PATH.

`python3 Tools/AbilityVerification/build-package.py --output /tmp/ability-runtime.tgz` builds a UPM tarball containing only the runtime directory. It does not bundle project adapters or scenario data. Install the tarball in Unity Package Manager, or use the Git package path `Assets/Scripts/Ability`.

For a ZIP, use `python3 Tools/AbilityVerification/build-package.py --output /tmp/ability-runtime.zip`. Extract it and choose the contained `com.novorizon.ability/package.json` with Unity's Add package from disk. The runtime ZIP includes the original repository license. `build-validation-bundle.py` creates a separate source verification ZIP with Core fixtures, JSON samples, isolated consumption and performance tools; Cube-specific Business checks are excluded.

The [acceptance report and validation matrix](../../Docs/AbilityValidation.md) records current evidence and remaining architecture, randomized testing, Unity, IL2CPP and target-device performance gates.

The [hero JSON reference pack](../../Data/AbilityJsonSources/Heroes/README.md) implements 14 skills across seven Dota 2 / Warcraft III heroes. Its fixtures load the actual files and verify independent/combined publication and runtime behavior. Runtime 0.2.0 adds level-scaled modifier properties without any hero-specific Core code. The [Odin editor plan](../../Docs/AbilityEditorOdinPlan.md) uses these files as authoring and round-trip acceptance cases; the optional Editor package is now implemented. Runtime sources have no Odin dependency; see [the editor guide](../../Docs/AbilityEditorGuide.md) and [editor verification](Editor/README.md).

The separate [performance probe](Performance/README.md) runs production Ability code with portable world/unit adapters and records warmed timing, managed allocation and execution-budget diagnostics. Its synthetic .NET results do not establish Unity or target-device frame rates.

The [combat/lifecycle probe](CombatPerformance/README.md) adds real HP application, damage/reflection/leech, fresh projectile impacts, modifier churn and wide-query correctness with raw per-batch P50/P95/P99 samples. The [2026-11-05 audit](../../Docs/Audits/AbilityRuntimeAudit-2026-11-05.md) records 0.2.1 fixes, Core419/Business49/Editor91/package7 results and 18,000 seeded invariant operations. Historical failed baseline rows remain marked invalid instead of being used as fast timing comparisons.

The core tool compiles production Ability code and the real EditMode test sources into **separate assemblies**, using a small Unity math/JSON substitute and the subset of NUnit assertions used by these fixtures. A fixture-name filter can be passed after `--`. JSON provider tests execute the actual strict production JSON reader; the Unity JSON substitute is only a compatibility surface for other code.

The business tool compiles Unity substitutes, production Ability, and actual Ability adapters/changed tower-defense managers/effects into three separate assemblies, enforcing the public Core boundary. It checks facade contracts, scoped handles, actual HP application, asynchronous settlement cancellation and source manifests. Unchanged Excel converter/provider and external resource/game services are substitutes; this does not validate real spreadsheets or generated-data compatibility.

These are reproducible source-level checks, **not NUnit Test Runner, Unity compilation, PlayMode, physics, asset loading, YooAsset configuration, or a performance test**. Run the EditMode fixtures in Unity 2022 and then test real tower-defense scenes before merging/releasing. When adding assertions, extend the compatibility subset explicitly or run the new fixture with real NUnit; do not silently skip it.

Build the optional editor ZIP with `python3 Tools/AbilityVerification/build-package.py --package editor --output /tmp/ability-studio.zip`. It contains no Sirenix binaries; install the project's Odin Inspector separately. The full validation bundle includes the editor package and its Core/Sandbox tests.
