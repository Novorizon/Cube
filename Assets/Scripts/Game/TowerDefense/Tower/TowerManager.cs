using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class TowerManager
    {
        public static TowerManager Instance { get; } = new TowerManager();

        private readonly List<Tower> activeTowers = new List<Tower>();

        public event System.Action<Tower> TowerRegistered;
        public event System.Action<Tower> TowerUnregistered;

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
            TowerRegistered?.Invoke(tower);

            Debug.Log($"Tower registered. configId: {tower.ConfigId}, coord: {tower.Coord}");
        }

        public void Unregister(Tower tower)
        {
            if (tower == null)
            {
                return;
            }

            if (activeTowers.Remove(tower))
            {
                TowerUnregistered?.Invoke(tower);
            }
        }

        public void Clear()
        {
            for (int i = activeTowers.Count - 1; i >= 0; i--)
            {
                Tower tower = activeTowers[i];
                if (tower != null)
                {
                    TowerUnregistered?.Invoke(tower);
                }
            }

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

            TowerLevelConfig config = DataManager.Instance.GetTowerLevel(tower.ConfigId, tower.Level);

            if (config == null)
            {
                return;
            }

            if (AbilityManager.Instance.IsActionRestricted(tower) || AbilityManager.Instance.IsAbilityExecuting(tower))
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

            //FaceTarget(tower, target);

            if (tower.Data.AttackTimer > 0f)
            {
                return;
            }

            // Freeze the attack request at launch. Damage and the optional on-hit skill settle after travel.
            AbilityAttackHandle attack = AbilityManager.Instance.LaunchTowerAttack(tower, target, config.Damage, config.AttackEffect, config.HitEffect, config.SkillId);
            if (!attack.IsValid) return;
            tower.Data.AttackTimer = Mathf.Max(0.05f, config.AttackInterval * AbilityManager.Instance.GetAttackIntervalMultiplier(tower));
            Debug.Log($"Tower attack launched. towerConfigId: {tower.ConfigId}, level: {tower.Level}, target: {target.name}, damage: {config.Damage}, attackId: {attack.AttackId}");
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

        public bool HasGold(int towerConfigId)
        {
            if (!DataManager.Instance.TryGetTowerLevel(towerConfigId, 1, out TowerLevelConfig config))
            {
                Debug.LogWarning($"Select tower failed. Missing tower level config: {towerConfigId}, level: 1");
                return false;
            }
            int costItemId = config.CostItemId > 0 ? config.CostItemId : ItemIds.Gold;
            int itemCount = BattleItemManager.Instance.GetCount(costItemId);
            if (itemCount < config.BuildCost)
            {
                Debug.LogWarning($"Build cost is not enough. itemId: {costItemId}, current: {itemCount}, need: {config.BuildCost}");
                return false;
            }
            return true;
        }

    }
}

