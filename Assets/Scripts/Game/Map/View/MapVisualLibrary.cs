using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Map Visual Library")]
public sealed class MapVisualLibrary : ScriptableObject
{
    public float cellSize = 1f;
    public float heightStep = 1f;
    public List<TileVisualEntry> entries = new();

    private Dictionary<TileType, TileVisualEntry> cache;

    public TileVisualEntry Get(TileType type)
    {
        EnsureCache();
        cache.TryGetValue(type, out TileVisualEntry entry);
        return entry;
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
