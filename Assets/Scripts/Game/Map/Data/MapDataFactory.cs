/// <summary>
/// 把 JSON 地图数据转换成运行时地图数据。
/// </summary>
public static class MapDataFactory
{
    public static MapData Create(MapJsonData json)
    {
        MapData map = new MapData(
            json.version,
            json.mapId,
            json.mapName,
            json.width,
            json.height,
            json.depth);

        for (int i = 0; i < json.tiles.Count; i++)
        {
            TileJsonData tile = json.tiles[i];
            TileType type = (TileType)tile.type;

            TileData data = new TileData
            {
                Exists = true,
                Coord = tile.coord,
                Type = type,
                IsBuildable = tile.isBuildable,

                // 这里先简单处理。
                // 后面寻路系统可以根据 TileType 再细化规则。
                IsWalkable = type != TileType.Water,

                HasTower = false,
                HasBridge = false
            };

            map.SetTile(data);
        }

        map.SpawnPoints.AddRange(json.spawnPoints);
        map.BasePoints.AddRange(json.basePoints);

        return map;
    }
}