using Game.Framework;
using UnityEngine;

/// <summary>
/// 负责生成 ItemDrop。
/// 当前示例使用 Resources.Load，实际项目里建议替换为你的 ResourceManager / YooAsset 加载方式。
/// </summary>
namespace Game
{
    public class ItemDropManager
    {
        public static ItemDropManager Instance { get; } = new ItemDropManager();

        private ItemDropManager()
        {
        }

        public void DropItem(int itemId, int count, Vector3 position)
        {
            DropItem(itemId, count, position, null);
        }

        public void DropItem(int itemId, int count, Vector3 position, bool? autoPickOverride)
        {
            if (count <= 0)
            {
                return;
            }

            if (DataManager.Instance.Item == null || !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) || config == null)
            {
                Debug.LogWarning($"Drop item failed. Missing item config. itemId: {itemId}");
                return;
            }

            if (string.IsNullOrEmpty(config.DropPrefabLocation))
            {
                AddItemDirectly(itemId, count, position);
                return;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.DropPrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Drop prefab not found. itemId: {itemId}, location: {config.DropPrefabLocation}");
                AddItemDirectly(itemId, count, position);
                return;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            ItemDrop itemDrop = instance.GetComponent<ItemDrop>();

            if (itemDrop == null)
            {
                itemDrop = instance.AddComponent<ItemDrop>();
            }

            bool autoPick = autoPickOverride ?? config.AutoPick;
            itemDrop.Initialize(itemId, count, autoPick);
        }

        private void AddItemDirectly(int itemId, int count, Vector3 position)
        {
            BattleItemManager.Instance.AddItem(itemId, count);
            NotifyItemFly(itemId, count, position);
        }

        private void NotifyItemFly(int itemId, int count, Vector3 position)
        {
            if (itemId <= 0 || count <= 0)
            {
                return;
            }

            ItemFlyMessage message = new ItemFlyMessage();
            message.WorldPosition = position;
            message.ItemId = itemId;
            message.Count = count;
            Messager.Instance.Notify(BattleMessageTopic.ItemFlyRequested, message);
        }
    }
}
