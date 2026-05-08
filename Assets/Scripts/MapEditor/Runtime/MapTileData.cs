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

        public void ApplyDefaultLogicByType(MapTileType type)
        {
            Type = type;
            Walkable = MapTileRule.IsWalkableTileType(type);
            Buildable = MapTileRule.IsBuildableTileType(type);
            MoveCost = MapTileRule.GetDefaultMoveCost(type);
        }
    }
}