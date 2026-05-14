using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class Enemy : Actor
    {
        private Npc config;
        private float moveSpeed;
        private int damageToBase;

        private readonly List<Vector3Int> path = new List<Vector3Int>();
        private int pathIndex;

        private bool moving;
        private bool reachedGoal;

        public Npc Config
        {
            get
            {
                return config;
            }
        }

        public float MoveSpeed
        {
            get
            {
                return moveSpeed;
            }
        }

        public int DamageToBase
        {
            get
            {
                return damageToBase;
            }
        }

        public IReadOnlyList<Vector3Int> Path
        {
            get
            {
                return path;
            }
        }

        public int PathIndex
        {
            get
            {
                return pathIndex;
            }
        }

        public bool Moving
        {
            get
            {
                return moving;
            }
        }

        public bool ReachedGoal
        {
            get
            {
                return reachedGoal;
            }
        }

        public void InitializeRaw(Npc config, IReadOnlyList<Vector3Int> path)
        {
            this.config = config;

            int configId = config != null ? config.Id : 0;
            InitializeActor(ActorType.Enemy, configId);

            moveSpeed = config != null ? config.MoveSpeed : 1f;
            damageToBase = config != null ? config.DamageToBase : 1;

            this.path.Clear();

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    this.path.Add(path[i]);
                }
            }

            pathIndex = this.path.Count > 1 ? 1 : 0;
            moving = this.path.Count > 0;
            reachedGoal = false;
        }

        public void SetPathIndexRaw(int value)
        {
            pathIndex = value;
        }

        public void SetMovingRaw(bool value)
        {
            moving = value;
        }

        public void SetReachedGoalRaw(bool value)
        {
            reachedGoal = value;
        }
    }
}
