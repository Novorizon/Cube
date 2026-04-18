using Unity.Mathematics;
using UnityEngine;

public sealed class TileView : MonoBehaviour
{
    public int3 Coord { get; private set; }
    public TileType Type { get; private set; }

    public void Bind(int3 coord, TileType type)
    {
        Coord = coord;
        Type = type;
    }
}
