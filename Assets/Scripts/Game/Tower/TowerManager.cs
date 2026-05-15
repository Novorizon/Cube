using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class TowerManager
    {
        public static TowerManager Instance { get; } = new TowerManager();

        private readonly List<Tower> activeTowers = new List<Tower>();

        public IReadOnlyList<Tower> ActiveTowers => activeTowers;

        private TowerManager()
        {
        }

        public void Initialize()
        {
            activeTowers.Clear();

            Debug.Log("TowerManager initialized.");
        }

        public void Register(Tower tower)
        {
            if (tower == null)
            {
                return;
            }

            if (activeTowers.Contains(tower))
            {
                return;
            }

            activeTowers.Add(tower);

            Debug.Log($"Tower registered. configId: {tower.ConfigId}, coord: {tower.Coord}");
        }

        public void Unregister(Tower tower)
        {
            if (tower == null)
            {
                return;
            }

            activeTowers.Remove(tower);
        }

        public void Clear()
        {
            activeTowers.Clear();
        }

        public void Update(float deltaTime)
        {
            for (int i = activeTowers.Count - 1; i >= 0; i--)
            {
                Tower tower = activeTowers[i];

                if (tower == null)
                {
                    activeTowers.RemoveAt(i);
                    continue;
                }

                UpdateTower(tower, deltaTime);
            }
        }

        private void UpdateTower(Tower tower, float deltaTime)
        {
            if (tower.Data == null)
            {
                return;
            }

            TowerConfig config = DataManager.Instance.Tower.Get(tower.ConfigId);

            if (config == null)
            {
                return;
            }

            tower.Data.AttackTimer -= deltaTime;

            Npc target = FindTarget(tower, config.Range);
            tower.Data.Target = target;

            if (target == null)
            {
                return;
            }

            FaceTarget(tower, target);

            if (tower.Data.AttackTimer > 0f)
            {
                return;
            }

            tower.Data.AttackTimer = config.AttackInterval;

            NpcManager.Instance.TakeDamage(target, config.Damage);

            Debug.Log($"Tower attack. towerConfigId: {tower.ConfigId}, target: {target.name}, damage: {config.Damage}");
        }

        private Npc FindTarget(Tower tower, float range)
        {
            IReadOnlyList<Npc> npcs = NpcManager.Instance.ActiveNpcs;

            Npc nearest = null;
            float nearestSqrDistance = range * range;
            Vector3 towerPosition = tower.transform.position;

            for (int i = 0; i < npcs.Count; i++)
            {
                Npc npc = npcs[i];

                if (npc == null || npc.Data == null)
                {
                    continue;
                }

                if (npc.ActorType != ActorType.Enemy)
                {
                    continue;
                }

                if (npc.Data.Dead || npc.Data.CurrentHp <= 0)
                {
                    continue;
                }

                float sqrDistance = (npc.transform.position - towerPosition).sqrMagnitude;

                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearest = npc;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }

        private void FaceTarget(Tower tower, Npc target)
        {
            Vector3 direction = target.transform.position - tower.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            tower.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}