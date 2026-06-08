///------------------------------------
/// Author閿涙uanjinbiao
/// Mail閿涙ovogooglor@gmail.com
/// Date閿?025-12-10
/// Description閿涙艾婀撮崶鍓ь吀閻炲棗娅?///------------------------------------

using Game.Framework;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using UI;
using UnityEngine;

namespace Game
{
    public class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Cube/Configs/MapDecorationPrefabConfig.asset";
        private const string BattleHudPrefabPath = "Assets/Arts/UI/TowerDefense/Prefabs/BattleHud.prefab";
        private const string MainMenuPagePath = "Assets/Arts/UI/Pages/MainMenuPage.prefab";

        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapDecorationPrefabConfig decorationPrefabConfig;
        private MapData currentMap;
        private int currentMapConfigId;

        private readonly Dictionary<Vector3Int, MapCellData> tileMap = new Dictionary<Vector3Int, MapCellData>();
        private readonly Dictionary<Vector3Int, TileData> tileDataMap = new Dictionary<Vector3Int, TileData>();
        private readonly Dictionary<Vector3Int, TileView> tileViews = new Dictionary<Vector3Int, TileView>();
        private readonly Dictionary<Vector3Int, List<MapObjectData>> objectsByCoord = new Dictionary<Vector3Int, List<MapObjectData>>();
        private readonly Dictionary<Vector2Int, TileData> topTileDataMap = new Dictionary<Vector2Int, TileData>();
        private readonly Dictionary<Vector2Int, TileData> topLogicTileDataMap = new Dictionary<Vector2Int, TileData>();

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

            initialized = true;
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
            RebuildObjectIndex();

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

            if (currentMap.Cells == null)
            {
                Debug.LogWarning("CreateMap failed. Current map tiles is null.");
                return;
            }

            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData MapCellData = currentMap.Cells[i];

                if (MapCellData == null)
                {
                    continue;
                }

                Vector3Int coord = new Vector3Int(MapCellData.X, MapCellData.Y, MapCellData.Z);

                if (!tileDataMap.TryGetValue(coord, out TileData tileData))
                {
                    continue;
                }

