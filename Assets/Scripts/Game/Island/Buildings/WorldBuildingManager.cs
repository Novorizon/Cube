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

            if (config.SizeX != 1 || config.SizeZ != 1)
            {
                Debug.LogWarning($"World building failed. Only 1x1 building is supported now. buildingId: {buildingId}");
                return false;
            }

            if (!MapManager.Instance.CanPlaceMapObject(coord))
            {
                Debug.Log($"World building failed. Tile is not buildable: {coord}");
                return false;
            }

            IReadOnlyList<WorldItem> costs = GetBuildCosts(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId > 0 && costs.Count == 0)
            {
                Debug.LogWarning($"World building failed. Empty build cost group. buildingId: {buildingId}, costGroupId: {levelConfig.BuildCostGroupId}");
                return false;
            }

            if (!WorldItemManager.Instance.TryConsumeItems(costs))
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
                WorldItemManager.Instance.AddItems(costs);
                building = null;
                Debug.LogWarning($"World building failed. Add map object failed. buildingId: {buildingId}, coord: {coord}");
                return false;
            }

            buildings.Add(instanceId, building);
            CreateView(building);
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

        public bool TryRemoveAt(Vector3Int coord)
        {
            int mapId = GetCurrentMapId();
            foreach (KeyValuePair<int, WorldBuilding> pair in buildings)
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.MapId == mapId && building.Coord == coord)
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

        public bool IsBuildingUnlocked(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding == null || !DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config))
            {
                return false;
            }

            return config != null && config.Enable && IsBuildingUnlocked(config);
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

                MapManager.Instance.TryAddMapObject(CreateMapObject(building));
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

        private IReadOnlyList<WorldItem> GetBuildCosts(int costGroupId)
        {
            if (costGroupId <= 0)
            {
                return Array.Empty<WorldItem>();
            }

            costResolver ??= new WorldCostResolver(DataManager.Instance.WorldCost);
            return costResolver.GetCostGroup(costGroupId);
        }

        private bool IsBuildingUnlocked(WorldBuildingConfig config)
        {
            if (config == null || !config.Enable)
            {
                return false;
            }

            if ((WorldBuildingType)config.BuildingType == WorldBuildingType.MainBase)
            {
                return true;
            }

            if (config.UnlockMainBaseLevel > 0 && GetHighestBuildingLevel(WorldBuildingType.MainBase) < config.UnlockMainBaseLevel)
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

            return true;
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
            Vector3 position = MapManager.Instance.GetTileWorldPosition(building.Coord) + Vector3.up * MapManager.Instance.TileSize;
            GameObject instance = null;

            if (!string.IsNullOrWhiteSpace(config.PrefabLocation))
            {
                GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);
                if (prefab != null)
                {
                    instance = GameObject.Instantiate(prefab, position, Quaternion.identity, buildingRoot);
                }
                else
                {
                    Debug.LogWarning($"Create world building view fallback. Missing prefab. buildingId: {building.ConfigId}, location: {config.PrefabLocation}");
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
                case WorldBuildingType.MainBase:
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
