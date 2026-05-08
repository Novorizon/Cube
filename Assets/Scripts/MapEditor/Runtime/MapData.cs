using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// 一张地图的完整导出数据。
    /// Width  对应 X 方向长度。
    /// Height 对应 Y 方向高度。
    /// Depth  对应 Z 方向长度。
    /// </summary>
    [Serializable]
    public class MapData
    {
        public int Id;
        public string Name;
        public string Description;

        public int Width;
        public int Height;
        public int Depth;

        public List<MapTileData> Tiles = new List<MapTileData>();

        public MapData()
        {
        }

        public MapData(int id,string name, int width, int height, int depth)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public MapTileData GetTile(int x, int y, int z)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                MapTileData tile = Tiles[i];

                if (tile.X == x && tile.Y == y && tile.Z == z)
                {
                    return tile;
                }
            }

            return null;
        }
    }
}
