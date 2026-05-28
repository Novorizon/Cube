#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ReferenceMapPrefabGenerator
    {
        private const float TileSize = 1f;
        private const string OutputDirectory = "Assets/Arts/Map/Prefabs";
        private const string OutputPath = OutputDirectory + "/ReferenceMap_01.prefab";

        private static readonly Dictionary<char, string> TilePrefabPaths = new Dictionary<char, string>
        {
            { 'G', "Assets/Arts/Map/Tiles/Grass.prefab" },
            { 'H', "Assets/Arts/Map/Tiles/Hill.prefab" },
            { 'S', "Assets/Arts/Map/Tiles/Snow.prefab" },
            { 'W', "Assets/Arts/Map/Tiles/Water.prefab" },
            { 'R', "Assets/Arts/Map/Tiles/Road.prefab" },
        };

        // Top row first. The shape is a visual blockout inspired by the reference image:
        // grass field, winding road, and two water areas. Decorations are intentionally omitted.
        private static readonly string[] Layout =
        {
            "GGGGGGGGGGGGGGGGGG",
            "GGGGGGGGRRRRRRGGGG",
            "GGGGGGGGRGGGGGGGGG",
            "GWWWGGGGRGGGGGRRRR",
            "GWWWWGGGRRRRRGGGRG",
            "GWWWGGGGGGGGRGGGRG",
            "GGGGGRRRRRRRRGGGRG",
            "GGGGGRGGWWWGGGGGRG",
            "GGRRRRGGWWWGGRRRRG",
            "GGRGGGGGWWGGGRGGGG",
            "GGRGGGGGGGGGGGGGGG",
            "GGGGGGGGGGGGGGGGGG",
        };

        [MenuItem("Tools/Map/Generate Reference Map Prefab")]
        public static void Generate()
        {
            GameObject root = new GameObject("ReferenceMap_01");
            Dictionary<char, Transform> groups = CreateGroups(root.transform);

            try
            {
                Dictionary<char, GameObject> prefabCache = LoadPrefabs();
                int depth = Layout.Length;
                int width = Layout[0].Length;

                for (int row = 0; row < depth; row++)
                {
                    string line = Layout[row];
                    int z = depth - 1 - row;

                    for (int x = 0; x < width; x++)
                    {
                        char code = line[x];

                        if (!prefabCache.TryGetValue(code, out GameObject prefab) || prefab == null)
                        {
                            Debug.LogWarning($"Skip missing tile code '{code}' at x:{x}, z:{z}.");
                            continue;
                        }

                        Transform parent = groups.TryGetValue(code, out Transform group) ? group : root.transform;
                        GameObject tile = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

                        if (tile == null)
                        {
                            tile = Object.Instantiate(prefab, parent);
                        }

                        tile.name = $"{GetTileName(code)}_{x:00}_{z:00}";
                        tile.transform.localPosition = GetCenteredPosition(x, z, width, depth);
                        tile.transform.localRotation = Quaternion.identity;
                        tile.transform.localScale = Vector3.one;
                    }
                }

                EnsureOutputDirectory();
                PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
                AssetDatabase.Refresh();

                Debug.Log($"Generate reference map prefab success: {OutputPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<char, GameObject> LoadPrefabs()
        {
            Dictionary<char, GameObject> prefabs = new Dictionary<char, GameObject>();

            foreach (KeyValuePair<char, string> pair in TilePrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);

                if (prefab == null)
                {
                    Debug.LogWarning($"Missing tile prefab. Code: {pair.Key}, Path: {pair.Value}");
                }

                prefabs[pair.Key] = prefab;
            }

            return prefabs;
        }

        private static Dictionary<char, Transform> CreateGroups(Transform root)
        {
            Dictionary<char, Transform> groups = new Dictionary<char, Transform>();

            foreach (char code in TilePrefabPaths.Keys)
            {
                GameObject group = new GameObject(GetTileName(code) + "Tiles");
                group.transform.SetParent(root, false);
                groups[code] = group.transform;
            }

            return groups;
        }

        private static Vector3 GetCenteredPosition(int x, int z, int width, int depth)
        {
            float originX = (width - 1) * 0.5f;
            float originZ = (depth - 1) * 0.5f;
            return new Vector3((x - originX) * TileSize, 0f, (z - originZ) * TileSize);
        }

        private static string GetTileName(char code)
        {
            switch (code)
            {
                case 'G':
                    return "Grass";
                case 'H':
                    return "Hill";
                case 'S':
                    return "Snow";
                case 'W':
                    return "Water";
                case 'R':
                    return "Road";
                default:
                    return "Unknown";
            }
        }

        private static void EnsureOutputDirectory()
        {
            if (AssetDatabase.IsValidFolder(OutputDirectory))
            {
                return;
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
            Directory.CreateDirectory(fullPath);
        }
    }
}

#endif
