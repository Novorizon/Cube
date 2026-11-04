# Portable Ability verification

Run with the .NET 8 SDK from the repository root:

```sh
dotnet run --project Tools/AbilityVerification/Core/AbilityTests.csproj
dotnet run --project Tools/AbilityVerification/Business/BusinessTests.csproj
bash Tools/AbilityVerification/PackageSmoke/run.sh
```

No external packages are restored. These tools are outside `Assets` and do not enter the Unity player.

`PackageSmoke` physically copies only the Ability directory into an empty temporary project, then builds an external consumer with its own units/world/resources/presentation/custom scripts. It verifies one-way package API usage without any project gameplay sources. Set `DOTNET` to a full SDK executable path if `dotnet` is not on PATH.

`python3 Tools/AbilityVerification/build-package.py --output /tmp/ability-runtime.tgz` builds a UPM tarball containing only the runtime directory. It does not bundle project adapters or scenario data. Install the tarball in Unity Package Manager, or use the Git package path `Assets/Scripts/Ability`.

The separate [performance probe](Performance/README.md) runs production Ability code with portable world/unit adapters and records warmed timing, managed allocation and execution-budget diagnostics. Its synthetic .NET results do not establish Unity or target-device frame rates.

The core tool compiles production Ability code and the real EditMode test sources into **separate assemblies**, using a small Unity math/JSON substitute and the subset of NUnit assertions used by these fixtures. A fixture-name filter can be passed after `--`. JSON provider tests execute the actual strict production JSON reader; the Unity JSON substitute is only a compatibility surface for other code.

The business tool compiles Unity substitutes, production Ability, and actual Ability adapters/changed tower-defense managers/effects into three separate assemblies, enforcing the public Core boundary. It checks facade contracts, scoped handles, actual HP application, asynchronous settlement cancellation and source manifests. Unchanged Excel converter/provider and external resource/game services are substitutes; this does not validate real spreadsheets or generated-data compatibility.

These are reproducible source-level checks, **not NUnit Test Runner, Unity compilation, PlayMode, physics, asset loading, YooAsset configuration, or a performance test**. Run the EditMode fixtures in Unity 2022 and then test real tower-defense scenes before merging/releasing. When adding assertions, extend the compatibility subset explicitly or run the new fixture with real NUnit; do not silently skip it.
