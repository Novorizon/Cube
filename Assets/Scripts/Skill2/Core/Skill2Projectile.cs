using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public sealed class SkillProjectile
    {
        public SkillProjectileDefinition Definition { get; }
        public SkillAbility Ability { get; }
        public ISkill2Unit Caster { get; }
        public ISkill2Unit Source { get; }
        public ISkill2Unit Target { get; }
        public bool Tracking { get; }
        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }
        public float DistanceTravelled { get; private set; }
        public bool Destroyed { get; private set; }

        private readonly HashSet<int> hitUnits = new HashSet<int>();

        internal SkillProjectile(SkillProjectileRequest request)
        {
            Definition = request.Definition;
            Ability = request.Ability;
            Caster = request.Caster;
            Source = request.Source;
            Target = request.Target;
            Tracking = request.Tracking;
            Position = request.Origin;
            Direction = request.Direction.sqrMagnitude > 0.0001f ? request.Direction.normalized : Vector3.forward;
        }

        internal void Tick(Skill2Engine engine, float deltaTime)
        {
            if (Destroyed)
            {
                return;
            }

            if (Tracking)
            {
                TickTracking(engine, deltaTime);
            }
            else
            {
                TickLinear(engine, deltaTime);
            }
        }

        internal void Destroy()
        {
            Destroyed = true;
        }

        private void TickTracking(Skill2Engine engine, float deltaTime)
        {
            if (Target == null || !Target.IsAlive)
            {
                Destroy();
                return;
            }

            Vector3 toTarget = Target.Position - Position;
            float step = Definition.Speed * deltaTime;

            if (toTarget.sqrMagnitude <= step * step)
            {
                Position = Target.Position;
                Hit(engine, Target);
                return;
            }

            Direction = toTarget.normalized;
            Position += Direction * step;
            DistanceTravelled += step;
        }

        private void TickLinear(Skill2Engine engine, float deltaTime)
        {
            float step = Definition.Speed * deltaTime;
            Position += Direction * step;
            DistanceTravelled += step;

            SkillTargetQuery query = new SkillTargetQuery
            {
                Caster = Caster,
                Team = Definition.TargetTeam,
                Types = Definition.TargetType,
                Flags = Definition.TargetFlags
            };

            List<ISkill2Unit> units = new List<ISkill2Unit>();
            if (engine.World != null)
            {
                engine.FindUnits(Position, Definition.Radius, query, units);
            }

            for (int i = 0; i < units.Count; i++)
            {
                ISkill2Unit unit = units[i];
                if (unit == null || hitUnits.Contains(unit.EntityId))
                {
                    continue;
                }

                hitUnits.Add(unit.EntityId);
                Hit(engine, unit);

                if (Destroyed)
                {
                    return;
                }
            }

            if (DistanceTravelled >= Definition.Distance)
            {
                Destroy();
            }
        }

        private void Hit(Skill2Engine engine, ISkill2Unit target)
        {
            bool destroy = true;

            if (Ability != null && Ability.Script != null)
            {
                destroy = Ability.Script.OnProjectileHit(this, target, Position);
            }

            engine.OnProjectileHit(this, target, Position);

            if (Definition.DeleteOnHit && destroy)
            {
                Destroy();
            }
        }
    }
}
