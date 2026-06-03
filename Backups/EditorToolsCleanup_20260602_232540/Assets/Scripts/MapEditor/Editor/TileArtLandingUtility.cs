using System.Collections.Generic;
using System.IO;
using Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MapEditor.Editor
{
    [InitializeOnLoad]
    public static class TileArtLandingUtility
    {
        private const string FbxPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.fbx";
        private const string PrefabPath = "Assets/Arts/Map/Tiles/Grass_ReferenceStyle_Test.prefab";
        private const string ConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string MapPath = "Assets/Data/Map/GrassReferenceStyle_6x6.json";
        private const string PreviewRootName = "GrassReferenceStyle_6x6_TestMap";
        private const string AutoRunFlagRelativePath = "Temp/RunTileArtLandingUtility.flag";

        static TileArtLandingUtility()
        {
            EditorApplication.delayCall += RunIfRequested;
            EditorApplication.update += RunIfRequested;
        }

        [MenuItem("Tools/Map/Tile Art/Build Grass Reference Test %#g")]
        public static void BuildGrassReferenceTest()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

            GameObject prefab = CreatePrefab();
            UpdateTilePrefabConfig(prefab);
            CreateGrassTestMapJson();
            CreateScenePreview(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Grass reference tile test ready: {PrefabPath}, {ConfigPath}, {MapPath}");
        }

        private static void RunIfRequested()
        {
            string flagPath = GetProjectPath(AutoRunFlagRelativePath);
            if (!File.Exists(flagPath))
            {
                return;
            }

            File.Delete(flagPath);
            EditorApplication.update -= RunIfRequested;
            BuildGrassReferenceTest();
        }

        private static string GetProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, relativePath);
        }

        private static GameObject CreatePrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (source == null)
            {
                throw new FileNotFoundException($"Missing FBX asset: {FbxPath}");
            }

            GameObject root = new GameObject("Grass_ReferenceStyle_Test");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.name = "ReferenceStyleGrassTile";
            model.transform.localPosition = Vector3.zero;
            // Blender's vertical stack arrives along Unity Z for this FBX.
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;

            AssignMaterials(root);
            NormalizeModelToTile(model);
            TileBoundsGizmo boundsGizmo = root.AddComponent<TileBoundsGizmo>();
            _ = boundsGizmo.BoundsSize;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                throw new IOException($"Failed to save prefab: {PrefabPath}");
            }

            return prefab;
        }

        private static void AssignMaterials(GameObject root)
        {
            Material grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Map/Tiles/Materials/Grass.mat");
            Material soil = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Map/Tiles/Materials/Soil.mat");
            Material rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/Map/Tiles/Materials/Rock.mat");

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                string name = renderer.gameObject.name.ToLowerInvariant();
                Material material = grass;

                if (name.Contains("rock"))
                {
                    material = rock;
                }
                else if (name.Contains("soil") || name.Contains("base"))
                {
                    material = soil;
                }

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void NormalizeModelToTile(GameObject model)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds))
            {
                return;
            }

            if (bounds.size.x <= 0.0001f || bounds.size.y <= 0.0001f || bounds.size.z <= 0.0001f)
            {
                return;
            }

            Vector3 scale = new Vector3(
                1f / bounds.size.x,
                1f / bounds.size.z,
                1f / bounds.size.y);
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scale);

            if (!TryGetRendererBounds(model, out bounds))
            {
                return;
            }

            Vector3 offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            model.transform.position += offset;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static void UpdateTilePrefabConfig(GameObject grassPrefab)
        {
            MapTilePrefabConfig config = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(ConfigPath);
            if (config == null)
            {
                throw new FileNotFoundException($"Missing tile prefab config: {ConfigPath}");
            }

            MapTilePrefabConfig.TilePrefabItem grassItem = null;
            for (int i = 0; i < config.Items.Count; i++)
            {
                MapTilePrefabConfig.TilePrefabItem item = config.Items[i];
                if (item != null && item.Type == MapTileType.Grass)
                {
                    grassItem = item;
                    break;
                }
            }

            if (grassItem == null)
            {
                grassItem = new MapTilePrefabConfig.TilePrefabItem { Type = MapTileType.Grass };
                config.Items.Add(grassItem);
            }

            grassItem.Prefab = grassPrefab;
            config.RebuildCache();
            EditorUtility.SetDirty(config);
        }

        private static void CreateGrassTestMapJson()
        {
            MapData mapData = new MapData(1001, "GrassReferenceStyle_6x6", 6, 1, 6)
            {
                Description = "6x6 visual test map for ReferenceStyleGrassTile."
            };

            for (int z = 0; z < 6; z++)
            {
                for (int x = 0; x < 6; x++)
                {
                    mapData.Tiles.Add(new MapTileData(x, 0, z, MapTileType.Grass));
                }
            }

            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(MapPath, json);
            AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateScenePreview(GameObject prefab)
        {
            GameObject oldRoot = GameObject.Find(PreviewRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(PreviewRootName);
            for (int z = 0; z < 6; z++)
            {
                for (int x = 0; x < 6; x++)
                {
                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    tile.name = $"Grass_{x}_{z}";
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.localPosition = new Vector3(x, 0f, z);
                }
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            FrameSceneView(root);
        }

        private static void FrameSceneView(GameObject target)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            sceneView.pivot = new Vector3(2.5f, 0.5f, 2.5f);
            sceneView.rotation = Quaternion.Euler(35f, -45f, 0f);
            sceneView.size = 5f;
            sceneView.FrameSelected();
            sceneView.Repaint();
        }
    }
}
