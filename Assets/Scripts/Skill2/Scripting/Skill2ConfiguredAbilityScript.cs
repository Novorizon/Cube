using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public sealed class ConfiguredSkillAbilityScript : SkillAbilityScript
    {
        private readonly List<ISkill2Unit> scratchTargets = new List<ISkill2Unit>();

        public override void OnSpellStart(SkillCastContext context)
        {
            if (Ability == null || Ability.Definition == null)
            {
                return;
            }

            IReadOnlyList<SkillActionDefinition> actions = Ability.Definition.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                ExecuteAction(actions[i], context);
            }
        }

        private void ExecuteAction(SkillActionDefinition action, SkillCastContext context)
        {
            if (action == null || context == null)
            {
                return;
            }

            switch (action.ActionType)
            {
                case SkillActionType.Damage:
                    ExecuteDamage(action, context);
                    break;

                case SkillActionType.Heal:
                    ExecuteHeal(action, context);
                    break;

                case SkillActionType.AddModifier:
                    ExecuteAddModifier(action, context);
                    break;

                case SkillActionType.Purge:
                    ExecutePurge(action, context);
                    break;

                case SkillActionType.CreateTrackingProjectile:
                    ExecuteTrackingProjectile(action, context);
                    break;

                case SkillActionType.CreateLinearProjectile:
                    ExecuteLinearProjectile(action, context);
                    break;

                case SkillActionType.PlayEffect:
                    ExecuteEffect(action, context);
                    break;

                case SkillActionType.PlaySound:
                    ExecuteSound(action, context);
                    break;
            }
        }

        private void ExecuteDamage(SkillActionDefinition action, SkillCastContext context)
        {
            ResolveTargets(action.Target, context);
            float amount = action.ResolveValue(Ability);

            for (int i = 0; i < scratchTargets.Count; i++)
            {
                Engine.ApplyDamage(new SkillDamageInfo
                {
                    Engine = Engine,
                    Attacker = Caster,
                    Victim = scratchTargets[i],
                    Ability = Ability,
                    Amount = amount,
                    DamageType = action.DamageType,
                    Flags = action.DamageFlags
                });
            }
        }

        private void ExecuteHeal(SkillActionDefinition action, SkillCastContext context)
        {
            ResolveTargets(action.Target, context);
            float amount = action.ResolveValue(Ability);

            for (int i = 0; i < scratchTargets.Count; i++)
            {
                Engine.Heal(new SkillHealInfo
                {
                    Source = Caster,
                    Target = scratchTargets[i],
                    Ability = Ability,
                    Amount = amount
                });
            }
        }

        private void ExecuteAddModifier(SkillActionDefinition action, SkillCastContext context)
        {
            if (string.IsNullOrEmpty(action.ModifierName))
            {
                return;
            }

            ResolveTargets(action.Target, context);
            float duration = action.ResolveDuration(Ability);

            for (int i = 0; i < scratchTargets.Count; i++)
            {
                SkillModifierApplyOptions options = new SkillModifierApplyOptions();
                options.Duration = duration;
                Engine.AddModifier(Caster, scratchTargets[i], Ability, action.ModifierName, options);
            }
        }

        private void ExecutePurge(SkillActionDefinition action, SkillCastContext context)
        {
            ResolveTargets(action.Target, context);

            for (int i = 0; i < scratchTargets.Count; i++)
            {
                Engine.Purge(scratchTargets[i], action.PurgePositiveBuffs, action.PurgeDebuffs, action.PurgeOnlyPurgable);
            }
        }

        private void ExecuteTrackingProjectile(SkillActionDefinition action, SkillCastContext context)
        {
            if (action.Projectile == null)
            {
                return;
            }

            ResolveTargets(action.Target, context);
            for (int i = 0; i < scratchTargets.Count; i++)
            {
                Engine.CreateTrackingProjectile(Ability, Caster, scratchTargets[i], action.Projectile);
            }
        }

        private void ExecuteLinearProjectile(SkillActionDefinition action, SkillCastContext context)
        {
            if (action.Projectile == null)
            {
                return;
            }

            Vector3 direction = context.Direction;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = context.TargetPosition - Caster.Position;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.forward;
            }

            Engine.CreateLinearProjectile(Ability, Caster, Caster.Position, direction, action.Projectile);
        }

        private void ExecuteEffect(SkillActionDefinition action, SkillCastContext context)
        {
            if (Engine.Presentation == null || string.IsNullOrEmpty(action.EffectName))
            {
                return;
            }

            ResolveTargets(action.Target, context);
            if (scratchTargets.Count == 0 || action.Target == SkillActionTarget.Point)
            {
                Engine.Presentation.PlayEffect(action.EffectName, context.TargetPosition);
                return;
            }

            for (int i = 0; i < scratchTargets.Count; i++)
            {
                Engine.Presentation.PlayEffect(action.EffectName, scratchTargets[i]);
            }
        }

        private void ExecuteSound(SkillActionDefinition action, SkillCastContext context)
        {
            if (Engine.Presentation == null || string.IsNullOrEmpty(action.SoundName))
            {
                return;
            }

            Engine.Presentation.PlaySound(action.SoundName, ResolvePoint(action.Target, context));
        }

        private void ResolveTargets(SkillActionTarget target, SkillCastContext context)
        {
            scratchTargets.Clear();

            switch (target)
            {
                case SkillActionTarget.Caster:
                    AddTarget(Caster);
                    break;

                case SkillActionTarget.PrimaryTarget:
                    AddTarget(context.Target);
                    if (scratchTargets.Count == 0 && context.Targets.Count > 0)
                    {
                        AddTarget(context.Targets[0]);
                    }
                    break;

                case SkillActionTarget.ContextTargets:
                    for (int i = 0; i < context.Targets.Count; i++)
                    {
                        AddTarget(context.Targets[i]);
                    }
                    if (scratchTargets.Count == 0)
                    {
                        AddTarget(context.Target);
                    }
                    break;
            }
        }

        private Vector3 ResolvePoint(SkillActionTarget target, SkillCastContext context)
        {
            switch (target)
            {
                case SkillActionTarget.Caster:
                    return Caster != null ? Caster.Position : context.TargetPosition;

                case SkillActionTarget.PrimaryTarget:
                    return context.Target != null ? context.Target.Position : context.TargetPosition;

                default:
                    return context.TargetPosition;
            }
        }

        private void AddTarget(ISkill2Unit target)
        {
            if (target != null && !scratchTargets.Contains(target))
            {
                scratchTargets.Add(target);
            }
        }
    }
}
