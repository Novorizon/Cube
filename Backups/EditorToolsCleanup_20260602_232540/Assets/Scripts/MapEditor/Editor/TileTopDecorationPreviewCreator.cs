#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopDecorationPreviewCreator
    {
        private const string PreviewRootName = "TileTopDecorationPreview";
        private const string TestTilePrefabPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab";

        private const string GrassMaterialPath = "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat";
        private const string SnowMaterialPath = "Assets/Arts/Map/Tiles/Materials/SnowTop_Stylized.mat";
        private const string RoadMaterialPath = "Assets/Arts/Map/Tiles/Materials/RoadTop_Stylized.mat";

        private const string GrassClumpPath = "Assets/Arts/Map/Tiles/Generated/Decorations/GrassClump_A.prefab";
        private const string SmallFlowerPath = "Assets/Arts/Map/Tiles/Generated/Decorations/SmallFlower_A.prefab";
        private const string PebblePath = "Assets/Arts/Map/Tiles/Generated/Decorations/Pebble_A.prefab";
        private const string StonePath = "Assets/Arts/Map/Decoration/Stone1/Stone1.prefab";
        private const string SnowPath = "Assets/Arts/Map/Decoration/Snow1/Meshy_AI_Snowbound_Summit_0519051541_texture.fbx";

        [MenuItem("Tools/Map Art/Tile Top/Create Decoration Preview Grid")]
        public static void CreatePreviewGrid()
        {
            TileTopMaterialCreator.CreateAll();

            GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestTilePrefabPath);
            if (tilePrefab == null)
            {
                Debug.LogError($"Missing test tile prefab: {TestTilePrefabPath}");
                return;
            }

            Material grassMaterial = LoadMaterial(GrassMaterialPath);
            Material snowMaterial = LoadMaterial(SnowMaterialPath);
            Material roadMaterial = LoadMaterial(RoadMaterialPath);
            if (grassMaterial == null || snowMaterial == null || roadMaterial == null)
            {
                return;
            }

            GameObject oldRoot = GameObject.Find(PreviewRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(PreviewRootName);
            CreateRow(root.transform, tilePrefab, grassMaterial, "GrassDecor", 0, AddGrassDecorations);
            CreateRow(root.transform, tilePrefab, snowMaterial, "SnowDecor", 1, AddSnowDecorations);
            CreateRow(root.transform, tilePrefab, roadMaterial, "RoadDecor", 2, AddRoadDecorations);

            Selection.activeGameObject = root;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"Tile top decoration preview grid created: {PreviewRootName}");
        }

        private static void CreateRow(
            Transform root,
            GameObject tilePrefab,
            Material material,
            string namePrefix,
            int row,
            System.Action<GameObject, int> addDecorations)
        {
            const int columns = 4;
            for (int column = 0; column < columns; column++)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(tilePrefab) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(tilePrefab);
                }

                instance.name = $"{namePrefix}_{column + 1}";
                instance.transform.SetParent(root, false);
                instance.transform.position = new Vector3(column * 1.08f, 0f, -row * 1.08f);

                RemoveAutoDecoration(instance);
                AssignTopMaterial(instance, material);
                addDecorations(instance, column);
            }
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogError($"Missing material: {path}");
            }

            return material;
        }

        private static void RemoveAutoDecoration(GameObject instance)
        {
            TileAutoDecoration autoDecoration = instance.GetComponent<TileAutoDecoration>();
            if (autoDecoration != null)
            {
                Object.DestroyImmediate(autoDecoration);
            }

            Transform generatedRoot = instance.transform.Find("__AutoDecorations");
            if (generatedRoot != null)
            {
                Object.DestroyImmediate(generatedRoot.gameObject);
            }
        }

        private static void AssignTopMaterial(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.name == "TopBody")
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void AddGrassDecorations(GameObject tile, int variant)
        {
            GameObject clump = AssetDatabase.LoadAssetAtPath<GameObject>(GrassClumpPath);
            GameObject flower = AssetDatabase.LoadAssetAtPath<GameObject>(SmallFlowerPath);
            GameObject pebble = AssetDatabase.LoadAssetAtPath<GameObject>(PebblePath);

            if (variant % 2 == 0)
            {
                AddDecoration(tile, clump, new Vector3(-0.22f, 1.055f, 0.18f), 0.85f, 28f);
                AddDecoration(tile, flower, new Vector3(0.22f, 1.060f, -0.18f), 0.85f, 0f);
            }
            else
            {
                AddDecoration(tile, flower, new Vector3(-0.18f, 1.060f, -0.20f), 0.78f, 0f);
                AddDecoration(tile, pebble, new Vector3(0.24f, 1.052f, 0.16f), 0.65f, 35f);
            }
        }

        private static void AddSnowDecorations(GameObject tile, int variant)
        {
            GameObject snow = AssetDatabase.LoadAssetAtPath<GameObject>(SnowPath);
            GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>(GrassClumpPath);

            if (variant % 2 == 0)
            {
                AddDecoration(tile, snow, new Vector3(-0.18f, 1.050f, 0.10f), 0.020f, 15f);
            }
            else
            {
                AddDecoration(tile, grass, new Vector3(0.14f, 1.052f, -0.18f), 0.52f, 10f);
            }
        }

        private static void AddRoadDecorations(GameObject tile, int variant)
        {
            GameObject stone = AssetDatabase.LoadAssetAtPath<GameObject>(StonePath);
            GameObject pebble = AssetDatabase.LoadAssetAtPath<GameObject>(PebblePath);
            GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>(GrassClumpPath);

            if (variant % 2 == 0)
            {
                AddDecoration(tile, pebble, new Vector3(-0.22f, 1.052f, 0.20f), 0.60f, 50f);
                AddDecoration(tile, grass, new Vector3(0.23f, 1.052f, -0.18f), 0.48f, 30f);
            }
            else
            {
                AddDecoration(tile, stone, new Vector3(0.12f, 1.052f, 0.12f), 0.035f, 20f);
            }
        }

        private static void AddDecoration(GameObject tile, GameObject prefab, Vector3 localPosition, float scale, float yaw)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, tile.transform) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, tile.transform);
            }

            instance.name = prefab.name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
        }
    }
}

#endif
