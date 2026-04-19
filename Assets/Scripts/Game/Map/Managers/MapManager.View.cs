using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MapManager 的可见地图生成部分。
/// 
/// 运行时加载 MapData 后，根据每个存在的 voxel 实例化 prefab。
/// </summary>
public sealed partial class MapManager
{
    private void RebuildView()
    {
        ClearView();

        if (CurrentMap == null || visualLibrary == null || mapRoot == null)
        {
            return;
        }

        for (int i = 0; i < CurrentMap.Tiles.Length; i++)
        {
            TileData tile = CurrentMap.Tiles[i];

            if (!tile.Exists)
            {
                continue;
            }

            CreateTileView(tile);
        }
    }

    private void CreateTileView(TileData tile)
    {
        GameObject prefab = visualLibrary.GetPrefab(tile.Type, tile.Coord);

        if (prefab == null)
        {
            Debug.LogError($"没有找到地块 prefab。type={tile.Type}, coord={tile.Coord}");
            return;
        }

        Vector3 worldPosition = ToWorldPosition(tile.Coord);
        GameObject instance = GameObject.Instantiate(prefab, worldPosition, Quaternion.identity, mapRoot);

        TileView tileView = instance.GetComponent<TileView>();
        if (tileView == null)
        {
            Debug.LogError($"地块 prefab 必须挂 TileView：{prefab.name}");
            GameObject.Destroy(instance);
            return;
        }

        tileView.Bind(tile, visualLibrary.cellSize, visualLibrary.heightStep);
        tileViews[tile.Coord] = tileView;
    }

    public Vector3 ToWorldPosition(int3 coord)
    {
        return new Vector3(
            coord.x * visualLibrary.cellSize,
            coord.y * visualLibrary.heightStep,
            coord.z * visualLibrary.cellSize);
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