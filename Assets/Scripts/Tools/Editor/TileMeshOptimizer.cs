#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileMeshOptimizer
    {
        private const string SourcePrefabPath = "Assets/Arts/Map/Tiles/Top.prefab";
        private const string OutputMeshPath = "Assets/Arts/Map/Tiles/Meshes/Top_NoBottom.asset";
        private const string OutputPrefabPath = "Assets/Arts/Map/Tiles/Top_NoBottom.prefab";
        private const string BlenderNoBottomFbxPath = "Assets/Arts/Map/Tiles/Meshes/Top_NoBottom_Blender.fbx";
        private const string BlenderNoBottomPrefabPath = "Assets/Arts/Map/Tiles/Top_NoBottom_Blender.prefab";
        private const string LowBevelFbxPath = "Assets/Arts/Map/Tiles/Meshes/Top_LowBevel.fbx";
        private const string LowBevelPrefabPath = "Assets/Arts/Map/Tiles/Top_LowBevel.prefab";

        [MenuItem("Debug/Map/Build Top No Bottom Tile")]
        public static void BuildTopNoBottomTile()
        {
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            if (sourceRoot == null)
            {
                Debug.LogError($"[TileMeshOptimizer] Missing source prefab: {SourcePrefabPath}");
                return;
            }

            try
            {
                MeshFilter sourceFilter = sourceRoot.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer sourceRenderer = sourceRoot.GetComponentInChildren<MeshRenderer>(true);
                if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
                {
                    Debug.LogError($"[TileMeshOptimizer] Source prefab has no MeshFilter/MeshRenderer: {SourcePrefabPath}");
                    return;
                }

                Mesh optimizedMesh = BuildMeshWithoutBottom(sourceRoot.transform, sourceFilter);
                if (optimizedMesh == null)
                {
                    return;
                }

                SaveMeshAsset(optimizedMesh);
                SavePrefab(optimizedMesh, sourceRenderer.sharedMaterials);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        [MenuItem("Debug/Map/Create Top No Bottom Blender Prefab")]
        public static void CreateTopNoBottomBlenderPrefab()
        {
            CreateFbxBasedTilePrefab(BlenderNoBottomFbxPath, BlenderNoBottomPrefabPath, "Top_NoBottom_Blender");
        }

        [MenuItem("Debug/Map/Create Top Low Bevel Prefab")]
        public static void CreateTopLowBevelPrefab()
        {
            CreateFbxBasedTilePrefab(LowBevelFbxPath, LowBevelPrefabPath, "Top_LowBevel");
        }

        private static void CreateFbxBasedTilePrefab(string fbxPath, string prefabPath, string prefabName)
        {
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            if (sourceRoot == null)
            {
                Debug.LogError($"[TileMeshOptimizer] Missing source prefab: {SourcePrefabPath}");
                return;
            }

            try
            {
                MeshFilter sourceFilter = sourceRoot.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer sourceRenderer = sourceRoot.GetComponentInChildren<MeshRenderer>(true);
                GameObject optimizedFbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (sourceFilter == null || sourceRenderer == null || optimizedFbx == null)
                {
                    Debug.LogError($"[TileMeshOptimizer] Missing source renderer or optimized fbx: {fbxPath}");
                    return;
                }

                GameObject root = new GameObject(prefabName);
                GameObject optimizedInstance = PrefabUtility.InstantiatePrefab(optimizedFbx) as GameObject;
                if (optimizedInstance == null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    Debug.LogError($"[TileMeshOptimizer] Instantiate optimized fbx failed: {fbxPath}");
                    return;
                }

                optimizedInstance.name = "Mesh";
                optimizedInstance.transform.SetParent(root.transform, false);
                optimizedInstance.transform.localPosition = sourceFilter.transform.localPosition;
                optimizedInstance.transform.localRotation = sourceFilter.transform.localRotation;
                optimizedInstance.transform.localScale = sourceFilter.transform.localScale;

                MeshRenderer optimizedRenderer = optimizedInstance.GetComponentInChildren<MeshRenderer>(true);
                if (optimizedRenderer != null)
                {
                    optimizedRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log($"[TileMeshOptimizer] Saved prefab: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static Mesh BuildMeshWithoutBottom(Transform root, MeshFilter sourceFilter)
        {
            Mesh sourceMesh = sourceFilter.sharedMesh;
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector2[] sourceUv = sourceMesh.uv;
            Color[] sourceColors = sourceMesh.colors;

            Matrix4x4 sourceToRoot = root.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<int[]> submeshTriangles = new List<int[]>();

            int sourceTriangleCount = 0;
            int removedTriangleCount = 0;
            int keptTriangleCount = 0;

            for (int submesh = 0; submesh < sourceMesh.subMeshCount; submesh++)
            {
                int[] sourceTriangles = sourceMesh.GetTriangles(submesh);
                List<int> keptTriangles = new List<int>(sourceTriangles.Length);
                sourceTriangleCount += sourceTriangles.Length / 3;

                for (int i = 0; i < sourceTriangles.Length; i += 3)
                {
                    int i0 = sourceTriangles[i];
                    int i1 = sourceTriangles[i + 1];
                    int i2 = sourceTriangles[i + 2];

                    Vector3 v0 = sourceToRoot.MultiplyPoint3x4(sourceVertices[i0]);
                    Vector3 v1 = sourceToRoot.MultiplyPoint3x4(sourceVertices[i1]);
                    Vector3 v2 = sourceToRoot.MultiplyPoint3x4(sourceVertices[i2]);
                    Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                    if (Vector3.Dot(normal, Vector3.down) > 0.85f)
                    {
                        removedTriangleCount++;
                        continue;
                    }

                    keptTriangles.Add(GetOrAddVertex(i0, v0, sourceUv, sourceColors, remap, vertices, uvs, colors));
                    keptTriangles.Add(GetOrAddVertex(i1, v1, sourceUv, sourceColors, remap, vertices, uvs, colors));
                    keptTriangles.Add(GetOrAddVertex(i2, v2, sourceUv, sourceColors, remap, vertices, uvs, colors));
                    keptTriangleCount++;
                }

                submeshTriangles.Add(keptTriangles.ToArray());
            }

            Mesh mesh = new Mesh
            {
                name = "Top_NoBottom",
                indexFormat = vertices.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            if (colors.Count == vertices.Count)
            {
                mesh.SetColors(colors);
            }

            mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++)
            {
                mesh.SetTriangles(submeshTriangles[i], i);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Debug.Log(
                $"[TileMeshOptimizer] Built Top_NoBottom. " +
                $"triangles {sourceTriangleCount} -> {keptTriangleCount}, " +
                $"removed bottom triangles {removedTriangleCount}, " +
                $"vertices {sourceMesh.vertexCount} -> {vertices.Count}.");

            return mesh;
        }

        private static int GetOrAddVertex(
            int sourceIndex,
            Vector3 bakedPosition,
            Vector2[] sourceUv,
            Color[] sourceColors,
            Dictionary<int, int> remap,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color> colors)
        {
            if (remap.TryGetValue(sourceIndex, out int existingIndex))
            {
                return existingIndex;
            }

            int newIndex = vertices.Count;
            remap.Add(sourceIndex, newIndex);
            vertices.Add(bakedPosition);
            if (sourceUv != null && sourceUv.Length > sourceIndex)
            {
                uvs.Add(sourceUv[sourceIndex]);
            }

            if (sourceColors != null && sourceColors.Length > sourceIndex)
            {
                colors.Add(sourceColors[sourceIndex]);
            }

            return newIndex;
        }

        private static void SaveMeshAsset(Mesh mesh)
        {
            AssetDatabase.DeleteAsset(OutputMeshPath);
            AssetDatabase.CreateAsset(mesh, OutputMeshPath);
        }

        private static void SavePrefab(Mesh mesh, Material[] materials)
        {
            GameObject root = new GameObject("Top_NoBottom");
            MeshFilter meshFilter = root.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterials = materials;

            PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[TileMeshOptimizer] Saved prefab: {OutputPrefabPath}");
        }
    }
}
#endif
