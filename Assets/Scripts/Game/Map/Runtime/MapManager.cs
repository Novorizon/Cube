///------------------------------------
/// Author锛歡uanjinbiao
/// Mail锛歯ovogooglor@gmail.com
/// Date锛?025-12-10
/// Description锛氬湴鍥剧鐞嗗櫒
///------------------------------------

using Game.Framework;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game
{
    public class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Cube/Configs/MapDecorationPrefabConfig.asset";
        private const string TerrainBlendConfigPath = "Assets/Data/Cube/Configs/MapTerrainBlendConfig.asset";
        private const string BattleHudPrefabPath = "Assets/Arts/UI/TowerDefense/Prefabs/BattleHud.prefab";
        private const string MainMenuPagePath = "Assets/Arts/UI/Pages/MainMenuPage.prefab";

        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapDecorationPrefabConfig decorationPrefabConfig;
        private MapTerrainBlendConfig terrainBlendConfig;
        private MapData currentMap;
        private int currentMapConfigId;

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
            decorationPrefabConfig = ResourceManager.Instance.LoadAsset<MapDecorationPrefabConfig>(DecorationConfigPath);
            terrainBlendConfig = LoadTerrainBlendConfig();

            if (mapTilePrefabConfig == null)
            {
                Debug.LogError($"MapManager initialize failed. Missing prefab config: {PrefabConfigPath}");
                initialized = false;
                return false;
            }

            if (decorationPrefabConfig != null)
            {
                decorationPrefabConfig.RebuildCache();
            }

            if (terrainBlendConfig != null)
            {
                terrainBlendConfig.RebuildCache();
            }

            initialized = true;
            return true;
        }

        private MapTerrainBlendConfig LoadTerrainBlendConfig()
        {
#if UNITY_EDITOR
            MapTerrainBlendConfig editorConfig = AssetDatabase.LoadAssetAtPath<MapTerrainBlendConfig>(TerrainBlendConfigPath);
            if (IsTerrainBlendConfigUsable(editorConfig))
            {
                editorConfig.RebuildCache();
                return editorConfig;
            }
#endif

            MapTerrainBlendConfig config = ResourceManager.Instance.LoadAsset<MapTerrainBlendConfig>(TerrainBlendConfigPath);
            if (!IsTerrainBlendConfigUsable(config))
            {
                Debug.LogWarning($"Map terrain blend config is missing or incomplete. Runtime terrain top blend is disabled: {TerrainBlendConfigPath}");
                return null;
            }

            config.RebuildCache();
            return config;
        }

        private bool IsTerrainBlendConfigUsable(MapTerrainBlendConfig config)
        {
            if (config == null || config.BlendMaterial == null)
            {
                return false;
            }

            if (config.GetTopTexture(MapTileType.Grass) == null) return false;
            if (config.GetTopTexture(MapTileType.Hill) == null) return false;
            if (config.GetTopTexture(MapTileType.Snow) == null) return false;
            if (config.GetTopTexture(MapTileType.Water) == null) return false;
            return true;
        }

        public bool LoadMap(int mapId)
        {
            string location = "Assets/Data/Map/" + mapId + ".json";

            if (!DataManager.Instance.Map.TryGet(mapId, out MapConfig mapConfig))
            {
                Debug.LogError($"Start wave test failed. Missing map config: {mapId}");
                return false;
            }

            ClearBattleRuntime(true);
            currentMapConfigId = mapId;
            ItemManager.Instance.AddItem(ItemIds.Gold, mapConfig.InitialGold);

            bool loadDataSuccess = LoadMapData(location);
            if (!loadDataSuccess)
            {
                return false;
            }

            CreateMap();
            AfterMapCreated(mapConfig);
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

            RefreshAllTerrainBlendViews();
            CreateDecorationViews();
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
            instance.name = $"{tileData.Type}_{tileData.Overlay}_{tileData.X}_{tileData.Y}_{tileData.Z}";
            CreateOverlayView(tileData, instance.transform);

            TileView tileView = instance.GetComponent<TileView>();

            if (tileView == null)
            {
                tileView = instance.AddComponent<TileView>();
            }

            tileView.Initialize(tileData);

            tileViews[key] = tileView;
        }

        private void CreateOverlayView(TileData tileData, Transform parent)
        {
            GameObject overlayPrefab = GetOverlayPrefab(tileData.Overlay);

            if (overlayPrefab == null)
            {
                return;
            }

            GameObject overlay = GameObject.Instantiate(overlayPrefab, parent);
            overlay.name = $"Overlay_{tileData.Overlay}_{tileData.Direction}";
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = GetDirectionRotation(tileData.Direction);
        }

        private GameObject GetOverlayPrefab(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Road:
                    return GetPrefab(MapTileType.Road);

                case MapTileOverlay.Bridge:
                    return GetPrefab(MapTileType.Bridge);

                default:
                    return null;
            }
        }

        private Quaternion GetDirectionRotation(MapDirection direction)
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

        private void CreateDecorationViews()
        {
            if (currentMap == null || currentMap.Decorations == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Decorations.Count; i++)
            {
                CreateDecorationView(currentMap.Decorations[i], i);
            }
        }

        private void RefreshAllTerrainBlendViews()
        {
            EnsureTerrainBlendConfig();

            if (terrainBlendConfig == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector3Int, TileView> pair in tileViews)
            {
                RefreshTerrainBlendView(pair.Key);
            }
        }

        private void RefreshTerrainBlendAround(Vector3Int coord)
        {
            RefreshTerrainBlendView(coord);
            RefreshTerrainBlendView(coord + Vector3Int.forward);
            RefreshTerrainBlendView(coord + Vector3Int.back);
            RefreshTerrainBlendView(coord + Vector3Int.left);
            RefreshTerrainBlendView(coord + Vector3Int.right);
        }

        private void RefreshTerrainBlendView(Vector3Int coord)
        {
            EnsureTerrainBlendConfig();

            if (terrainBlendConfig == null)
            {
                return;
            }

            if (!tileViews.TryGetValue(coord, out TileView tileView) || tileView == null)
            {
                return;
            }

            MapTerrainBlendUtility.Apply(
                tileView.gameObject,
                terrainBlendConfig,
                GetTileTypeOrNone(coord),
                GetTileTypeOrNone(coord + Vector3Int.forward),
                GetTileTypeOrNone(coord + Vector3Int.right),
                GetTileTypeOrNone(coord + Vector3Int.back),
                GetTileTypeOrNone(coord + Vector3Int.left));
        }

        private void EnsureTerrainBlendConfig()
        {
            if (IsTerrainBlendConfigUsable(terrainBlendConfig))
            {
                return;
            }

            terrainBlendConfig = LoadTerrainBlendConfig();
        }

        private void CreateDecorationView(MapDecorationData decoration, int index)
        {
            if (decoration == null || decoration.DecorationId <= 0)
            {
                return;
            }

            if (!tileViews.TryGetValue(decoration.Coord, out TileView tileView) || tileView == null)
            {
                Debug.LogWarning($"Decoration skipped. Tile not found. Id: {decoration.DecorationId}, Coord: {decoration.Coord}");
                return;
            }

            GameObject prefab = GetDecorationPrefab(decoration);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab. Id: {decoration.DecorationId}");
                return;
            }

            GameObject instance = GameObject.Instantiate(prefab, tileView.transform);
            instance.name = $"Decoration_{index}_{prefab.name}";
            instance.transform.localPosition = decoration.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(decoration.LocalEuler);
            instance.transform.localScale = decoration.LocalScale;
        }

        private GameObject GetDecorationPrefab(MapDecorationData decoration)
        {
            if (decorationPrefabConfig != null && decoration.DecorationId > 0)
            {
                GameObject prefab = decorationPrefabConfig.GetPrefab(decoration.DecorationId);
                if (prefab != null) return prefab;
            }

            return null;
        }

        private void AfterMapCreated(MapConfig mapConfig)
        {
            CameraManager.Instance.Initialize();
            CameraManager.Instance.SetViewAngle(55f, 45f);
            CameraManager.Instance.SetPadding(2f);
            CameraManager.Instance.FocusCurrentMap();

            ShowBattleHudAsync().Forget();
            BattleFlowManager.Instance.BeginBattle(mapConfig);

            if (!BaseManager.Instance.LoadBase(mapConfig.BaseId))
            {
                BattleFlowManager.Instance.CompleteDefeat("Base load failed.");
                return;
            }

            GameInputManager.Instance.SetMode(InputMode.Battle);

            if (!DataManager.Instance.LoadWave(mapConfig.WaveNormal))
            {
                BattleFlowManager.Instance.CompleteDefeat("Wave data load failed.");
                return;
            }

            //WaveConfig waveConfig = DataManager.Instance.Wave.Get(1);
            if (!WaveManager.Instance.StartWave())
            {
                BattleFlowManager.Instance.CompleteDefeat("Wave start failed.");
            }
        }

        private async Task ShowBattleHudAsync()
        {
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(BattleHudPrefabPath);
            if (!handle.IsValid)
            {
                return;
            }

            if (handle.View is BattleHudController battleHud)
            {
                battleHud.SkillClicked -= OnBattleHudSkillClicked;
                battleHud.SkillClicked += OnBattleHudSkillClicked;
                battleHud.AutoNextWaveChanged -= OnBattleHudAutoNextWaveChanged;
                battleHud.AutoNextWaveChanged += OnBattleHudAutoNextWaveChanged;
                battleHud.TowerSellTargetClicked -= OnBattleHudTowerSellClicked;
                battleHud.TowerSellTargetClicked += OnBattleHudTowerSellClicked;
                battleHud.TowerUpgradeTargetClicked -= OnBattleHudTowerUpgradeClicked;
                battleHud.TowerUpgradeTargetClicked += OnBattleHudTowerUpgradeClicked;
                battleHud.ItemClicked -= OnBattleHudItemClicked;
                battleHud.ItemClicked += OnBattleHudItemClicked;
            }
        }

        private void OnBattleHudSkillClicked(int skillId)
        {
            Ability.CastResult result = AbilityManager.Instance.CastBaseAbilityAtBestTarget(skillId);
            if (result == null || result.Success)
            {
                return;
            }

            Debug.LogWarning($"Cast skill failed. skillId: {skillId}, reason: {result.FailureReason}, message: {result.Message}");
        }

        private void OnBattleHudAutoNextWaveChanged(bool autoNextWave)
        {
            // true means waves chain immediately after spawn completion; false waits for the field to clear.
            WaveManager.Instance.SetWaitAllEnemiesKilledBeforeNextWave(!autoNextWave);
        }

        private void OnBattleHudTowerSellClicked(TdTargetRuntimeInfo info)
        {
            if (info.Type != TdTargetInfoType.Tower)
            {
                return;
            }

            if (!TryGetTower(info.Coord, out Tower tower) || tower == null)
            {
                Toast.Warning("未找到要出售的塔");
                return;
            }

            if (!TowerBuildManager.Instance.TrySellTower(tower, out int sellItemId, out int sellCount))
            {
                return;
            }

            BattleTargetClickManager.Instance.ClearSelection();
            Toast.Info($"出售成功 +{sellCount}");
        }

        private void OnBattleHudTowerUpgradeClicked(TdTargetRuntimeInfo info)
        {
            if (info.Type != TdTargetInfoType.Tower)
            {
                return;
            }

            if (!TryGetTower(info.Coord, out Tower tower) || tower == null)
            {
                Toast.Warning("未找到要升级的塔");
                return;
            }

            if (TowerBuildManager.Instance.TryUpgradeTower(tower))
            {
                BattleTargetClickManager.Instance.ClearSelection();
            }
        }

        private void OnBattleHudItemClicked(int itemId)
        {
            Toast.Warning($"道具 {itemId} 的使用逻辑尚未配置");
        }

        public void RestartCurrentMap()
        {
            int mapId = currentMapConfigId;
            if (mapId <= 0 && BattleFlowManager.Instance.LastEndMessage != null)
            {
                mapId = BattleFlowManager.Instance.LastEndMessage.MapId;
            }

            if (mapId <= 0)
            {
                Debug.LogWarning("Restart map failed. Current map id is invalid.");
                return;
            }

            LoadMap(mapId);
        }

        public bool HasNextMap(int mapId)
        {
            return TryGetNextMapId(mapId, out int nextMapId);
        }

        public bool LoadNextMap(int mapId)
        {
            if (!TryGetNextMapId(mapId, out int nextMapId))
            {
                Toast.Info("已是最后一关");
                return false;
            }

            return LoadMap(nextMapId);
        }

        public void ReturnToMainMenu()
        {
            ReturnToMainMenuAsync().Forget();
        }

        private async Task ReturnToMainMenuAsync()
        {
            ClearBattleRuntime(true);

            if (GameInputManager.IsCreated)
            {
                GameInputManager.Instance.SetMode(InputMode.Gameplay);
            }

            await UIManager.Instance.Pages.ResetToAsync(MainMenuPagePath);
        }

        private bool TryGetNextMapId(int mapId, out int nextMapId)
        {
            nextMapId = 0;

            if (DataManager.Instance.Map == null || DataManager.Instance.Map.GetAll() == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, MapConfig> pair in DataManager.Instance.Map.GetAll())
            {
                int candidateId = pair.Key;
                if (candidateId <= mapId)
                {
                    continue;
                }

                if (nextMapId == 0 || candidateId < nextMapId)
                {
                    nextMapId = candidateId;
                }
            }

            return nextMapId > 0;
        }

        private void ClearBattleRuntime(bool hideBattleUi)
        {
            Time.timeScale = 1f;
            WaveManager.Instance.Stop();
            WaveManager.Instance.Clear();
            NpcManager.Instance.Clear();
            TowerManager.Instance.Clear();
            TowerBuildManager.Instance.Clear();
            BattleTargetClickManager.Instance.ClearSelection();
            BaseManager.Instance.ClearBaseObject();
            AbilityManager.Instance.Release();
            AbilityManager.Instance.Initialize();
            ItemManager.Instance.Clear();
            DataManager.Instance.ClearWave();
            BattleFlowManager.Instance.Initialize();
            ClearMap();

            if (hideBattleUi)
            {
                UIManager.Instance.Panels.HideAll(true);
            }
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

                mapTileData.ApplyDefaultLogic();

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

            RefreshTerrainBlendAround(coord);
            return true;
        }

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            return tileMap.TryGetValue(coord, out MapTileData tile) ? tile.Type : MapTileType.None;
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
