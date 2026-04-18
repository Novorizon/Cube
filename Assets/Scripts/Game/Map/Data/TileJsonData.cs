using System;
using Unity.Mathematics;

[Serializable]
public sealed class TileJsonData
{
    public int3 coord;
    public int type;
    public bool isBuildable = true;
}
