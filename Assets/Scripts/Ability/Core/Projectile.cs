using System.Collections.Generic;
using UnityEngine;

namespace Game.Ability
{
    public sealed class Projectile
    {
        public ProjectileDefinition Definition { get; }
        public Ability Ability { get; }
        public IUnit Caster { get; }
        public IUnit Source { get; }
        public IUnit Target { get; }
        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; }
        public bool Tracking { get; }
        public bool Destroyed { get; private set; }

        private float traveledDistance;

        internal Projectile(ProjectileRequest request)
        {
            Definition = request.Definition;
            Ability = request.Ability;
            Caster = request.Caster;
            Source = request.Source;
            Target = request.Target;
            Position = request.Origin;
            Direction = request.Direction.sqrMagnitude > 0.0001f ? request.Direction.normalized : Vector3.forward;
            Tracking = request.Tracking;
        }

        internal void Tick(AbilitySystem engine, float deltaTime)
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

        private void TickTracking(AbilitySystem engine, float deltaTime)
        {
            if (Target == null || !Target.IsAlive)
            {
                Destroyed = true;
                return;
            }

            Vector3 toTarget = Target.Position - Position;
            float step = Mathf.Max(0f, Definition.Speed) * deltaTime;
            if (toTarget.sqrMagnitude <= step * step)
            {
                Position = Target.Position;
                Hit(engine, Target);
                return;
            }

            Position += toTarget.normalized * step;
        }

        private void TickLinear(AbilitySystem engine, float deltaTime)
        {
            float step = Mathf.Max(0f, Definition.Speed) * deltaTime;
            Position += Direction * step;
            traveledDistance += step;

            TargetQuery query = new TargetQuery
            {
                Caster = Caster,
                Team = Definition.TargetTeam,
                Types = Definition.TargetType,
                Flags = Definition.TargetFlags
            };

            List<IUnit> units = new List<IUnit>();
            engine.FindUnits(Position, Definition.Radius, query, units);
            for (int i = 0; i < units.Count; i++)
            {
                Hit(engine, units[i]);
                if (Destroyed)
                {
                    return;
                }
            }

            if (traveledDistance >= Definition.Distance)
            {
                Destroyed = true;
            }
        }

        private void Hit(AbilitySystem engine, IUnit target)
        {
            bool destroy = Definition.DeleteOnHit;
            if (Ability != null && Ability.Script != null)
            {
                destroy = Ability.Script.OnProjectileHit(this, target, Position) || destroy;
            }

            engine.OnProjectileHit(this, target, Position);
            if (destroy)
            {
                Destroyed = true;
            }
        }
    }
}
