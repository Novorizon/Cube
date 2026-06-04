using System;

namespace Game
{
    [Serializable]
    [Obsolete("Use MapCellData instead.")]
    public class MapTileData : MapCellData
    {
        public MapTileData()
        {
        }

        public MapTileData(int x, int y, int z, MapTileType type)
            : base(x, y, z, type)
        {
        }
    }
}
