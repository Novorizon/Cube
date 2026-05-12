using UnityEngine;

namespace Game
{
    /// <summary>
    /// 地图地块规则。
    /// 
    /// 负责：
    /// 1. 编辑器放置规则
    /// 2. 默认逻辑属性
    /// 3. 地块堆叠合法性
    /// 4. 出生点 / 基地点位合法性
    /// 
    /// 不负责运行时塔占用，也不负责寻路算法本身。
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
                case MapTileType.Hill:
                case MapTileType.Snow:
                case MapTileType.Water:
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

        /// <summary>
        /// placeType 是否允许放在 belowType 上方。
        /// </summary>
        public static bool CanPlaceOn(MapTileType placeType, MapTileType belowType)
        {
            switch (placeType)
            {
                case MapTileType.Grass:
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass;

                case MapTileType.Hill:
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass ||
                           belowType == MapTileType.Hill;

                case MapTileType.Snow:
                    return belowType == MapTileType.Soil ||
                           belowType == MapTileType.Grass ||
                           belowType == MapTileType.Hill ||
                           belowType == MapTileType.Snow;

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

            mapData.EnsureRuntimeCollections();

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

        /// <summary>
        /// 出生点 / 基地 必须放在暴露的、可走的逻辑地块上。
        /// </summary>
        public static bool IsValidMapPoint(Vector3Int coord, MapData mapData, out string reason)
        {
            reason = string.Empty;

            if (mapData == null)
            {
                reason = "map data is null";
                return false;
            }

            mapData.EnsureRuntimeCollections();

            MapTileData tile = mapData.GetTile(coord);

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

            if (!IsWalkableTileType(tile.Type))
            {
                reason = $"target tile is not walkable: {tile.Type}";
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