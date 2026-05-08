using System;

namespace Game
{
    /// <summary>
    /// 单个地图格子的静态配置数据。
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
        public bool Buildable;

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
        /// 初版先写死，后面可以改成配置表。
        /// </summary>
        public void ApplyDefaultLogicByType(MapTileType type)
        {
            Type = type;

            switch (type)
            {
                case MapTileType.Soil:
                    Walkable = false;
                    Buildable = true;
                    MoveCost = 0;
                    break;

                case MapTileType.Grass:
                    Walkable = true;
                    Buildable = true;
                    MoveCost = 10;
                    break;

                case MapTileType.Hill:
                    Walkable = true;
                    Buildable = false;
                    MoveCost = 20;
                    break;

                case MapTileType.Snow:
                    Walkable = true;
                    Buildable = false;
                    MoveCost = 15;
                    break;

                case MapTileType.Water:
                    Walkable = false;
                    Buildable = false;
                    MoveCost = 0;
                    break;

                default:
                    Walkable = false;
                    Buildable = false;
                    MoveCost = 0;
                    break;
            }
        }
    }
}