using Game.Framework;
using Luban;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class DataManager
    {
        private const string BinPathPrefix = "Assets/Data/Bin/";

        public static DataManager Instance { get; } = new DataManager();

        public ConfigTableReader<NpcConfig> Npc { get; private set; }
        public ConfigTableReader<NpcDropConfig> NpcDrop { get; private set; }
        public ConfigTableReader<TowerConfig> Tower { get; private set; }
        public ConfigTableReader<TowerLevelConfig> TowerLevel { get; private set; }
        public ConfigTableReader<ItemConfig> Item { get; private set; }
        public IReadOnlyDictionary<string, LocalizationConfig> Localization { get; private set; }
        public ConfigTableReader<StorageCapacityConfig> StorageCapacity { get; private set; }
        public ConfigTableReader<WorldCostConfig> WorldCost { get; private set; }
        public ConfigTableReader<WorldGatherConfig> WorldGather { get; private set; }
        public ConfigTableReader<WorldRewardConfig> WorldReward { get; private set; }
        public ConfigTableReader<WorldBuildingConfig> WorldBuilding { get; private set; }
        public ConfigTableReader<WorldBuildingLevelConfig> WorldBuildingLevel { get; private set; }
        public ConfigTableReader<WorldBuildingIncomeConfig> WorldBuildingIncome { get; private set; }
        public ConfigTableReader<WorldCropConfig> WorldCrop { get; private set; }
        public ConfigTableReader<WorldResourceConfig> WorldResource { get; private set; }
        public ConfigTableReader<TechNodeConfig> TechNode { get; private set; }
        public ConfigTableReader<BaseConfig> Base { get; private set; }
        public ConfigTableReader<MapConfig> Map { get; private set; }
        public ConfigTableReader<SkillConfig> Skill { get; private set; }
        public ConfigTableReader<SkillActionConfig> SkillAction { get; private set; }
        public ConfigTableReader<SkillModifierConfig> SkillModifier { get; private set; }
        public ConfigTableReader<SkillSystemEnumConfig> SkillSystemEnum { get; private set; }

        /// <summary>
        /// 当前已加载地图对应的波次表。
        /// 注意：这不是所有地图的波次，只是当前地图的 wave bytes。
        /// </summary>
        public ConfigTableReader<WaveConfig> Wave { get; private set; }

        private Tables tables;
        private readonly Dictionary<int, List<NpcDropConfig>> npcDropMap = new Dictionary<int, List<NpcDropConfig>>();
        private readonly Dictionary<int, Dictionary<int, WorldBuildingLevelConfig>> worldBuildingLevelsByBuildingId = new Dictionary<int, Dictionary<int, WorldBuildingLevelConfig>>();
        private readonly Dictionary<int, Dictionary<int, TowerLevelConfig>> towerLevelsByTowerId = new Dictionary<int, Dictionary<int, TowerLevelConfig>>();

        private DataManager()
        {
        }

        public void Initialize()
        {
            tables = LoadTables();

            if (tables == null)
            {
                Debug.LogError("DataManager initialize failed. Tables is null.");
                return;
            }

            Npc = new ConfigTableReader<NpcConfig>("TbNpc", tables.TbNpc.DataMap);
            NpcDrop = new ConfigTableReader<NpcDropConfig>("TbNpcDrop", tables.TbNpcDrop.DataMap);
            Tower = new ConfigTableReader<TowerConfig>("TbTower", tables.TbTower.DataMap);
            TowerLevel = new ConfigTableReader<TowerLevelConfig>("TbTowerLevel", tables.TbTowerLevel.DataMap);
            Item = new ConfigTableReader<ItemConfig>("TbItem", tables.TbItem.DataMap);
            Localization = tables.TbLocalization.DataMap;
            StorageCapacity = new ConfigTableReader<StorageCapacityConfig>("TbStorageCapacity", tables.TbStorageCapacity.DataMap);
            WorldCost = new ConfigTableReader<WorldCostConfig>("TbWorldCost", tables.TbWorldCost.DataMap);
            WorldGather = new ConfigTableReader<WorldGatherConfig>("TbWorldGather", tables.TbWorldGather.DataMap);
            WorldReward = new ConfigTableReader<WorldRewardConfig>("TbWorldReward", tables.TbWorldReward.DataMap);
            WorldBuilding = new ConfigTableReader<WorldBuildingConfig>("TbWorldBuilding", tables.TbWorldBuilding.DataMap);
            WorldBuildingLevel = new ConfigTableReader<WorldBuildingLevelConfig>("TbWorldBuildingLevel", tables.TbWorldBuildingLevel.DataMap);
            WorldBuildingIncome = new ConfigTableReader<WorldBuildingIncomeConfig>("TbWorldBuildingIncome", tables.TbWorldBuildingIncome.DataMap);
            WorldCrop = new ConfigTableReader<WorldCropConfig>("TbWorldCrop", tables.TbWorldCrop.DataMap);
            WorldResource = new ConfigTableReader<WorldResourceConfig>("TbWorldResource", tables.TbWorldResource.DataMap);
            TechNode = new ConfigTableReader<TechNodeConfig>("TbTechNode", tables.TbTechNode.DataMap);
            Base = new ConfigTableReader<BaseConfig>("TbBase", tables.TbBase.DataMap);
            Map = new ConfigTableReader<MapConfig>("TbMap", tables.TbMap.DataMap);
            Skill = new ConfigTableReader<SkillConfig>("TbSkill", tables.TbSkill.DataMap);
            SkillAction = new ConfigTableReader<SkillActionConfig>("TbSkillAction", tables.TbSkillAction.DataMap);
            SkillModifier = new ConfigTableReader<SkillModifierConfig>("TbSkillModifier", tables.TbSkillModifier.DataMap);
            SkillSystemEnum = new ConfigTableReader<SkillSystemEnumConfig>("TbSkillSystemEnum", tables.TbSkillSystemEnum.DataMap);

            Wave = null;
            BuildNpcDropIndex();
            BuildWorldBuildingLevelIndex();
            BuildTowerLevelIndex();

            Debug.Log("DataManager initialized.");
        }

        public bool LoadWave(string waveLocation)
        {
            Wave = null;

            if (string.IsNullOrWhiteSpace(waveLocation))
            {
                Debug.LogError("Load wave failed. waveLocation is empty.");
                return false;
            }

            //string location = NormalizeBinLocation(waveLocation);
            string location = waveLocation;

            TextAsset textAsset = ResourceManager.Instance.LoadTextAsset(location);

            if (textAsset == null)
            {
                Debug.LogError($"Load wave failed. location: {location}");
                return false;
            }

            ByteBuf byteBuf = new ByteBuf(textAsset.bytes);
            TbWave tbWave = new TbWave(byteBuf);

            Wave = new ConfigTableReader<WaveConfig>("TbWave", tbWave.DataMap);

            Debug.Log($"Load wave success. location: {location}, count: {tbWave.DataList.Count}");

            return true;
        }

        public void ClearWave()
        {
            Wave = null;
        }

        public bool TryGetWorldBuildingLevel(int buildingId, int level, out WorldBuildingLevelConfig config)
        {
            config = null;

            if (buildingId <= 0 || level <= 0)
            {
                return false;
            }

            if (!worldBuildingLevelsByBuildingId.TryGetValue(buildingId, out Dictionary<int, WorldBuildingLevelConfig> levels) ||
                !levels.TryGetValue(level, out config))
            {
                return false;
            }

            return config != null && config.Enable;
        }

        public TowerLevelConfig GetTowerLevel(int towerId, int level)
        {
            if (TryGetTowerLevel(towerId, level, out TowerLevelConfig config))
            {
                return config;
            }

            Debug.LogError($"Tower level config not found. towerId: {towerId}, level: {level}");
            return null;
        }

        public bool TryGetTowerLevel(int towerId, int level, out TowerLevelConfig config)
        {
            config = null;

            if (towerId <= 0 || level <= 0)
            {
                return false;
            }

            if (!towerLevelsByTowerId.TryGetValue(towerId, out Dictionary<int, TowerLevelConfig> levels) ||
                !levels.TryGetValue(level, out config))
            {
                return false;
            }

            return config != null && config.Enable;
        }

        public IReadOnlyList<NpcDropConfig> GetNpcDrops(int npcId)
        {
            if (npcId <= 0)
            {
                return Array.Empty<NpcDropConfig>();
            }

            if (!npcDropMap.TryGetValue(npcId, out List<NpcDropConfig> drops))
            {
                return Array.Empty<NpcDropConfig>();
            }

            return drops;
        }

        public bool TryGetNextTowerLevel(Tower tower, out TowerLevelConfig config)
        {
            config = null;

            if (tower == null)
            {
                return false;
            }

            return TryGetTowerLevel(tower.ConfigId, tower.Level + 1, out config);
        }

        public int GetMaxTowerLevel(int towerId)
        {
            int maxLevel = 0;

            if (TowerLevel == null || TowerLevel.GetAll() == null)
            {
                return maxLevel;
            }

            foreach (KeyValuePair<int, TowerLevelConfig> pair in TowerLevel.GetAll())
            {
                TowerLevelConfig config = pair.Value;
                if (config != null && config.Enable && config.TowerId == towerId && config.Level > maxLevel)
                {
                    maxLevel = config.Level;
                }
            }

            return maxLevel;
        }

        private Tables LoadTables()
        {
            return new Tables(LoadByteBuf);
        }

        private void BuildNpcDropIndex()
        {
            npcDropMap.Clear();

            IReadOnlyDictionary<int, NpcDropConfig> configs = NpcDrop?.GetAll();
            if (configs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, NpcDropConfig> pair in configs)
            {
                NpcDropConfig config = pair.Value;
                if (config == null || config.NpcId <= 0 || config.ItemId <= 0)
                {
                    continue;
                }

                if (!npcDropMap.TryGetValue(config.NpcId, out List<NpcDropConfig> drops))
                {
                    drops = new List<NpcDropConfig>();
                    npcDropMap.Add(config.NpcId, drops);
                }

                drops.Add(config);
            }
        }

        private void BuildWorldBuildingLevelIndex()
        {
            worldBuildingLevelsByBuildingId.Clear();

            IReadOnlyDictionary<int, WorldBuildingLevelConfig> configs = WorldBuildingLevel?.GetAll();
            if (configs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, WorldBuildingLevelConfig> pair in configs)
            {
                WorldBuildingLevelConfig config = pair.Value;
                if (config == null || config.BuildingId <= 0 || config.Level <= 0)
                {
                    continue;
                }

                if (!worldBuildingLevelsByBuildingId.TryGetValue(config.BuildingId, out Dictionary<int, WorldBuildingLevelConfig> levels))
                {
                    levels = new Dictionary<int, WorldBuildingLevelConfig>();
                    worldBuildingLevelsByBuildingId.Add(config.BuildingId, levels);
                }

                levels[config.Level] = config;
            }
        }

        private void BuildTowerLevelIndex()
        {
            towerLevelsByTowerId.Clear();

            IReadOnlyDictionary<int, TowerLevelConfig> configs = TowerLevel?.GetAll();
            if (configs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, TowerLevelConfig> pair in configs)
            {
                TowerLevelConfig config = pair.Value;
                if (config == null || config.TowerId <= 0 || config.Level <= 0)
                {
                    continue;
                }

                if (!towerLevelsByTowerId.TryGetValue(config.TowerId, out Dictionary<int, TowerLevelConfig> levels))
                {
                    levels = new Dictionary<int, TowerLevelConfig>();
                    towerLevelsByTowerId.Add(config.TowerId, levels);
                }

                levels[config.Level] = config;
            }
        }

        private ByteBuf LoadByteBuf(string file)
        {
            string location = $"{BinPathPrefix}{file}.bytes";
            TextAsset textAsset = ResourceManager.Instance.LoadTextAsset(location);

            if (textAsset == null)
            {
                Debug.LogError($"Load config bytes failed. location: {location}");
                return new ByteBuf(System.Array.Empty<byte>());
            }

            return new ByteBuf(textAsset.bytes);
        }

        private string NormalizeBinLocation(string location)
        {
            if (location.StartsWith("Assets/"))
            {
                return location;
            }

            return $"{BinPathPrefix}{location}";
        }
    }
}
