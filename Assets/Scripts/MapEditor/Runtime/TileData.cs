using UnityEngine;

namespace Game
{
    /// <summary>
    /// 地块运行时数据。
    /// 每次加载地图时，根据 MapTileData 创建。
    /// </summary>
    public sealed class TileData
    {
        private readonly MapTileData mapTileData;

        private Tower tower;

        public TileData(MapTileData mapTileData)
        {
            this.mapTileData = mapTileData;
        }

        public MapTileData MapTileData
        {
            get
            {
                return mapTileData;
            }
        }

        public int X
        {
            get
            {
                return mapTileData.X;
            }
        }

        public int Y
        {
            get
            {
                return mapTileData.Y;
            }
        }

        public int Z
        {
            get
            {
                return mapTileData.Z;
            }
        }

        public Vector3Int Coord
        {
            get
            {
                return new Vector3Int(X, Y, Z);
            }
        }

        public MapTileType Type
        {
            get
            {
                return mapTileData.Type;
            }
        }

        public bool Walkable
        {
            get
            {
                return mapTileData.Walkable;
            }
        }

        public bool Buildable
        {
            get
            {
                return mapTileData.Buildable;
            }
        }

        public int MoveCost
        {
            get
            {
                return mapTileData.MoveCost;
            }
        }

        public bool HasTower
        {
            get
            {
                return tower != null;
            }
        }

        public Tower Tower
        {
            get
            {
                return tower;
            }
        }

        public bool IsRuntimeWalkable
        {
            get
            {
                if (!Walkable)
                {
                    return false;
                }

                if (HasTower)
                {
                    return false;
                }

                return true;
            }
        }

        public bool IsRuntimeBuildable
        {
            get
            {
                if (!Buildable)
                {
                    return false;
                }

                if (HasTower)
                {
                    return false;
                }

                return true;
            }
        }

        public bool TrySetTower(Tower tower)
        {
            if (tower == null)
            {
                return false;
            }

            if (HasTower)
            {
                return false;
            }

            this.tower = tower;
            return true;
        }

        public void ClearTower()
        {
            tower = null;
        }
    }
}