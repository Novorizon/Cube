using UnityEngine;

namespace Game
{
    /// <summary>
    /// 地图地块规则。
    /// 只负责静态地图编辑规则与默认逻辑规则。
    /// 运行时是否有塔、是否被遮挡，由 MapManager / TileData 判断。
    /// </summary>
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
                case MapTileType.Snow:
                case MapTileType.Hill:
                case MapTileType.Water:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsSupportTile(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Soil:
                case MapTileType.Grass:
                case MapTileType.Snow:
                case MapTileType.Hill:
                    return true;

                default:
                    return false;
            }
        }

        public static bool CanPlaceTile(MapTileType placeType, int x, int y, int z, MapData mapData)
        {
            if (mapData == null)
            {
                return false;
            }

            if (placeType == MapTileType.None)
            {
                return false;
            }

            if (!IsInsideMapRange(x, y, z, placeType, mapData))
            {
                return false;
            }

            if (mapData.GetTile(x, y, z) != null)
            {
                return false;
            }

            if (placeType == MapTileType.Soil)
            {
                return y == -1;
            }

            MapTileData belowTile = mapData.GetTile(x, y - 1, z);

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
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass;

                case MapTileType.Snow:
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass ||
                           belowType == MapTileType.Hill ||
                           belowType == MapTileType.Snow;

                case MapTileType.Hill:
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass ||
                           belowType == MapTileType.Hill;

                case MapTileType.Water:
                    return belowType == MapTileType.Soil;

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

            MapTileData tile = mapData.GetTile(x, y, z);

            if (tile == null)
            {
                return false;
            }

            MapTileData aboveTile = mapData.GetTile(x, y + 1, z);

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

            return mapData.GetTile(x, y + 1, z) == null;
        }

        public static bool IsWalkableTileType(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                case MapTileType.Snow:
                case MapTileType.Hill:
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

        public static int GetDefaultMoveCost(MapTileType type)
        {
            switch (type)
            {
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
                return y == -1;
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