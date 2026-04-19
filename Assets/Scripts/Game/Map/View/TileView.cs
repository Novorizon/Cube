using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 地块 prefab 上挂的脚本。
/// 
/// 它既可以用于编辑器阶段，也可以用于运行时阶段。
/// 
/// 不要把 Editor 文件夹里的脚本挂到 prefab 上。
/// prefab 应该挂这个运行时代码里的 TileView。
/// </summary>
public sealed class TileView : MonoBehaviour
{
    [SerializeField] private int3 coord;
    [SerializeField] private TileType type;
    [SerializeField] private bool isBuildable = true;
    [SerializeField] private bool isSpawn;
    [SerializeField] private bool isBase;

    public int3 Coord => coord;
    public TileType Type => type;
    public bool IsBuildable => isBuildable;
    public bool IsSpawn => isSpawn;
    public bool IsBase => isBase;

    /// <summary>
    /// 运行时绑定。
    /// </summary>
    public void Bind(TileData data, float cellSize, float heightStep)
    {
        coord = data.Coord;
        type = data.Type;
        isBuildable = data.IsBuildable;

        RefreshTransform(cellSize, heightStep);
    }

    /// <summary>
    /// 编辑器从 JSON 重建场景时使用。
    /// </summary>
    public void ApplyFromData(TileJsonData data, float cellSize, float heightStep)
    {
        coord = data.coord;
        type = (TileType)data.type;
        isBuildable = data.isBuildable;

        RefreshTransform(cellSize, heightStep);
    }

    /// <summary>
    /// 编辑器保存地图时，把当前地块转成 JSON 数据。
    /// </summary>
    public TileJsonData ToData()
    {
        return new TileJsonData
        {
            coord = coord,
            type = (int)type,
            isBuildable = isBuildable
        };
    }

    public void SetCoord(int3 value, float cellSize, float heightStep)
    {
        coord = value;
        RefreshTransform(cellSize, heightStep);
    }

    public void SetType(TileType value)
    {
        type = value;
        RefreshName();
    }

    public void SetBuildable(bool value)
    {
        isBuildable = value;
    }

    public void SetSpawn(bool value)
    {
        isSpawn = value;
    }

    public void SetBase(bool value)
    {
        isBase = value;
    }

    public void RefreshTransform(float cellSize, float heightStep)
    {
        transform.localPosition = new Vector3(
            coord.x * cellSize,
            coord.y * heightStep,
            coord.z * cellSize);

        RefreshName();
    }

    private void RefreshName()
    {
        gameObject.name = $"Tile_{coord.x}_{coord.y}_{coord.z}_{type}";
    }
}