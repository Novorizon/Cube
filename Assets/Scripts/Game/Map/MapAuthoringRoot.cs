using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 地图编辑根节点。
/// 
/// 这个脚本挂在场景里的 MapAuthoringRoot 对象上。
/// 它负责：
/// - 新建 3D 体素地图
/// - 从 JSON 重建编辑器场景
/// - 从当前场景导出 JSON 数据
/// 
/// 注意：
/// 它虽然主要给编辑器使用，但它是 MonoBehaviour，
/// 所以不要放在 Editor 文件夹里。
/// </summary>
public sealed class MapAuthoringRoot : MonoBehaviour
{
    [SerializeField] private MapVisualLibrary visualLibrary;
    [SerializeField] private Transform tileRoot;

    public MapVisualLibrary VisualLibrary => visualLibrary;
    public Transform TileRoot => tileRoot != null ? tileRoot : transform;

    public TileView[] GetAllTiles()
    {
        return GetComponentsInChildren<TileView>(true);
    }

    /// <summary>
    /// 新建完整 3D 体素地图。
    /// 
    /// 生成范围：
    /// x: 0 ~ width - 1
    /// y: 0 ~ height - 1
    /// z: 0 ~ depth - 1
    /// 
    /// 每个坐标都会生成一个真实 cube。
    /// </summary>
    public void CreateNewMap(int width, int height, int depth, TileType defaultTileType)
    {
        ClearAllTiles();

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        depth = Mathf.Max(1, depth);

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    TileJsonData data = new TileJsonData
                    {
                        coord = new int3(x, y, z),
                        type = (int)defaultTileType,
                        isBuildable = true
                    };

                    CreateTileFromData(data);
                }
            }
        }
    }

    /// <summary>
    /// 清空当前所有地块。
    /// </summary>
    public void ClearAllTiles()
    {
        TileView[] tiles = GetAllTiles();

        for (int i = tiles.Length - 1; i >= 0; i--)
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                Object.DestroyImmediate(tiles[i].gameObject);
#endif
            }
            else
            {
                Object.Destroy(tiles[i].gameObject);
            }
        }
    }

    /// <summary>
    /// 从当前场景导出 JSON 数据。
    /// 
    /// 参数 width/height/depth 用于保留地图尺寸。
    /// 即使边缘某些 voxel 被删除，也不会因为导出而缩小地图尺寸。
    /// </summary>
    public MapJsonData ExportToJsonData(int width, int height, int depth)
    {
        TileView[] tiles = GetAllTiles();

        MapJsonData data = new MapJsonData
        {
            width = Mathf.Max(1, width),
            height = Mathf.Max(1, height),
            depth = Mathf.Max(1, depth),
            tiles = new List<TileJsonData>(tiles.Length)
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            TileView tile = tiles[i];

            data.tiles.Add(tile.ToData());

            if (tile.IsSpawn)
            {
                data.spawnPoints.Add(tile.Coord);
            }

            if (tile.IsBase)
            {
                data.basePoints.Add(tile.Coord);
            }
        }

        return data;
    }

    /// <summary>
    /// 根据 JSON 数据重建编辑器场景。
    /// </summary>
    public void RebuildFromJsonData(MapJsonData data)
    {
        ClearAllTiles();

        if (data == null)
        {
            return;
        }

        Dictionary<int3, TileView> created = new();

        for (int i = 0; i < data.tiles.Count; i++)
        {
            TileJsonData tileData = data.tiles[i];
            TileView tile = CreateTileFromData(tileData);

            if (tile != null)
            {
                created[tile.Coord] = tile;
            }
        }

        for (int i = 0; i < data.spawnPoints.Count; i++)
        {
            int3 coord = data.spawnPoints[i];

            if (created.TryGetValue(coord, out TileView tile))
            {
                tile.SetSpawn(true);
            }
        }

        for (int i = 0; i < data.basePoints.Count; i++)
        {
            int3 coord = data.basePoints[i];

            if (created.TryGetValue(coord, out TileView tile))
            {
                tile.SetBase(true);
            }
        }
    }

    /// <summary>
    /// 根据 TileJsonData 创建一个具体 prefab。
    /// </summary>
    private TileView CreateTileFromData(TileJsonData data)
    {
        if (visualLibrary == null)
        {
            Debug.LogError("MapAuthoringRoot 缺少 MapVisualLibrary。");
            return null;
        }

        TileType type = (TileType)data.type;
        GameObject prefab = visualLibrary.GetPrefab(type, data.coord);

        if (prefab == null)
        {
            Debug.LogError($"没有找到地块 prefab。type={type}, coord={data.coord}");
            return null;
        }

        GameObject instance = null;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            instance = PrefabUtility.InstantiatePrefab(prefab, TileRoot) as GameObject;
#endif
        }
        else
        {
            instance = Instantiate(prefab, TileRoot);
        }

        if (instance == null)
        {
            return null;
        }

        TileView tileView = instance.GetComponent<TileView>();
        if (tileView == null)
        {
            Debug.LogError($"地块 prefab 必须挂 TileView：{prefab.name}");

            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                DestroyImmediate(instance);
#endif
            }
            else
            {
                Destroy(instance);
            }

            return null;
        }

        tileView.ApplyFromData(data, visualLibrary.cellSize, visualLibrary.heightStep);
        return tileView;
    }
}