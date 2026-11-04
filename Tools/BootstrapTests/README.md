# Bootstrap verification

Run with the .NET 8 SDK:

```sh
dotnet run --project Tools/BootstrapTests/BootstrapTests.csproj --configuration Release
```

No package restore network access is needed. The project references the SDK's bundled Json.NET for the existing save serializer.

The executable compiles the real lifecycle runtime, Unity entry adapter, Cube composition root, world-entry partial, and save code. `IntegrationFakes.cs` supplies boundary doubles outside `Assets`, so none of these doubles enter a Unity build. Scenarios cover delayed and failed startup, cancellation/reentrancy, partial cleanup, duplicate ownership, UI readiness, and save-file preservation.

These checks do not validate Unity object lifetime, rendering, input-system behavior, or YooAsset. Run the EditMode tests and the player/Editor checks in `Docs/Architecture/GameLifecycle.md` with the project's Unity 2022.3 editor as well.
