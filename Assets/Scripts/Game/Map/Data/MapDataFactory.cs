using Unity.Mathematics;

public static class MapDataFactory
{
    public static MapData Create(MapJsonData json)
    {
        MapData map = new MapData(
            json.version,
            json.mapId,
            json.mapName,
            json.width,
            json.depth);

        for (int i = 0; i < json.tiles.Count; i++)
        {
            TileJsonData tile = json.tiles[i];
            TileType type = (TileType)tile.type;

            map.SetTile(new TileData
            {
                Coord = tile.coord,
                Type = type,
                IsBuildable = tile.isBuildable,
                IsWalkable = type != TileType.Water,
                HasTower = false,
                HasBridge = false
            });
        }

        map.SpawnPoints.AddRange(json.spawnPoints);
        map.BasePoints.AddRange(json.basePoints);
        return map;
    }
}
