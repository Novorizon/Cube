# Skill System

This folder contains an independent C# skill system inspired by Dota-style Ability + Action + Modifier design.

## Design Rules

- The skill system must not depend on `Assets/Scripts/Game` business managers.
- Business code connects through small interfaces:
  - `ISkillUnit`
  - `ISkillWorld`
  - `ISkillResourceOwner`
  - `ISkillEffectService`
- Common skills should be assembled from config data:
  - `SkillConfigData`
  - `SkillActionData`
  - `SkillModifierData`
- Special behavior should be extended with C# code, not Lua:
  - `ISkillAction`
  - `ISkillModifierLogic`

## Core Parts

- `SkillManager` is the facade.
- `SkillCastSystem` handles cast point, channel, cooldown, and interrupt.
- `SkillActionSystem` registers and executes action groups.
- `SkillModifierManager` handles modifier lifetime, properties, states, purge, interval, and trigger events.
- `SkillAbilityBook` stores owned abilities and applies intrinsic modifiers for passive abilities.
- `SkillEventDispatcher` is the internal skill event dispatcher. It replaces framework-level message usage inside the skill system.

## Important Decisions

- `SkillContext.Config` is optional.
- `SkillContext.SkillId` is the stable source skill id used by actions.
- Modifier-triggered action groups do not need fake config objects.
- `ISkillUnit` only exposes minimum unit information and damage/heal entry points.
- Modifier properties and states are queried through `SkillManager` / `SkillModifierManager`.

## Typical Business Integration

Business code should implement adapters outside this folder, for example under `Assets/Scripts/Game/SkillAdapters`.

Example responsibilities:

- `TdSkillUnit` wraps Npc, Tower, Base, or other combat actors.
- `TdSkillWorld` finds units from the current battle world.
- `TdSkillResourceOwner` consumes gold, mana, item count, or other resources.
- `TdSkillEffectService` plays Unity effects and sounds.

## Extension Pattern

Use config for ordinary skills:

- damage
- heal
- apply modifier
- periodic modifier
- trigger modifier

Use C# extension for special skills:

- custom projectile behavior
- thinker area behavior
- chain lightning
- shield absorption
- critical strike
- lifesteal
- reflection
- stack-based damage
