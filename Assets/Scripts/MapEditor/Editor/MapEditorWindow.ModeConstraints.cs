#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public partial class MapEditorWindow
    {
        private bool ApplyEditorModeConstraints(MapData mapData, bool askBeforeChanging, string actionName)
        {
            if (mapData == null)
            {
                return true;
            }

            mapData.EnsureRuntimeCollections();

            int towerPointCount = GetTowerPointCount(mapData);
            int resourceObjectCount = GetObjectCount(mapData, MapObjectType.Resource);
            bool needsPointCleanup = !SupportsPointsTab && towerPointCount > 0;
            bool needsResourceCleanup = !SupportsResourcesTab && resourceObjectCount > 0;

            if (!needsPointCleanup && !needsResourceCleanup)
            {
                return true;
            }

            List<string> messages = new List<string>();
            if (needsPointCleanup)
            {
                messages.Add($"Remove tower-defense points: {towerPointCount}");
            }

            if (needsResourceCleanup)
            {
                messages.Add($"Remove world resource objects: {resourceObjectCount}");
            }

            if (askBeforeChanging &&
                !EditorUtility.DisplayDialog(
                    $"{EditorTitle} Data Cleanup",
                    $"{actionName} data contains fields not supported by this editor mode.\n\n{string.Join("\n", messages)}\n\nContinue and clean them?",
                    "Clean And Continue",
                    "Cancel"))
            {
                return false;
            }

            if (needsPointCleanup)
            {
                ClearTowerDefensePointData(mapData);
            }

            if (needsResourceCleanup)
            {
                mapData.Objects.RemoveAll(mapObject => mapObject != null && mapObject.ObjectType == MapObjectType.Resource);
            }

            return true;
        }

        private void AppendEditorModeValidationErrors(MapData mapData, List<string> errors)
        {
            if (mapData == null || errors == null)
            {
                return;
            }

            mapData.EnsureRuntimeCollections();

            if (!SupportsPointsTab)
            {
                int towerPointCount = GetTowerPointCount(mapData);
                if (towerPointCount > 0)
                {
                    errors.Add($"{EditorTitle} does not support tower-defense points. Count: {towerPointCount}");
                }
            }

            if (!SupportsResourcesTab)
            {
                int resourceObjectCount = GetObjectCount(mapData, MapObjectType.Resource);
                if (resourceObjectCount > 0)
                {
                    errors.Add($"{EditorTitle} does not support world resource objects. Count: {resourceObjectCount}");
                }
            }
        }

        private void ClearCurrentModeUnsupportedState()
        {
            if (currentMap == null)
            {
                return;
            }

            if (!SupportsPointsTab)
            {
                ClearTowerDefensePointData(currentMap);
            }

            if (!SupportsResourcesTab)
            {
                currentMap.EnsureRuntimeCollections();
                currentMap.Objects.RemoveAll(mapObject => mapObject != null && mapObject.ObjectType == MapObjectType.Resource);
            }
        }

        private void ClearTowerDefensePointData(MapData mapData)
        {
            if (mapData == null)
            {
                return;
            }

            mapData.EnsureRuntimeCollections();
            mapData.SpawnPoints.Clear();
            mapData.HasGoalPoint = false;
            mapData.GoalPoint = default;

            if (ReferenceEquals(mapData, currentMap))
            {
                spawnPoints.Clear();
                hasGoalPoint = false;
                goalPoint = default;
            }
        }

        private static int GetTowerPointCount(MapData mapData)
        {
            if (mapData == null)
            {
                return 0;
            }

            mapData.EnsureRuntimeCollections();
            int count = mapData.SpawnPoints != null ? mapData.SpawnPoints.Count : 0;
            return mapData.HasGoalPoint ? count + 1 : count;
        }

        private static int GetObjectCount(MapData mapData, MapObjectType objectType)
        {
            if (mapData == null || mapData.Objects == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < mapData.Objects.Count; i++)
            {
                MapObjectData mapObject = mapData.Objects[i];
                if (mapObject != null && mapObject.ObjectType == objectType)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

#endif
