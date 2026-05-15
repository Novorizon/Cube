using Game;
using Game.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当前 ItemManager 是 TD 战斗内道具管理器。
/// 金币也是 Item。
/// 以后全局经营道具不要占用这个名字，可以使用 GlobalItemManager / GameItemManager。
/// </summary>
public class ItemManager
{
    public static ItemManager Instance { get; } = new ItemManager();

    private readonly Dictionary<int, ItemData> itemMap = new Dictionary<int, ItemData>();

    public event Action<int, int> OnItemChanged;

    private ItemManager()
    {
    }

    public void Initialize()
    {
        itemMap.Clear();

        AddItem(ItemIds.Gold, 300);

        Debug.Log("ItemManager initialized for TD battle.");
    }

    public int GetCount(int itemId)
    {
        if (itemMap.TryGetValue(itemId, out ItemData itemData))
        {
            return itemData.Count;
        }

        return 0;
    }

    public bool HasItem(int itemId, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        return GetCount(itemId) >= count;
    }

    public void AddItem(int itemId, int count)
    {
        if (itemId <= 0)
        {
            Debug.LogWarning($"Invalid item id: {itemId}");
            return;
        }

        if (count <= 0)
        {
            return;
        }

        if (!itemMap.TryGetValue(itemId, out ItemData itemData))
        {
            itemData = new ItemData(itemId, 0);
            itemMap.Add(itemId, itemData);
        }

        itemData.Count += count;

        OnItemChanged?.Invoke(itemId, itemData.Count);

        Debug.Log($"Item added. itemId: {itemId}, count: {count}, current: {itemData.Count}");


        if(itemId == ItemIds.Gold)
        {
            GoldsMessage message = new GoldsMessage();
            message.Gold = count;
            Messager.Instance.Notify(BattleMessageTopic.GoldChanged, message);
        }
    }

    public bool TryConsume(int itemId, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        if (!itemMap.TryGetValue(itemId, out ItemData itemData))
        {
            Debug.Log($"Item not enough. itemId: {itemId}, need: {count}, current: 0");
            return false;
        }

        if (itemData.Count < count)
        {
            Debug.Log($"Item not enough. itemId: {itemId}, need: {count}, current: {itemData.Count}");
            return false;
        }

        itemData.Count -= count;

        if (itemData.Count <= 0)
        {
            itemMap.Remove(itemId);
            OnItemChanged?.Invoke(itemId, 0);
        }
        else
        {
            OnItemChanged?.Invoke(itemId, itemData.Count);
        }

        Debug.Log($"Item consumed. itemId: {itemId}, count: {count}, left: {GetCount(itemId)}");

        return true;
    }

    public IReadOnlyDictionary<int, ItemData> GetAllItems()
    {
        return itemMap;
    }

    public void Clear()
    {
        itemMap.Clear();
    }
}
