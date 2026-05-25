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

Business code implements `IUnit`, `IResourceOwner`, `IWorld`, and `IPresentation`.
This project provides TD adapters under `Assets/Scripts/Game/AbilityAdapters`, with `AbilityManager` as the business-facing facade.

## Business Calls

- Add ability: `AddAbilityToNpc`, `AddAbilityToTower`, `AddAbilityToBase`.
- Cast ability: `CastNpcAbilityAtBestTarget`, `CastTowerAbilityAtBestTarget`, `CastBaseAbilityAtBestTarget`, `CastAbilityOnTarget`, `CastAbilityOnPosition`.
- Custom C# scripts: `RegisterAbilityScript(skillId, factory)`, `RegisterModifierScript(modifierId, factory)`.
- Runtime modifiers: `AddModifierToNpc`, `AddModifierToTower`, `AddModifierToBase`.
- Combat integration: `ApplyTowerAttackDamage`, `ApplyNpcAttackDamageToBase`.
- Stat/state reads: `IsStunned`, `IsRooted`, `IsCommandRestricted`, `GetMoveSpeedMultiplier`, `GetAttackIntervalMultiplier`.
