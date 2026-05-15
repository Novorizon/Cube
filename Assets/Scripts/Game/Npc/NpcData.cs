using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class NpcData
    {
        public int ConfigId;

        public int MaxHp;
        public int CurrentHp;
        public int RewardGold;
        public bool Dead;

        public float MoveSpeed;
        public int DamageToBase;
        public float AttackRange;
        public float AttackInterval;

        public readonly List<Vector3Int> Path = new List<Vector3Int>();

        public int PathIndex;
        public bool Moving;
        public bool ReachedGoal;

        public bool Attacking;
        public float AttackTimer;

        public Animator Animator;

        public void Initialize(NpcConfig config, IReadOnlyList<Vector3Int> path)
        {
            ConfigId = config != null ? config.Id : 0;

            MaxHp = config != null ? config.MaxHp : 1;
            if (MaxHp <= 0)
            {
                MaxHp = 1;
            }

            CurrentHp = MaxHp;
            RewardGold = config != null ? config.RewardGold : 0;
            Dead = false;

            MoveSpeed = config != null ? config.MoveSpeed : 1f;
            DamageToBase = config != null ? config.DamageToBase : 0;
            AttackRange = config != null ? config.AttackRange : 0.8f;
            AttackInterval = config != null ? config.AttackInterval : 1f;

            if (MoveSpeed <= 0f)
            {
                MoveSpeed = 1f;
            }

            if (AttackRange <= 0f)
            {
                AttackRange = 0.8f;
            }

            if (AttackInterval <= 0f)
            {
                AttackInterval = 1f;
            }

            Path.Clear();

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Path.Add(path[i]);
                }
            }

            PathIndex = Path.Count > 1 ? 1 : 0;
            Moving = Path.Count > 1;
            ReachedGoal = false;

            Attacking = false;
            AttackTimer = 0f;
            Animator = null;
        }
    }
}