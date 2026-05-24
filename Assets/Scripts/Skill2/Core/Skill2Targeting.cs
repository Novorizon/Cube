using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public sealed class SkillTargetQuery
    {
        public ISkill2Unit Caster;
        public SkillTargetTeam Team;
        public SkillUnitType Types = SkillUnitType.All;
        public SkillTargetFlags Flags = SkillTargetFlags.None;

        public bool IsValid(ISkill2Unit target)
        {
            if (target == null)
            {
                return false;
            }

            if (Caster != null)
            {
                if ((Flags & SkillTargetFlags.ExcludeSelf) != 0 && target.EntityId == Caster.EntityId)
                {
                    return false;
                }

                if ((Flags & SkillTargetFlags.IncludeSelf) == 0 && target.EntityId == Caster.EntityId && Team == SkillTargetTeam.Enemy)
                {
                    return false;
                }
            }

            if (!target.IsAlive && (Flags & SkillTargetFlags.Dead) == 0)
            {
                return false;
            }

            if (target.IsMagicImmune && (Flags & SkillTargetFlags.MagicImmuneEnemies) == 0 && IsEnemy(target))
            {
                return false;
            }

            if (target.IsInvulnerable && (Flags & SkillTargetFlags.Invulnerable) == 0)
            {
                return false;
            }

            if ((target.UnitType & Types) == 0)
            {
                return false;
            }

            if (!IsTeamValid(target))
            {
                return false;
            }

            if ((Flags & SkillTargetFlags.VisibleOnly) != 0 && Caster != null && !target.IsVisibleToTeam(Caster.TeamId))
            {
                return false;
            }

            return true;
        }

        private bool IsTeamValid(ISkill2Unit target)
        {
            if (Caster == null)
            {
                return Team != SkillTargetTeam.None;
            }

            bool sameTeam = target.TeamId == Caster.TeamId;

            if (sameTeam && (Team & SkillTargetTeam.Friendly) != 0)
            {
                return true;
            }

            if (!sameTeam && (Team & SkillTargetTeam.Enemy) != 0)
            {
                return true;
            }

            return false;
        }

        private bool IsEnemy(ISkill2Unit target)
        {
            return Caster != null && target != null && Caster.TeamId != target.TeamId;
        }
    }

    public static class SkillTargeting
    {
        public static bool BuildCastContext(Skill2Engine engine, SkillAbility ability, SkillCastOrder order, out SkillCastContext context, out SkillCastResult result)
        {
            context = null;
            result = null;

            if (engine == null || ability == null || order == null || order.Caster == null)
            {
                result = SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget);
                return false;
            }

            SkillAbilityDefinition definition = ability.Definition;
            SkillAbilityBehavior behavior = definition.Behavior;

            context = new SkillCastContext();
            context.Engine = engine;
            context.Ability = ability;
            context.Caster = order.Caster;
            context.Target = order.Target;
            context.TargetPosition = order.HasTargetPosition ? order.TargetPosition : order.Caster.Position;
            context.Direction = order.Direction;

            SkillTargetQuery query = CreateQuery(ability);
            float castRange = Mathf.Max(0f, ability.Script.GetCastRange() + engine.GetModifierProperty(order.Caster, SkillModifierProperty.CastRangeBonus, null));
            float aoeRadius = Mathf.Max(0f, ability.Script.GetAoeRadius());

            if ((behavior & SkillAbilityBehavior.NoTarget) != 0)
            {
                context.TargetPosition = order.Caster.Position;
                AddAreaTargets(engine, context, query, order.Caster.Position, aoeRadius);
                result = SkillCastResult.Ok();
                return true;
            }

            if ((behavior & SkillAbilityBehavior.UnitTarget) != 0)
            {
                if (order.Target == null)
                {
                    if ((behavior & SkillAbilityBehavior.OptionalUnitTarget) == 0)
                    {
                        result = SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget, "Unit target is required.");
                        return false;
                    }
                }
                else
                {
                    if (!query.IsValid(order.Target))
                    {
                        result = SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget);
                        return false;
                    }

                    if (!IsInRange(order.Caster.Position, order.Target.Position, castRange))
                    {
                        result = SkillCastResult.Fail(SkillCastFailureReason.OutOfRange);
                        return false;
                    }

                    if (!HasLineOfSight(engine, order.Caster, order.Target.Position, definition.TargetFlags))
                    {
                        result = SkillCastResult.Fail(SkillCastFailureReason.NoVision);
                        return false;
                    }

                    context.TargetPosition = order.Target.Position;
                    context.AddTarget(order.Target);
                }

                if ((behavior & SkillAbilityBehavior.Aoe) != 0 && aoeRadius > 0f)
                {
                    AddAreaTargets(engine, context, query, context.TargetPosition, aoeRadius);
                }

                result = SkillCastResult.Ok();
                return true;
            }

            if ((behavior & SkillAbilityBehavior.PointTarget) != 0)
            {
                if (!order.HasTargetPosition)
                {
                    result = SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget, "Target point is required.");
                    return false;
                }

                if (!IsInRange(order.Caster.Position, order.TargetPosition, castRange))
                {
                    result = SkillCastResult.Fail(SkillCastFailureReason.OutOfRange);
                    return false;
                }

                if (!HasLineOfSight(engine, order.Caster, order.TargetPosition, definition.TargetFlags))
                {
                    result = SkillCastResult.Fail(SkillCastFailureReason.NoVision);
                    return false;
                }

                context.TargetPosition = order.TargetPosition;

                if ((behavior & SkillAbilityBehavior.Aoe) != 0 && aoeRadius > 0f)
                {
                    AddAreaTargets(engine, context, query, order.TargetPosition, aoeRadius);
                }

                result = SkillCastResult.Ok();
                return true;
            }

            result = SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget);
            return false;
        }

        public static SkillTargetQuery CreateQuery(SkillAbility ability)
        {
            SkillAbilityDefinition definition = ability.Definition;
            return new SkillTargetQuery
            {
                Caster = ability.Owner,
                Team = definition.TargetTeam,
                Types = definition.TargetType,
                Flags = definition.TargetFlags
            };
        }

        private static void AddAreaTargets(Skill2Engine engine, SkillCastContext context, SkillTargetQuery query, Vector3 center, float radius)
        {
            if (engine.World == null || radius <= 0f)
            {
                return;
            }

            List<ISkill2Unit> results = ListPool.Get();
            engine.FindUnits(center, radius, query, results);

            for (int i = 0; i < results.Count; i++)
            {
                context.AddTarget(results[i]);
            }

            ListPool.Release(results);
        }

        private static bool HasLineOfSight(Skill2Engine engine, ISkill2Unit caster, Vector3 position, SkillTargetFlags flags)
        {
            if ((flags & SkillTargetFlags.IgnoreLineOfSight) != 0)
            {
                return true;
            }

            if (engine.World == null)
            {
                return true;
            }

            return engine.World.HasLineOfSight(caster, position);
        }

        private static bool IsInRange(Vector3 from, Vector3 to, float range)
        {
            if (range <= 0f)
            {
                return true;
            }

            return (to - from).sqrMagnitude <= range * range;
        }

        private static class ListPool
        {
            private static readonly Stack<List<ISkill2Unit>> Pool = new Stack<List<ISkill2Unit>>();

            public static List<ISkill2Unit> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<ISkill2Unit>();
            }

            public static void Release(List<ISkill2Unit> list)
            {
                if (list == null)
                {
                    return;
                }

                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
