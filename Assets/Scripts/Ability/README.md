# Ability

`Ability` is an independent Dota-style ability runtime. It intentionally lives outside the simplified `Game.Skill` module and uses the `Game.Ability` namespace.

## Main Concepts

- `AbilitySystem`: battle-scoped facade and runtime owner.
- `AbilityDefinition`: data that describes behavior, target rules, cast point, channel time, cooldown, cost, charges, and special values.
- `Ability`: per-unit runtime instance with level, cooldown, charges, toggle, casting, and channeling state.
- `AbilityScript`: C# replacement for Lua ability scripts.
- `ModifierDefinition`: data that describes visibility, purge, duration, stacks, states, properties, interval think, trigger actions, and aura behavior.
- `Modifier`: runtime modifier instance on a unit.
- `ModifierScript`: C# replacement for Lua modifier scripts.
- `Projectile`: tracking and linear projectile runtime.
- `Thinker`: point-based thinker runtime for persistent area abilities.

## Integration Boundary

Business objects must not implement or retain Ability runtime types directly. The integration
layer implements `IUnit`, `IResourceOwner`, `IWorld`, and `IPresentation`, then registers or binds
business objects to those adapters before passing them to the core.

The existing TD integration layer lives under
`Assets/Scripts/Game/TowerDefense/AbilityAdapters`:

- `AbilityManager` initializes the runtime, registers definitions, binds units, and forwards
  game-facing operations.
- `TdUnit` adapts `Npc`, `Tower`, and `Base` to `IUnit`.
- `TdWorld` exposes live TD targets to core world queries.
- `TdResourceOwner` maps game resources to ability costs.
- `TdPresentation` maps abstract effect and sound requests to the game presentation system.
- `AbilityConfigConverter` converts Excel/Luban records into core definitions.

This integration layer is extended incrementally rather than replaced. Neutral lifecycle events
from the TD managers drive immediate adapter registration and unregistration; lazy binding and
per-frame synchronization remain compatibility and invalid-object fallbacks. Adapters use stable
battle-local entity ids, and core `RemoveUnit` cleanup releases associated abilities, modifiers,
projectiles, and thinkers. The core must never reference TD classes, managers, data tables, or
concrete presentation services.

The business-neutral battle-completed event clears runtime objects, adapter caches, search
buffers, and battle-local ids after settlement. During a full runtime clear,
`IsClearingRuntime` prevents configured modifier destroy actions from applying post-settlement
damage, healing, or spawned effects; custom scripts still receive their lifecycle callback and
can use the same flag to distinguish teardown from normal gameplay removal.

The integration must also complete the runtime loop: it supplies base identity, team, position,
attributes, and states to the core, then applies calculated damage, healing, modifier properties,
final states, effects, sounds, projectiles, and persistent presentation back to the matching
business object. Removing or replacing a business object must clean up its abilities, modifiers,
target references, and persistent presentation.

## Business Calls

New gameplay and UI code uses the game-layer contracts in `AbilityBusinessContracts.cs`:

```csharp
// Register the business actor with its manager before binding it.
AbilityOwnerHandle owner = AbilityManager.Instance.BindOwner(npc);
AbilityKey key = AbilityKey.ByStableId("frost_bolt"); // ByName also accepts Excel, JSON and code definitions.
AbilityManager.Instance.GrantAbility(owner, key, level: 2, resourceItemId: ItemIds.Gold);
AbilityCommandResult receipt = AbilityManager.Instance.CastAtBestTarget(owner, key);
AbilityManager.Instance.TryGetAbilityStatus(owner, key, out AbilityStatus status);
```

The resource item above is a caller-selected business resource; use an item ID that actually exists
in the project. A free ability can omit `resourceItemId`. Neither the core nor JSON definitions
depend on the item manager. `GrantAbility` is idempotent; intentional training/upgrades use
`SetAbilityLevel`. Casting requires a granted ability. `TryGetAbilityStatus`, `FindOwner`, state
reads and legacy base cooldown queries never create bindings or train/grant abilities.

`AbilityCommandResult.Accepted` reports acceptance, with `CastId` and `Queued` identifying the
order. Subscribe to `CommandUpdated` for Started, Executed, channel, Interrupted, Completed and
Rejected updates with the same owner/name/cast ID. An immediate cast can publish updates before
the call returns. Owner, modifier and attack handles belong to one battle; completion/release
invalidates them. `BattleStarted` opens a fresh runtime scope. An ended battle cannot be revived
by polling a UI or calling a cast/attack helper.

Use `ApplyStatusEffect`/`TryGetStatusEffect`/`RemoveStatusEffect` for controlled standalone
modifier handles. Ability script authors can use core types and register script factories by
internal name. `Engine`, raw runtime instances and the legacy integer APIs remain available for
existing scripts and workbench integration; new gameplay/UI code should retain facade DTOs.

Tower basic attacks use `BeginTowerAttack`/`ResolveTowerAttack`/`CancelTowerAttack` once-only
attack handles. `LaunchTowerAttack` supplies the current visual travel policy: launch freezes the
damage request, impact settles damage before asynchronously loading the hit effect, then a living
target can receive the configured on-hit skill. Missing projectile presentation falls back to an
immediate impact. Removing the actor or ending the battle cancels pending travel and settlement.
NPC melee attacks remain immediate. Attack restrictions include disarm/hex as well as stun and
command restriction; roots prevent movement while allowing attacks.

`TdUnit` implements the optional precise combat boundary. Core applies armor/resistance/block;
the adapter rounds integer HP once and returns actual HP changes and the lethal transition.
Base final damage bypasses the legacy defense method, preventing a second defense reduction.
TD towers currently have no health data and do not accept damage/healing; fog of war and line of
sight remain the TD world's explicit always-visible policy until those business systems exist.

## Optional hand-authored JSON sources

No resource path is assumed or scanned. Before `AbilityManager.Initialize`, call
`ConfigureJsonSources(manifestAssetPath)` with a full asset location configured in the project's
resource package. Keep hand-authored definitions and the manifest outside generated
`Assets/Data/Json`. A schema version 1 manifest contains explicitly enabled entries:

```json
{
  "schemaVersion": 1,
  "sources": [
    { "enabled": true, "assetPath": "<full Assets path of your authored ability JSON>" }
  ]
}
```

`TdJsonAbilityManifestProvider` loads `TextAsset` instances through the existing `ResourceManager`,
then merges the enabled documents with Excel and code providers. Disabled entries are not loaded.
Missing assets, malformed JSON, duplicate entries, definition collisions or unsupported features
fail initialization without publishing a partial definition set. Register any required custom
script factories before initialization. `AddDefinitionProvider` remains the explicit integration
point for other sources.

Legacy Excel skills currently expose constant numeric values and have no authored ability-level
limit or per-level curves. Their compatibility level is 1; a tower upgrade does not silently train
its skill. JSON/code definitions can declare levels/curves and use explicit `SetAbilityLevel`.

