using System.Collections.Generic;
using Game.Ability;
using UnityEngine;

namespace Game.Tests.Ability
{
    public sealed class FakeWorld : IWorld
    {
        private readonly List<IUnit> units = new List<IUnit>();

        public float Time { get; private set; }
        public bool HasLineOfSightResult { get; set; } = true;
        public IReadOnlyList<IUnit> Units => units;

        public void AddUnit(IUnit unit)
        {
            if (unit != null && !units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void Advance(float deltaTime)
        {
            Time += Mathf.Max(0f, deltaTime);
        }

        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            if (results == null)
            {
                return;
            }

            float radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            for (int i = 0; i < units.Count; i++)
            {
                IUnit unit = units[i];
                if (unit != null && (unit.Position - center).sqrMagnitude <= radiusSquared)
                {
                    results.Add(unit);
                }
            }
        }

        public bool HasLineOfSight(IUnit viewer, Vector3 position)
        {
            return HasLineOfSightResult;
        }
    }
}
