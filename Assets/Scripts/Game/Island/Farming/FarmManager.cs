using System;
using System.Collections.Generic;
using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class WorldCropDefinition
    {
        public int Id;
        public string Name;
        public int SeedItemId;
        public int SeedCost;
        public int OutputItemId;
        public int GrowSeconds;
        public int OutputCountPerSecond;
        public Color PlotColor;
        public Color CropColor;
    }

    public sealed class Farm
    {
        private readonly List<Vector3Int> cells = new List<Vector3Int>();

        public int FarmId { get; }
        public int MapId { get; }
        public int CropId { get; private set; }
        public long PlantedAtUnixTime { get; private set; }
        public long MatureAtUnixTime { get; private set; }
        public long NextIncomeAtUnixTime { get; private set; }

        public Farm(int farmId, int mapId, IReadOnlyList<Vector3Int> farmCells, int cropId, long plantedAtUnixTime, long matureAtUnixTime, long nextIncomeAtUnixTime)
        {
            FarmId = farmId;
            MapId = mapId;
            CropId = cropId;
            PlantedAtUnixTime = plantedAtUnixTime;
            MatureAtUnixTime = matureAtUnixTime;
            NextIncomeAtUnixTime = nextIncomeAtUnixTime;

            if (farmCells != null)
            {
                for (int i = 0; i < farmCells.Count; i++)
                {
                    if (!cells.Contains(farmCells[i]))
                    {
                        cells.Add(farmCells[i]);
                    }
                }
            }
        }

        public IReadOnlyList<Vector3Int> Cells => cells;
        public int CellCount => cells.Count;
        public bool HasCrop => CropId > 0;

        public void Plant(int cropId, long currentUnixTime, int growSeconds)
        {
            CropId = cropId;
            PlantedAtUnixTime = currentUnixTime;
            MatureAtUnixTime = currentUnixTime + Mathf.Max(0, growSeconds);
            NextIncomeAtUnixTime = MatureAtUnixTime;
        }

        public void SetNextIncomeAt(long unixTime)
        {
            NextIncomeAtUnixTime = unixTime > 0 ? unixTime : 0;
        }
    }

    public sealed class FarmManager
    {
        public const string FarmPlotPrefabPath = "Assets/Arts/Map/Farming/Prefabs/WorldFarmPlot.prefab";
        public const string CropPrefabPath = "Assets/Arts/Map/Farming/Prefabs/WorldCropSphere.prefab";

        private const int FirstFarmId = 1;

        public static FarmManager Instance { get; } = new FarmManager();

        private readonly Dictionary<int, Farm> farms = new Dictionary<int, Farm>();
        private readonly Dictionary<Vector3Int, int> farmIdByCoord = new Dictionary<Vector3Int, int>();
        private readonly Dictionary<int, WorldCropDefinition> crops = new Dictionary<int, WorldCropDefinition>();
        private readonly Dictionary<Vector3Int, GameObject> plotViews = new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Vector3Int, GameObject> cropViews = new Dictionary<Vector3Int, GameObject>();
        private GameObject farmPlotPrefab;
        private GameObject cropPrefab;
        private Transform farmRoot;
        private int nextFarmId = FirstFarmId;

        private FarmManager()
        {
        }

        public IReadOnlyDictionary<int, WorldCropDefinition> Crops => crops;

        public IReadOnlyDictionary<int, Farm> GetAllFarms()
        {
            return farms;
        }

        public int CountFarmsOnCurrentMap()
        {
            int mapId = GetCurrentMapId();
            if (mapId <= 0)
            {
                return 0;
            }

            int count = 0;
            foreach (KeyValuePair<int, Farm> pair in farms)
            {
                Farm farm = pair.Value;
                if (farm != null && farm.MapId == mapId && farm.CellCount > 0)
                {
                    count++;
                }
            }

            return count;
        }

        public void Initialize()
        {
            BuildCropConfigs();
            LoadPrefabs();
            ClearViews();
        }

        public bool UpdateIncome(long currentUnixTime)
        {
            int mapId = GetCurrentMapId();
            if (mapId <= 0)
            {
                return false;
            }

            bool changed = false;

            foreach (KeyValuePair<int, Farm> pair in farms)
            {
                Farm farm = pair.Value;
                if (farm == null || farm.MapId != mapId || !farm.HasCrop || farm.CellCount <= 0)
                {
                    continue;
                }

                if (!crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) || crop == null || crop.OutputItemId <= 0)
                {
                    continue;
                }

                if (currentUnixTime < farm.MatureAtUnixTime)
                {
                    continue;
                }

                if (farm.NextIncomeAtUnixTime <= 0)
                {
                    farm.SetNextIncomeAt(currentUnixTime);
                    changed = true;
                }

                if (currentUnixTime < farm.NextIncomeAtUnixTime)
                {
                    continue;
                }

                int passedSeconds = Mathf.Max(1, (int)(currentUnixTime - farm.NextIncomeAtUnixTime) + 1);
                int income = Mathf.Max(0, crop.OutputCountPerSecond) * farm.CellCount * passedSeconds;
                if (income > 0)
                {
                    ItemManager.Instance.AddItem(crop.OutputItemId, income);
                }

                farm.SetNextIncomeAt(farm.NextIncomeAtUnixTime + passedSeconds);
                RefreshFarmCropViews(farm);
                changed = true;
            }

            if (changed)
            {
                return true;
            }

            return false;
        }

        public bool TryGetFarm(int farmId, out Farm farm)
        {
            return farms.TryGetValue(farmId, out farm);
        }

        public bool TryGetFarmAt(Vector3Int coord, out Farm farm)
        {
            farm = null;
            if (!farmIdByCoord.TryGetValue(coord, out int farmId))
            {
                return false;
            }

            return farms.TryGetValue(farmId, out farm);
        }

        public Farm CreateFarmArea(Vector3Int a, Vector3Int b)
        {
            int mapId = GetCurrentMapId();
            if (mapId <= 0)
            {
                return null;
            }

            List<Vector3Int> cells = CollectAvailableCells(a, b);
            if (cells.Count == 0)
            {
                return null;
            }

            int farmId = AllocateFarmId();
            Farm farm = new Farm(farmId, mapId, cells, 0, 0, 0, 0);
            farms.Add(farmId, farm);

            for (int i = 0; i < cells.Count; i++)
            {
                farmIdByCoord[cells[i]] = farmId;
                CreatePlotView(farm, cells[i]);
            }

            StorageManager.Instance.MarkDirty();
            return farm;
        }

        public bool TryPlant(Farm farm, int cropId)
        {
            return TryPlant(farm, cropId, out _);
        }

        public bool TryPlant(Farm farm, int cropId, out RequirementResult requirement)
        {
            requirement = FarmRequirementChecker.CheckCanPlant(farm, cropId);
            if (!requirement.Succeeded)
            {
                return false;
            }

            WorldCropDefinition crop = crops[cropId];

            int seedCost = crop.SeedItemId > 0 ? GetSeedCostPerCell(crop) * farm.CellCount : 0;
            if (crop.SeedItemId > 0 && seedCost > 0 && !ItemManager.Instance.TryConsumeItem(crop.SeedItemId, seedCost))
            {
                requirement = FarmRequirementChecker.CheckCanPlant(farm, cropId);
                return false;
            }

            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            farm.Plant(cropId, currentUnixTime, crop.GrowSeconds);
            RefreshFarmPlotViews(farm);
            RefreshFarmCropViews(farm);
            if (crop.SeedItemId > 0 && seedCost > 0)
            {
                ItemManager.Instance.NotifyUseCompleted(crop.SeedItemId, seedCost);
            }

            StorageManager.Instance.MarkDirty();
            requirement = RequirementResult.Success();
            return true;
        }

        public SaveFarmData[] CreateSaveData()
        {
            List<SaveFarmData> result = new List<SaveFarmData>();
            foreach (KeyValuePair<int, Farm> pair in farms)
            {
                Farm farm = pair.Value;
                if (farm == null || farm.MapId <= 0 || farm.CellCount <= 0)
                {
                    continue;
                }

                SaveFarmCellData[] cells = new SaveFarmCellData[farm.CellCount];
                for (int i = 0; i < farm.CellCount; i++)
                {
                    Vector3Int coord = farm.Cells[i];
                    cells[i] = new SaveFarmCellData
                    {
                        X = coord.x,
                        Y = coord.y,
                        Z = coord.z,
                    };
                }

                result.Add(new SaveFarmData
                {
                    FarmId = farm.FarmId,
                    MapId = farm.MapId,
                    CropId = farm.CropId,
                    PlantedAtUnixTime = farm.PlantedAtUnixTime,
                    MatureAtUnixTime = farm.MatureAtUnixTime,
                    NextIncomeAtUnixTime = farm.NextIncomeAtUnixTime,
                    Cells = cells,
                });
            }

            return result.ToArray();
        }

        public void LoadSaveData(IReadOnlyList<SaveFarmData> savedFarms, IReadOnlyList<SaveWorldFarmPlotData> legacyPlots)
        {
            farms.Clear();
            farmIdByCoord.Clear();
            ClearViews();
            nextFarmId = FirstFarmId;

            if (savedFarms != null && savedFarms.Count > 0)
            {
                LoadFarms(savedFarms);
                return;
            }

            LoadLegacyPlots(legacyPlots);
        }

        public void CreateViews()
        {
            ClearViews();
            int mapId = GetCurrentMapId();
            foreach (KeyValuePair<int, Farm> pair in farms)
            {
                Farm farm = pair.Value;
                if (farm == null || farm.MapId != mapId)
                {
                    continue;
                }

                for (int i = 0; i < farm.CellCount; i++)
                {
                    CreatePlotView(farm, farm.Cells[i]);
                }

                RefreshFarmCropViews(farm);
            }
        }

        public void ClearViews()
        {
            foreach (KeyValuePair<Vector3Int, GameObject> pair in plotViews)
            {
                if (pair.Value != null)
                {
                    GameObject.Destroy(pair.Value);
                }
            }

            foreach (KeyValuePair<Vector3Int, GameObject> pair in cropViews)
            {
                if (pair.Value != null)
                {
                    GameObject.Destroy(pair.Value);
                }
            }

            plotViews.Clear();
            cropViews.Clear();
        }

        private void LoadFarms(IReadOnlyList<SaveFarmData> savedFarms)
        {
            for (int i = 0; i < savedFarms.Count; i++)
            {
                SaveFarmData saved = savedFarms[i];
                if (saved == null || saved.MapId <= 0 || saved.Cells == null || saved.Cells.Length == 0)
                {
                    continue;
                }

                List<Vector3Int> cells = new List<Vector3Int>();
                for (int j = 0; j < saved.Cells.Length; j++)
                {
                    SaveFarmCellData cell = saved.Cells[j];
                    if (cell == null)
                    {
                        continue;
                    }

                    Vector3Int coord = new Vector3Int(cell.X, cell.Y, cell.Z);
                    if (!farmIdByCoord.ContainsKey(coord))
                    {
                        cells.Add(coord);
                    }
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                int farmId = saved.FarmId > 0 ? saved.FarmId : AllocateFarmId();
                Farm farm = new Farm(
                    farmId,
                    saved.MapId,
                    cells,
                    saved.CropId,
                    saved.PlantedAtUnixTime,
                    saved.MatureAtUnixTime,
                    saved.NextIncomeAtUnixTime);

                farms[farm.FarmId] = farm;
                if (farm.FarmId >= nextFarmId)
                {
                    nextFarmId = farm.FarmId + 1;
                }

                for (int j = 0; j < farm.CellCount; j++)
                {
                    farmIdByCoord[farm.Cells[j]] = farm.FarmId;
                }
            }
        }

        private void LoadLegacyPlots(IReadOnlyList<SaveWorldFarmPlotData> legacyPlots)
        {
            if (legacyPlots == null)
            {
                return;
            }

            for (int i = 0; i < legacyPlots.Count; i++)
            {
                SaveWorldFarmPlotData saved = legacyPlots[i];
                if (saved == null || saved.MapId <= 0)
                {
                    continue;
                }

                Vector3Int coord = new Vector3Int(saved.X, saved.Y, saved.Z);
                if (farmIdByCoord.ContainsKey(coord))
                {
                    continue;
                }

                int farmId = AllocateFarmId();
                Farm farm = new Farm(
                    farmId,
                    saved.MapId,
                    new[] { coord },
                    saved.CropId,
                    saved.PlantedAtUnixTime,
                    saved.MatureAtUnixTime,
                    saved.NextIncomeAtUnixTime);

                farms.Add(farmId, farm);
                farmIdByCoord[coord] = farmId;
            }
        }

        private List<Vector3Int> CollectAvailableCells(Vector3Int a, Vector3Int b)
        {
            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.z, b.z);
            int maxZ = Mathf.Max(a.z, b.z);
            int y = a.y;
            List<Vector3Int> cells = new List<Vector3Int>();

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    if (farmIdByCoord.ContainsKey(coord))
                    {
                        continue;
                    }

                    if (!MapManager.Instance.IsBuildable(coord))
                    {
                        continue;
                    }

                    cells.Add(coord);
                }
            }

            return cells;
        }

        private int AllocateFarmId()
        {
            while (farms.ContainsKey(nextFarmId))
            {
                nextFarmId++;
            }

            return nextFarmId++;
        }

        private static int GetSeedCostPerCell(WorldCropDefinition crop)
        {
            if (crop == null || crop.SeedItemId <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, crop.SeedCost);
        }

        private void BuildCropConfigs()
        {
            crops.Clear();

            IReadOnlyDictionary<int, WorldCropConfig> configs = DataManager.Instance.WorldCrop?.GetAll();
            if (configs == null)
            {
                Debug.LogWarning("FarmManager build crops skipped. WorldCrop config table is null.");
                return;
            }

            foreach (KeyValuePair<int, WorldCropConfig> pair in configs)
            {
                WorldCropConfig config = pair.Value;
                if (config == null || !config.Enable || config.Id <= 0)
                {
                    continue;
                }

                crops[config.Id] = new WorldCropDefinition
                {
                    Id = config.Id,
                    Name = config.Name,
                    SeedItemId = config.SeedItemId,
                    SeedCost = config.SeedCost,
                    OutputItemId = config.OutputItemId,
                    GrowSeconds = config.GrowSeconds,
                    OutputCountPerSecond = config.OutputCountPerSecond,
                    PlotColor = ParseColor(config.PlotColor, new Color(0.42f, 0.25f, 0.12f)),
                    CropColor = ParseColor(config.CropColor, new Color(0.32f, 0.72f, 0.28f)),
                };
            }
        }

        private void CreatePlotView(Farm farm, Vector3Int coord)
        {
            if (farm == null || plotViews.ContainsKey(coord))
            {
                return;
            }

            RegisterFarmMapObject(farm, coord);
            EnsureFarmRoot();
            GameObject instance = CreatePlotInstance();
            if (instance == null)
            {
                return;
            }

            instance.name = $"Farm_{farm.FarmId}_Plot_{coord.x}_{coord.y}_{coord.z}";
            instance.transform.SetParent(farmRoot, false);
            instance.transform.position = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.03f);
            instance.transform.localScale = Vector3.one * MapManager.Instance.TileSize;
            RemoveCollider(instance);
            SetMaterial(instance, GetPlotColor(farm));
            plotViews.Add(coord, instance);
        }

        private static void RegisterFarmMapObject(Farm farm, Vector3Int coord)
        {
            int objectId = MakeFarmObjectId(coord);
            if (MapManager.Instance.TryGetMapObject(objectId, out _))
            {
                return;
            }

            MapObjectData mapObject = new MapObjectData(
                objectId,
                MapObjectType.Interactable,
                farm.FarmId,
                coord,
                Vector3.zero,
                Vector3.zero,
                Vector3.one)
            {
                BlocksBuild = true,
                BlocksMove = false,
            };

            MapManager.Instance.TryAddMapObject(mapObject);
        }

        private static int MakeFarmObjectId(Vector3Int coord)
        {
            unchecked
            {
                int hash = 23;
                hash = hash * 31 + coord.x;
                hash = hash * 31 + coord.y;
                hash = hash * 31 + coord.z;
                return 850000000 + Mathf.Abs(hash % 100000000);
            }
        }

        private void RefreshFarmCropViews(Farm farm)
        {
            if (farm == null)
            {
                return;
            }

            for (int i = 0; i < farm.CellCount; i++)
            {
                RefreshCropView(farm, farm.Cells[i]);
            }
        }

        private void RefreshFarmPlotViews(Farm farm)
        {
            if (farm == null)
            {
                return;
            }

            for (int i = 0; i < farm.CellCount; i++)
            {
                Vector3Int coord = farm.Cells[i];
                if (plotViews.TryGetValue(coord, out GameObject instance) && instance != null)
                {
                    SetMaterial(instance, GetPlotColor(farm));
                }
            }
        }

        private Color GetPlotColor(Farm farm)
        {
            if (farm != null && farm.HasCrop && crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) && crop != null)
            {
                return crop.PlotColor;
            }

            return new Color(0.42f, 0.25f, 0.12f);
        }

        private void RefreshCropView(Farm farm, Vector3Int coord)
        {
            if (farm == null)
            {
                return;
            }

            if (!farm.HasCrop || !crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) || crop == null)
            {
                DestroyCropView(coord);
                return;
            }

            if (!cropViews.TryGetValue(coord, out GameObject instance) || instance == null)
            {
                EnsureFarmRoot();
                instance = CreateCropInstance();
                if (instance == null)
                {
                    return;
                }

                instance.name = $"Farm_{farm.FarmId}_Crop_{crop.Name}_{coord.x}_{coord.y}_{coord.z}";
                instance.transform.SetParent(farmRoot, false);
                RemoveCollider(instance);
                cropViews[coord] = instance;
            }

            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool mature = currentUnixTime >= farm.MatureAtUnixTime;
            float scale = mature ? 0.55f : 0.32f;
            instance.transform.position = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.38f);
            instance.transform.localScale = Vector3.one * (MapManager.Instance.TileSize * scale);
            SetMaterial(instance, mature ? crop.CropColor : Color.Lerp(crop.CropColor, Color.gray, 0.45f));
        }

        private void LoadPrefabs()
        {
            farmPlotPrefab = ResourceManager.Instance.LoadGameObject(FarmPlotPrefabPath);
            cropPrefab = ResourceManager.Instance.LoadGameObject(CropPrefabPath);
        }

        private GameObject CreatePlotInstance()
        {
            if (farmPlotPrefab == null)
            {
                Debug.LogError($"Missing farm plot prefab: {FarmPlotPrefabPath}");
                return null;
            }

            return GameObject.Instantiate(farmPlotPrefab);
        }

        private GameObject CreateCropInstance()
        {
            if (cropPrefab == null)
            {
                Debug.LogError($"Missing crop prefab: {CropPrefabPath}");
                return null;
            }

            return GameObject.Instantiate(cropPrefab);
        }

        private void DestroyCropView(Vector3Int coord)
        {
            if (!cropViews.TryGetValue(coord, out GameObject instance))
            {
                return;
            }

            if (instance != null)
            {
                GameObject.Destroy(instance);
            }

            cropViews.Remove(coord);
        }

        private void EnsureFarmRoot()
        {
            if (farmRoot != null)
            {
                return;
            }

            GameObject root = GameObject.Find("FarmRoot");
            if (root == null)
            {
                root = new GameObject("FarmRoot");
            }

            farmRoot = root.transform;
        }

        private static void SetMaterial(GameObject instance, Color color)
        {
            if (instance == null)
            {
                return;
            }

            Material material = new Material(FindRuntimeColorShader());
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void RemoveCollider(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null)
                {
                    GameObject.Destroy(colliders[i]);
                }
            }
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

        private static Color ParseColor(string value, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value, out Color color))
            {
                return color;
            }

            return fallback;
        }

        private static int GetCurrentMapId()
        {
            return MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
        }
    }
}
