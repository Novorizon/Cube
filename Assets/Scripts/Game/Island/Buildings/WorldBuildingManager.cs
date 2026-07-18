using Game.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WorldBuildingManager
    {
        private const int FirstInstanceId = 100000000;

        public static WorldBuildingManager Instance { get; } = new WorldBuildingManager();

        private readonly Dictionary<int, WorldBuilding> buildings = new Dictionary<int, WorldBuilding>();
        private readonly Dictionary<int, GameObject> buildingViews = new Dictionary<int, GameObject>();
        private readonly HashSet<int> runtimeUnlockedBuildingIds = new HashSet<int>();
        private WorldCostResolver costResolver;
        private int nextInstanceId = FirstInstanceId;
        private Transform buildingRoot;

        private WorldBuildingManager()
        {
        }

        public void Initialize()
        {
            buildings.Clear();
            ClearViews();
            runtimeUnlockedBuildingIds.Clear();
            nextInstanceId = FirstInstanceId;
            costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);
        }

        public void Update()
        {
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool changed = false;

            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.Status != WorldBuildingStatus.Constructing)
                {
                    continue;
                }

                if (building.FinishAtUnixTime > 0 && currentUnixTime >= building.FinishAtUnixTime)
                {
                    building.CompleteConstruction();
                    changed = true;
                }
            }

            if (changed)
            {
                StorageManager.Instance.MarkDirty();
            }
        }

        public bool TryBuild(int buildingId, Vector3Int coord)
        {
            return TryBuild(buildingId, coord, out _);
        }

        public bool TryBuild(int buildingId, Vector3Int coord, out WorldBuilding building)
        {
            building = null;

            if (!TryGetBuildConfigs(buildingId, out WorldBuildingConfig config, out WorldBuildingLevelConfig levelConfig))
            {
                return false;
            }

            if (!IsBuildingUnlocked(config))
            {
                Debug.Log($"World building failed. Building is locked. buildingId: {buildingId}");
                return false;
            }

            if (config.MaxCount > 0 && CountBuildings(config.Id) >= config.MaxCount)
            {
                Debug.Log($"World building failed. Building count reached max. buildingId: {buildingId}, maxCount: {config.MaxCount}");
                return false;
            }

            int sizeX = WorldBuildingFootprint.GetSizeX(config);
            int sizeZ = WorldBuildingFootprint.GetSizeZ(config);
            if (!MapManager.Instance.CanPlaceMapObject(coord, sizeX, sizeZ))
            {
                Debug.Log($"World building failed. Footprint is not buildable. buildingId: {buildingId}, coord: {coord}, size: {sizeX}x{sizeZ}");
                return false;
            }

            IReadOnlyList<ItemStack> costs = GetBuildCosts(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId > 0 && costs.Count == 0)
            {
                Debug.LogWarning($"World building failed. Empty build cost group. buildingId: {buildingId}, costGroupId: {levelConfig.BuildCostGroupId}");
                return false;
            }

            if (!ItemManager.Instance.TryConsumeItems(costs))
            {
                Debug.Log($"World building failed. Cost is not enough. buildingId: {buildingId}, costGroupId: {levelConfig.BuildCostGroupId}");
                return false;
            }

            int instanceId = AllocateInstanceId();
            int mapId = GetCurrentMapId();
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WorldBuildingStatus status = levelConfig.BuildSeconds > 0 ? WorldBuildingStatus.Constructing : WorldBuildingStatus.Active;
            long finishAtUnixTime = status == WorldBuildingStatus.Constructing ? currentUnixTime + levelConfig.BuildSeconds : 0;

            building = new WorldBuilding(instanceId, mapId, buildingId, levelConfig.Level, coord, status, finishAtUnixTime, 0);
            MapObjectData mapObject = CreateMapObject(building);
            if (!MapManager.Instance.TryAddMapObject(mapObject))
            {
                ItemManager.Instance.AddItems(costs);
                building = null;
                Debug.LogWarning($"World building failed. Add map object failed. buildingId: {buildingId}, coord: {coord}");
                return false;
            }

            buildings.Add(instanceId, building);
            CreateView(building);
            QuestManager.Instance.NotifyEvent(QuestEventType.BuildBuilding, buildingId);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        public bool TryRemove(int instanceId)
        {
            if (!buildings.TryGetValue(instanceId, out WorldBuilding building) || building == null)
            {
                return false;
            }

            if (!MapManager.Instance.TryRemoveMapObject(instanceId))
            {
                Debug.LogWarning($"Remove world building failed. Map object not found. instanceId: {instanceId}");
                return false;
            }

            DestroyView(instanceId);
            buildings.Remove(instanceId);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        public bool TryUpgrade(int instanceId)
        {
            if (!buildings.TryGetValue(instanceId, out WorldBuilding building) || building == null)
            {
                return false;
            }

            if (building.Status != WorldBuildingStatus.Active)
            {
                return false;
            }

            if (!DataManager.Instance.TryGetWorldBuildingLevel(building.ConfigId, building.Level + 1, out WorldBuildingLevelConfig nextLevelConfig))
            {
                return false;
            }

            IReadOnlyList<ItemStack> costs = GetBuildCosts(nextLevelConfig.BuildCostGroupId);
            if (nextLevelConfig.BuildCostGroupId > 0 && costs.Count == 0)
            {
                return false;
            }

            if (!ItemManager.Instance.TryConsumeItems(costs))
            {
                return false;
            }

            building.UpgradeTo(nextLevelConfig.Level);
            QuestManager.Instance.NotifyEvent(QuestEventType.UpgradeBuilding, building.ConfigId);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        public bool CanUpgrade(int instanceId, out string reason)
        {
            reason = string.Empty;
            if (!buildings.TryGetValue(instanceId, out WorldBuilding building) || building == null)
            {
                reason = LocalizationManager.Get("ui.build.reason.missing_building");
                return false;
            }

            if (building.Status != WorldBuildingStatus.Active)
            {
                reason = LocalizationManager.Get("ui.build.reason.construction");
                return false;
            }

            if (!DataManager.Instance.TryGetWorldBuildingLevel(building.ConfigId, building.Level + 1, out WorldBuildingLevelConfig nextLevelConfig))
            {
                reason = LocalizationManager.Get("ui.build.reason.max_level");
                return false;
            }

            IReadOnlyList<ItemStack> costs = GetBuildCosts(nextLevelConfig.BuildCostGroupId);
            if (nextLevelConfig.BuildCostGroupId > 0 && costs.Count == 0)
            {
                reason = LocalizationManager.Get("ui.build.reason.cost_config");
                return false;
            }

            if (!ItemManager.Instance.HasItems(costs))
            {
                reason = FormatCosts(costs);
                return false;
            }

            return true;
        }

        public bool TryRemoveAt(Vector3Int coord)
        {
            int mapId = GetCurrentMapId();
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.MapId == mapId && ContainsBuildingCoord(building, coord))
                {
                    return TryRemove(building.InstanceId);
                }
            }

            return false;
        }

        public bool TryGetBuilding(int instanceId, out WorldBuilding building)
        {
            return buildings.TryGetValue(instanceId, out building);
        }

        public IReadOnlyDictionary<int, WorldBuilding> GetAllBuildings()
        {
            return buildings;
        }

        public bool TryGetConfig(int buildingId, out WorldBuildingConfig config)
        {
            config = null;
            return DataManager.Instance.WorldBuilding != null &&
                   DataManager.Instance.WorldBuilding.TryGet(buildingId, out config) &&
                   config != null;
        }

        public bool IsBuildingType(WorldBuilding building, WorldBuildingType buildingType)
        {
            if (building == null || !TryGetConfig(building.ConfigId, out WorldBuildingConfig config))
            {
                return false;
            }

            return (WorldBuildingType)config.BuildingType == buildingType;
        }

        public bool HasActiveBuildingType(WorldBuildingType buildingType)
        {
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (IsBuildingType(building, buildingType))
                {
                    return true;
                }
            }

            return false;
        }

        public int CountActiveBuildingType(WorldBuildingType buildingType)
        {
            int count = 0;
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.MapId != GetCurrentMapId() || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (IsBuildingType(building, buildingType))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsBuildingUnlocked(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding == null || !DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config))
            {
                return false;
            }

            return config != null && config.Enable && IsBuildingUnlocked(config);
        }

        public string GetUnlockRequirementText(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding == null || !DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config))
            {
                return LocalizationManager.Get("ui.build.reason.missing_config");
            }

            return GetUnlockRequirementText(config);
        }

        public string GetUnlockRequirementText(WorldBuildingConfig config)
        {
            if (config == null || !config.Enable)
            {
                return LocalizationManager.Get("ui.build.reason.disabled");
            }

            if ((WorldBuildingType)config.BuildingType == WorldBuildingType.House)
            {
                return string.Empty;
            }

            if (config.UnlockHouseLevel > 0)
            {
                int currentLevel = GetHighestBuildingLevel(WorldBuildingType.House);
                if (currentLevel < config.UnlockHouseLevel)
                {
                    return LocalizationManager.Format("ui.build.require.house_level", config.UnlockHouseLevel);
                }
            }

            if (config.UnlockBuildingId > 0)
            {
                int requiredLevel = config.UnlockBuildingLevel > 0 ? config.UnlockBuildingLevel : 1;
                if (GetHighestBuildingLevel(config.UnlockBuildingId) < requiredLevel)
                {
                    return LocalizationManager.Format("ui.build.require.building_level", GetBuildingName(config.UnlockBuildingId), requiredLevel);
                }
            }

            string sourceRequirement = GetUnlockSourceRequirementText(config);
            if (!string.IsNullOrWhiteSpace(sourceRequirement))
            {
                return sourceRequirement;
            }

            return string.Empty;
        }

        public void UnlockBuildingAtRuntime(int buildingId)
        {
            if (buildingId <= 0 || runtimeUnlockedBuildingIds.Contains(buildingId))
            {
                return;
            }

            runtimeUnlockedBuildingIds.Add(buildingId);
            StorageManager.Instance.MarkDirty();
        }

        public int[] CreateRuntimeUnlockSaveData()
        {
            int[] ids = new int[runtimeUnlockedBuildingIds.Count];
            runtimeUnlockedBuildingIds.CopyTo(ids);
            Array.Sort(ids);
            return ids;
        }

        public void LoadRuntimeUnlockSaveData(IReadOnlyList<int> buildingIds)
        {
            runtimeUnlockedBuildingIds.Clear();
            if (buildingIds == null)
            {
                return;
            }

            for (int i = 0; i < buildingIds.Count; i++)
            {
                int buildingId = buildingIds[i];
                if (buildingId > 0)
                {
                    runtimeUnlockedBuildingIds.Add(buildingId);
                }
            }
        }

        public SaveWorldBuildingData[] CreateSaveData()
        {
            List<SaveWorldBuildingData> result = new List<SaveWorldBuildingData>();
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.InstanceId <= 0 || building.ConfigId <= 0)
                {
                    continue;
                }

                result.Add(new SaveWorldBuildingData
                {
                    InstanceId = building.InstanceId,
                    MapId = building.MapId,
                    ConfigId = building.ConfigId,
                    Level = building.Level,
                    X = building.Coord.x,
                    Y = building.Coord.y,
                    Z = building.Coord.z,
                    Status = (int)building.Status,
                    FinishAtUnixTime = building.FinishAtUnixTime,
                    NextIncomeAtUnixTime = building.NextIncomeAtUnixTime,
                });
            }

            return result.ToArray();
        }

        public void LoadSaveData(IReadOnlyList<SaveWorldBuildingData> savedBuildings)
        {
            buildings.Clear();
            ClearViews();
            nextInstanceId = FirstInstanceId;

            if (savedBuildings == null)
            {
                return;
            }

            for (int i = 0; i < savedBuildings.Count; i++)
            {
                SaveWorldBuildingData savedBuilding = savedBuildings[i];
                if (savedBuilding == null || savedBuilding.InstanceId <= 0 || savedBuilding.ConfigId <= 0)
                {
                    continue;
                }

                Vector3Int coord = new Vector3Int(savedBuilding.X, savedBuilding.Y, savedBuilding.Z);
                WorldBuilding building = new WorldBuilding(
                    savedBuilding.InstanceId,
                    savedBuilding.MapId,
                    savedBuilding.ConfigId,
                    savedBuilding.Level > 0 ? savedBuilding.Level : 1,
                    coord,
                    ToStatus(savedBuilding.Status),
                    savedBuilding.FinishAtUnixTime,
                    savedBuilding.NextIncomeAtUnixTime);

                buildings[building.InstanceId] = building;
                if (building.InstanceId >= nextInstanceId)
                {
                    nextInstanceId = building.InstanceId + 1;
                }
            }
        }

        public void RegisterMapObjects()
        {
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null)
                {
                    continue;
                }

                if (building.MapId != GetCurrentMapId())
                {
                    continue;
                }

                if (MapManager.Instance.TryGetMapObject(building.InstanceId, out _))
                {
                    continue;
                }

                if (!MapManager.Instance.TryAddMapObject(CreateMapObject(building)))
                {
                    Debug.LogWarning($"Register world building map object failed. instanceId: {building.InstanceId}, buildingId: {building.ConfigId}, coord: {building.Coord}");
                }
            }
        }

        public void CreateViews()
        {
            ClearViews();
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.MapId == GetCurrentMapId())
                {
                    CreateView(building);
                }
            }
        }

        public void ClearViews()
        {
            foreach (KeyValuePair<int, GameObject> pair in buildingViews)
            {
                if (pair.Value != null)
                {
                    GameObject.Destroy(pair.Value);
                }
            }

            buildingViews.Clear();
        }

        private bool TryGetBuildConfigs(int buildingId, out WorldBuildingConfig config, out WorldBuildingLevelConfig levelConfig)
        {
            config = null;
            levelConfig = null;

            if (!DataManager.Instance.WorldBuilding.TryGet(buildingId, out config) || config == null || !config.Enable)
            {
                Debug.LogWarning($"World building failed. Missing config: {buildingId}");
                return false;
            }

            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out levelConfig))
            {
                Debug.LogWarning($"World building failed. Missing level config: {buildingId}, level: 1");
                return false;
            }

            return true;
        }

        private IReadOnlyList<ItemStack> GetBuildCosts(int costGroupId)
        {
            if (costGroupId <= 0)
            {
                return Array.Empty<ItemStack>();
            }

            costResolver ??= new WorldCostResolver(DataManager.Instance.WorldCost);
            return costResolver.GetCostGroup(costGroupId);
        }

        private static string FormatCosts(IReadOnlyList<ItemStack> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.free");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                ItemStack cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {cost.Count}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : LocalizationManager.Get("ui.common.free");
        }

        private bool IsBuildingUnlocked(WorldBuildingConfig config)
        {
            if (config == null || !config.Enable)
            {
                return false;
            }

            if (config.UnlockHouseLevel > 0 && GetHighestBuildingLevel(WorldBuildingType.House) < config.UnlockHouseLevel)
            {
                return false;
            }

            if (config.UnlockBuildingId > 0)
            {
                int requiredLevel = config.UnlockBuildingLevel > 0 ? config.UnlockBuildingLevel : 1;
                if (GetHighestBuildingLevel(config.UnlockBuildingId) < requiredLevel)
                {
                    return false;
                }
            }

            if (!IsUnlockSourceSatisfied(config))
            {
                return false;
            }

            return true;
        }

        private static bool IsUnlockSourceSatisfied(WorldBuildingConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (config.DefaultUnlocked)
            {
                return true;
            }

            WorldBuildingUnlockSource source = (WorldBuildingUnlockSource)config.UnlockSourceType;
            switch (source)
            {
                case WorldBuildingUnlockSource.Default:
                    return true;
                case WorldBuildingUnlockSource.Tech:
                    return TechManager.Instance.IsBuildingUnlockedByTech(config.Id);
                case WorldBuildingUnlockSource.Runtime:
                    return Instance.runtimeUnlockedBuildingIds.Contains(config.Id);
                case WorldBuildingUnlockSource.None:
                default:
                    return false;
            }
        }

        private static string GetUnlockSourceRequirementText(WorldBuildingConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            if (config.DefaultUnlocked)
            {
                return string.Empty;
            }

            WorldBuildingUnlockSource source = (WorldBuildingUnlockSource)config.UnlockSourceType;
            switch (source)
            {
                case WorldBuildingUnlockSource.Default:
                    return string.Empty;
                case WorldBuildingUnlockSource.Tech:
                    return TechManager.Instance.GetBuildingUnlockRequirementText(config.Id);
                case WorldBuildingUnlockSource.Runtime:
                    return LocalizationManager.Get("ui.build.reason.not_unlocked");
                case WorldBuildingUnlockSource.None:
                default:
                    return LocalizationManager.Get("ui.build.reason.not_unlockable");
            }
        }

        public int CountBuildingConfig(int configId)
        {
            return CountBuildings(configId);
        }

        private int CountBuildings(int configId)
        {
            int count = 0;
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.MapId == GetCurrentMapId() && building.ConfigId == configId)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetHighestBuildingLevel(WorldBuildingType buildingType)
        {
            int highestLevel = 0;
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (!DataManager.Instance.WorldBuilding.TryGet(building.ConfigId, out WorldBuildingConfig config) || config == null)
                {
                    continue;
                }

                if ((WorldBuildingType)config.BuildingType == buildingType && building.Level > highestLevel)
                {
                    highestLevel = building.Level;
                }
            }

            return highestLevel;
        }

        private int GetHighestBuildingLevel(int configId)
        {
            int highestLevel = 0;
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.Status == WorldBuildingStatus.Active && building.ConfigId == configId && building.Level > highestLevel)
                {
                    highestLevel = building.Level;
                }
            }

            return highestLevel;
        }

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private static bool ContainsBuildingCoord(WorldBuilding building, Vector3Int coord)
        {
            if (building == null)
            {
                return false;
            }

            if (DataManager.Instance.WorldBuilding == null ||
                !DataManager.Instance.WorldBuilding.TryGet(building.ConfigId, out WorldBuildingConfig config) ||
                config == null)
            {
                return building.Coord == coord;
            }

            return WorldBuildingFootprint.Contains(
                building.Coord,
                WorldBuildingFootprint.GetSizeX(config),
                WorldBuildingFootprint.GetSizeZ(config),
                coord);
        }

        private int AllocateInstanceId()
        {
            while (buildings.ContainsKey(nextInstanceId))
            {
                nextInstanceId++;
            }

            return nextInstanceId++;
        }

        private static int GetCurrentMapId()
        {
            return MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
        }

        private void CreateView(WorldBuilding building)
        {
            if (building == null || buildingViews.ContainsKey(building.InstanceId))
            {
                return;
            }

            if (!DataManager.Instance.WorldBuilding.TryGet(building.ConfigId, out WorldBuildingConfig config) || config == null)
            {
                return;
            }

            EnsureBuildingRoot();
            int sizeX = WorldBuildingFootprint.GetSizeX(config);
            int sizeZ = WorldBuildingFootprint.GetSizeZ(config);
            Vector3 position = WorldBuildingFootprint.GetCenterWorldPosition(building.Coord, sizeX, sizeZ, MapManager.Instance.TileSize) + Vector3.up * MapManager.Instance.TileSize;
            GameObject instance = null;

            string prefabLocation = GetPrefabLocation(config);
            if (!string.IsNullOrWhiteSpace(prefabLocation))
            {
                GameObject prefab = ResourceManager.Instance.LoadGameObject(prefabLocation);
                if (prefab != null)
                {
                    instance = GameObject.Instantiate(prefab, position, prefab.transform.rotation, buildingRoot);
                }
                else
                {
                    Debug.LogError($"Create world building view failed. Missing prefab. buildingId: {building.ConfigId}, location: {prefabLocation}");
                    return;
                }
            }

            if (instance == null)
            {
                instance = CreateFallbackBuildingView(config, position);
            }

            if (instance == null)
            {
                return;
            }

            instance.name = $"WorldBuilding_{building.ConfigId}_{building.InstanceId}_{building.Coord.x}_{building.Coord.y}_{building.Coord.z}";
            buildingViews.Add(building.InstanceId, instance);
        }

        private GameObject CreateFallbackBuildingView(WorldBuildingConfig config, Vector3 position)
        {
            PrimitiveType primitiveType = PrimitiveType.Cube;
            Vector3 scale = new Vector3(0.82f, 0.56f, 0.82f);
            Color color = new Color(0.45f, 0.45f, 0.48f);

            switch ((WorldBuildingType)config.BuildingType)
            {
                case WorldBuildingType.House:
                    scale = new Vector3(0.95f, 0.9f, 0.95f);
                    color = new Color(0.20f, 0.42f, 0.85f);
                    break;

                case WorldBuildingType.Warehouse:
                    scale = new Vector3(0.9f, 0.62f, 0.9f);
                    color = new Color(0.58f, 0.42f, 0.24f);
                    break;

                case WorldBuildingType.Workbench:
                    scale = new Vector3(0.86f, 0.42f, 0.86f);
                    color = new Color(0.55f, 0.35f, 0.18f);
                    break;

                case WorldBuildingType.CarpentryBench:
                    scale = new Vector3(0.86f, 0.48f, 0.86f);
                    color = new Color(0.62f, 0.39f, 0.16f);
                    break;

                case WorldBuildingType.Furnace:
                    scale = new Vector3(0.84f, 0.7f, 0.84f);
                    color = new Color(0.42f, 0.36f, 0.32f);
                    break;

                case WorldBuildingType.Blacksmith:
                    scale = new Vector3(0.9f, 0.65f, 0.9f);
                    color = new Color(0.25f, 0.28f, 0.32f);
                    break;

                case WorldBuildingType.Mill:
                    primitiveType = PrimitiveType.Cylinder;
                    scale = new Vector3(0.72f, 0.55f, 0.72f);
                    color = new Color(0.78f, 0.70f, 0.42f);
                    break;

                case WorldBuildingType.Mine:
                    scale = new Vector3(0.88f, 0.62f, 0.88f);
                    color = new Color(0.34f, 0.34f, 0.36f);
                    break;
            }

            int sizeX = WorldBuildingFootprint.GetSizeX(config);
            int sizeZ = WorldBuildingFootprint.GetSizeZ(config);
            scale = new Vector3(scale.x * sizeX, scale.y, scale.z * sizeZ);

            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.transform.SetParent(buildingRoot, false);
            instance.transform.position = position;
            instance.transform.localScale = scale;

            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                GameObject.Destroy(collider);
            }

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(FindRuntimeColorShader());
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else
                {
                    material.color = color;
                }

                renderer.sharedMaterial = material;
            }

            return instance;
        }

        public static string GetPrefabLocation(WorldBuildingConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(config.PrefabLocation))
            {
                return config.PrefabLocation;
            }

            return string.Empty;
        }

        private void DestroyView(int instanceId)
        {
            if (!buildingViews.TryGetValue(instanceId, out GameObject instance))
            {
                return;
            }

            if (instance != null)
            {
                GameObject.Destroy(instance);
            }

            buildingViews.Remove(instanceId);
        }

        private void EnsureBuildingRoot()
        {
            if (buildingRoot != null)
            {
                return;
            }

            GameObject rootObject = GameObject.Find("WorldBuildingRoot");
            if (rootObject == null)
            {
                rootObject = new GameObject("WorldBuildingRoot");
                rootObject.transform.position = Vector3.zero;
            }

            buildingRoot = rootObject.transform;
        }

        private static MapObjectData CreateMapObject(WorldBuilding building)
        {
            return new MapObjectData(
                building.InstanceId,
                MapObjectType.Building,
                building.ConfigId,
                building.Coord,
                Vector3.zero,
                Vector3.zero,
                Vector3.one)
            {
                BlocksBuild = true,
                BlocksMove = true,
            };
        }

        private static WorldBuildingStatus ToStatus(int value)
        {
            WorldBuildingStatus status = (WorldBuildingStatus)value;
            if (status == WorldBuildingStatus.Constructing || status == WorldBuildingStatus.Active)
            {
                return status;
            }

            return WorldBuildingStatus.Active;
        }

        private static Shader FindRuntimeColorShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Sprites/Default");
        }
    }
}
