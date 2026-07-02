#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor
{
    public static class MapEditorValidator
    {
        public static List<string> Validate(
            MapData mapData,
            MapEditorValidationMode mode,
            Action<MapObjectData, List<string>> validateMapObject = null)
        {
            List<string> errors = new List<string>();
            if (mapData == null)
            {
                errors.Add("MapData is null.");
                return errors;
            }

            mapData.EnsureRuntimeCollections();
            Dictionary<Vector3Int, MapCellData> temp = ValidateCells(mapData, errors);
            ValidateStacks(mapData, temp, errors);
            ValidateObjects(mapData, temp, validateMapObject, errors);

            if (mode == MapEditorValidationMode.TowerDefense)
            {
                ValidateTowerDefensePoints(mapData, errors);
            }

            return errors;
        }

        private static Dictionary<Vector3Int, MapCellData> ValidateCells(MapData mapData, List<string> errors)
        {
            Dictionary<Vector3Int, MapCellData> temp = new Dictionary<Vector3Int, MapCellData>();

            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];
                if (tile == null)
                {
                    errors.Add($"Tile index {i} is null.");
                    continue;
                }

                Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
                if (temp.ContainsKey(coord))
                {
                    errors.Add($"Duplicate tile coord: {coord}");
                    continue;
                }

                temp.Add(coord, tile);

                if (tile.X < 0 || tile.X >= mapData.Width || tile.Z < 0 || tile.Z >= mapData.Depth)
                {
                    errors.Add($"Tile outside positive map range: {coord}");
                }

                if (tile.Type == MapTileType.Soil)
                {
                    errors.Add($"Soil is not used by this editor: {coord}");
                }

                if (tile.Type != MapTileType.Soil && tile.Y < 0)
                {
                    errors.Add($"Non-soil tile must be y>=0: {coord}");
                }

                if (tile.MoveCost < 0)
                {
                    errors.Add($"MoveCost must be >= 0: {coord}");
                }
            }

            return temp;
        }

        private static void ValidateStacks(MapData mapData, Dictionary<Vector3Int, MapCellData> temp, List<string> errors)
        {
            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];
                if (tile == null || tile.Type == MapTileType.Soil)
                {
                    continue;
                }

                if (tile.Y == 0)
                {
                    continue;
                }

                Vector3Int below = new Vector3Int(tile.X, tile.Y - 1, tile.Z);
                if (!temp.TryGetValue(below, out MapCellData belowTile))
                {
                    errors.Add($"Tile missing support below: ({tile.X}, {tile.Y}, {tile.Z})");
                    continue;
                }

                if (!MapTileRule.CanPlaceOn(tile.Type, belowTile.Type))
                {
                    errors.Add($"Invalid stack. Below: {belowTile.Type}, Above: {tile.Type}, Coord: ({tile.X}, {tile.Y}, {tile.Z})");
                }
            }
        }

        private static void ValidateObjects(
            MapData mapData,
            Dictionary<Vector3Int, MapCellData> temp,
            Action<MapObjectData, List<string>> validateMapObject,
            List<string> errors)
        {
            HashSet<int> objectIds = new HashSet<int>();
            if (mapData.Objects == null)
            {
                return;
            }

            for (int i = 0; i < mapData.Objects.Count; i++)
            {
                MapObjectData mapObject = mapData.Objects[i];
                if (mapObject == null)
                {
                    errors.Add($"Map object index {i} is null.");
                    continue;
                }

                if (mapObject.ObjectId <= 0)
                {
                    errors.Add($"Map object has invalid object id. Index: {i}, Config: {mapObject.ConfigId}");
                }
                else if (!objectIds.Add(mapObject.ObjectId))
                {
                    errors.Add($"Duplicate map object id: {mapObject.ObjectId}");
                }

                if (!temp.ContainsKey(mapObject.Coord))
                {
                    errors.Add($"Map object placed on missing tile. ObjectId: {mapObject.ObjectId}, Coord: {mapObject.Coord}");
                }

                validateMapObject?.Invoke(mapObject, errors);
            }
        }

        private static void ValidateTowerDefensePoints(MapData mapData, List<string> errors)
        {
            bool hasValidGoal = false;
            if (mapData.SpawnPoints == null || mapData.SpawnPoints.Count == 0)
            {
                errors.Add("Tower defense map should have at least one spawn point.");
            }

            if (!mapData.HasGoalPoint)
            {
                errors.Add("Tower defense map should have one goal point.");
            }
            else if (!MapTileRule.IsValidMapPoint(mapData.GoalPoint, mapData, out string goalReason))
            {
                errors.Add($"Invalid goal point {mapData.GoalPoint}: {goalReason}");
            }
            else
            {
                hasValidGoal = true;
            }

            if (mapData.SpawnPoints == null)
            {
                return;
            }

            MapDataAStarPathFinder pathFinder = hasValidGoal ? new MapDataAStarPathFinder() : null;
            List<Vector3Int> path = hasValidGoal ? new List<Vector3Int>() : null;

            for (int i = 0; i < mapData.SpawnPoints.Count; i++)
            {
                Vector3Int spawn = mapData.SpawnPoints[i];
                if (!MapTileRule.IsValidMapPoint(spawn, mapData, out string spawnReason))
                {
                    errors.Add($"Invalid spawn point {spawn}: {spawnReason}");
                    continue;
                }

                if (hasValidGoal && !pathFinder.TryFindPath(mapData, spawn, mapData.GoalPoint, path))
                {
                    errors.Add($"Spawn point cannot reach goal. Spawn: {spawn}, Goal: {mapData.GoalPoint}");
                }
            }
        }
    }
}
#endif
