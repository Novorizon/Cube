using System;

namespace Game
{
    [Serializable]
    public class MapTileLayerData
    {
        public MapTileType Type;
        public MapDirection Direction = MapDirection.North;
        public int VariantId;

        public MapTileLayerData()
        {
        }

        public MapTileLayerData(MapTileType type, MapDirection direction = MapDirection.North, int variantId = 0)
        {
            Type = type;
            Direction = direction == MapDirection.None ? MapDirection.North : direction;
            VariantId = variantId;
        }
    }
}
