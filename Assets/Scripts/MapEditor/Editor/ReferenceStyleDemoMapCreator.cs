#if UNITY_EDITOR

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class ReferenceStyleDemoMapCreator
    {
        private const string RootName = "ReferenceStyleDemoMap";
        private const string JsonPath = "Assets/Data/Map/ReferenceStyleDemo.json";
        private const string TilePrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const float TileSize = 1f;

        [MenuItem("Tools/Map Art/Reference Layout/Create Reference Style Demo Map")]
        public static void CreateReferenceStyleDemoMap()
        {
            MapData mapData = BuildMapData();
            SaveMapJson(mapData);
            CreateScenePreview(mapData);

            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Reference style demo map created: {RootName}, json: {JsonPath}");
        }

        private static MapData BuildMapData()
        {
            MapData mapData = new MapData(2001, "ReferenceStyleDemo", 16, 2, 11)
            {
                Description = "Reference-style tower defense layout using existing tile and decoration assets."
            };

            HashSet<Vector3Int> water = new HashSet<Vector3Int>();
            AddRect(water, 2, 6, 3, 2);
            AddRect(water, 8, 4, 3, 2);
            AddRect(water, 6, 2, 3, 1);

            HashSet<Vector3Int> snow = new HashSet<Vector3Int>();
            AddRect(snow, 12, 8, 4, 3);
            AddRect(snow, 13, 7, 3, 1);

            HashSet<Vector3Int> road = new HashSet<Vector3Int>();
            AddRect(road, 1, 3, 8, 1);
            AddRect(road, 8, 3, 1, 4);
            AddRect(road, 3, 8, 5, 1);
            AddRect(road, 10, 7, 5, 1);
            AddRect(road, 12, 4, 1, 4);

            HashSet<Vector3Int> bridge = new HashSet<Vector3Int>
            {
                new Vector3Int(7, 0, 2)
            };

            HashSet<Vector3Int> stair = new HashSet<Vector3Int>
            {
                new Vector3Int(7, 0, 8)
            };

            HashSet<Vector3Int> plateau = new HashSet<Vector3Int>();
            AddRect(plateau, 4, 7, 3, 3);

            for (int z = 0; z < mapData.Depth; z++)
            {
                for (int x = 0; x < mapData.Width; x++)
                {
                    Vector3Int coord = new Vector3Int(x, 0, z);
                    MapTileType type = MapTileType.Grass;

                    if (bridge.Contains(coord))
                    {
                        type = MapTileType.Water;
                    }
                    else if (stair.Contains(coord))
                    {
                        type = MapTileType.Grass;
                    }
                    else if (road.Contains(coord))
                    {
                        type = MapTileType.Road;
                    }
                    else if (water.Contains(coord))
                    {
                        type = MapTileType.Water;
                    }
                    else if (snow.Contains(coord))
                    {
                        type = MapTileType.Snow;
                    }

                    MapCellData tile = CreateTile(coord, type);
                    if (bridge.Contains(coord))
                    {
                        tile.Overlay.Type = MapTileOverlay.Bridge;
                        tile.OverlayDirection = MapDirection.East;
                        tile.ApplyDefaultLogic();
                    }
                    else if (stair.Contains(coord))
                    {
                        tile.Overlay.Type = MapTileOverlay.Stair;
                        tile.OverlayDirection = MapDirection.West;
                        tile.ApplyDefaultLogic();
                    }
                    mapData.Cells.Add(tile);
                }
            }

            foreach (Vector3Int baseCoord in plateau)
            {
                MapCellData upperTile = CreateTile(new Vector3Int(baseCoord.x, 1, baseCoord.z), MapTileType.Grass);
                if (baseCoord.x == 5 && baseCoord.z == 8)
                {
                    upperTile = CreateTile(new Vector3Int(baseCoord.x, 1, baseCoord.z), MapTileType.Road);
                }

                mapData.Cells.Add(upperTile);
            }

            mapData.SpawnPoints.Add(new Vector3Int(1, 0, 3));
            mapData.SpawnPoints.Add(new Vector3Int(14, 0, 7));
            mapData.HasGoalPoint = true;
            mapData.GoalPoint = new Vector3Int(5, 1, 8);

            return mapData;
        }

        private static MapCellData CreateTile(Vector3Int coord, MapTileType type)
        {
            MapCellData tile = new MapCellData(coord.x, coord.y, coord.z, type);
            tile.ApplyDefaultLogic();
            return tile;
        }

        private static void AddRect(HashSet<Vector3Int> coords, int x, int z, int width, int depth)
        {
            for (int dz = 0; dz < depth; dz++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    coords.Add(new Vector3Int(x + dx, 0, z + dz));
                }
            }
        }

        private static void SaveMapJson(MapData mapData)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), JsonPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(absolutePath, json, new UTF8Encoding(false));
        }

        private static void CreateScenePreview(MapData mapData)
        {
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(RootName);
            Dictionary<MapTileType, GameObject> tilePrefabs = LoadTilePrefabs();

            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];
                GameObject tileRoot = InstantiateTileVisual(tile.Type, tilePrefabs);
                if (tileRoot == null)
                {
                    Debug.LogWarning($"Missing tile visual for {tile.Type}, coord: {tile.X},{tile.Y},{tile.Z}");
                    continue;
                }

                tileRoot.name = $"{tile.Type}_{tile.Overlay.Type}_{tile.X}_{tile.Y}_{tile.Z}";
                tileRoot.transform.SetParent(root.transform, false);
                tileRoot.transform.position = GetWorldPosition(tile);

                TileView tileView = TileView.InitializeHierarchy(tileRoot, new TileData(tile));
                if (tileView == null)
                {
                    Debug.LogWarning($"Tile prefab root must contain TileView: {tile.Type}, Instance: {tileRoot.name}");
                }

                if (tileRoot.GetComponent<Collider>() == null)
                {
                    Debug.LogWarning($"Tile prefab root should contain a Collider for picking: {tile.Type}, Instance: {tileRoot.name}");
                }

                tileRoot.transform.localRotation = GetDirectionRotation(tile.TypeDirection);

                GameObject overlayVisual = InstantiateOverlayVisual(tile.Overlay.Type, tile.OverlayDirection, tilePrefabs);
                if (overlayVisual != null)
                {
                    overlayVisual.name = $"Overlay_{tile.Overlay.Type}";
                    overlayVisual.transform.SetParent(tileRoot.transform, false);
                    overlayVisual.transform.localPosition = GetOverlayLocalPosition(tile.Overlay.Type);
                    overlayVisual.transform.localRotation = Quaternion.Inverse(tileRoot.transform.localRotation) * GetDirectionRotation(tile.OverlayDirection);
                }
            }

            CreateDecorationPreview(root.transform);
            SetupPreviewLightingAndCamera(root);
            Selection.activeGameObject = root;
            FocusSceneView(new Vector3((mapData.Width - 1) * 0.5f, 0.55f, (mapData.Depth - 1) * 0.5f));
        }

        private static Dictionary<MapTileType, GameObject> LoadTilePrefabs()
        {
            Dictionary<MapTileType, GameObject> prefabs = new Dictionary<MapTileType, GameObject>();
            MapTilePrefabConfig config = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(TilePrefabConfigPath);
            if (config != null)
            {
                config.RebuildCache();
            }

            prefabs[MapTileType.Grass] = LoadTilePrefab(MapTileType.Grass, config,
                "Assets/Arts/Map/Tiles/Generated/Grass.prefab",
                "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab",
                "Assets/Arts/Map/Tiles/Grass.prefab");
            prefabs[MapTileType.Water] = LoadTilePrefab(MapTileType.Water, config,
                "Assets/Arts/Map/Tiles/Generated/Water.prefab",
                "Assets/Arts/Map/Tiles/Water.prefab");
            prefabs[MapTileType.Road] = LoadTilePrefab(MapTileType.Road, config,
                "Assets/Arts/Map/Tiles/Generated/Road.prefab",
                "Assets/Arts/Map/Tiles/Road.prefab");
            prefabs[MapTileType.Snow] = LoadTilePrefab(MapTileType.Snow, config,
                "Assets/Arts/Map/Tiles/Generated/Snow.prefab",
                "Assets/Arts/Map/Tiles/Snow.prefab");
            prefabs[MapTileType.Hill] = LoadTilePrefab(MapTileType.Hill, config,
                "Assets/Arts/Map/Tiles/Generated/Hill.prefab",
                "Assets/Arts/Map/Tiles/Hill.prefab");
            prefabs[MapTileType.Bridge] = LoadTilePrefab(MapTileType.Bridge, config,
                "Assets/Arts/Map/Tiles/Generated/Bridge.prefab");

            return prefabs;
        }

        private static GameObject LoadTilePrefab(MapTileType type, MapTilePrefabConfig config, params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            if (config != null)
            {
                GameObject configPrefab = config.GetPrefab(type);
                if (configPrefab != null)
                {
                    return configPrefab;
                }
            }

            Debug.LogWarning($"Missing tile prefab for {type}. A colored cube fallback will be used.");
            return null;
        }

        private static GameObject InstantiateTileVisual(MapTileType type, Dictionary<MapTileType, GameObject> prefabs)
        {
            prefabs.TryGetValue(type, out GameObject prefab);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                return instance != null ? instance : Object.Instantiate(prefab);
            }

            return CreateFallbackCube(type, new Vector3(TileSize, TileSize, TileSize), Vector3.zero);
        }

        private static GameObject InstantiateOverlayVisual(MapTileOverlay overlay, MapDirection direction, Dictionary<MapTileType, GameObject> prefabs)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return InstantiateDecoration("Assets/Arts/Map/Decoration/Bridge/Meshy_AI_Wooden_Plank_Bridge_0528100753_texture.prefab", direction);

                case MapTileOverlay.Stair:
                    return InstantiateDecoration("Assets/Arts/Map/Decoration/Stair/Meshy_AI_Golden_Staircase_0530101420_texture.prefab", direction);

                default:
                    return null;
            }
        }

        private static Vector3 GetOverlayLocalPosition(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return Vector3.up * TileSize;

                case MapTileOverlay.Stair:
                    return Vector3.up * (TileSize * 0.5f);

                default:
                    return Vector3.up * 0.02f;
            }
        }

        private static GameObject CreateFallbackCube(MapTileType type, Vector3 scale, Vector3 localPosition)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = scale;
            cube.transform.localPosition = localPosition;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = GetFallbackColor(type);
                renderer.sharedMaterial = material;
            }

            return cube;
        }

        private static Color GetFallbackColor(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass:
                    return new Color(0.46f, 0.68f, 0.18f, 1f);

                case MapTileType.Water:
                    return new Color(0.08f, 0.64f, 0.86f, 1f);

                case MapTileType.Snow:
                    return new Color(0.88f, 0.94f, 1f, 1f);

                case MapTileType.Road:
                    return new Color(0.82f, 0.62f, 0.35f, 1f);

                default:
                    return new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }

        private static void CreateDecorationPreview(Transform root)
        {
            Transform decorationRoot = new GameObject("Decorations").transform;
            decorationRoot.SetParent(root, false);

            PlaceDecoration(decorationRoot, "Tree_A", "Assets/Arts/Map/Decoration/Tree/Meshy_AI_Tri_Tier_Pine_0518115420_texture.prefab", new Vector3(1f, 0f, 8.5f), 0f, 0.55f);
            PlaceDecoration(decorationRoot, "Tree_B", "Assets/Arts/Map/Decoration/Tree/Meshy_AI_Tri_Tier_Pine_0518115420_texture.prefab", new Vector3(13.5f, 0f, 9.2f), -25f, 0.55f);
            PlaceDecoration(decorationRoot, "Tree_C", "Assets/Arts/Map/Decoration/Tree/Tree.prefab", new Vector3(12.2f, 0f, 1.5f), 35f, 0.75f);
            PlaceDecoration(decorationRoot, "Tree_D", "Assets/Arts/Map/Decoration/Tree/Tree.prefab", new Vector3(4.8f, 1f, 9.2f), 20f, 0.7f);

            PlaceDecoration(decorationRoot, "Stone_A", "Assets/Arts/Map/Decoration/Stone1/Meshy_AI_Mossy_Boulder_Stack_0518160212_texture.prefab", new Vector3(2.1f, 0f, 1.8f), 10f, 0.45f);
            PlaceDecoration(decorationRoot, "Stone_B", "Assets/Arts/Map/Decoration/Stone1/Meshy_AI_Mossy_Boulder_Stack_0518160212_texture.prefab", new Vector3(11.2f, 0f, 4.4f), -35f, 0.38f);
            PlaceDecoration(decorationRoot, "Stone_C", "Assets/Arts/Map/Decoration/Stone1/Stone1.prefab", new Vector3(14.2f, 0f, 4.2f), 0f, 0.65f);
            PlaceDecoration(decorationRoot, "Stone_D", "Assets/Arts/Map/Decoration/Stone1/Stone1.prefab", new Vector3(4.1f, 1f, 8.9f), 15f, 0.55f);

        }

        private static void PlaceDecoration(Transform parent, string name, string path, Vector3 position, float yRotation, float uniformScale)
        {
            GameObject instance = InstantiateDecoration(path, MapDirection.None);
            if (instance == null)
            {
                return;
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            instance.transform.localScale = Vector3.one * uniformScale;
        }

        private static GameObject InstantiateDecoration(string path, MapDirection direction)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab: {path}");
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
            }

            instance.transform.localRotation = GetDirectionRotation(direction);
            return instance;
        }

        private static Quaternion GetDirectionRotation(MapDirection direction)
        {
            switch (direction)
            {
                case MapDirection.East:
                    return Quaternion.Euler(0f, 90f, 0f);

                case MapDirection.South:
                    return Quaternion.Euler(0f, 180f, 0f);

                case MapDirection.West:
                    return Quaternion.Euler(0f, 270f, 0f);

                default:
                    return Quaternion.identity;
            }
        }

        private static Vector3 GetWorldPosition(MapCellData tile)
        {
            return new Vector3(tile.X * TileSize, tile.Y * TileSize, tile.Z * TileSize);
        }

        private static void SetupPreviewLightingAndCamera(GameObject root)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.55f, 0.50f, 1f);

            Vector3 focus = new Vector3(7.5f, 0.65f, 5f);

            GameObject cameraObject = new GameObject("ReferenceStyleDemoCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = focus + new Vector3(8.8f, 8.2f, -9.4f);
            cameraObject.transform.LookAt(focus);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.40f, 0.36f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8.2f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.depth = 10f;
            Camera.SetupCurrent(camera);

            GameObject keyLightObject = new GameObject("ReferenceStyleDemoKeyLight");
            keyLightObject.transform.SetParent(root.transform, false);
            keyLightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.00f, 0.96f, 0.88f, 1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.28f;

            GameObject fillLightObject = new GameObject("ReferenceStyleDemoFillLight");
            fillLightObject.transform.SetParent(root.transform, false);
            fillLightObject.transform.position = focus + new Vector3(-4f, 3f, 4f);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.72f, 0.83f, 1.00f, 1f);
            fillLight.intensity = 0.45f;
            fillLight.range = 12f;
            fillLight.shadows = LightShadows.None;
        }

        private static void FocusSceneView(Vector3 focus)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SceneView.FrameLastActiveSceneView();
                return;
            }

            sceneView.orthographic = true;
            sceneView.LookAt(focus, Quaternion.Euler(32f, -40f, 0f), 10.2f);
            sceneView.Repaint();
        }
    }
}

#endif
