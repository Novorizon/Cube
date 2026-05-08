#if UNITY_EDITOR

using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Editor
{
    public class SimpleMapEditorWindow : OdinEditorWindow
    {
        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        [Title("Map Settings")]

        [LabelText("地图名称")]
        [SerializeField]
        private string mapName = "NewMap";

        [LabelText("地图Id")]
        [SerializeField]
        private int id = 0;

        [LabelText("地图描述")]
        [SerializeField]
        private string description = "这是地图描述";

        [LabelText("宽度 X")]
        [MinValue(1)]
        [SerializeField]
        private int width = 10;

        [LabelText("高度 Y")]
        [MinValue(1)]
        [SerializeField]
        private int height = 3;

        [LabelText("深度 Z")]
        [MinValue(1)]
        [SerializeField]
        private int depth = 10;

        [Title("Preview Prefabs")]

        [LabelText("地图根节点")]
        [SerializeField]
        private Transform previewRoot;

        [Title("Prefab Config")]
        [LabelText("地块 Prefab 配置")]
        [SerializeField]
        private MapTilePrefabConfig prefabConfig;

        [LabelText("格子尺寸")]
        [SerializeField]
        private float tileSize = 1f;

        [Title("Current Map")]

        [ShowInInspector]
        [ReadOnly]
        [LabelText("是否已创建地图")]
        private bool HasMap
        {
            get
            {
                return currentMap != null;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("地块数量")]
        private int TileCount
        {
            get
            {
                if (currentMap == null || currentMap.Tiles == null)
                {
                    return 0;
                }

                return currentMap.Tiles.Count;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("当前地图数据")]
        private MapData currentMap;

        [MenuItem("Tools/Cube/Simple Map Editor")]
        public static void Open()
        {
            SimpleMapEditorWindow window = GetWindow<SimpleMapEditorWindow>();
            window.titleContent = new GUIContent("Simple Map Editor");
            window.Show();
        }
        protected override void OnEnable()
        {
            base.OnEnable();

            TryLoadPrefabConfig();
        }

        private void TryLoadPrefabConfig()
        {
            if (prefabConfig != null)
            {
                return;
            }

            prefabConfig = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(PrefabConfigPath);

            if (prefabConfig == null)
            {
                Debug.LogWarning($"MapTilePrefabConfig not found. Path: {PrefabConfigPath}");
            }
        }

        /// <summary>
        /// 创建一张默认地图。
        /// 当前策略：
        /// 所有格子都生成 Grass。
        ///
        /// 注意：
        /// 这里会生成 width * height * depth 个逻辑地块。
        /// 后续如果你决定“每个 x/z 只有一个表面地块”，这里再改。
        /// </summary>
        [Button("创建地图", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        private void CreateMap()
        {
            currentMap = new MapData(id, mapName, width, height, depth);

            //创建一层土壤，位于y=0,仅为了好看
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    MapTileData tile = new MapTileData(x, -1, z, MapTileType.Soil);
                    currentMap.Tiles.Add(tile);
                }
            }
            //从y=0开始创建地块
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int value = UnityEngine.Random.Range((int)MapTileType.None, (int)MapTileType.Water + 1);
                        MapTileData tile = new MapTileData(x, y, z, (MapTileType)value);
                        currentMap.Tiles.Add(tile);


                    }
                }
            }
            CreatePreviewObjects();
            //for
            Debug.Log($"Create map success. Name: {mapName}, Size: {width}x{height}x{depth}, Tiles: {currentMap.Tiles.Count}");
        }
        private void RebuildTileIndex()
        {
            tileMap.Clear();

            if (currentMap == null || currentMap.Tiles == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];
                Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);

                tileMap[key] = tile;
            }
        }
        private void CreatePreviewObjects()
        {
            if (currentMap == null)
            {
                Debug.LogWarning("Current map is null.");
                return;
            }

            ClearPreviewObjects();

            GameObject rootObject = GameObject.Find("MapRoot");
            if (rootObject != null)
            {
                GameObject.DestroyImmediate(rootObject);
            }
            rootObject = new GameObject("MapRoot");
            rootObject.transform.position = Vector3.zero;
            previewRoot = rootObject.transform;

            tileMap.Clear();
            tileObjects.Clear();

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];
                Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);

                tileMap[key] = tile;

                GameObject prefab = GetPrefab(tile.Type);

                if (prefab == null)
                {
                    Debug.LogWarning($"Missing prefab for tile type: {tile.Type}");
                    continue;
                }

                Vector3 position = GetWorldPosition(tile.X, tile.Y, tile.Z);

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                if (instance == null)
                {
                    instance = Instantiate(prefab);
                }

                instance.name = $"{tile.Type}_{tile.X}_{tile.Y}_{tile.Z}";
                instance.transform.SetParent(previewRoot, false);
                instance.transform.position = position;

                tileObjects[key] = instance;
            }

            Debug.Log($"Create preview objects success. Count: {tileObjects.Count}");
        }
        private Vector3 GetWorldPosition(int x, int y, int z)
        {
            return new Vector3(x * tileSize, y * tileSize, z * tileSize);
        }
        private void ClearPreviewObjects()
        {
            if (previewRoot == null)
            {
                return;
            }

            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = previewRoot.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
            }

            tileObjects.Clear();
        }
        private GameObject GetPrefab(MapTileType type)
        {
            if (prefabConfig == null)
            {
                TryLoadPrefabConfig();
            }

            if (prefabConfig == null)
            {
                Debug.LogWarning("Prefab config is null.");
                return null;
            }

            GameObject prefab = prefabConfig.GetPrefab(type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {type}");
            }

            return prefab;
        }
        /// <summary>
        /// 导出当前地图为 Json。
        /// 使用 Newtonsoft.Json。
        /// </summary>
        [Button("导出 Json", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.6f, 1.0f)]
        private void ExportJson()
        {
            if (currentMap == null)
            {
                EditorUtility.DisplayDialog("Export Failed", "请先创建地图。", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export Map Json", Application.dataPath, currentMap.Id + ".json", "json");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };

            string json = JsonConvert.SerializeObject(currentMap, settings);
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();

            Debug.Log($"Export map json success: {path}");
        }

        /// <summary>
        /// 清空当前编辑器里的地图数据。
        /// 不会删除已经导出的 Json 文件。
        /// </summary>
        [Button("清空当前地图")]
        [GUIColor(1.0f, 0.6f, 0.3f)]
        private void ClearMap()
        {
            currentMap = null;
            Debug.Log("Clear current map.");
        }
        [Button("导入 Json 并生成地图", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.7f, 1.0f)]
        private void ImportJsonAndCreateMap()
        {
            string path = EditorUtility.OpenFilePanel("Import Map Json", Application.dataPath, "json");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                MapData mapData = JsonConvert.DeserializeObject<MapData>(json);

                if (mapData == null)
                {
                    EditorUtility.DisplayDialog("Import Failed", "Json 解析失败，MapData 为空。", "OK");
                    return;
                }

                if (mapData.Tiles == null)
                {
                    EditorUtility.DisplayDialog("Import Failed", "Json 中 Tiles 为空。", "OK");
                    return;
                }

                currentMap = mapData;

                id = currentMap.Id;
                mapName = currentMap.Name;
                width = currentMap.Width;
                height = currentMap.Height;
                depth = currentMap.Depth;

                RebuildTileIndex();
                CreatePreviewObjects();

                Debug.Log($"Import map success. Path: {path}, Name: {currentMap.Name}, Size: {currentMap.Width}x{currentMap.Height}x{currentMap.Depth}, Tiles: {currentMap.Tiles.Count}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Import Failed", exception.Message, "OK");
            }
        }
    }
}

#endif
