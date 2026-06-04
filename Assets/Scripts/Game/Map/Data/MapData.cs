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

        public List<MapCellData> Cells = new List<MapCellData>();
        public List<MapObjectData> Objects = new List<MapObjectData>();

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

            Cells = new List<MapCellData>();
            Objects = new List<MapObjectData>();
            SpawnPoints = new List<Vector3Int>();
            HasGoalPoint = false;
            GoalPoint = default;
        }

        public void EnsureRuntimeCollections()
        {
            if (Cells == null)
            {
                Cells = new List<MapCellData>();
            }

            if (SpawnPoints == null)
            {
                SpawnPoints = new List<Vector3Int>();
            }

            if (Objects == null)
            {
                Objects = new List<MapObjectData>();
            }
        }

        public MapCellData GetCell(int x, int y, int z)
        {
            if (Cells == null)
            {
                return null;
            }

            for (int i = 0; i < Cells.Count; i++)
            {
                MapCellData cell = Cells[i];

                if (cell == null)
                {
                    continue;
                }

                if (cell.X == x && cell.Y == y && cell.Z == z)
                {
                    return cell;
                }
            }

            return null;
        }

        public MapCellData GetCell(Vector3Int coord)
        {
            return GetCell(coord.x, coord.y, coord.z);
        }

        public List<MapObjectData> GetObjectsAt(Vector3Int coord, List<MapObjectData> results = null)
        {
            results ??= new List<MapObjectData>();
            results.Clear();

            if (Objects == null)
            {
                return results;
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObjectData mapObject = Objects[i];
                if (mapObject != null && mapObject.Coord == coord)
                {
                    results.Add(mapObject);
                }
            }

            return results;
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
