using System;
using System.Collections.Generic;
using UnityEngine;

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
        public List<MapDecorationData> Decorations = new List<MapDecorationData>();

        /// <summary>
        /// 敌对 NPC 出生点。
        /// 数量规则：1~3 个。
        /// </summary>
        public List<Vector3Int> SpawnPoints = new List<Vector3Int>();

        /// <summary>
        /// 是否已经设置玩家基地。
        /// 因为 Vector3Int 是 struct，不能用 null 表示未设置。
        /// </summary>
        public bool HasGoalPoint;

        /// <summary>
        /// 玩家基地坐标。
        /// HasGoalPoint 为 true 时有效。
        /// </summary>
        public Vector3Int GoalPoint;

        public MapData()
        {
        }

        public MapData(int id, string name, int width, int height, int depth)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Depth = depth;

            Tiles = new List<MapTileData>();
            Decorations = new List<MapDecorationData>();
            SpawnPoints = new List<Vector3Int>();
            HasGoalPoint = false;
            GoalPoint = default;
        }

        public void EnsureRuntimeCollections()
        {
            if (Tiles == null)
            {
                Tiles = new List<MapTileData>();
            }

            if (SpawnPoints == null)
            {
                SpawnPoints = new List<Vector3Int>();
            }

            if (Decorations == null)
            {
                Decorations = new List<MapDecorationData>();
            }
        }

        public MapTileData GetTile(int x, int y, int z)
        {
            if (Tiles == null)
            {
                return null;
            }

            for (int i = 0; i < Tiles.Count; i++)
            {
                MapTileData tile = Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                if (tile.X == x && tile.Y == y && tile.Z == z)
                {
                    return tile;
                }
            }

            return null;
        }

        public MapTileData GetTile(Vector3Int coord)
        {
            return GetTile(coord.x, coord.y, coord.z);
        }

        public bool HasSpawnPoint(Vector3Int coord)
        {
            if (SpawnPoints == null)
            {
                return false;
            }

            for (int i = 0; i < SpawnPoints.Count; i++)
            {
                if (SpawnPoints[i] == coord)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsGoalPoint(Vector3Int coord)
        {
            if (!HasGoalPoint)
            {
                return false;
            }

            return GoalPoint == coord;
        }

        public bool HasAnyPoint(Vector3Int coord)
        {
            if (HasSpawnPoint(coord))
            {
                return true;
            }

            if (IsGoalPoint(coord))
            {
                return true;
            }

            return false;
        }
    }
}
