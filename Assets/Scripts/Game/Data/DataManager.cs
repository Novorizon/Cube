using Game.Framework;
using Luban;
using UnityEngine;

namespace Game
{
    public class DataManager
    {
        private const string BinPathPrefix = "Assets/Data/Bin/";

        public static DataManager Instance { get; } = new DataManager();

        public ConfigTableReader<NpcConfig> Npc { get; private set; }
        public ConfigTableReader<TowerConfig> Tower { get; private set; }
        public ConfigTableReader<ItemConfig> Item { get; private set; }
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
            Tower = new ConfigTableReader<TowerConfig>("TbTower", tables.TbTower.DataMap);
            Item = new ConfigTableReader<ItemConfig>("TbItem", tables.TbItem.DataMap);
            Map = new ConfigTableReader<MapConfig>("TbMap", tables.TbMap.DataMap);
            Skill = new ConfigTableReader<SkillConfig>("TbSkill", tables.TbSkill.DataMap);
            SkillAction = new ConfigTableReader<SkillActionConfig>("TbSkillAction", tables.TbSkillAction.DataMap);
            SkillModifier = new ConfigTableReader<SkillModifierConfig>("TbSkillModifier", tables.TbSkillModifier.DataMap);
            SkillSystemEnum = new ConfigTableReader<SkillSystemEnumConfig>("TbSkillSystemEnum", tables.TbSkillSystemEnum.DataMap);

            Wave = null;

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

        private Tables LoadTables()
        {
            return new Tables(LoadByteBuf);
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