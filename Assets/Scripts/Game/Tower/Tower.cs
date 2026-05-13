using UnityEngine;

namespace Game
{
    public sealed class Tower : MonoBehaviour
    {
        private TowerConfigItem config;
        private Vector3Int coord;

        public TowerConfigItem Config
        {
            get
            {
                return config;
            }
        }

        public TowerType Type
        {
            get
            {
                if (config == null)
                {
                    return TowerType.None;
                }

                return config.Type;
            }
        }

        public Vector3Int Coord
        {
            get
            {
                return coord;
            }
        }

        public void Initialize(TowerConfigItem config, Vector3Int coord)
        {
            this.config = config;
            this.coord = coord;
        }
    }
}
