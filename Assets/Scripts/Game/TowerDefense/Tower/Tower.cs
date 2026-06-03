using UnityEngine;

namespace Game
{
    public class Tower : MonoBehaviour
    {
        public TowerData Data { get; private set; }
        public Vector3Int Coord { get; private set; }

        public int ConfigId
        {
            get
            {
                if (Data == null)
                {
                    return 0;
                }

                return Data.ConfigId;
            }
        }

        public int Level
        {
            get
            {
                if (Data == null)
                {
                    return 0;
                }

                return Data.Level;
            }
        }

        public void Initialize(int configId, Vector3Int coord)
        {
            Initialize(configId, 1, coord);
        }

        public void Initialize(int configId, int level, Vector3Int coord)
        {
            Data = new TowerData
            {
                ConfigId = configId,
                Level = Mathf.Max(1, level),
                AttackTimer = 0f,
                Target = null
            };

            Coord = coord;
        }

        public void SetLevel(int level)
        {
            if (Data == null)
            {
                return;
            }

            Data.Level = Mathf.Max(1, level);
            Data.AttackTimer = 0f;
        }
    }
}