                CreateTileView(tileData);
            }

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
            instance.transform.localRotation = GetDirectionRotation(tileData.TypeDirection);
            CreateOverlayView(tileData, instance.transform);

            TileView tileView = TileView.InitializeHierarchy(instance, tileData);
            if (tileView == null)
            {
                Debug.LogWarning($"Tile prefab root must contain TileView. Type: {tileData.Type}, Coord: {key}, Instance: {instance.name}");
                return;
            }

            if (instance.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"Tile prefab root should contain a Collider for picking. Type: {tileData.Type}, Coord: {key}, Instance: {instance.name}");
            }

            ApplyTileVisual(tileData, tileView);
            tileViews[key] = tileView;
        }

        private void ApplyTileVisual(TileData tileData, TileView tileView)
        {
            if (tileData == null || tileView == null)
            {
                return;
            }

            GrassTileMaterialOverride grassVisual = tileView.GetComponent<GrassTileMaterialOverride>();
            if (grassVisual == null)
            {
                return;
            }

            MapGrassVisualData visualData = tileData.Type == MapTileType.Grass
                ? tileData.MapCellData?.GrassVisual
                : null;
            grassVisual.ApplyVisualData(visualData);
        }

        private void CreateOverlayView(TileData tileData, Transform parent)
        {
            GameObject overlay = CreateOverlayInstance(tileData.Overlay);
            if (overlay == null) return;

            overlay.transform.SetParent(parent, false);
            overlay.name = $"Overlay_{tileData.Overlay}_{tileData.OverlayDirection}";
            overlay.transform.localPosition = GetOverlayLocalPosition(tileData.Overlay);
            overlay.transform.localRotation = Quaternion.Inverse(parent.localRotation) * GetDirectionRotation(tileData.OverlayDirection);
        }

        private GameObject CreateOverlayInstance(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    GameObject bridgePrefab = GetPrefab(MapTileType.Bridge);
                    return bridgePrefab != null ? GameObject.Instantiate(bridgePrefab) : null;

                case MapTileOverlay.Stair:
                    return CreateOverlayFallback("Stair", new Color(0.75f, 0.62f, 0.42f));

                case MapTileOverlay.Ramp:
                    return CreateOverlayFallback("Ramp", new Color(0.65f, 0.55f, 0.35f));

                default:
                    return null;
            }
        }

        private GameObject CreateOverlayFallback(string name, Color color)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = name;
            fallback.transform.localScale = new Vector3(tileSize * 0.85f, 0.08f, tileSize * 0.85f);

            Renderer renderer = fallback.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            return fallback;
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

        private Vector3 GetOverlayLocalPosition(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return Vector3.up * tileSize;

                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return Vector3.up * (tileSize * 0.5f);

                default:
                    return Vector3.zero;
            }
        }

        private void CreateDecorationViews()
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                CreateDecorationView(currentMap.Objects[i], i);
            }
        }

        private void CreateDecorationView(MapObjectData decoration, int index)
        {
            if (decoration == null || decoration.ConfigId <= 0)
            {
                return;
            }

            if (!tileViews.TryGetValue(decoration.Coord, out TileView tileView) || tileView == null)
            {
                Debug.LogWarning($"Decoration skipped. Tile not found. Id: {decoration.ConfigId}, Coord: {decoration.Coord}");
                return;
            }

            GameObject prefab = GetDecorationPrefab(decoration);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab. Id: {decoration.ConfigId}");
                return;
            }

            GameObject instance = GameObject.Instantiate(prefab, tileView.transform);
            instance.name = $"Decoration_{index}_{prefab.name}";
            instance.transform.localPosition = decoration.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(decoration.LocalEuler);
            instance.transform.localScale = decoration.LocalScale;
        }

        private GameObject GetDecorationPrefab(MapObjectData decoration)
        {
            if (decorationPrefabConfig != null && decoration.ConfigId > 0)
            {
                GameObject prefab = decorationPrefabConfig.GetPrefab(decoration.ConfigId);
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
                Toast.Warning("鏈壘鍒拌鍑哄敭鐨勫");
                return;
            }

            if (!TowerBuildManager.Instance.TrySellTower(tower, out int sellItemId, out int sellCount))
            {
                return;
            }

            BattleTargetClickManager.Instance.ClearSelection();
            Toast.Info($"鍑哄敭鎴愬姛 +{sellCount}");
        }

        private void OnBattleHudTowerUpgradeClicked(TdTargetRuntimeInfo info)
        {
            if (info.Type != TdTargetInfoType.Tower)
            {
                return;
            }

            if (!TryGetTower(info.Coord, out Tower tower) || tower == null)
            {
                Toast.Warning("鏈壘鍒拌鍗囩骇鐨勫");
                return;
            }

            if (TowerBuildManager.Instance.TryUpgradeTower(tower))
            {
                BattleTargetClickManager.Instance.ClearSelection();
            }
        }

        private void OnBattleHudItemClicked(int itemId)
        {
            Toast.Warning($"閬撳叿 {itemId} 鐨勪娇鐢ㄩ€昏緫灏氭湭閰嶇疆");
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
                Toast.Info("已经是最后一关");
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
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();

            if (currentMap == null || currentMap.Cells == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData MapCellData = currentMap.Cells[i];

                if (MapCellData == null)
                {
                    continue;
                }

                MapCellData.EnsureLayers();

                Vector3Int key = new Vector3Int(MapCellData.X, MapCellData.Y, MapCellData.Z);

                tileMap[key] = MapCellData;
                tileDataMap[key] = new TileData(MapCellData);
            }

            RebuildTopTileIndex();
        }

        private void RebuildTopTileIndex()
        {
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();

            foreach (KeyValuePair<Vector3Int, TileData> pair in tileDataMap)
            {
                Vector3Int coord = pair.Key;
                TileData tileData = pair.Value;
                Vector2Int column = new Vector2Int(coord.x, coord.z);

                if (!topTileDataMap.TryGetValue(column, out TileData topTile) || coord.y > topTile.Y)
                {
                    topTileDataMap[column] = tileData;
                }

                if (!MapTileRule.IsLogicTile(tileData.Type))
                {
                    continue;
                }

                if (!topLogicTileDataMap.TryGetValue(column, out TileData topLogicTile) || coord.y > topLogicTile.Y)
                {
                    topLogicTileDataMap[column] = tileData;
                }
            }
        }

        private void RebuildObjectIndex()
        {
            objectsByCoord.Clear();

            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                AddObjectToIndex(currentMap.Objects[i]);
            }
        }

        private void AddObjectToIndex(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return;
            }

            Vector3Int coord = mapObject.Coord;
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects))
            {
                objects = new List<MapObjectData>();
                objectsByCoord[coord] = objects;
            }

            objects.Add(mapObject);
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
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();
            objectsByCoord.Clear();
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

        public bool TryGetMapCellData(Vector3Int coord, out MapCellData MapCellData)
        {
            return tileMap.TryGetValue(coord, out MapCellData);
        }

        public bool TryGetMapCellData(int x, int y, int z, out MapCellData MapCellData)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetMapCellData(coord, out MapCellData);
        }

        public bool TryGetMapObjectsAt(Vector3Int coord, out IReadOnlyList<MapObjectData> objects)
        {
            if (objectsByCoord.TryGetValue(coord, out List<MapObjectData> result) && result.Count > 0)
            {
                objects = result;
                return true;
            }

            objects = null;
            return false;
        }

        public bool TryGetMapObjectsAt(int x, int y, int z, out IReadOnlyList<MapObjectData> objects)
        {
            return TryGetMapObjectsAt(new Vector3Int(x, y, z), out objects);
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

            return TileView.TryGetValidFrom(hit.collider.transform, out tileView);
        }

        public Vector3 GetTileWorldPosition(Vector3Int coord)
        {
            return GetWorldPosition(coord.x, coord.y, coord.z);
        }

        public Vector3 GetTileWorldPosition(MapCellData MapCellData)
        {
            if (MapCellData == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(MapCellData.X, MapCellData.Y, MapCellData.Z);
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

            if (HasBlockingObject(coord, blockMove: true))
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

            if (HasBlockingObject(coord, blockMove: false))
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

        private bool HasBlockingObject(Vector3Int coord, bool blockMove)
        {
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject == null)
                {
                    continue;
                }

                if (blockMove ? ObjectBlocksMove(mapObject) : ObjectBlocksBuild(mapObject))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ObjectBlocksMove(MapObjectData mapObject)
        {
            if (mapObject.BlocksMove)
            {
                return true;
            }

            MapDecorationPrefabConfig.DecorationPrefabItem item = decorationPrefabConfig != null ? decorationPrefabConfig.GetItem(mapObject.ConfigId) : null;
            return item != null && item.BlocksMove;
        }

        private bool ObjectBlocksBuild(MapObjectData mapObject)
        {
            if (mapObject.BlocksBuild)
            {
                return true;
            }

            MapDecorationPrefabConfig.DecorationPrefabItem item = decorationPrefabConfig != null ? decorationPrefabConfig.GetItem(mapObject.ConfigId) : null;
            return item != null && item.BlocksBuild;
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
            RebuildTopTileIndex();

            if (currentMap != null && currentMap.Cells != null)
            {
                currentMap.Cells.Remove(tileData.MapCellData);
            }

            RemoveMapObjectsAt(coord);

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

        private void RemoveMapObjectsAt(Vector3Int coord)
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            currentMap.Objects.RemoveAll(mapObject => mapObject != null && mapObject.Coord == coord);
            RebuildObjectIndex();
        }

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            return tileMap.TryGetValue(coord, out MapCellData tile) ? tile.Type : MapTileType.None;
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
            return topTileDataMap.TryGetValue(new Vector2Int(x, z), out tileData);
        }

        public bool TryGetTopLogicTile(int x, int z, out TileData tileData)
        {
            return topLogicTileDataMap.TryGetValue(new Vector2Int(x, z), out tileData);
        }

    }
}
