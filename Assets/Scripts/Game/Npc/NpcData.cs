using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class NpcData
    {
        public int ConfigId;
        public float MoveSpeed;
        public int DamageToBase;

        public readonly List<Vector3Int> Path = new List<Vector3Int>();

        public int PathIndex;
        public bool Moving;
        public bool ReachedGoal;

        public void Initialize(NpcConfig config, IReadOnlyList<Vector3Int> path)
        {
            ConfigId = config != null ? config.Id : 0;
            MoveSpeed = config != null ? config.MoveSpeed : 1f;
            DamageToBase = config != null ? config.DamageToBase : 0;

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
        }
    }
}
