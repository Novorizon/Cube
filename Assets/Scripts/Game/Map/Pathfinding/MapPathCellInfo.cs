using UnityEngine;

namespace Game
{
    public readonly struct MapPathCellInfo
    {
        public readonly Vector3Int Coord;
        public readonly MapTileType Type;
        public readonly MapDirection TypeDirection;
        public readonly MapTileOverlay Overlay;
        public readonly MapDirection OverlayDirection;

        public MapPathCellInfo(
            Vector3Int coord,
            MapTileType type,
            MapDirection typeDirection,
            MapTileOverlay overlay,
            MapDirection overlayDirection)
        {
            Coord = coord;
            Type = type;
            TypeDirection = typeDirection;
            Overlay = overlay;
            OverlayDirection = overlayDirection;
        }

        public static MapPathCellInfo From(MapCellData cell)
        {
            if (cell == null)
            {
                return default;
            }

            cell.EnsureLayers();

            return new MapPathCellInfo(
                cell.Coord,
                cell.Tile.Type,
                cell.Tile.Direction,
                cell.Overlay.Type,
                cell.Overlay.Direction);
        }

        public static MapPathCellInfo From(TileData tile)
        {
            if (tile == null)
            {
                return default;
            }

            return new MapPathCellInfo(
                tile.Coord,
                tile.Type,
                tile.TypeDirection,
                tile.Overlay,
                tile.OverlayDirection);
        }
    }
}
