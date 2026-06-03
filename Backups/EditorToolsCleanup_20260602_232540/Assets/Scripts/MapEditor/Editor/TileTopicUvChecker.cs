#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopicUvChecker
    {
        private const string TopicName = "Topic";
        private const string TopicTopName = "TopicTop";

        private static readonly string[] DefaultTilePrefabPaths =
        {
            "Assets/Arts/Map/Tiles/Grass.prefab",
            "Assets/Arts/Map/Tiles/Hill.prefab",
            "Assets/Arts/Map/Tiles/Snow.prefab",
            "Assets/Arts/Map/Tiles/Water.prefab",
            "Assets/Arts/Map/Tiles/Topic.prefab"
        };

        [MenuItem("Tools/Map/Check Tile Topic UVs")]
        private static void CheckDefaultTileTopicUvs()
        {
            for (int i = 0; i < DefaultTilePrefabPaths.Length; i++)
            {
                CheckPrefab(DefaultTilePrefabPaths[i]);
            }
        }

        [MenuItem("Tools/Map/Check Selected Tile Topic UVs")]
        private static void CheckSelectedTileTopicUvs()
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
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                CheckPrefab(path);
            }
        }

        private static void CheckPrefab(string prefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"UV check skipped. Failed to load prefab: {prefabPath}");
                return;
            }

            try
            {
                Transform checkTarget = FindCheckTargetTransform(prefabRoot.transform);
                if (checkTarget == null)
                {
                    Debug.LogWarning($"UV check skipped. TopicTop/Topic not found: {prefabPath}");
                    return;
                }

                MeshFilter meshFilter = checkTarget.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = checkTarget.GetComponentInChildren<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"UV check skipped. {checkTarget.name} mesh not found: {prefabPath}");
                    return;
                }

                AnalyzeTopicMesh(prefabPath, checkTarget, meshFilter.sharedMesh);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Transform FindTopicTransform(Transform root)
        {
            return FindTransformByName(root, TopicName);
        }

        private static Transform FindCheckTargetTransform(Transform root)
        {
            Transform topicTop = FindTransformByName(root, TopicTopName);
            if (topicTop != null)
            {
                return topicTop;
            }

            return FindTopicTransform(root);
        }

        private static Transform FindTransformByName(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            Transform direct = root.Find(name);
            if (direct != null)
            {
                return direct;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static void AnalyzeTopicMesh(string prefabPath, Transform target, Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;

            if (vertices == null || vertices.Length == 0 || uvs == null || uvs.Length != vertices.Length || triangles == null)
            {
                Debug.LogWarning($"UV check failed. Mesh has missing vertices/uv/triangles: {prefabPath}, Mesh: {mesh.name}");
                return;
            }

            List<Vector3> topPositions = new List<Vector3>();
            List<Vector2> topUvs = new List<Vector2>();
            int topTriangleCount = 0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                Vector3 worldA = target.TransformPoint(vertices[a]);
                Vector3 worldB = target.TransformPoint(vertices[b]);
                Vector3 worldC = target.TransformPoint(vertices[c]);
                Vector3 faceNormal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;

                if (Vector3.Dot(faceNormal, Vector3.up) < 0.75f)
                {
                    continue;
                }

                topTriangleCount++;
                AddUniqueTopVertex(worldA, uvs[a], topPositions, topUvs);
                AddUniqueTopVertex(worldB, uvs[b], topPositions, topUvs);
                AddUniqueTopVertex(worldC, uvs[c], topPositions, topUvs);
            }

            if (topTriangleCount == 0 || topUvs.Count == 0)
            {
                Debug.LogWarning($"UV check failed. No upward top faces found: {prefabPath}, Mesh: {mesh.name}");
                return;
            }

            BuildReport(prefabPath, target.name, mesh.name, topTriangleCount, topPositions, topUvs);
        }

        private static void AddUniqueTopVertex(Vector3 position, Vector2 uv, List<Vector3> positions, List<Vector2> uvs)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i] - position).sqrMagnitude < 0.000001f && (uvs[i] - uv).sqrMagnitude < 0.000001f)
                {
                    return;
                }
            }

            positions.Add(position);
            uvs.Add(uv);
        }

        private static void BuildReport(string prefabPath, string targetName, string meshName, int topTriangleCount, List<Vector3> positions, List<Vector2> uvs)
        {
            Vector2 uvMin = uvs[0];
            Vector2 uvMax = uvs[0];
            Vector3 posMin = positions[0];
            Vector3 posMax = positions[0];

            for (int i = 1; i < uvs.Count; i++)
            {
                uvMin = Vector2.Min(uvMin, uvs[i]);
                uvMax = Vector2.Max(uvMax, uvs[i]);
                posMin = Vector3.Min(posMin, positions[i]);
                posMax = Vector3.Max(posMax, positions[i]);
            }

            Vector2 uvRange = uvMax - uvMin;
            bool uvLooksUsable = uvRange.x >= 0.8f && uvRange.y >= 0.8f;
            string xToUv = EstimateAxisMapping(positions, uvs, true);
            string zToUv = EstimateAxisMapping(positions, uvs, false);

            string status = uvLooksUsable ? "OK" : "CHECK";
            Debug.Log(
                $"[{status}] {targetName} UV check: {prefabPath}\n" +
                $"Mesh: {meshName}, TopTriangles: {topTriangleCount}, TopVertices: {uvs.Count}\n" +
                $"Top UV Min/Max: ({uvMin.x:F3}, {uvMin.y:F3}) -> ({uvMax.x:F3}, {uvMax.y:F3}), Range: ({uvRange.x:F3}, {uvRange.y:F3})\n" +
                $"World Top Bounds XZ: X {posMin.x:F3}->{posMax.x:F3}, Z {posMin.z:F3}->{posMax.z:F3}\n" +
                $"Axis Mapping Estimate: X -> {xToUv}, Z -> {zToUv}");
        }

        private static string EstimateAxisMapping(List<Vector3> positions, List<Vector2> uvs, bool useX)
        {
            float minAxis = useX ? positions[0].x : positions[0].z;
            float maxAxis = minAxis;

            for (int i = 1; i < positions.Count; i++)
            {
                float axis = useX ? positions[i].x : positions[i].z;
                if (axis < minAxis) minAxis = axis;
                if (axis > maxAxis) maxAxis = axis;
            }

            float epsilon = Mathf.Max(0.0001f, (maxAxis - minAxis) * 0.05f);
            Vector2 minAverage = AverageUvAtAxis(positions, uvs, useX, minAxis, epsilon);
            Vector2 maxAverage = AverageUvAtAxis(positions, uvs, useX, maxAxis, epsilon);
            Vector2 delta = maxAverage - minAverage;

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f ? "+U" : "-U";
            }

            return delta.y >= 0f ? "+V" : "-V";
        }

        private static Vector2 AverageUvAtAxis(List<Vector3> positions, List<Vector2> uvs, bool useX, float target, float epsilon)
        {
            Vector2 total = Vector2.zero;
            int count = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                float axis = useX ? positions[i].x : positions[i].z;
                if (Mathf.Abs(axis - target) > epsilon)
                {
                    continue;
                }

                total += uvs[i];
                count++;
            }

            return count > 0 ? total / count : Vector2.zero;
        }
    }
}

#endif
