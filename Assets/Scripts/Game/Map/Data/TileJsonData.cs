using System;
using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// 单个体素地块的 JSON 数据。
/// 
/// coord:
/// - x = 地图 X 坐标
/// - y = 地图 Y 坐标
/// - z = 地图 Z 坐标
/// 
/// type:
/// - 对应 TileType 的 int 值
/// 
/// isBuildable:
/// - 是否允许在这个 voxel 顶部或位置上建造
/// </summary>
[Serializable]
public sealed class TileJsonData
{
    public int3 coord;
    public int type;
    public bool isBuildable = true;
}