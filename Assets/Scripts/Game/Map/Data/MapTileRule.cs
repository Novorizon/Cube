using UnityEngine;

namespace Game
{
    public static class MapTileRule
    {
        public static bool IsBaseTile(MapTileType type)
        {
            return type == MapTileType.Soil;
        }

        public static bool IsLogicTile(MapTileType type)
        {
            return type != MapTileType.None && type != MapTileType.Soil;
        }

        public static bool IsSurfaceTile(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                case MapTileType.Hill:
                case MapTileType.Snow:
                case MapTileType.Water:
                case MapTileType.Road:
                case MapTileType.Bridge:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsEditableBaseTile(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                case MapTileType.Hill:
                case MapTileType.Snow:
                case MapTileType.Water:
                case MapTileType.Road:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsWalkableTileType(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                case MapTileType.Hill:
                case MapTileType.Snow:
                case MapTileType.Road:
                case MapTileType.Bridge:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsBuildableTileType(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsWalkable(MapTileType type, MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return true;

                default:
                    return IsWalkableTileType(type);
            }
        }

        public static bool IsBuildable(MapTileType type, MapTileOverlay overlay)
        {
            if (overlay != MapTileOverlay.None)
            {
                return false;
            }

            return IsBuildableTileType(type);
        }

        public static int GetDefaultMoveCost(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Road:
                case MapTileType.Bridge:
                    return 8;

                case MapTileType.Grass:
                    return 10;

                case MapTileType.Snow:
                    return 15;

                case MapTileType.Hill:
                    return 20;

                default:
                    return 0;
            }
        }

        public static int GetDefaultMoveCost(MapTileType type, MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return 8;

                default:
                    return GetDefaultMoveCost(type);
            }
        }

        public static bool CanPlaceTile(MapTileType placeType, int x, int y, int z, MapData mapData)
        {
            if (mapData == null)
            {
                return false;
            }

            mapData.EnsureRuntimeCollections();

            if (placeType == MapTileType.None)
            {
                return false;
            }

            if (!IsInsideMapRange(x, y, z, placeType, mapData))
            {
                return false;
            }

            if (mapData.GetCell(x, y, z) != null)
            {
                return false;
            }

            if (placeType == MapTileType.Soil)
            {
                return false;
            }

            if (y == 0)
            {
                return true;
            }

            MapCellData belowTile = mapData.GetCell(x, y - 1, z);

            if (belowTile == null)
            {
                return false;
            }

            return CanPlaceOn(placeType, belowTile.Type);
        }

        public static bool CanPlaceOn(MapTileType placeType, MapTileType belowType)
        {
            switch (placeType)
            {
                case MapTileType.Grass:
                case MapTileType.Hill:
                case MapTileType.Snow:
                case MapTileType.Water:
                    return belowType == MapTileType.Soil ||
                           IsEditableBaseTile(belowType);

                case MapTileType.Road:
                    return IsSurfaceTile(belowType);

                case MapTileType.Bridge:
                    return IsSurfaceTile(belowType);

                default:
                    return false;
            }
        }

        public static bool CanHaveTileAbove(MapTileType belowType, MapTileType aboveType)
        {
            return CanPlaceOn(aboveType, belowType);
        }

        public static bool CanRemoveTile(int x, int y, int z, MapData mapData)
        {
            if (mapData == null)
            {
                return false;
            }

            mapData.EnsureRuntimeCollections();

            MapCellData tile = mapData.GetCell(x, y, z);

            if (tile == null)
            {
                return false;
            }

            MapCellData aboveTile = mapData.GetCell(x, y + 1, z);

            if (aboveTile != null)
            {
                return false;
            }

            return true;
        }

        public static bool IsExposed(int x, int y, int z, MapData mapData)
        {
            if (mapData == null)
            {
                return false;
            }

            return mapData.GetCell(x, y + 1, z) == null;
        }

        public static bool IsValidMapPoint(Vector3Int coord, MapData mapData, out string reason)
        {
            reason = string.Empty;

            if (mapData == null)
            {
                reason = "map data is null";
                return false;
            }

            mapData.EnsureRuntimeCollections();

            MapCellData tile = mapData.GetCell(coord);

            if (tile == null)
            {
                reason = "target tile not found";
                return false;
            }

            if (!IsLogicTile(tile.Type))
            {
                reason = $"target tile is not logic tile: {tile.Type}";
                return false;
            }

            if (!IsWalkable(tile.Type, tile.Overlay.Type))
            {
                reason = $"target tile is not walkable: {tile.Type}, overlay: {tile.Overlay.Type}";
                return false;
            }

            if (!IsExposed(coord.x, coord.y, coord.z, mapData))
            {
                reason = "target tile has upper tile";
                return false;
            }

            return true;
        }

        private static bool IsInsideMapRange(int x, int y, int z, MapTileType placeType, MapData mapData)
        {
            if (mapData.Width > 0)
            {
                if (x < 0 || x >= mapData.Width)
                {
                    return false;
                }
            }

            if (mapData.Depth > 0)
            {
                if (z < 0 || z >= mapData.Depth)
                {
                    return false;
                }
            }

            if (placeType == MapTileType.Soil)
            {
                return false;
            }

            if (y < 0)
            {
                return false;
            }

            if (mapData.Height > 0 && y >= mapData.Height)
            {
                return false;
            }

            return true;
        }
    }
}
