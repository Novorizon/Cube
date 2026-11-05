using System;
using System.Collections.Generic;
using Game.Ability;
using UnityEngine;

// This consumer is compiled in a different assembly after copying only Assets/Scripts/Ability
// into an otherwise empty temporary project. No TD adapters, Framework, Game entities or test fakes are present.
internal static class Program
{
    private static int passed;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        passed++;
        Console.WriteLine("PASS " + message);
    }

    private static void Main()
    {
        World world = new World();
        Unit caster = new Unit(1, 10, Vector3.zero);
        Unit target = new Unit(2, 20, Vector3.right);
        world.Units.Add(caster);
        world.Units.Add(target);
        Presentation presentation = new Presentation();
        Resources resources = new Resources();
        AbilitySystem engine = new AbilitySystem();
        engine.Initialize(world, presentation);
        engine.RegisterModifierDefinition(new ModifierDefinition { Name = "external.poison", Duration = 1f, Interval = 0.25f });
        engine.RegisterModifierScript("external.poison", () => new Poison());
        AbilityDefinition definition = new AbilityDefinition
        {
            Name = "external.bolt", Behavior = AbilityBehavior.UnitTarget, TargetTeam = TargetTeam.Enemy,
            CastRange = LevelValue.Constant(5f), CastPoint = LevelValue.Constant(0.1f), ManaCost = LevelValue.Constant(3f), Cooldown = LevelValue.Constant(2f)
        };
        definition.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = LevelValue.Constant(12f), DamageType = DamageType.Pure });
        definition.Actions.Add(new ActionDefinition { ActionType = ActionType.PlayEffect, Target = ActionTarget.PrimaryTarget, EffectName = "external.hit" });
        engine.RegisterAbilityDefinition(definition);
        ExternalSpell script = new ExternalSpell();
        engine.RegisterAbilityScript(definition.Name, () => script);
        Game.Ability.Ability ability = engine.AddAbility(caster, definition.Name, 1, resources);
        Check(ability != null && ReferenceEquals(engine.FindAbility(caster, definition.Name), ability), "an external project grants and queries public runtime objects");
        CastOrder order = new CastOrder { Caster = caster, AbilityName = definition.Name, Target = target };
        Check(engine.EvaluateOrder(order).Success && resources.Mana == 10f && target.Health == 100f, "external preview runs without applying gameplay effects");
        CastResult cast = engine.IssueOrder(order);
        Check(cast.Success && cast.CastId > 0 && ability.Phase == AbilityPhase.Casting, "external project receives the public cast result and lifecycle");
        engine.Update(0.1f);
        Check(target.Health == 88f && resources.Mana == 7f && presentation.Effects == 1 && script.Starts == 1, "custom entities resources presentation and ability scripts execute without Game dependencies");
        engine.Update(0.25f);
        Check(target.Health == 86f && engine.Modifiers.Count == 1, "external modifier scripts use the package combat API");
        Check(engine.EvaluateOrder(order).FailureReason == CastFailureReason.Cooldown && ability.CooldownDuration == 2f, "public cooldown query and validation are available to consumers");
        engine.ClearRuntime();
        Check(engine.Modifiers.Count == 0 && engine.FindAbility(caster, definition.Name) == null, "public cleanup invalidates imported runtime objects");
        Console.WriteLine("Package import smoke: " + passed + " passed; only the Ability directory was imported.");
    }

    private sealed class Unit : IUnit
    {
        public Unit(int id, int team, Vector3 position) { EntityId = id; TeamId = team; Position = position; }
        public int EntityId { get; }
        public int TeamId { get; }
        public UnitType UnitType => UnitType.Hero;
        public bool IsAlive => Health > 0f;
        public bool IsMagicImmune => false;
        public bool IsInvulnerable => false;
        public Vector3 Position { get; }
        public float Health { get; private set; } = 100f;
        public bool IsVisibleToTeam(int teamId) => true;
        public void ApplyDamage(DamageResult result) { Health = Math.Max(0f, Health - result.FinalAmount); }
        public void Heal(HealInfo info) { Health = Math.Min(100f, Health + info.Amount); }
    }

    private sealed class World : IWorld
    {
        public readonly List<IUnit> Units = new List<IUnit>();
        public float Time => 0f;
        public bool HasLineOfSight(IUnit viewer, Vector3 position) => true;
        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            foreach (IUnit unit in Units) if ((unit.Position - center).sqrMagnitude <= radius * radius) results.Add(unit);
        }
    }

    private sealed class Resources : IResourceOwner
    {
        public float Mana { get; private set; } = 10f;
        public bool HasMana(float amount) => Mana >= amount;
        public bool SpendMana(float amount) { if (!HasMana(amount)) return false; Mana -= amount; return true; }
    }

    private sealed class Presentation : IPresentation
    {
        public int Effects;
        public void PlayEffect(string name, Vector3 position) { Effects++; }
        public void PlayEffect(string name, IUnit target) { Effects++; }
        public IPresentationHandle PlayPersistentEffect(string name, IUnit target) => null;
        public void PlaySound(string name, Vector3 position) { }
    }

    private sealed class ExternalSpell : AbilityScript
    {
        public int Starts;
        public override void OnSpellStart(CastContext context)
        {
            Starts++;
            Engine.AddModifier(Caster, context.Target, Ability, "external.poison");
            ActionRunner.Execute(Ability.Definition.Actions, context);
        }
    }

    private sealed class Poison : ModifierScript
    {
        public override void OnIntervalThink()
        {
            Engine.ApplyDamage(new DamageInfo { Attacker = Caster, Victim = Parent, Ability = Ability, Amount = 2f, DamageType = DamageType.Pure });
        }
    }
}
