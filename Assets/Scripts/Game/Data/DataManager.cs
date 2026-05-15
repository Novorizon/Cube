using Game;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配置统一入口。
/// 当前约定：
/// DataManager.Instance.Npc.Get(id)
/// DataManager.Instance.Tower.Get(id)
/// DataManager.Instance.Item.Get(id)
///
/// 注意：LoadTables 需要接你当前项目里已有的 Luban bytes 加载逻辑。
/// </summary>
namespace Game
{
    public class DataManager
    {
        public static DataManager Instance { get; } = new DataManager();

        public ConfigTableReader<NpcConfig> Npc { get; private set; }
        public ConfigTableReader<TowerConfig> Tower { get; private set; }
        public ConfigTableReader<ItemConfig> Item { get; private set; }

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

            Npc = new ConfigTableReader<NpcConfig>("TbNpc", ConvertToReadOnlyMap(tables.TbNpc.DataMap));
            Tower = new ConfigTableReader<TowerConfig>("TbTower", ConvertToReadOnlyMap(tables.TbTower.DataMap));
            Item = new ConfigTableReader<ItemConfig>("TbItem", ConvertToReadOnlyMap(tables.TbItem.DataMap));

            Debug.Log("DataManager initialized.");
        }

        private Tables LoadTables()
        {
            // 这里需要接你当前项目已有的 Luban bytes 加载逻辑。
            //
            // 常见 Luban 生成代码大概会类似：
            //
            // tables = new Tables(file =>
            // {
            //     byte[] bytes = LoadBytesFromYooAssetOrResources(file);
            //     return new ByteBuf(bytes);
            // });
            //
            // 由于你的 ResourceManager / YooAsset 加载接口没有贴出来，
            // 这里不强行写死，避免生成一段无法编译的资源加载代码。
            //
            // 你只需要保证最终返回 Luban 生成的 Tables 实例即可。

            throw new NotImplementedException("Please connect this method to your existing Luban bytes loading logic.");
        }

        private IReadOnlyDictionary<int, TConfig> ConvertToReadOnlyMap<TConfig>(Dictionary<int, TConfig> map)
        {
            return map;
        }
    }
}