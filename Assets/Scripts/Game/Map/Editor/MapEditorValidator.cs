#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public static class MapEditorValidator
{
    public static bool ValidateForSave(MapJsonData currentMap, string currentFilePath, out string error)
    {
        error = null;

        if (currentMap == null)
        {
            error = "Map data is null.";
            return false;
        }

        if (currentMap.version <= 0)
        {
            error = "version 必须大于 0。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentMap.mapId))
        {
            error = "mapId 不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentMap.mapName))
        {
            error = "mapName 不能为空。";
            return false;
        }

        if (currentMap.tiles == null || currentMap.tiles.Count == 0)
        {
            error = "tiles 不能为空。";
            return false;
        }

        if (currentMap.spawnPoints == null || currentMap.spawnPoints.Count == 0)
        {
            error = "至少需要一个 spawnPoint。";
            return false;
        }

        if (currentMap.basePoints == null || currentMap.basePoints.Count == 0)
        {
            error = "至少需要一个 basePoint。";
            return false;
        }

        HashSet<int3> coords = new();
        for (int i = 0; i < currentMap.tiles.Count; i++)
        {
            int3 coord = currentMap.tiles[i].coord;
            if (!coords.Add(coord))
            {
                error = $"存在重复地块坐标: {coord}";
                return false;
            }
        }

        string mapFolder = "Assets/GameData/Maps";
        if (!Directory.Exists(mapFolder))
        {
            return true;
        }

        string normalizedCurrentPath = NormalizePath(currentFilePath);
        string[] files = Directory.GetFiles(mapFolder, "*.json", SearchOption.TopDirectoryOnly);

        for (int i = 0; i < files.Length; i++)
        {
            string file = NormalizePath(files[i]);
            if (string.Equals(file, normalizedCurrentPath, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string json = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            MapJsonData other = MapJsonIO.Deserialize(json);
            if (other == null)
            {
                continue;
            }

            if (string.Equals(other.mapId, currentMap.mapId, System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"mapId 重名: {currentMap.mapId}";
                return false;
            }

            if (string.Equals(other.mapName, currentMap.mapName, System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"mapName 重名: {currentMap.mapName}";
                return false;
            }
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace("\\", "/");
    }
}
#endif
