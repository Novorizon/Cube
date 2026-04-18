using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class MapAuthoringRoot : MonoBehaviour
{
    [SerializeField] private TileAuthoring tilePrefab;
    [SerializeField] private Transform tileRoot;

    public TileAuthoring TilePrefab => tilePrefab;
    public Transform TileRoot => tileRoot != null ? tileRoot : transform;

    public TileAuthoring[] GetAllTiles()
    {
        return GetComponentsInChildren<TileAuthoring>(true);
    }

    public void ClearAllTiles()
    {
        TileAuthoring[] tiles = GetAllTiles();
        for (int i = tiles.Length - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(tiles[i].gameObject);
            }
            else
#endif
            {
                Object.Destroy(tiles[i].gameObject);
            }
        }
    }

    public MapJsonData ExportToJsonData()
    {
        TileAuthoring[] tiles = GetAllTiles();

        MapJsonData data = new MapJsonData();
        data.tiles = new List<TileJsonData>(tiles.Length);

        int maxX = 0;
        int maxZ = 0;

        for (int i = 0; i < tiles.Length; i++)
        {
            TileAuthoring tile = tiles[i];
            data.tiles.Add(tile.ToData());

            if (tile.isSpawn)
            {
                data.spawnPoints.Add(tile.coord);
            }

            if (tile.isBase)
            {
                data.basePoints.Add(tile.coord);
            }

            if (tile.coord.x > maxX)
            {
                maxX = tile.coord.x;
            }

            if (tile.coord.z > maxZ)
            {
                maxZ = tile.coord.z;
            }
        }

        data.width = maxX + 1;
        data.depth = maxZ + 1;
        return data;
    }

    public void RebuildFromJsonData(MapJsonData data)
    {
        ClearAllTiles();

        if (data == null || tilePrefab == null)
        {
            return;
        }

        Dictionary<int3, TileAuthoring> created = new();

        for (int i = 0; i < data.tiles.Count; i++)
        {
            TileJsonData tileData = data.tiles[i];
            TileAuthoring tile = Instantiate(tilePrefab, TileRoot);
            tile.ApplyFromData(tileData);
            created[tile.coord] = tile;
        }

        for (int i = 0; i < data.spawnPoints.Count; i++)
        {
            int3 coord = data.spawnPoints[i];
            if (created.TryGetValue(coord, out TileAuthoring tile))
            {
                tile.isSpawn = true;
            }
        }

        for (int i = 0; i < data.basePoints.Count; i++)
        {
            int3 coord = data.basePoints[i];
            if (created.TryGetValue(coord, out TileAuthoring tile))
            {
                tile.isBase = true;
            }
        }
    }
}
