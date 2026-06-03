#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopicTopPlaneGenerator
    {
        private const string TopicName = "Topic";
        private const string TopicTopName = "TopicTop";
        private const float PlaneOffset = 0.003f;
        private const float PlaneInset = 0.02f;
        private const string GeneratedMeshDirectory = "Assets/Arts/Map/Tiles/Generated";
        private const string GeneratedMeshPath = GeneratedMeshDirectory + "/TopicTopPlane.asset";

        private static readonly string[] DefaultTilePrefabPaths =
        {
            "Assets/Arts/Map/Tiles/Grass.prefab",
            "Assets/Arts/Map/Tiles/Hill.prefab",
            "Assets/Arts/Map/Tiles/Snow.prefab",
            "Assets/Arts/Map/Tiles/Water.prefab"
        };

        [MenuItem("Tools/Map/Create TopicTop Planes For Default Tiles")]
        private static void CreateTopicTopPlanesForDefaultTiles()
        {
            for (int i = 0; i < DefaultTilePrefabPaths.Length; i++)
            {
                CreateOrUpdateTopicTopPlane(DefaultTilePrefabPaths[i]);
            }
        }

        [MenuItem("Tools/Map/Create TopicTop Planes For Selected Tiles")]
        private static void CreateTopicTopPlanesForSelectedTiles()
        {
            Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("No selected prefab assets.");
                return;
            }

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selectedObjects[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    continue;
                }

                CreateOrUpdateTopicTopPlane(path);
            }
        }

        private static void CreateOrUpdateTopicTopPlane(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"TopicTop skipped. Failed to load prefab: {prefabPath}");
                return;
            }

            try
            {
                Transform topic = FindTopic(root.transform);
                if (topic == null)
                {
                    Debug.LogWarning($"TopicTop skipped. Topic not found: {prefabPath}");
                    return;
                }

                if (!TryGetRendererBounds(topic, out Bounds topicWorldBounds))
                {
                    Debug.LogWarning($"TopicTop skipped. Topic renderer bounds not found: {prefabPath}");
                    return;
                }

                Transform existing = topic.Find(TopicTopName);
                GameObject plane = existing != null ? existing.gameObject : new GameObject(TopicTopName);
                plane.transform.SetParent(topic, false);

                MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = plane.AddComponent<MeshFilter>();

                MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = plane.AddComponent<MeshRenderer>();

                meshFilter.sharedMesh = GetOrCreateSharedPlaneMesh();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;
                ApplyPlaneTransform(plane.transform, topic, topicWorldBounds);

                Renderer topicRenderer = topic.GetComponentInChildren<Renderer>();
                if (topicRenderer != null)
                {
                    meshRenderer.sharedMaterials = topicRenderer.sharedMaterials;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"TopicTop created/updated: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindTopic(Transform root)
        {
            if (root.name == TopicName)
            {
                return root;
            }

            Transform direct = root.Find(TopicName);
            if (direct != null)
            {
                return direct;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == TopicName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.transform.name == TopicTopName)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Mesh GetOrCreateSharedPlaneMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedMeshPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(GeneratedMeshDirectory);

            Mesh mesh = new Mesh();
            mesh.name = "TopicTopPlane";
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            };

            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };

            mesh.triangles = new[]
            {
                0, 1, 2,
                0, 2, 3
            };

            mesh.normals = new[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };

            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };

            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, GeneratedMeshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedMeshPath);
        }

        private static void ApplyPlaneTransform(Transform plane, Transform topic, Bounds topicWorldBounds)
        {
            float y = topicWorldBounds.max.y + PlaneOffset;
            Vector3 worldMin = topicWorldBounds.min;
            Vector3 worldMax = topicWorldBounds.max;

            float width = Mathf.Max(0.01f, topicWorldBounds.size.x - PlaneInset * 2f);
            float depth = Mathf.Max(0.01f, topicWorldBounds.size.z - PlaneInset * 2f);
            Vector3 worldCenter = new Vector3(topicWorldBounds.center.x, y, topicWorldBounds.center.z);

            plane.localPosition = topic.InverseTransformPoint(worldCenter);
            plane.localRotation = Quaternion.identity;
            plane.localScale = new Vector3(
                width / Mathf.Max(0.0001f, topic.lossyScale.x),
                1f,
                depth / Mathf.Max(0.0001f, topic.lossyScale.z));
        }
    }
}

#endif
