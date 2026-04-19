using System;
using System.Collections.Generic;
using Unity.Mathematics;

[Serializable]
public sealed class MapJsonData
{
    public int version = 1;
    public string mapId = "new_map";
    public string mapName = "New Map";

    public int width = 1;
    public int height = 1;
    public int depth = 1;

    public List<TileJsonData> tiles = new();
    public List<int3> spawnPoints = new();
    public List<int3> basePoints = new();
}
