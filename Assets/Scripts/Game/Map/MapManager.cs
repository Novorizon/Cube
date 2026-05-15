///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2025-12-10
/// Description：地图管理器
///------------------------------------

using Game.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapData currentMap;

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, TileData> tileDataMap = new Dictionary<Vector3Int, TileData>();
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

        public IReadOnlyList<Vector3Int> SpawnPoints
        {
            get
            {
                if (currentMap == null || currentMap.SpawnPoints == null)
                {
                    return null;
                }

                return currentMap.SpawnPoints;
            }
        }

        public bool HasGoalPoint
        {
            get
            {
                if (currentMap == null)
                {
                    return false;
                }

                return currentMap.HasGoalPoint;
            }
        }

        public Vector3Int GoalPoint
        {
            get
            {
                if (currentMap == null)
                {
                    return default;
                }

                return currentMap.GoalPoint;
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

            data.EnsureRuntimeCollections();

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
                MapTileData mapTileData = currentMap.Tiles[i];

                if (mapTileData == null)
                {
                    continue;
                }

                Vector3Int coord = new Vector3Int(mapTileData.X, mapTileData.Y, mapTileData.Z);

                if (!tileDataMap.TryGetValue(coord, out TileData tileData))
                {
                    continue;
                }

                CreateTileView(tileData);
            }

            Debug.Log($"Create map success. Count: {tileViews.Count}");
        }

        private void CreateTileView(TileData tileData)
        {
            Vector3Int key = tileData.Coord;

            GameObject prefab = GetPrefab(tileData.Type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {tileData.Type}, Coord: {key}");
                return;
            }

            Vector3 position = GetWorldPosition(tileData.X, tileData.Y, tileData.Z);

            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, mapRoot);
            instance.name = $"{tileData.Type}_{tileData.X}_{tileData.Y}_{tileData.Z}";

            TileView tileView = instance.GetComponent<TileView>();

            if (tileView == null)
            {
                tileView = instance.AddComponent<TileView>();
            }

            tileView.Initialize(tileData);

            tileViews[key] = tileView;
        }

        private void AfterMapCreated()
        {
            CameraManager.Instance.Initialize();
            CameraManager.Instance.SetViewAngle(55f, 45f);
            CameraManager.Instance.SetPadding(2f);
            CameraManager.Instance.FocusCurrentMap();

            BaseManager.Instance.LoadCurrentMapBase();

            string StatusPanelPath = "Assets/Arts/UI/Panels/StatusPanel.prefab";
            _ = UIManager.Instance.Panels.ShowAsync(StatusPanelPath);

            GameInputManager.Instance.SetMode(InputMode.Build);
        }

        private void RebuildTileIndex()
        {
            tileMap.Clear();
            tileDataMap.Clear();

            if (currentMap == null || currentMap.Tiles == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData mapTileData = currentMap.Tiles[i];

                if (mapTileData == null)
                {
                    continue;
                }

                mapTileData.ApplyDefaultLogicByType(mapTileData.Type);

                Vector3Int key = new Vector3Int(mapTileData.X, mapTileData.Y, mapTileData.Z);

                tileMap[key] = mapTileData;
                tileDataMap[key] = new TileData(mapTileData);
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
            BaseManager.Instance.ClearBaseObject();

            currentMap = null;
            tileMap.Clear();
            tileDataMap.Clear();
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

        public bool TryGetMapTileData(Vector3Int coord, out MapTileData mapTileData)
        {
            return tileMap.TryGetValue(coord, out mapTileData);
        }

        public bool TryGetMapTileData(int x, int y, int z, out MapTileData mapTileData)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetMapTileData(coord, out mapTileData);
        }

        public bool TryGetTileData(Vector3Int coord, out TileData tileData)
        {
            return tileDataMap.TryGetValue(coord, out tileData);
        }

        public bool TryGetTileData(int x, int y, int z, out TileData tileData)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetTileData(coord, out tileData);
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

        public Vector3 GetTileWorldPosition(MapTileData mapTileData)
        {
            if (mapTileData == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(mapTileData.X, mapTileData.Y, mapTileData.Z);
        }

        public Vector3 GetTileWorldPosition(TileData tileData)
        {
            if (tileData == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(tileData.X, tileData.Y, tileData.Z);
        }

        public Vector3 GetMapPointWorldPosition(Vector3Int coord)
        {
            return GetTileWorldPosition(coord);
        }

        public bool TryGetGoalPoint(out Vector3Int coord)
        {
            coord = default;

            if (currentMap == null)
            {
                return false;
            }

            if (!currentMap.HasGoalPoint)
            {
                return false;
            }

            coord = currentMap.GoalPoint;
            return true;
        }

        public bool IsInsideMap(Vector3Int coord)
        {
            return tileDataMap.ContainsKey(coord);
        }

        public bool IsLogicTile(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            return MapTileRule.IsLogicTile(tileData.Type);
        }

        public bool IsLogicTileType(MapTileType type)
        {
            return MapTileRule.IsLogicTile(type);
        }

        public bool HasTileAbove(Vector3Int coord)
        {
            Vector3Int aboveCoord = new Vector3Int(coord.x, coord.y + 1, coord.z);
            return tileDataMap.ContainsKey(aboveCoord);
        }

        public bool IsExposed(Vector3Int coord)
        {
            if (!tileDataMap.ContainsKey(coord))
            {
                return false;
            }

            return !HasTileAbove(coord);
        }

        public bool IsWalkable(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tileData.Type))
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            return tileData.IsRuntimeWalkable;
        }

        public bool IsBuildable(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tileData.Type))
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            return tileData.IsRuntimeBuildable;
        }

        public int GetMoveCost(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return int.MaxValue;
            }

            if (!IsWalkable(coord))
            {
                return int.MaxValue;
            }

            return tileData.MoveCost;
        }

        public bool HasTower(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            return tileData.HasTower;
        }

        public bool TryGetTower(Vector3Int coord, out Tower tower)
        {
            tower = null;

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!tileData.HasTower)
            {
                return false;
            }

            tower = tileData.Tower;
            return true;
        }

        public bool CanPlaceTower(Vector3Int coord)
        {
            return IsBuildable(coord);
        }

        public bool TryPlaceTower(Vector3Int coord, Tower tower)
        {
            if (tower == null)
            {
                return false;
            }

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!IsBuildable(coord))
            {
                return false;
            }

            return tileData.TrySetTower(tower);
        }

        public bool RemoveTower(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!tileData.HasTower)
            {
                return false;
            }

            tileData.ClearTower();
            return true;
        }

        public bool CanRemoveTile(Vector3Int coord)
        {
            if (!tileDataMap.ContainsKey(coord))
            {
                return false;
            }

            if (HasTileAbove(coord))
            {
                return false;
            }

            if (HasTower(coord))
            {
                return false;
            }

            return true;
        }

        public bool TryRemoveTile(Vector3Int coord)
        {
            if (!CanRemoveTile(coord))
            {
                return false;
            }

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            tileDataMap.Remove(coord);
            tileMap.Remove(coord);

            if (currentMap != null && currentMap.Tiles != null)
            {
                currentMap.Tiles.Remove(tileData.MapTileData);
            }

            if (tileViews.TryGetValue(coord, out TileView tileView))
            {
                if (tileView != null)
                {
                    GameObject.Destroy(tileView.gameObject);
                }

                tileViews.Remove(coord);
            }

            return true;
        }

        public bool TryDestroyHill(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (tileData.Type != MapTileType.Hill)
            {
                return false;
            }

            return TryRemoveTile(coord);
        }

        public bool TryGetTopTile(int x, int z, out TileData tileData)
        {
            tileData = null;

            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, TileData> pair in tileDataMap)
            {
                Vector3Int coord = pair.Key;

                if (coord.x != x || coord.z != z)
                {
                    continue;
                }

                if (coord.y > topY)
                {
                    topY = coord.y;
                    tileData = pair.Value;
                }
            }

            return tileData != null;
        }

        public bool TryGetTopLogicTile(int x, int z, out TileData tileData)
        {
            tileData = null;

            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, TileData> pair in tileDataMap)
            {
                Vector3Int coord = pair.Key;
                TileData currentTileData = pair.Value;

                if (coord.x != x || coord.z != z)
                {
                    continue;
                }

                if (!MapTileRule.IsLogicTile(currentTileData.Type))
                {
                    continue;
                }

                if (coord.y > topY)
                {
                    topY = coord.y;
                    tileData = currentTileData;
                }
            }

            return tileData != null;
        }

        public void GetWalkableNeighbors(Vector3Int coord, List<Vector3Int> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            TryAddWalkableNeighbor(results, coord.x + 1, coord.y, coord.z);
            TryAddWalkableNeighbor(results, coord.x - 1, coord.y, coord.z);
            TryAddWalkableNeighbor(results, coord.x, coord.y, coord.z + 1);
            TryAddWalkableNeighbor(results, coord.x, coord.y, coord.z - 1);
        }

        private void TryAddWalkableNeighbor(List<Vector3Int> results, int x, int y, int z)
        {
            Vector3Int coord = new Vector3Int(x, y, z);

            if (!IsWalkable(coord))
            {
                return;
            }

            results.Add(coord);
        }
    }
}