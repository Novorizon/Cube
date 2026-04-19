using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// 运行时地图数据。
/// 
/// 注意：
/// MapJsonData 是存储格式。
/// MapData 是运行时可修改格式。
/// 
/// 这里用一维数组保存 3D 体素数据。
/// index 计算方式：
/// index = (y * Depth + z) * Width + x
/// </summary>
public sealed class MapData
{
    public int Version { get; }
    public string MapId { get; }
    public string MapName { get; }

    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }

    public TileData[] Tiles { get; }

    public List<int3> SpawnPoints { get; }
    public List<int3> BasePoints { get; }

    public MapData(int version, string mapId, string mapName, int width, int height, int depth)
    {
        Version = version;
        MapId = mapId;
        MapName = mapName;

        Width = width;
        Height = height;
        Depth = depth;

        Tiles = new TileData[width * height * depth];

        SpawnPoints = new List<int3>();
        BasePoints = new List<int3>();
    }

    /// <summary>
    /// 设置一个 voxel。
    /// 设置后 Exists 会被认为是 true。
    /// </summary>
    public void SetTile(TileData tile)
    {
        tile.Exists = true;
        Tiles[ToIndex(tile.Coord.x, tile.Coord.y, tile.Coord.z)] = tile;
    }

    /// <summary>
    /// 获取指定坐标的 tile 引用。
    /// 调用前最好先 IsInBounds。
    /// </summary>
    public ref TileData GetTile(int x, int y, int z)
    {
        return ref Tiles[ToIndex(x, y, z)];
    }

    /// <summary>
    /// 尝试获取存在的 voxel。
    /// 如果坐标越界或这个位置是空的，返回 false。
    /// </summary>
    public bool TryGetTile(int x, int y, int z, out TileData tile)
    {
        tile = default;

        if (!IsInBounds(x, y, z))
        {
            return false;
        }

        TileData value = Tiles[ToIndex(x, y, z)];
        if (!value.Exists)
        {
            return false;
        }

        tile = value;
        return true;
    }

    public bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < Width
            && y >= 0 && y < Height
            && z >= 0 && z < Depth;
    }

    public int ToIndex(int x, int y, int z)
    {
        return (y * Depth + z) * Width + x;
    }
}

/// <summary>
/// 运行时单个体素地块。
/// 
/// Exists:
/// - true  表示这个坐标真的有 voxel
/// - false 表示这个坐标是空的
/// 
/// 对 3D 体素来说，这个字段很重要。
/// 因为地图尺寸里可能有空洞。
/// </summary>
public struct TileData
{
    public bool Exists;

    public int3 Coord;
    public TileType Type;

    public bool IsWalkable;
    public bool IsBuildable;

    public bool HasTower;
    public bool HasBridge;
}