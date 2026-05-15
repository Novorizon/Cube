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

        public void Initialize(int configId, Vector3Int coord)
        {
            Data = new TowerData
            {
                ConfigId = configId,
                AttackTimer = 0f,
                Target = null
            };

            Coord = coord;
        }
    }
}