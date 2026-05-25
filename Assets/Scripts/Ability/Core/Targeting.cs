using System.Collections.Generic;
using UnityEngine;

namespace Game.Ability
{
    /// <summary>
    /// Canonical target filter shared by casts, auras, projectiles, and business helpers.
    /// </summary>
    public sealed class TargetQuery
    {
        public IUnit Caster;
        public TargetTeam Team;
        public UnitType Types = UnitType.All;
        public TargetFlags Flags = TargetFlags.None;

        public bool IsValid(IUnit target)
        {
            if (target == null)
            {
                return false;
            }

            if (Caster != null)
            {
                if ((Flags & TargetFlags.ExcludeSelf) != 0 && target.EntityId == Caster.EntityId)
                {
                    return false;
                }

                if ((Flags & TargetFlags.IncludeSelf) == 0 && target.EntityId == Caster.EntityId && Team == TargetTeam.Enemy)
                {
                    return false;
                }
            }

            if (!target.IsAlive && (Flags & TargetFlags.Dead) == 0)
            {
                return false;
            }

            if (target.IsMagicImmune && (Flags & TargetFlags.MagicImmuneEnemies) == 0 && Caster != null && Caster.TeamId != target.TeamId)
            {
                return false;
            }

            if (target.IsInvulnerable && (Flags & TargetFlags.Invulnerable) == 0)
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

            if ((Flags & TargetFlags.VisibleOnly) != 0 && Caster != null && !target.IsVisibleToTeam(Caster.TeamId))
            {
                return false;
            }

            return true;
        }

        private bool IsTeamValid(IUnit target)
        {
            if (Caster == null)
            {
                return Team != TargetTeam.None;
            }

            bool sameTeam = target.TeamId == Caster.TeamId;
            return (sameTeam && (Team & TargetTeam.Friendly) != 0) || (!sameTeam && (Team & TargetTeam.Enemy) != 0);
        }
    }

    /// <summary>
    /// Builds a cast context from a raw order and validates Dota-like targeting rules.
    /// </summary>
    public static class Targeting
    {
        public static bool BuildCastContext(AbilitySystem engine, Ability ability, CastOrder order, out CastContext context, out CastResult result)
        {
            context = null;
            result = null;
            if (engine == null || ability == null || order == null || order.Caster == null)
            {
                result = CastResult.Fail(CastFailureReason.InvalidTarget);
                return false;
            }

            AbilityDefinition definition = ability.Definition;
            AbilityBehavior behavior = definition.Behavior;
            context = new CastContext
            {
                Engine = engine,
                Ability = ability,
                Caster = order.Caster,
                Target = order.Target,
                TargetPosition = order.HasTargetPosition ? order.TargetPosition : order.Caster.Position,
                Direction = order.Direction
            };

            TargetQuery query = CreateQuery(ability);
            float castRange = Mathf.Max(0f, ability.Script.GetCastRange() + engine.GetModifierProperty(order.Caster, ModifierProperty.CastRangeBonus, null));
            float aoeRadius = Mathf.Max(0f, ability.Script.GetAoeRadius());

            if ((behavior & AbilityBehavior.NoTarget) != 0)
            {
                // No-target spells execute at the caster position, but may still collect AOE targets.
                context.TargetPosition = order.Caster.Position;
                AddAreaTargets(engine, context, query, order.Caster.Position, aoeRadius);
                result = CastResult.Ok();
                return true;
            }

            if ((behavior & AbilityBehavior.UnitTarget) != 0)
            {
                if (order.Target == null)
                {
                    if ((behavior & AbilityBehavior.OptionalUnitTarget) == 0)
                    {
                        result = CastResult.Fail(CastFailureReason.InvalidTarget, "Unit target is required.");
                        return false;
                    }
                }
                else
                {
                    if (!query.IsValid(order.Target))
                    {
                        result = CastResult.Fail(CastFailureReason.InvalidTarget);
                        return false;
                    }

                    if (!IsInRange(order.Caster.Position, order.Target.Position, castRange))
                    {
                        result = CastResult.Fail(CastFailureReason.OutOfRange);
                        return false;
                    }

                    if (!HasLineOfSight(engine, order.Caster, order.Target.Position, definition.TargetFlags))
                    {
                        result = CastResult.Fail(CastFailureReason.NoVision);
                        return false;
                    }

                    context.TargetPosition = order.Target.Position;
                    context.AddTarget(order.Target);
                }

                if ((behavior & AbilityBehavior.Aoe) != 0 && aoeRadius > 0f)
                {
                    // The clicked unit remains Target; nearby units are added to Targets.
                    AddAreaTargets(engine, context, query, context.TargetPosition, aoeRadius);
                }

                result = CastResult.Ok();
                return true;
            }

            if ((behavior & AbilityBehavior.PointTarget) != 0)
            {
                if (!order.HasTargetPosition)
                {
                    result = CastResult.Fail(CastFailureReason.InvalidTarget, "Target point is required.");
                    return false;
                }

                if (!IsInRange(order.Caster.Position, order.TargetPosition, castRange))
                {
                    result = CastResult.Fail(CastFailureReason.OutOfRange);
                    return false;
                }

                if (!HasLineOfSight(engine, order.Caster, order.TargetPosition, definition.TargetFlags))
                {
                    result = CastResult.Fail(CastFailureReason.NoVision);
                    return false;
                }

                context.TargetPosition = order.TargetPosition;
                if ((behavior & AbilityBehavior.Aoe) != 0 && aoeRadius > 0f)
                {
                    // Point-target AOE spells resolve their affected units once at cast time.
                    AddAreaTargets(engine, context, query, order.TargetPosition, aoeRadius);
                }

                result = CastResult.Ok();
                return true;
            }

            result = CastResult.Fail(CastFailureReason.InvalidTarget);
            return false;
        }

        public static TargetQuery CreateQuery(Ability ability)
        {
            AbilityDefinition definition = ability.Definition;
            return new TargetQuery
            {
                Caster = ability.Owner,
                Team = definition.TargetTeam,
                Types = definition.TargetType,
                Flags = definition.TargetFlags
            };
        }

        private static void AddAreaTargets(AbilitySystem engine, CastContext context, TargetQuery query, Vector3 center, float radius)
        {
            if (engine.World == null || radius <= 0f)
            {
                return;
            }

            List<IUnit> results = new List<IUnit>();
            engine.FindUnits(center, radius, query, results);
            for (int i = 0; i < results.Count; i++)
            {
                context.AddTarget(results[i]);
            }
        }

        private static bool HasLineOfSight(AbilitySystem engine, IUnit caster, Vector3 position, TargetFlags flags)
        {
            return (flags & TargetFlags.IgnoreLineOfSight) != 0 || engine.World == null || engine.World.HasLineOfSight(caster, position);
        }

        private static bool IsInRange(Vector3 from, Vector3 to, float range)
        {
            return range <= 0f || (to - from).sqrMagnitude <= range * range;
        }
    }
}
