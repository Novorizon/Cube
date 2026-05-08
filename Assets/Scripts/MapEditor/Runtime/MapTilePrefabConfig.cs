using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MapTilePrefabConfig", menuName = "Cube/Map/Tile Prefab Config")]
    public class MapTilePrefabConfig : ScriptableObject
    {
        [Serializable]
        public class TilePrefabItem
        {
            public MapTileType Type;
            public GameObject Prefab;
        }

        public List<TilePrefabItem> Items = new List<TilePrefabItem>();

        private Dictionary<MapTileType, GameObject> prefabMap;

        public GameObject GetPrefab(MapTileType type)
        {
            if (prefabMap == null)
            {
                RebuildCache();
            }

            prefabMap.TryGetValue(type, out GameObject prefab);
            return prefab;
        }

        public void RebuildCache()
        {
            prefabMap = new Dictionary<MapTileType, GameObject>();

            for (int i = 0; i < Items.Count; i++)
            {
                TilePrefabItem item = Items[i];

                if (item == null)
                {
                    continue;
                }

                prefabMap[item.Type] = item.Prefab;
            }
        }
    }
}