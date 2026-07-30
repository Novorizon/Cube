using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MapDecorationPrefabConfig", menuName = "Cube/Map/Decoration Prefab Config")]
    public class MapDecorationPrefabConfig : ScriptableObject
    {
        [Serializable]
        public class DecorationPrefabItem
        {
            [HorizontalGroup("Header", Width = 70)]
            [LabelWidth(18)]
            public int Id;

            [HorizontalGroup("Header")]
            [LabelWidth(45)]
            public string Name;

            [HorizontalGroup("Header", Width = 120)]
            [LabelWidth(60)]
            public string Category;

            [AssetsOnly]
            [LabelWidth(80)]
            public GameObject Prefab;

            [FoldoutGroup("Defaults")]
            public Vector3 DefaultLocalPosition;

            [FoldoutGroup("Defaults")]
            public Vector3 DefaultLocalEuler;

            [FoldoutGroup("Defaults")]
            public Vector3 DefaultLocalScale = Vector3.one;

            [HorizontalGroup("Flags")]
            public bool BlocksBuild;

            [HorizontalGroup("Flags")]
            public bool BlocksMove;

            [FoldoutGroup("Mini Map")]
            [LabelText("Show By Default")]
            public bool ShowOnMiniMap;

            [FoldoutGroup("Mini Map")]
            [AssetsOnly]
            [PreviewField(48f)]
            public Sprite MiniMapIcon;
        }

        [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowIndexLabels = true)]
        [OnValueChanged(nameof(OnItemsChanged), true)]
        public List<DecorationPrefabItem> Items = new List<DecorationPrefabItem>();

        private Dictionary<int, DecorationPrefabItem> itemMap;

        public DecorationPrefabItem GetItem(int id)
        {
            if (itemMap == null)
            {
                RebuildCache();
            }

            itemMap.TryGetValue(id, out DecorationPrefabItem item);
            return item;
        }

        public GameObject GetPrefab(int id)
        {
            DecorationPrefabItem item = GetItem(id);
            return item != null ? item.Prefab : null;
        }

        public int GetNextId()
        {
            int maxId = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                DecorationPrefabItem item = Items[i];
                if (item != null && item.Id > maxId) maxId = item.Id;
            }

            return maxId + 1;
        }

        public void RebuildCache()
        {
            itemMap = new Dictionary<int, DecorationPrefabItem>();

            for (int i = 0; i < Items.Count; i++)
            {
                DecorationPrefabItem item = Items[i];

                if (item == null)
                {
                    continue;
                }

                itemMap[item.Id] = item;
            }
        }

        [Button("Normalize Ids")]
        public void NormalizeIds()
        {
            if (Items == null)
            {
                Items = new List<DecorationPrefabItem>();
            }

            HashSet<int> usedIds = new HashSet<int>();
            int nextId = 1;

            for (int i = 0; i < Items.Count; i++)
            {
                DecorationPrefabItem item = Items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Id <= 0 || usedIds.Contains(item.Id))
                {
                    while (usedIds.Contains(nextId)) nextId++;
                    item.Id = nextId;
                }

                usedIds.Add(item.Id);
                if (string.IsNullOrEmpty(item.Name) && item.Prefab != null)
                {
                    item.Name = item.Prefab.name;
                }
            }

            RebuildCache();
        }

        private void OnItemsChanged()
        {
            NormalizeIds();
        }
    }
}
