using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 地图视觉资源库。
/// 
/// 使用 ScriptableObject 是因为：
/// - 它是配置数据
/// - 不属于某一个场景实例
/// - 可以在 Inspector 中配置 TileType -> prefab
/// 
/// 地块 prefab 本身挂 TileView。
/// 这个 SO 只负责登记资源。
/// </summary>
[CreateAssetMenu(menuName = "Cube/Map Visual Library")]
public sealed class MapVisualLibrary : ScriptableObject
{
    public float cellSize = 1f;
    public float heightStep = 1f;

    public List<TileVisualEntry> entries = new();

    private Dictionary<TileType, TileVisualEntry> cache;

    /// <summary>
    /// 根据地块类型和坐标获取 prefab。
    /// 
    /// 如果同一种类型配置了多个 prefab，
    /// 会根据坐标 hash 固定选择一个。
    /// 这样同一张地图每次加载出来外观一致。
    /// </summary>
    public GameObject GetPrefab(TileType type, int3 coord)
    {
        EnsureCache();

        if (!cache.TryGetValue(type, out TileVisualEntry entry))
        {
            return null;
        }

        if (entry.prefabs == null || entry.prefabs.Count == 0)
        {
            return null;
        }

        if (entry.prefabs.Count == 1)
        {
            return entry.prefabs[0];
        }

        int hash = math.abs(coord.x * 73856093 ^ coord.y * 19349663 ^ coord.z * 83492791);
        int index = hash % entry.prefabs.Count;

        return entry.prefabs[index];
    }

    private void EnsureCache()
    {
        if (cache != null)
        {
            return;
        }

        cache = new Dictionary<TileType, TileVisualEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            TileVisualEntry entry = entries[i];

            if (entry == null)
            {
                continue;
            }

            if (!cache.ContainsKey(entry.type))
            {
                cache.Add(entry.type, entry);
            }
        }
    }
}

[Serializable]
public sealed class TileVisualEntry
{
    public TileType type;
    public List<GameObject> prefabs = new();
}