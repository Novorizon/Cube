using Unity.Mathematics;
using UnityEngine;

public sealed partial class MapManager
{
    private void RebuildView()
    {
        ClearView();

        if (CurrentMap == null || visualLibrary == null || mapRoot == null)
        {
            return;
        }

        for (int z = 0; z < CurrentMap.Depth; z++)
        {
            for (int x = 0; x < CurrentMap.Width; x++)
            {
                ref TileData tile = ref CurrentMap.GetTile(x, z);
                CreateTileView(tile);
            }
        }
    }

    private void CreateTileView(TileData tile)
    {
        TileVisualEntry entry = visualLibrary.Get(tile.Type);
        if (entry == null || entry.prefabs == null || entry.prefabs.Count == 0)
        {
            return;
        }

        GameObject prefab = PickPrefab(entry, tile.Coord);
        if (prefab == null)
        {
            return;
        }

        Vector3 worldPos = ToWorldPosition(tile.Coord);
        GameObject instance = GameObject.Instantiate(prefab, worldPos, Quaternion.identity, mapRoot);

        TileView tileView = instance.GetComponent<TileView>();
        if (tileView == null)
        {
            tileView = instance.AddComponent<TileView>();
        }

        tileView.Bind(tile.Coord, tile.Type);
        tileViews[tile.Coord] = tileView;
    }

    private GameObject PickPrefab(TileVisualEntry entry, int3 coord)
    {
        if (entry.prefabs.Count == 1)
        {
            return entry.prefabs[0];
        }

        int hash = math.abs(coord.x * 73856093 ^ coord.y * 19349663 ^ coord.z * 83492791);
        int index = hash % entry.prefabs.Count;
        return entry.prefabs[index];
    }

    public Vector3 ToWorldPosition(int3 coord)
    {
        float x = coord.x * visualLibrary.cellSize;
        float y = coord.y * visualLibrary.heightStep;
        float z = coord.z * visualLibrary.cellSize;
        return new Vector3(x, y, z);
    }

    private void ClearView()
    {
        tileViews.Clear();

        if (mapRoot == null)
        {
            return;
        }

        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            GameObject.Destroy(mapRoot.GetChild(i).gameObject);
        }
    }
}
