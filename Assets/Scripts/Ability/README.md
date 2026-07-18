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

- Add ability: `AddAbilityToNpc`, `AddAbilityToTower`, `AddAbilityToBase`.
- Cast ability: `CastNpcAbilityAtBestTarget`, `CastTowerAbilityAtBestTarget`, `CastBaseAbilityAtBestTarget`, `CastAbilityOnTarget`, `CastAbilityOnPosition`.
- Custom C# scripts: `RegisterAbilityScript(skillId, factory)`, `RegisterModifierScript(modifierId, factory)`.
- Runtime modifiers: `AddModifierToNpc`, `AddModifierToTower`, `AddModifierToBase`.
- Combat integration: `ApplyTowerAttackDamage`, `ApplyNpcAttackDamageToBase`.
- Stat/state reads: `IsStunned`, `IsRooted`, `IsCommandRestricted`, `GetMoveSpeedMultiplier`, `GetAttackIntervalMultiplier`.
