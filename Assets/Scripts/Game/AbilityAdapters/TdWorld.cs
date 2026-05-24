using System.Collections.Generic;
using Game.Ability;
using UnityEngine;

namespace Game
{
    public sealed class TdWorld : IWorld
    {
        private readonly BattleAbilityManager owner;

        public TdWorld(BattleAbilityManager owner)
        {
            this.owner = owner;
        }

        public float Time => UnityEngine.Time.time;

        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            if (results == null || owner == null)
            {
                return;
            }

            AddNpcs(center, radius, results);
            AddTowers(center, radius, results);
            AddBase(center, radius, results);
        }

        public bool HasLineOfSight(IUnit viewer, Vector3 position)
        {
            return true;
        }

        private void AddNpcs(Vector3 center, float radius, IList<IUnit> results)
        {
            IReadOnlyList<Npc> npcs = NpcManager.Instance.ActiveNpcs;
            if (npcs == null)
            {
                return;
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                Npc npc = npcs[i];
                if (npc == null || !IsInRadius(center, npc.transform.position, radius))
                {
                    continue;
                }

                TdUnit unit = owner.GetUnit(npc);
                if (unit != null)
                {
                    results.Add(unit);
                }
            }
        }

        private void AddTowers(Vector3 center, float radius, IList<IUnit> results)
        {
            IReadOnlyList<Tower> towers = TowerManager.Instance.ActiveTowers;
            if (towers == null)
            {
                return;
            }

            for (int i = 0; i < towers.Count; i++)
            {
                Tower tower = towers[i];
                if (tower == null || !IsInRadius(center, tower.transform.position, radius))
                {
                    continue;
                }

                TdUnit unit = owner.GetUnit(tower);
                if (unit != null)
                {
                    results.Add(unit);
                }
            }
        }

        private void AddBase(Vector3 center, float radius, IList<IUnit> results)
        {
            if (!BaseManager.Instance.HasBaseObject)
            {
                return;
            }

            if (!IsInRadius(center, BaseManager.Instance.BasePosition, radius))
            {
                return;
            }

            TdUnit unit = owner.GetBaseUnit();
            if (unit != null)
            {
                results.Add(unit);
            }
        }

        private static bool IsInRadius(Vector3 center, Vector3 position, float radius)
        {
            if (radius <= 0f)
            {
                return true;
            }

            return (position - center).sqrMagnitude <= radius * radius;
        }
    }
}
