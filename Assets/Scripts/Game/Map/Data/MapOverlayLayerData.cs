using System;

namespace Game
{
    [Serializable]
    public class MapOverlayLayerData
    {
        public MapTileOverlay Type;
        public MapDirection Direction = MapDirection.None;
        public int VariantId;

        public MapOverlayLayerData()
        {
        }

        public MapOverlayLayerData(MapTileOverlay type, MapDirection direction = MapDirection.None, int variantId = 0)
        {
            Type = type;
            Direction = type == MapTileOverlay.None ? MapDirection.None : (direction == MapDirection.None ? MapDirection.North : direction);
            VariantId = variantId;
        }
    }
}
