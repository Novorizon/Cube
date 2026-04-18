using UnityEngine;

public sealed partial class MapManager
{
    public bool LoadById(string mapId)
    {
        if (!entryById.TryGetValue(mapId, out MapEntry entry))
        {
            Debug.LogError($"Map not found by id: {mapId}");
            return false;
        }

        return SetCurrentMap(entry.CachedJson);
    }

    public bool LoadByName(string mapName)
    {
        if (!entryByName.TryGetValue(mapName, out MapEntry entry))
        {
            Debug.LogError($"Map not found by name: {mapName}");
            return false;
        }

        return SetCurrentMap(entry.CachedJson);
    }

    private bool SetCurrentMap(MapJsonData json)
    {
        if (json == null)
        {
            return false;
        }

        CurrentMap = MapDataFactory.Create(json);
        RebuildView();
        Debug.Log($"Loaded map: id={CurrentMap.MapId}, name={CurrentMap.MapName}, version={CurrentMap.Version}");
        return true;
    }
}
