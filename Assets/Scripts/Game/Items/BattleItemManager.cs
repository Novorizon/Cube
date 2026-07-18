using Game;
using Game.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores items that only exist during a tower-defense battle.
/// Persistent inventory is owned by Game.ItemManager.
/// </summary>
public sealed class BattleItemManager
{
    public static BattleItemManager Instance { get; } = new BattleItemManager();

    private readonly Dictionary<int, BattleItemData> itemMap = new Dictionary<int, BattleItemData>();

    public event Action<int, int> OnItemChanged;

    private BattleItemManager()
    {
    }

    public void Initialize()
    {
        itemMap.Clear();

        AddItem(ItemIds.Gold, 300);

        Debug.Log("BattleItemManager initialized for TD battle.");
    }

    public int GetCount(int itemId)
    {
        if (itemMap.TryGetValue(itemId, out BattleItemData itemData))
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

        if (!itemMap.TryGetValue(itemId, out BattleItemData itemData))
        {
            itemData = new BattleItemData(itemId, 0);
            itemMap.Add(itemId, itemData);
        }

        itemData.Count += count;

        OnItemChanged?.Invoke(itemId, itemData.Count);

        Debug.Log($"Item added. itemId: {itemId}, count: {count}, current: {itemData.Count}");

        Notify(itemId);
    }

    public bool TryConsume(int itemId, int count)
    {
        if (count <= 0)
        {
            return true;
        }

        if (!itemMap.TryGetValue(itemId, out BattleItemData itemData))
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

        Notify(itemId);

        return true;
    }

    public IReadOnlyDictionary<int, BattleItemData> GetAllItems()
    {
        return itemMap;
    }

    public void Clear()
    {
        itemMap.Clear();
        //Notify(itemId);
    }
    private void Notify(int itemId)
    {
        switch(itemId)
        {
            case ItemIds.Gold:
                GoldsMessage message = new GoldsMessage();
                message.Gold = GetCount(ItemIds.Gold);
                Messager.Instance.Notify(BattleMessageTopic.GoldChanged, message);
                break;
            default:
                break;
        }
    }
}
