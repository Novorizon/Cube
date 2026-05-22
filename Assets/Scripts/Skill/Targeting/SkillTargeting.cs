using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    public sealed class SkillTargetFilter
    {
        public ISkillUnit Caster;
        public SkillTargetTeam TargetTeam;

        public bool IsValid(ISkillUnit target)
        {
            if (target == null || !target.IsAlive)
            {
                return false;
            }

            if (Caster == null)
            {
                return true;
            }

            bool sameTeam = target.TeamId == Caster.TeamId;

            switch (TargetTeam)
            {
                case SkillTargetTeam.Friendly:
                    return sameTeam;

                case SkillTargetTeam.Enemy:
                    return !sameTeam;

                case SkillTargetTeam.Both:
                    return true;

                default:
                    return false;
            }
        }
    }

    public sealed class SkillTargetResult
    {
        private readonly List<ISkillUnit> units = new List<ISkillUnit>();

        public IReadOnlyList<ISkillUnit> Units
        {
            get
            {
                return units;
            }
        }

        public int Count
        {
            get
            {
                return units.Count;
            }
        }

        public ISkillUnit this[int index]
        {
            get
            {
                return units[index];
            }
        }

        public void Clear()
        {
            units.Clear();
        }

        public void Add(ISkillUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            if (units.Contains(unit))
            {
                return;
            }

            units.Add(unit);
        }
    }

    public static class SkillTargetSystem
    {
        public static bool BuildTargets(SkillContext context)
        {
            if (context == null || context.Config == null || context.Caster == null)
            {
                return false;
            }

            context.Targets.Clear();

            SkillAbilityBehavior behavior = context.Config.Behavior;

            if ((behavior & SkillAbilityBehavior.NoTarget) != 0)
            {
                return true;
            }

            if ((behavior & SkillAbilityBehavior.UnitTarget) != 0)
            {
                if (context.TargetUnit == null)
                {
                    return false;
                }

                if (!IsTargetTeamValid(context.Caster, context.TargetUnit, context.Config.TargetTeam))
                {
                    return false;
                }

                if (!IsInCastRange(context.Caster.Position, context.TargetUnit.Position, context.Config.CastRange))
                {
                    return false;
                }

                context.Targets.Add(context.TargetUnit);
                return true;
            }

            if ((behavior & SkillAbilityBehavior.PointTarget) != 0)
            {
                if (!IsInCastRange(context.Caster.Position, context.TargetPosition, context.Config.CastRange))
                {
                    return false;
                }

                if ((behavior & SkillAbilityBehavior.Aoe) != 0)
                {
                    if (context.World == null)
                    {
                        return false;
                    }

                    SkillTargetFilter filter = new SkillTargetFilter();
                    filter.Caster = context.Caster;
                    filter.TargetTeam = context.Config.TargetTeam;
                    context.World.FindUnits(context.TargetPosition, context.Config.AoeRadius, filter, context.Targets);
                }

                return true;
            }

            if ((behavior & SkillAbilityBehavior.Passive) != 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsTargetTeamValid(ISkillUnit caster, ISkillUnit target, SkillTargetTeam targetTeam)
        {
            if (caster == null || target == null)
            {
                return false;
            }

            bool sameTeam = caster.TeamId == target.TeamId;

            switch (targetTeam)
            {
                case SkillTargetTeam.Friendly:
                    return sameTeam;

                case SkillTargetTeam.Enemy:
                    return !sameTeam;

                case SkillTargetTeam.Both:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsInCastRange(Vector3 from, Vector3 to, float castRange)
        {
            if (castRange <= 0f)
            {
                return true;
            }

            float sqrRange = castRange * castRange;
            return (to - from).sqrMagnitude <= sqrRange;
        }
    }
}
