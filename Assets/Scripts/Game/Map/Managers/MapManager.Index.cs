using UnityEngine;

public sealed partial class MapManager
{
    private void BuildIndex()
    {
        entries.Clear();
        entryById.Clear();
        entryByName.Clear();

        for (int i = 0; i < mapJsonAssets.Count; i++)
        {
            TextAsset asset = mapJsonAssets[i];
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                continue;
            }

            MapJsonData json = MapJsonIO.Deserialize(asset.text);
            if (json == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(json.mapId))
            {
                Debug.LogError($"Map json missing mapId. Asset={asset.name}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(json.mapName))
            {
                Debug.LogError($"Map json missing mapName. Asset={asset.name}");
                continue;
            }

            if (entryById.ContainsKey(json.mapId))
            {
                Debug.LogError($"Duplicate mapId: {json.mapId}");
                continue;
            }

            if (entryByName.ContainsKey(json.mapName))
            {
                Debug.LogError($"Duplicate mapName: {json.mapName}");
                continue;
            }

            MapEntry entry = new MapEntry
            {
                MapId = json.mapId,
                MapName = json.mapName,
                Version = json.version,
                JsonAsset = asset,
                CachedJson = json
            };

            entries.Add(entry);
            entryById.Add(entry.MapId, entry);
            entryByName.Add(entry.MapName, entry);
        }
    }
}
