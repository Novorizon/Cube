# Skill2

`Skill2` is an independent Dota-style skill runtime. It intentionally lives outside `Game.Skill` and uses the `Skill2` namespace.

## Main Concepts

- `Skill2Engine`: battle-scoped facade and runtime owner.
- `SkillAbilityDefinition`: data that describes an ability's behavior, target rules, cast point, channel time, cooldown, mana cost, charges, and special values.
- `SkillAbility`: per-unit ability instance with level, cooldown, charges, toggle, casting, and channeling state.
- `SkillAbilityScript`: C# replacement for Lua ability scripts.
- `SkillModifierDefinition`: data that describes modifier visibility, purge, duration, stacks, states, properties, interval think, and aura behavior.
- `SkillModifier`: runtime modifier instance on a unit.
- `SkillModifierScript`: C# replacement for Lua modifier scripts.
- `SkillProjectile`: tracking and linear projectile runtime.
- `SkillThinker`: point-based thinker runtime for persistent area skills.
- `ConfiguredSkillAbilityScript`: optional data-driven executor for ordinary skills.

## Integration Boundary

Business code should implement:

- `ISkill2Unit`
- `ISkill2ResourceOwner`
- `ISkill2World`
- `ISkill2Presentation`

The runtime does not know about project-specific managers, prefabs, item systems, tower systems, NPC systems, or UI.
`ISkill2World.FindUnits` may return broad candidates; `Skill2Engine` applies `SkillTargetQuery` filtering again before skills consume results.

## Typical Flow

1. Create one `Skill2Engine` for a battle.
2. Call `Initialize(world, presentation)`.
3. Register ability and modifier definitions.
4. Register C# ability and modifier scripts.
5. Add abilities to units.
6. Drive `Update(deltaTime)` from the battle loop.
7. Issue orders through `CastAbility`, `CastAbilityOnTarget`, `CastAbilityOnPosition`, or `IssueOrder`.

## Business API Surface

Business code calls the runtime through `Skill2Engine`:

- Register: `RegisterAbilityDefinition`, `RegisterModifierDefinition`, `RegisterAbilityScript`, `RegisterModifierScript`.
- Runtime ownership: `AddAbility`, `RemoveAbility`, `FindAbility`, `GetAbilities`.
- Casting: `CastAbility`, `CastAbilityOnTarget`, `CastAbilityOnPosition`, `IssueOrder`.
- Effects of skills: `AddModifier`, `RemoveModifier`, `Purge`, `ApplyDamage`, `Heal`.
- Queries: `HasState`, `GetModifierProperty`, `FindUnits`.
- Runtime objects: `CreateTrackingProjectile`, `CreateLinearProjectile`, `CreateThinker`.
- Events: subscribe to `EventRaised`.

## Skill Composition

Simple skills can be assembled by adding `SkillActionDefinition` rows to `SkillAbilityDefinition.Actions`.
When an ability definition has actions and no custom script is registered, `ConfiguredSkillAbilityScript` executes those actions automatically.

Supported configured actions:

- Damage
- Heal
- AddModifier
- Purge
- CreateTrackingProjectile
- CreateLinearProjectile
- PlayEffect
- PlaySound

Complex Dota-style skills should use `SkillAbilityScript`, `SkillModifierScript`, and `SkillThinkerScript`.

## Scripting Pattern

```csharp
public sealed class FireballAbility : SkillAbilityScript
{
    public override void OnSpellStart(SkillCastContext context)
    {
        for (int i = 0; i < context.Targets.Count; i++)
        {
            Engine.ApplyDamage(new SkillDamageInfo
            {
                Attacker = Caster,
                Victim = context.Targets[i],
                Ability = Ability,
                Amount = GetSpecialValue("damage"),
                DamageType = SkillDamageType.Magical
            });
        }
    }
}
```

## Configured Skill Example

```csharp
SkillAbilityDefinition frostNova = new SkillAbilityDefinition
{
    Name = "frost_nova",
    Behavior = SkillAbilityBehavior.PointTarget | SkillAbilityBehavior.Aoe,
    TargetTeam = SkillTargetTeam.Enemy,
    TargetType = SkillUnitType.All,
    CastRange = SkillLevelValue.Constant(700f),
    AoeRadius = SkillLevelValue.Constant(300f),
    Cooldown = SkillLevelValue.Constant(8f),
    ManaCost = SkillLevelValue.Constant(100f)
};

frostNova.SpecialValues["damage"] = SkillLevelValue.Constant(120f);
frostNova.Actions.Add(new SkillActionDefinition
{
    ActionType = SkillActionType.Damage,
    Target = SkillActionTarget.ContextTargets,
    ValueSpecialName = "damage",
    DamageType = SkillDamageType.Magical
});
frostNova.Actions.Add(new SkillActionDefinition
{
    ActionType = SkillActionType.AddModifier,
    Target = SkillActionTarget.ContextTargets,
    ModifierName = "modifier_frost_slow",
    Duration = SkillLevelValue.Constant(4f)
});
```
