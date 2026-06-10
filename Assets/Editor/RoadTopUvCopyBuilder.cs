using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RoadTopUvCopyBuilder
    {
        private const string TopPrefabPath = "Assets/Arts/Map/Tiles/Top.prefab";
        private const string TopicPrefabPath = "Assets/Arts/Map/Tiles/Topic.prefab";
        private const string RoadPrefabPath = "Assets/Arts/Map/Tiles/Road.prefab";
        private const string RoadTopMaterialPath = "Assets/Arts/Map/Tiles/RoadTop/RoadTop_SoftTile.mat";
        private const string MeshAssetPath = "Assets/Arts/Map/Tiles/RoadTop/RoadTop_Top_UVRect.asset";
        private const string TopCopyPrefabPath = "Assets/Arts/Map/Tiles/RoadTop/RoadTop_Top_UVRect.prefab";
        private const string TopicCopyPrefabPath = "Assets/Arts/Map/Tiles/RoadTop/RoadTopic_Top_UVRect.prefab";
        private const string RunMarkerPath = "Temp/RoadTopUvCopyBuilder.run";

        [InitializeOnLoadMethod]
        private static void RunWhenRequested()
        {
            var markerPath = Path.Combine(Directory.GetCurrentDirectory(), RunMarkerPath);
            if (!File.Exists(markerPath))
            {
                return;
            }

            File.Delete(markerPath);
            try
            {
                CreateUvCopy();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Map/Road Top/Create UV Copy")]
        public static void CreateUvCopy()
        {
            var topPrefab = LoadRequired<GameObject>(TopPrefabPath);
            var topicPrefab = LoadRequired<GameObject>(TopicPrefabPath);
            var roadMaterial = LoadRequired<Material>(RoadTopMaterialPath);

            var sourceMesh = GetSourceMesh(topPrefab);
            var uvMesh = CreateOrUpdateUvMesh(sourceMesh);

            CreateTopCopy(topPrefab, uvMesh, roadMaterial);
            CreateTopicCopy(topicPrefab, uvMesh, roadMaterial);
            ReplaceRoadTop();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Road top UV copy created and assigned to Road.prefab.");
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException($"Missing asset: {path}");
            }

            return asset;
        }

        private static Mesh GetSourceMesh(GameObject topPrefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(topPrefab);
            try
            {
                var meshFilter = instance.GetComponentInChildren<MeshFilter>(true);
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    throw new System.InvalidOperationException("Top prefab has no MeshFilter with a mesh.");
                }

                return meshFilter.sharedMesh;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Mesh CreateOrUpdateUvMesh(Mesh sourceMesh)
        {
            var meshCopy = Object.Instantiate(sourceMesh);
            meshCopy.name = "RoadTop_Top_UVRect";

            var bounds = meshCopy.bounds;
            var min = bounds.min;
            var size = bounds.size;
            var invX = size.x > 0.0001f ? 1.0f / size.x : 1.0f;
            var invY = size.y > 0.0001f ? 1.0f / size.y : 1.0f;

            var rectUv = new List<Vector2>(meshCopy.vertexCount);
            var vertices = meshCopy.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                rectUv.Add(new Vector2((vertex.x - min.x) * invX, (vertex.y - min.y) * invY));
            }

            meshCopy.SetUVs(1, rectUv);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(meshCopy, existing);
                existing.name = "RoadTop_Top_UVRect";
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(meshCopy);
                return existing;
            }

            AssetDatabase.CreateAsset(meshCopy, MeshAssetPath);
            return meshCopy;
        }

        private static void CreateTopCopy(GameObject topPrefab, Mesh uvMesh, Material roadMaterial)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(topPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "RoadTop_Top_UVRect";
                ApplyMeshAndMaterial(instance, uvMesh, roadMaterial);
                PrefabUtility.SaveAsPrefabAsset(instance, TopCopyPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void CreateTopicCopy(GameObject topicPrefab, Mesh uvMesh, Material roadMaterial)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(topicPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "RoadTopic_Top_UVRect";
                ApplyMeshAndMaterial(instance, uvMesh, roadMaterial);
                PrefabUtility.SaveAsPrefabAsset(instance, TopicCopyPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ApplyMeshAndMaterial(GameObject root, Mesh uvMesh, Material roadMaterial)
        {
            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                meshFilter.sharedMesh = uvMesh;
                EditorUtility.SetDirty(meshFilter);
            }

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = roadMaterial;
                }

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void ReplaceRoadTop()
        {
            var topicCopy = LoadRequired<GameObject>(TopicCopyPrefabPath);
            var roadRoot = PrefabUtility.LoadPrefabContents(RoadPrefabPath);
            try
            {
                var oldTop = roadRoot.transform.Find("Top") ?? roadRoot.transform.Find("Topic");
                if (oldTop == null)
                {
                    throw new System.InvalidOperationException("Road.prefab has no direct Top/Topic child.");
                }

                var localPosition = oldTop.localPosition;
                var localRotation = oldTop.localRotation;
                var localScale = oldTop.localScale;
                var siblingIndex = oldTop.GetSiblingIndex();

                Object.DestroyImmediate(oldTop.gameObject);

                var newTop = (GameObject)PrefabUtility.InstantiatePrefab(topicCopy, roadRoot.transform);
                newTop.name = "Top";
                newTop.transform.SetSiblingIndex(siblingIndex);
                newTop.transform.localPosition = localPosition;
                newTop.transform.localRotation = localRotation;
                newTop.transform.localScale = localScale;

                PrefabUtility.SaveAsPrefabAsset(roadRoot, RoadPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(roadRoot);
            }
        }
    }
}
