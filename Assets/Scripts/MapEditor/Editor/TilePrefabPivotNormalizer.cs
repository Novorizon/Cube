#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class TilePrefabPivotNormalizer
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        [MenuItem("Tools/Map/Normalize Selected Tile Prefab Pivots")]
        private static void NormalizeSelectedPrefabs()
        {
            Object[] objects = Selection.objects;
            int changedCount = 0;

            for (int i = 0; i < objects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(objects[i]);

                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    continue;
                }

                if (NormalizePrefab(path))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Normalize selected tile prefab pivots complete. Changed: {changedCount}");
        }

        [MenuItem("Tools/Map/Normalize Current Prefab Stage Tile Pivot")]
        private static void NormalizeCurrentPrefabStage()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage == null || prefabStage.prefabContentsRoot == null)
            {
                Debug.LogWarning("Normalize current prefab stage skipped. Open a tile prefab in Prefab Mode first.");
                return;
            }

            GameObject root = prefabStage.prefabContentsRoot;

            if (!NormalizePrefabRoot(root.transform, true, out Vector3 offset))
            {
                Debug.LogWarning($"Normalize current prefab stage skipped. No pivot change needed or no renderer found: {root.name}");
                return;
            }

            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            Debug.Log($"Normalize current prefab stage pivot complete. Root: {root.name}, offset: {offset}. Save the prefab to keep the change.");
        }

        [MenuItem("Tools/Map/Normalize Selected Transform Tile Pivot")]
        private static void NormalizeSelectedTransform()
        {
            Transform root = Selection.activeTransform;

            if (root == null)
            {
                Debug.LogWarning("Normalize selected transform skipped. Select a tile root in Hierarchy or Prefab Mode.");
                return;
            }

            if (!NormalizePrefabRoot(root, true, out Vector3 offset))
            {
                Debug.LogWarning($"Normalize selected transform skipped. Root: {root.name}, localPosition: {root.localPosition}");
                return;
            }

            EditorUtility.SetDirty(root.gameObject);

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            }

            Debug.Log($"Normalize selected transform pivot complete. Root: {root.name}, offset: {offset}, localPosition: {root.localPosition}");
        }

        [MenuItem("Tools/Map/Reset Map Tiles Prefab Root Transforms")]
        private static void ResetMapTilesPrefabRootTransforms()
        {
            string[] prefabPaths =
            {
                "Assets/Arts/Map/Tiles/Grass.prefab",
                "Assets/Arts/Map/Tiles/Hill.prefab",
                "Assets/Arts/Map/Tiles/Snow.prefab",
                "Assets/Arts/Map/Tiles/Water.prefab",
                "Assets/Arts/Map/Tiles/Road.prefab",
            };

            int changedCount = 0;

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string path = prefabPaths[i];

                if (!File.Exists(path))
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    Transform transform = root.transform;
                    bool changed = transform.localPosition != Vector3.zero ||
                                   transform.localRotation != Quaternion.identity;

                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedCount++;
                        Debug.Log($"Reset tile prefab root transform: {path}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Reset map tile prefab root transforms complete. Changed: {changedCount}");
        }

        [MenuItem("Tools/Map/Normalize Configured Tile Prefab Pivots")]
        private static void NormalizeConfiguredTilePrefabs()
        {
            MapTilePrefabConfig config = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(PrefabConfigPath);

            if (config == null || config.Items == null)
            {
                Debug.LogWarning($"MapTilePrefabConfig not found: {PrefabConfigPath}");
                return;
            }

            int changedCount = 0;
            HashSet<string> normalizedPaths = new HashSet<string>();

            for (int i = 0; i < config.Items.Count; i++)
            {
                MapTilePrefabConfig.TilePrefabItem item = config.Items[i];

                if (item == null || item.Prefab == null)
                {
                    continue;
                }

                if (item.Type == MapTileType.None || item.Type == MapTileType.Soil)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(item.Prefab);

                if (string.IsNullOrEmpty(path) || normalizedPaths.Contains(path))
                {
                    continue;
                }

                normalizedPaths.Add(path);

                if (NormalizePrefab(path))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Normalize configured tile prefab pivots complete. Changed: {changedCount}");
        }

        private static bool NormalizePrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                if (!NormalizePrefabRoot(root.transform, false, out Vector3 pivotOffset))
                {
                    Debug.LogWarning($"Normalize pivot skipped. No pivot change needed or no renderer found: {prefabPath}");
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Normalize pivot: {prefabPath}, offset: {pivotOffset}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool NormalizePrefabRoot(Transform root, bool recordUndo, out Vector3 pivotOffset)
        {
            pivotOffset = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            if (!TryCalculateLocalRendererBounds(root, renderers, out Bounds bounds))
            {
                return false;
            }

            pivotOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bool hasContentOffset = pivotOffset.sqrMagnitude >= 0.000001f;
            bool hasRootPosition = root.localPosition.sqrMagnitude >= 0.000001f;

            if (!hasContentOffset && !hasRootPosition)
            {
                return false;
            }

            if (recordUndo)
            {
                Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Normalize Tile Prefab Pivot");
            }

            if (hasContentOffset)
            {
                MoveRootContent(root, -pivotOffset);
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            return true;
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                EncapsulateWorldBounds(root, rendererBounds, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateWorldBounds(Transform root, Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            EncapsulatePoint(root, new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulatePoint(root, new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
        }

        private static void EncapsulatePoint(Transform root, Vector3 worldPoint, ref Bounds localBounds, ref bool hasBounds)
        {
            Vector3 localPoint = root.InverseTransformPoint(worldPoint);

            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(localPoint);
        }

        private static void MoveRootContent(Transform root, Vector3 localDelta)
        {
            if (root.childCount == 0)
            {
                Debug.LogWarning($"Prefab root has no children. Root renderer pivot was not changed: {root.name}");
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                child.localPosition += localDelta;
            }
        }
    }
}

#endif
