///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2025-12-10
/// Description：资源管理器
///------------------------------------

using Game.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game
{
    public class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapData currentMap;
        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();
        private Transform previewRoot;
        private float tileSize = 1f;
        public MapData CurrentMap
        {
            get
            {
                return currentMap;
            }
        }
        public float TileSize
        {
            get
            {
                return tileSize;
            }
        }


        private bool initialized = false;
        public bool Initialized
        {
            get { return initialized; }
        }


        public bool Initialize()
        {
            mapTilePrefabConfig = ResourceManager.Instance.LoadAsset<MapTilePrefabConfig>(PrefabConfigPath);
            if (mapTilePrefabConfig != null)
            {
                // 解析地图配置
                // MapConfig config = ParseMapConfig(asset);
                initialized = true;
            }
            else
            {
                initialized = false;
            }
            return initialized;
        }

        public void LoadMap(int id)
        {
            if (!initialized)
                return;

            string name = "Assets/Data/Map/" + id + ".json";
            LoadMapData(name);
            CreateMap();

            CameraManager.Instance.SetViewAngle(55f, 45f);
            CameraManager.Instance.SetPadding(2f);
            CameraManager.Instance.FocusCurrentMap();
        }

        public void LoadMap(string name)
        {
            if (!initialized)
                return;

            name = "Assets/Data/Map/" + name + ".json";
            LoadMapData(name);
            CreateMap();
        }


        private void LoadMapData(string name)
        {
            TextAsset json = ResourceManager.Instance.LoadTextAsset(name);
            if (json == null)
            {
                Debug.LogError($"Failed to load map json: {name}");
                return;
            }
            MapData data = JsonConvert.DeserializeObject<MapData>(json.text);
            if (data == null)
            {
                Debug.LogError($"Failed to parse map json: {name}");
                return;
            }

            currentMap = data;
            RebuildTileIndex();
        }

        private void CreateMap()
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

                GameObject instance = GameObject.Instantiate(prefab);

                if (instance == null)
                {
                    instance = GameObject.Instantiate(prefab);
                }

                instance.name = $"{tile.Type}_{tile.X}_{tile.Y}_{tile.Z}";
                instance.transform.SetParent(previewRoot, false);
                instance.transform.position = position;

                tileObjects[key] = instance;
            }

            Debug.Log($"Create preview objects success. Count: {tileObjects.Count}");
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

                GameObject.Destroy(child.gameObject);
            }

            tileObjects.Clear();
        }

        private GameObject GetPrefab(MapTileType type)
        {
            if (mapTilePrefabConfig == null)
            {
                Debug.LogWarning("Prefab config is null.");
                return null;
            }

            GameObject prefab = mapTilePrefabConfig.GetPrefab(type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {type}");
            }

            return prefab;
        }
    }
}
