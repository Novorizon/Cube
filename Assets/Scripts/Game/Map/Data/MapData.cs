using System.Collections.Generic;
using Unity.Mathematics;

public sealed class MapData
{
    public int Version { get; }
    public string MapId { get; }
    public string MapName { get; }
    public int Width { get; }
    public int Depth { get; }
    public TileData[] Tiles { get; }
    public List<int3> SpawnPoints { get; }
    public List<int3> BasePoints { get; }

    public MapData(int version, string mapId, string mapName, int width, int depth)
    {
        Version = version;
        MapId = mapId;
        MapName = mapName;
        Width = width;
        Depth = depth;
        Tiles = new TileData[width * depth];
        SpawnPoints = new List<int3>();
        BasePoints = new List<int3>();
    }

    public void SetTile(TileData tile)
    {
        int index = tile.Coord.z * Width + tile.Coord.x;
        Tiles[index] = tile;
    }

    public ref TileData GetTile(int x, int z)
    {
        return ref Tiles[z * Width + x];
    }

    public bool IsInBounds(int x, int z)
    {
        return x >= 0 && x < Width && z >= 0 && z < Depth;
    }
}

public struct TileData
{
    public int3 Coord;
    public TileType Type;
    public bool IsWalkable;
    public bool IsBuildable;
    public bool HasTower;
    public bool HasBridge;
}
