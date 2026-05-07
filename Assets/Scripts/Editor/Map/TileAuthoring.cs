using Unity.Mathematics;
using UnityEngine;

public sealed class TileAuthoring : MonoBehaviour
{
    public int3 coord;
    public TileType type;
    public bool isBuildable = true;
    public bool isSpawn;
    public bool isBase;

    public void ApplyFromData(TileJsonData data)
    {
        coord = data.coord;
        type = (TileType)data.type;
        isBuildable = data.isBuildable;
        RefreshTransform();
    }

    public TileJsonData ToData()
    {
        return new TileJsonData
        {
            coord = coord,
            type = (int)type,
            isBuildable = isBuildable
        };
    }

    public void RefreshTransform()
    {
        transform.position = new Vector3(coord.x, coord.y, coord.z);
        gameObject.name = $"Tile_{coord.x}_{coord.y}_{coord.z}_{type}";
    }
}
