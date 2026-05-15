using Game.Framework;
using Luban;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 配置统一入口。
    /// 当前约定：
    /// DataManager.Instance.Npc.Get(id)
    /// DataManager.Instance.Tower.Get(id)
    /// DataManager.Instance.Item.Get(id)
    /// </summary>
    public class DataManager
    {
        private const string BinPathPrefix = "Assets/Data/Bin/";

        public static DataManager Instance { get; } = new DataManager();

        public ConfigTableReader<NpcConfig> Npc { get; private set; }
        public ConfigTableReader<TowerConfig> Tower { get; private set; }
        public ConfigTableReader<ItemConfig> Item { get; private set; }
        public ConfigTableReader<MapConfig> Map { get; private set; }
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
            Wave = new ConfigTableReader<WaveConfig>("TbWave", tables.TbWave.DataMap);

            Debug.Log("DataManager initialized.");
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
    }
}
