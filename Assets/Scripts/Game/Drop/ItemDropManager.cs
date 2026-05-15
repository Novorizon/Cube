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
            ItemConfig config = DataManager.Instance.Item.Get(itemId);

            if (config == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(config.DropPrefabLocation))
            {
                ItemManager.Instance.AddItem(itemId, count);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(config.DropPrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Drop prefab not found. itemId: {itemId}, location: {config.DropPrefabLocation}");
                ItemManager.Instance.AddItem(itemId, count);
                return;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            ItemDrop itemDrop = instance.GetComponent<ItemDrop>();

            if (itemDrop == null)
            {
                itemDrop = instance.AddComponent<ItemDrop>();
            }

            itemDrop.Initialize(itemId, count, config.AutoPick);
        }
    }
}