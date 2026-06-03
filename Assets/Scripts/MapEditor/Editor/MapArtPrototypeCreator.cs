#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class MapArtPrototypeCreator
    {
        private const string TileModelPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.fbx";
        private const string TilePrefabPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab";
        private const string DecorationRoot = "Assets/Arts/Map/Tiles/Generated/Decorations";
        private const string GrassClumpModelPath = DecorationRoot + "/GrassClump_A.fbx";
        private const string FlowerModelPath = DecorationRoot + "/SmallFlower_A.fbx";
        private const string PebbleModelPath = DecorationRoot + "/Pebble_A.fbx";
        private const string GrassClumpPrefabPath = DecorationRoot + "/GrassClump_A.prefab";
        private const string FlowerPrefabPath = DecorationRoot + "/SmallFlower_A.prefab";
        private const string PebblePrefabPath = DecorationRoot + "/Pebble_A.prefab";
        private const string GrassTopMaterialPath = "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat";
        private const string TilePrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string OriginalGrassPrefabEditorPrefsKey = "Cube.MapArtPrototype.OriginalGrassPrefabPath";
        private const string PreviewRootName = "ReferenceGrassTilePreviewGrid";

        [MenuItem("Tools/Map Art/Reference Grass/Full Setup And Preview")]
        public static void FullSetupAndPreview()
        {
            CreatePrototypeAssets();
            UsePrototypeAsGrass();
            CreatePreviewGrid();
        }

        [MenuItem("Tools/Map Art/Reference Grass/Create Prototype Assets")]
        public static void CreatePrototypeAssets()
        {
            TileTopMaterialCreator.CreateGrass();

            AssetDatabase.ImportAsset(TileModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(GrassClumpModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FlowerModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PebbleModelPath, ImportAssetOptions.ForceUpdate);

            GameObject grassClumpPrefab = CreatePrefabFromModel(GrassClumpModelPath, GrassClumpPrefabPath);
            GameObject flowerPrefab = CreatePrefabFromModel(FlowerModelPath, FlowerPrefabPath);
            GameObject pebblePrefab = CreatePrefabFromModel(PebbleModelPath, PebblePrefabPath);
            CreateGrassTilePrefab(grassClumpPrefab, flowerPrefab, pebblePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Reference grass prototype assets created: {TilePrefabPath}");
        }

        [MenuItem("Tools/Map Art/Reference Grass/Use Prototype As Grass")]
        public static void UsePrototypeAsGrass()
        {
            MapTilePrefabConfig config = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(TilePrefabConfigPath);
            GameObject prototype = AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath);

            if (config == null)
            {
                Debug.LogError($"Missing tile prefab config: {TilePrefabConfigPath}");
                return;
            }

            if (prototype == null)
            {
                Debug.LogError($"Missing prototype prefab. Run Create Prototype Assets first: {TilePrefabPath}");
                return;
            }

            MapTilePrefabConfig.TilePrefabItem item = GetOrCreateItem(config, MapTileType.Grass);
            string currentPath = item.Prefab != null ? AssetDatabase.GetAssetPath(item.Prefab) : string.Empty;
            if (!string.IsNullOrEmpty(currentPath) && currentPath != TilePrefabPath)
            {
                EditorPrefs.SetString(OriginalGrassPrefabEditorPrefsKey, currentPath);
            }

            Undo.RecordObject(config, "Use Reference Grass Prototype");
            item.Prefab = prototype;
            config.RebuildCache();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"MapTilePrefabConfig Grass now uses prototype: {TilePrefabPath}");
        }

        [MenuItem("Tools/Map Art/Reference Grass/Restore Original Grass")]
        public static void RestoreOriginalGrass()
        {
            string originalPath = EditorPrefs.GetString(OriginalGrassPrefabEditorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(originalPath))
            {
                Debug.LogWarning("No original Grass prefab path was stored.");
                return;
            }

            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
            MapTilePrefabConfig config = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(TilePrefabConfigPath);

            if (originalPrefab == null)
            {
                Debug.LogError($"Stored original Grass prefab is missing: {originalPath}");
                return;
            }

            if (config == null)
            {
                Debug.LogError($"Missing tile prefab config: {TilePrefabConfigPath}");
                return;
            }

            Undo.RecordObject(config, "Restore Original Grass Prefab");
            GetOrCreateItem(config, MapTileType.Grass).Prefab = originalPrefab;
            config.RebuildCache();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"MapTilePrefabConfig Grass restored: {originalPath}");
        }

        [MenuItem("Tools/Map Art/Reference Grass/Create 6x6 Preview Grid")]
        public static void CreatePreviewGrid()
        {
            GameObject prototype = AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath);
            if (prototype == null)
            {
                Debug.LogError($"Missing prototype prefab. Run Create Prototype Assets first: {TilePrefabPath}");
                return;
            }

            GameObject oldRoot = GameObject.Find(PreviewRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(PreviewRootName);
            const int size = 6;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(prototype) as GameObject;
                    if (instance == null)
                    {
                        instance = Object.Instantiate(prototype);
                    }

                    instance.name = $"ReferenceGrass_{x}_{z}";
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(x, 0f, z);

                    TileAutoDecoration autoDecoration = instance.GetComponent<TileAutoDecoration>();
                    if (autoDecoration != null)
                    {
                        autoDecoration.Refresh();
                    }
                }
            }

            Selection.activeGameObject = root;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("Reference grass preview grid created.");
        }

        private static GameObject CreatePrefabFromModel(string modelPath, string prefabPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"Missing model: {modelPath}");
                return null;
            }

            EnsureDirectory(Path.GetDirectoryName(prefabPath));
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(model);
            }

            instance.name = Path.GetFileNameWithoutExtension(prefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void CreateGrassTilePrefab(GameObject grassClumpPrefab, GameObject flowerPrefab, GameObject pebblePrefab)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TileModelPath);
            if (model == null)
            {
                Debug.LogError($"Missing tile model: {TileModelPath}");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(model);
            }

            instance.name = "ReferenceStyleGrassTile_Test";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Material grassTopMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassTopMaterialPath);
            if (grassTopMaterial != null)
            {
                AssignNamedRendererMaterial(instance, "TopBody", grassTopMaterial);
            }

            TileAutoDecoration autoDecoration = instance.GetComponent<TileAutoDecoration>();
            if (autoDecoration == null)
            {
                autoDecoration = instance.AddComponent<TileAutoDecoration>();
            }

            ConfigureAutoDecoration(autoDecoration, grassClumpPrefab, flowerPrefab, pebblePrefab);

            TileBoundsGizmo boundsGizmo = instance.GetComponent<TileBoundsGizmo>();
            if (boundsGizmo == null)
            {
                boundsGizmo = instance.AddComponent<TileBoundsGizmo>();
            }

            EnsureDirectory(Path.GetDirectoryName(TilePrefabPath));
            PrefabUtility.SaveAsPrefabAsset(instance, TilePrefabPath);
            Object.DestroyImmediate(instance);
        }

        private static void AssignNamedRendererMaterial(GameObject root, string rendererName, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.name == rendererName)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void ConfigureAutoDecoration(
            TileAutoDecoration autoDecoration,
            GameObject grassClumpPrefab,
            GameObject flowerPrefab,
            GameObject pebblePrefab)
        {
            SerializedObject serializedObject = new SerializedObject(autoDecoration);
            serializedObject.FindProperty("generateInEditMode").boolValue = true;
            serializedObject.FindProperty("maxCount").intValue = 4;
            serializedObject.FindProperty("spawnChance").floatValue = 0.62f;
            serializedObject.FindProperty("topY").floatValue = 1.045f;
            serializedObject.FindProperty("xRange").vector2Value = new Vector2(-0.34f, 0.34f);
            serializedObject.FindProperty("zRange").vector2Value = new Vector2(-0.34f, 0.34f);

            SerializedProperty options = serializedObject.FindProperty("options");
            options.arraySize = 3;
            ConfigureDecorationOption(options.GetArrayElementAtIndex(0), grassClumpPrefab, 5, new Vector2(0.75f, 1.15f));
            ConfigureDecorationOption(options.GetArrayElementAtIndex(1), flowerPrefab, 2, new Vector2(0.75f, 1.1f));
            ConfigureDecorationOption(options.GetArrayElementAtIndex(2), pebblePrefab, 1, new Vector2(0.75f, 1.05f));
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDecorationOption(SerializedProperty property, GameObject prefab, int weight, Vector2 scaleRange)
        {
            property.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            property.FindPropertyRelative("Weight").intValue = weight;
            property.FindPropertyRelative("ScaleRange").vector2Value = scaleRange;
        }

        private static MapTilePrefabConfig.TilePrefabItem GetOrCreateItem(MapTilePrefabConfig config, MapTileType type)
        {
            for (int i = 0; i < config.Items.Count; i++)
            {
                MapTilePrefabConfig.TilePrefabItem item = config.Items[i];
                if (item != null && item.Type == type)
                {
                    return item;
                }
            }

            MapTilePrefabConfig.TilePrefabItem newItem = new MapTilePrefabConfig.TilePrefabItem
            {
                Type = type
            };
            config.Items.Add(newItem);
            return newItem;
        }

        private static void EnsureDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
        }
    }
}

#endif
