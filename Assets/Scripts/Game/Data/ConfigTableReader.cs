using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class ConfigTableReader<TConfig>
    {
        private readonly IReadOnlyDictionary<int, TConfig> configMap;
        private readonly string tableName;

        public ConfigTableReader(string tableName, IReadOnlyDictionary<int, TConfig> configMap)
        {
            this.tableName = tableName;
            this.configMap = configMap;
        }

        public TConfig Get(int id)
        {
            if (configMap != null && configMap.TryGetValue(id, out TConfig config))
            {
                return config;
            }

            Debug.LogError($"Config not found. table: {tableName}, id: {id}");
            return default;
        }

        public bool TryGet(int id, out TConfig config)
        {
            if (configMap == null)
            {
                config = default;
                return false;
            }

            return configMap.TryGetValue(id, out config);
        }

        public IReadOnlyDictionary<int, TConfig> GetAll()
        {
            return configMap;
        }
    }
}
