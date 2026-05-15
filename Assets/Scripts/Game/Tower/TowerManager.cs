using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class TowerManager : Singleton<TowerManager>
    {
        private readonly List<Tower> activeTowers = new List<Tower>();

        private bool initialized;

        public IReadOnlyList<Tower> ActiveTowers
        {
            get
            {
                return activeTowers;
            }
        }

        public bool Initialize()
        {
            initialized = true;
            return true;
        }

        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

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

            Debug.Log($"Tower registered. Type: {tower.Type}, Coord: {tower.Coord}");
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

        private void UpdateTower(Tower tower, float deltaTime)
        {
            if (tower == null || tower.Config == null || tower.Data == null)
            {
                return;
            }

            tower.Data.AttackTimer -= deltaTime;

            if (tower.Data.AttackTimer > 0f)
            {
                return;
            }

            Npc target = FindTarget(tower);

            if (target == null)
            {
                tower.Data.Target = null;
                return;
            }

            tower.Data.Target = target;
            tower.Data.AttackTimer = GetAttackInterval(tower);

            AttackTarget(tower, target);
        }

        private Npc FindTarget(Tower tower)
        {
            if (tower == null || tower.Config == null)
            {
                return null;
            }

            float range = GetRange(tower);
            float rangeSqr = range * range;

            Vector3 towerPosition = tower.transform.position;
            towerPosition.y = 0f;

            Npc bestTarget = null;
            float bestDistanceSqr = float.MaxValue;

            IReadOnlyList<Npc> npcs = NpcManager.Instance.ActiveNpcs;

            for (int i = 0; i < npcs.Count; i++)
            {
                Npc npc = npcs[i];

                if (!IsValidTarget(npc))
                {
                    continue;
                }

                Vector3 npcPosition = npc.transform.position;
                npcPosition.y = 0f;

                float distanceSqr = (npcPosition - towerPosition).sqrMagnitude;

                if (distanceSqr > rangeSqr)
                {
                    continue;
                }

                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestTarget = npc;
            }

            return bestTarget;
        }

        private bool IsValidTarget(Npc npc)
        {
            if (npc == null)
            {
                return false;
            }

            if (npc.ActorType != ActorType.Enemy)
            {
                return false;
            }

            if (npc.Data == null)
            {
                return false;
            }

            if (npc.Data.Dead)
            {
                return false;
            }

            if (npc.Data.CurrentHp <= 0)
            {
                return false;
            }

            return true;
        }

        private void AttackTarget(Tower tower, Npc target)
        {
            if (tower == null || target == null)
            {
                return;
            }

            FaceToTarget(tower, target);

            int damage = GetDamage(tower);

            Debug.Log($"Tower attack. Tower: {tower.Type}, Target: {target.Config?.Id}, Damage: {damage}");

            NpcManager.Instance.TakeDamage(target, damage);
        }

        private void FaceToTarget(Tower tower, Npc target)
        {
            if (tower == null || target == null)
            {
                return;
            }

            Vector3 direction = target.transform.position - tower.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            tower.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private float GetRange(Tower tower)
        {
            if (tower == null || tower.Config == null)
            {
                return 0f;
            }

            float range = tower.Config.Range;

            if (range <= 0f)
            {
                range = 3f;
            }

            return range;
        }

        private int GetDamage(Tower tower)
        {
            if (tower == null || tower.Config == null)
            {
                return 0;
            }

            int damage = tower.Config.Damage;

            if (damage <= 0)
            {
                damage = 1;
            }

            return damage;
        }

        private float GetAttackInterval(Tower tower)
        {
            if (tower == null || tower.Config == null)
            {
                return 1f;
            }

            float interval = tower.Config.AttackInterval;

            if (interval <= 0f)
            {
                interval = 1f;
            }

            return interval;
        }
    }
}