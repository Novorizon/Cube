using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 地图编辑根节点。
///
/// 本脚本挂在场景中的 MapAuthoringRoot 对象上，负责：
/// - 新建程序化地形地图
/// - 从 JSON 重建编辑场景
/// - 从当前场景导出 JSON 数据
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
    /// 新建一个带地形起伏的 3D 地图。
    ///
    /// 说明：
    /// - 不再默认填满整个 x*y*z 体积。
    /// - 通过高度噪声给每个 (x,z) 生成地表高度，然后仅填充到该高度。
    /// - 顶层地块会根据高度/噪声自动混合为 Grass/Hill/Snow/Water。
    /// </summary>
    public void CreateNewMap(
        int width,
        int height,
        int depth,
        TileType defaultTileType,
        int seed,
        float heightNoiseScale,
        int minSurfaceHeight,
        int maxSurfaceHeight,
        int waterLevel,
        int snowLevel)
    {
        ClearAllTiles();

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        depth = Mathf.Max(1, depth);

        int minY = Mathf.Clamp(minSurfaceHeight, 0, height - 1);
        int maxY = Mathf.Clamp(maxSurfaceHeight, minY, height - 1);
        waterLevel = Mathf.Clamp(waterLevel, 0, height - 1);
        snowLevel = Mathf.Clamp(snowLevel, 0, height - 1);

        float safeNoiseScale = Mathf.Max(0.01f, heightNoiseScale);
        float offsetX = seed * 0.173f;
        float offsetZ = seed * 0.297f;

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float noise = Mathf.PerlinNoise((x + offsetX) / safeNoiseScale, (z + offsetZ) / safeNoiseScale);
                int surfaceY = Mathf.RoundToInt(Mathf.Lerp(minY, maxY, noise));

                for (int y = 0; y <= surfaceY; y++)
                {
                    TileType type = ResolveTileType(defaultTileType, x, y, z, surfaceY, waterLevel, snowLevel, seed);

                    TileJsonData data = new TileJsonData
                    {
                        coord = new int3(x, y, z),
                        type = (int)type,
                        isBuildable = type != TileType.Water
                    };

                    CreateTileFromData(data);
                }
            }
        }
    }

    private static TileType ResolveTileType(
        TileType fallbackType,
        int x,
        int y,
        int z,
        int surfaceY,
        int waterLevel,
        int snowLevel,
        int seed)
    {
        bool isSurface = y == surfaceY;
        if (!isSurface)
        {
            // 内部体素默认用 Hill，保证视觉上更像“地基”。
            return TileType.Hill;
        }

        if (surfaceY <= waterLevel)
        {
            return TileType.Water;
        }

        if (surfaceY >= snowLevel)
        {
            return TileType.Snow;
        }

        float biomeNoise = Mathf.PerlinNoise((x + seed * 0.11f) * 0.18f, (z + seed * 0.07f) * 0.18f);
        if (biomeNoise > 0.62f)
        {
            return TileType.Hill;
        }

        if (biomeNoise < 0.08f)
        {
            // 少量湖泊/湿地，确保新建地图不仅有 grass。
            return TileType.Water;
        }

        return fallbackType == TileType.Water ? TileType.Grass : fallbackType;
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
    /// 从当前编辑场景导出 JSON 数据。
    ///
    /// 其中 width/height/depth 用于保存地图尺寸边界。
    /// 即使某些 voxel 被删除，也仍保留原尺寸信息。
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
    /// 根据 JSON 数据重建编辑场景。
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
    /// 根据 TileJsonData 创建一个地块 prefab。
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
            Debug.LogError($"未找到地块 prefab：type={type}, coord={data.coord}");
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
            Debug.LogError($"地块 prefab 缺少 TileView：{prefab.name}");

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