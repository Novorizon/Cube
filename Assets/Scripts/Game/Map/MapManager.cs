///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2025-12-10
/// Description：地图管理器
///------------------------------------

using Game.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapData currentMap;

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, TileView> tileViews = new Dictionary<Vector3Int, TileView>();

        private Transform mapRoot;
        private float tileSize = 1f;

        private bool initialized = false;

        public bool Initialized
        {
            get
            {
                return initialized;
            }
        }

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

        public Transform MapRoot
        {
            get
            {
                return mapRoot;
            }
        }

        public bool Initialize()
        {
            mapTilePrefabConfig = ResourceManager.Instance.LoadAsset<MapTilePrefabConfig>(PrefabConfigPath);

            if (mapTilePrefabConfig == null)
            {
                Debug.LogError($"MapManager initialize failed. Missing prefab config: {PrefabConfigPath}");
                initialized = false;
                return false;
            }

            initialized = true;
            return true;
        }

        public bool LoadMap(int id)
        {
            if (!initialized)
            {
                Debug.LogError("LoadMap failed. MapManager is not initialized.");
                return false;
            }

            string location = "Assets/Data/Map/" + id + ".json";
            return LoadMapInternal(location);
        }

        public bool LoadMap(string name)
        {
            if (!initialized)
            {
                Debug.LogError("LoadMap failed. MapManager is not initialized.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogError("LoadMap failed. Map name is empty.");
                return false;
            }

            string location = "Assets/Data/Map/" + name + ".json";
            return LoadMapInternal(location);
        }

        private bool LoadMapInternal(string location)
        {
            bool loadDataSuccess = LoadMapData(location);

            if (!loadDataSuccess)
            {
                return false;
            }

            CreateMap();
            AfterMapCreated();

            return true;
        }

        private bool LoadMapData(string location)
        {
            TextAsset json = ResourceManager.Instance.LoadTextAsset(location);

            if (json == null)
            {
                Debug.LogError($"Failed to load map json: {location}");
                return false;
            }

            MapData data = JsonConvert.DeserializeObject<MapData>(json.text);

            if (data == null)
            {
                Debug.LogError($"Failed to parse map json: {location}");
                return false;
            }

            currentMap = data;
            RebuildTileIndex();

            return true;
        }

        private void CreateMap()
        {
            if (currentMap == null)
            {
                Debug.LogWarning("CreateMap failed. Current map is null.");
                return;
            }

            ClearMapObjects();
            EnsureMapRoot();

            tileViews.Clear();

            if (currentMap.Tiles == null)
            {
                Debug.LogWarning("CreateMap failed. Current map tiles is null.");
                return;
            }

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                CreateTileView(tile);
            }

            Debug.Log($"Create map success. Count: {tileViews.Count}");
        }

        private void CreateTileView(MapTileData tile)
        {
            Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);

            GameObject prefab = GetPrefab(tile.Type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {tile.Type}, Coord: {key}");
                return;
            }

            Vector3 position = GetWorldPosition(tile.X, tile.Y, tile.Z);

            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, mapRoot);
            instance.name = $"{tile.Type}_{tile.X}_{tile.Y}_{tile.Z}";

            TileView tileView = instance.GetComponent<TileView>();

            if (tileView == null)
            {
                tileView = instance.AddComponent<TileView>();
            }

            tileView.Initialize(tile);

            tileViews[key] = tileView;
        }

        private void AfterMapCreated()
        {
            CameraManager.Instance.Initialize();
            CameraManager.Instance.SetViewAngle(55f, 45f);
            CameraManager.Instance.SetPadding(2f);
            CameraManager.Instance.FocusCurrentMap();

            GameInputManager.Instance.SetMode(InputMode.Build);
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

                if (tile == null)
                {
                    continue;
                }

                Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);
                tileMap[key] = tile;
            }
        }

        private void EnsureMapRoot()
        {
            GameObject rootObject = GameObject.Find("MapRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("MapRoot");
                rootObject.transform.position = Vector3.zero;
            }

            mapRoot = rootObject.transform;
        }

        public void ClearMap()
        {
            currentMap = null;
            tileMap.Clear();
            ClearMapObjects();
        }

        private void ClearMapObjects()
        {
            if (mapRoot == null)
            {
                GameObject oldRoot = GameObject.Find("MapRoot");

                if (oldRoot != null)
                {
                    mapRoot = oldRoot.transform;
                }
            }

            if (mapRoot == null)
            {
                tileViews.Clear();
                return;
            }

            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = mapRoot.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                GameObject.Destroy(child.gameObject);
            }

            tileViews.Clear();
        }

        private Vector3 GetWorldPosition(int x, int y, int z)
        {
            return new Vector3(x * tileSize, y * tileSize, z * tileSize);
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

        public bool TryGetTile(Vector3Int coord, out MapTileData tile)
        {
            return tileMap.TryGetValue(coord, out tile);
        }

        public bool TryGetTile(int x, int y, int z, out MapTileData tile)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetTile(coord, out tile);
        }

        public bool TryGetTileView(Vector3Int coord, out TileView tileView)
        {
            return tileViews.TryGetValue(coord, out tileView);
        }

        public bool TryGetTileView(int x, int y, int z, out TileView tileView)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetTileView(coord, out tileView);
        }

        public bool TryPickTile(Vector2 screenPosition, Camera camera, out TileView tileView)
        {
            tileView = null;

            if (camera == null)
            {
                Debug.LogWarning("TryPickTile failed. Camera is null.");
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return false;
            }

            tileView = hit.collider.GetComponentInParent<TileView>();

            if (tileView == null)
            {
                return false;
            }

            return true;
        }

        public Vector3 GetTileWorldPosition(Vector3Int coord)
        {
            return GetWorldPosition(coord.x, coord.y, coord.z);
        }

        public Vector3 GetTileWorldPosition(MapTileData tile)
        {
            if (tile == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(tile.X, tile.Y, tile.Z);
        }
    }
}