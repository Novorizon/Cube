using System.Collections.Generic;
using UnityEngine;

namespace Game.Ability
{
    /// <summary>
    /// Executes simple data-driven actions. Custom AbilityScript/ModifierScript code can still
    /// call the same primitives when a skill needs logic beyond config.
    /// </summary>
    public static class ActionRunner
    {
        // Reused within one execution path to avoid allocations in common action loops.
        private static readonly List<IUnit> ScratchTargets = new List<IUnit>();

        public static void Execute(IReadOnlyList<ActionDefinition> actions, CastContext context)
        {
            if (actions == null || context == null)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                Execute(actions[i], context);
            }
        }

        public static void Execute(ActionDefinition action, CastContext context)
        {
            if (action == null || context == null || context.Engine == null || context.Ability == null)
            {
                return;
            }

            // ActionType performs the gameplay operation; optional VFX/SFX are also played below.
            switch (action.ActionType)
            {
                case ActionType.Damage:
                    ExecuteDamage(action, context);
                    break;
                case ActionType.Heal:
                    ExecuteHeal(action, context);
                    break;
                case ActionType.AddModifier:
                    ExecuteAddModifier(action, context);
                    break;
                case ActionType.Purge:
                    ExecutePurge(action, context);
                    break;
                case ActionType.CreateTrackingProjectile:
                    ExecuteTrackingProjectile(action, context);
                    break;
                case ActionType.CreateLinearProjectile:
                    ExecuteLinearProjectile(action, context);
                    break;
                case ActionType.PlayEffect:
                    ExecuteEffect(action, context);
                    break;
                case ActionType.PlaySound:
                    ExecuteSound(action, context);
                    break;
            }

            if (action.ActionType != ActionType.PlayEffect)
            {
                ExecuteEffect(action, context);
            }

            if (action.ActionType != ActionType.PlaySound)
            {
                ExecuteSound(action, context);
            }
        }

        public static CastContext CreateSingleTargetContext(AbilitySystem engine, Ability ability, IUnit caster, IUnit target, Vector3 position)
        {
            CastContext context = new CastContext
            {
                Engine = engine,
                Ability = ability,
                Caster = caster,
                Target = target,
                TargetPosition = target != null ? target.Position : position
            };

            if (target != null)
            {
                context.AddTarget(target);
            }

            return context;
        }

        private static void ExecuteDamage(ActionDefinition action, CastContext context)
        {
            ResolveTargets(action.Target, context);
            float amount = action.ResolveValue(context.Ability);
            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.ApplyDamage(new DamageInfo
                {
                    Engine = context.Engine,
                    Attacker = context.Caster,
                    Victim = ScratchTargets[i],
                    Ability = context.Ability,
                    Amount = amount,
                    DamageType = action.DamageType,
                    Flags = action.DamageFlags
                });
            }
        }

        private static void ExecuteHeal(ActionDefinition action, CastContext context)
        {
            ResolveTargets(action.Target, context);
            float amount = action.ResolveValue(context.Ability);
            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.Heal(new HealInfo
                {
                    Source = context.Caster,
                    Target = ScratchTargets[i],
                    Ability = context.Ability,
                    Amount = amount
                });
            }
        }

        private static void ExecuteAddModifier(ActionDefinition action, CastContext context)
        {
            if (string.IsNullOrEmpty(action.ModifierName))
            {
                return;
            }

            ResolveTargets(action.Target, context);
            float duration = action.ResolveDuration(context.Ability);
            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.AddModifier(context.Caster, ScratchTargets[i], context.Ability, action.ModifierName, new ModifierApplyOptions { Duration = duration });
            }
        }

        private static void ExecutePurge(ActionDefinition action, CastContext context)
        {
            ResolveTargets(action.Target, context);
            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.Purge(ScratchTargets[i], action.PurgePositiveBuffs, action.PurgeDebuffs, action.PurgeOnlyPurgable);
            }
        }

        private static void ExecuteTrackingProjectile(ActionDefinition action, CastContext context)
        {
            if (action.Projectile == null)
            {
                return;
            }

            ResolveTargets(action.Target, context);
            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.CreateTrackingProjectile(context.Ability, context.Caster, ScratchTargets[i], action.Projectile);
            }
        }

        private static void ExecuteLinearProjectile(ActionDefinition action, CastContext context)
        {
            if (action.Projectile == null)
            {
                return;
            }

            Vector3 direction = context.Direction;
            if (direction.sqrMagnitude <= 0.0001f && context.Caster != null)
            {
                direction = context.TargetPosition - context.Caster.Position;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            context.Engine.CreateLinearProjectile(context.Ability, context.Caster, context.Caster.Position, direction, action.Projectile);
        }

        private static void ExecuteEffect(ActionDefinition action, CastContext context)
        {
            if (context.Engine.Presentation == null || string.IsNullOrEmpty(action.EffectName))
            {
                return;
            }

            ResolveTargets(action.Target, context);
            if (ScratchTargets.Count == 0 || action.Target == ActionTarget.Point)
            {
                context.Engine.Presentation.PlayEffect(action.EffectName, ResolvePoint(action.Target, context));
                return;
            }

            for (int i = 0; i < ScratchTargets.Count; i++)
            {
                context.Engine.Presentation.PlayEffect(action.EffectName, ScratchTargets[i]);
            }
        }

        private static void ExecuteSound(ActionDefinition action, CastContext context)
        {
            if (context.Engine.Presentation == null || string.IsNullOrEmpty(action.SoundName))
            {
                return;
            }

            context.Engine.Presentation.PlaySound(action.SoundName, ResolvePoint(action.Target, context));
        }

        private static void ResolveTargets(ActionTarget target, CastContext context)
        {
            // ContextTargets are pre-filtered by Targeting for AOE/no-target casts.
            ScratchTargets.Clear();
            switch (target)
            {
                case ActionTarget.Caster:
                    AddTarget(context.Caster);
                    break;
                case ActionTarget.PrimaryTarget:
                    AddTarget(context.Target);
                    if (ScratchTargets.Count == 0 && context.Targets.Count > 0)
                    {
                        AddTarget(context.Targets[0]);
                    }
                    break;
                case ActionTarget.ContextTargets:
                    for (int i = 0; i < context.Targets.Count; i++)
                    {
                        AddTarget(context.Targets[i]);
                    }
                    if (ScratchTargets.Count == 0)
                    {
                        AddTarget(context.Target);
                    }
                    break;
            }
        }

        private static Vector3 ResolvePoint(ActionTarget target, CastContext context)
        {
            switch (target)
            {
                case ActionTarget.Caster:
                    return context.Caster != null ? context.Caster.Position : context.TargetPosition;
                case ActionTarget.PrimaryTarget:
                    return context.Target != null ? context.Target.Position : context.TargetPosition;
                default:
                    return context.TargetPosition;
            }
        }

        private static void AddTarget(IUnit target)
        {
            if (target != null && !ScratchTargets.Contains(target))
            {
                ScratchTargets.Add(target);
            }
        }
    }
}
