using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MapTilePrefabConfig", menuName = "Cube/Map/Tile Prefab Config")]
    public class MapTilePrefabConfig : ScriptableObject
    {
        public const float DefaultTopLocalY = 1f;

        [Serializable]
        public class TilePrefabItem
        {
            public MapTileType Type;
            public GameObject Prefab;
            public float TopLocalY = DefaultTopLocalY;
        }

        public List<TilePrefabItem> Items = new List<TilePrefabItem>();

        private Dictionary<MapTileType, GameObject> prefabMap;
        private Dictionary<MapTileType, float> topLocalYMap;

        public GameObject GetPrefab(MapTileType type)
        {
            if (prefabMap == null)
            {
                RebuildCache();
            }

            prefabMap.TryGetValue(type, out GameObject prefab);
            return prefab;
        }

        public float GetTopLocalY(MapTileType type)
        {
            if (topLocalYMap == null)
            {
                RebuildCache();
            }

            return topLocalYMap.TryGetValue(type, out float topLocalY) ? topLocalY : DefaultTopLocalY;
        }

        public void RebuildCache()
        {
            prefabMap = new Dictionary<MapTileType, GameObject>();
            topLocalYMap = new Dictionary<MapTileType, float>();

            for (int i = 0; i < Items.Count; i++)
            {
                TilePrefabItem item = Items[i];

                if (item == null)
                {
                    continue;
                }

                prefabMap[item.Type] = item.Prefab;
                topLocalYMap[item.Type] = item.TopLocalY;
            }
        }
    }
}
