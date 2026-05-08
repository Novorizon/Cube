using System;

namespace Game
{
    /// <summary>
    /// 单个地图格子的数据。
    /// 这里的 X/Y/Z 是逻辑坐标，不是 Unity 世界坐标。
    /// </summary>
    [Serializable]
    public class MapTileData
    {
        public int X;
        public int Y;
        public int Z;

        public MapTileType Type;

        public bool Walkable;

        public int MoveCost;

        public MapTileData()
        {
        }

        public MapTileData(int x, int y, int z, MapTileType type)
        {
            X = x;
            Y = y;
            Z = z;
            Type = type;

            ApplyDefaultLogicByType(type);
        }

        /// <summary>
        /// 根据地块类型设置默认逻辑。
        /// 这里只是初版默认值，后面可以改成配置表。
        /// </summary>
        public void ApplyDefaultLogicByType(MapTileType type)
        {
            Type = type;

            switch (type)
            {
                case MapTileType.Soil:
                    Walkable = false;
                    MoveCost = 0;
                    break;

                case MapTileType.Grass:
                    Walkable = true;
                    MoveCost = 10;
                    break;

                case MapTileType.Hill:
                    Walkable = true;
                    MoveCost = 20;
                    break;

                case MapTileType.Snow:
                    Walkable = true;
                    MoveCost = 15;
                    break;

                case MapTileType.Water:
                    Walkable = false;
                    MoveCost = 0;
                    break;

                default:
                    Walkable = false;
                    MoveCost = 0;
                    break;
            }
        }
    }
}
