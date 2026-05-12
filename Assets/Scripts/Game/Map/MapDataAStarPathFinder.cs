using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 基于 MapData 的 A* 寻路。
    /// 
    /// 用途：
    /// 1. 编辑器中检查 SpawnPoint 到 GoalPoint 是否有路。
    /// 2. 后续也可以给运行时做基础路径测试。
    /// 
    /// 当前寻路规则：
    /// 1. Soil 不参与寻路。
    /// 2. Water 不可走。
    /// 3. Grass / Snow / Hill 可走。
    /// 4. 被上层地块覆盖的地块不可走。
    /// 5. 前后左右移动。
    /// 6. 相邻列取顶层逻辑地块作为候选节点。
    /// 7. 高度差超过 MaxStepHeight 时不可走。
    /// 8. 路径包含起点和终点。
    /// </summary>
    public sealed class MapDataAStarPathFinder
    {
        private const int MaxStepHeight = 1;

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly List<Vector3Int> neighborBuffer = new List<Vector3Int>();

        public bool TryFindPath(MapData mapData, Vector3Int start, Vector3Int goal, List<Vector3Int> result)
        {
            if (result == null)
            {
                return false;
            }

            result.Clear();

            if (mapData == null)
            {
                Debug.LogWarning("AStar failed. MapData is null.");
                return false;
            }

            mapData.EnsureRuntimeCollections();
            RebuildTileIndex(mapData);

            if (!IsWalkable(start))
            {
                Debug.LogWarning($"AStar failed. Start is not walkable: {start}");
                return false;
            }

            if (!IsWalkable(goal))
            {
                Debug.LogWarning($"AStar failed. Goal is not walkable: {goal}");
                return false;
            }

            if (start == goal)
            {
                result.Add(start);
                return true;
            }

            Dictionary<Vector3Int, PathNode> allNodes = new Dictionary<Vector3Int, PathNode>();
            List<PathNode> openList = new List<PathNode>();

            PathNode startNode = GetOrCreateNode(start, allNodes);
            startNode.GCost = 0;
            startNode.HCost = GetHeuristicCost(start, goal);
            startNode.Opened = true;

            openList.Add(startNode);

            while (openList.Count > 0)
            {
                PathNode currentNode = GetBestOpenNode(openList);

                if (currentNode.Coord == goal)
                {
                    BuildPath(currentNode, result);
                    return true;
                }

                openList.Remove(currentNode);
                currentNode.Closed = true;

                GetWalkableNeighbors(currentNode.Coord, neighborBuffer);

                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    Vector3Int neighborCoord = neighborBuffer[i];

                    if (!IsWalkable(neighborCoord))
                    {
                        continue;
                    }

                    PathNode neighborNode = GetOrCreateNode(neighborCoord, allNodes);

                    if (neighborNode.Closed)
                    {
                        continue;
                    }

                    int moveCost = GetMoveCost(neighborCoord);

                    if (moveCost == int.MaxValue)
                    {
                        continue;
                    }

                    int heightCost = GetHeightExtraCost(currentNode.Coord, neighborCoord);
                    int newGCost = currentNode.GCost + moveCost + heightCost;

                    if (!neighborNode.Opened || newGCost < neighborNode.GCost)
                    {
                        neighborNode.GCost = newGCost;
                        neighborNode.HCost = GetHeuristicCost(neighborCoord, goal);
                        neighborNode.Parent = currentNode;

                        if (!neighborNode.Opened)
                        {
                            neighborNode.Opened = true;
                            openList.Add(neighborNode);
                        }
                    }
                }
            }

            return false;
        }

        private void RebuildTileIndex(MapData mapData)
        {
            tileMap.Clear();

            if (mapData == null || mapData.Tiles == null)
            {
                return;
            }

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                tile.ApplyDefaultLogicByType(tile.Type);

                Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
                tileMap[coord] = tile;
            }
        }

        private bool IsWalkable(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tile.Type))
            {
                return false;
            }

            if (!MapTileRule.IsWalkableTileType(tile.Type))
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            return true;
        }

        private bool IsExposed(Vector3Int coord)
        {
            Vector3Int aboveCoord = new Vector3Int(coord.x, coord.y + 1, coord.z);
            return !tileMap.ContainsKey(aboveCoord);
        }

        private int GetMoveCost(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                return int.MaxValue;
            }

            if (!IsWalkable(coord))
            {
                return int.MaxValue;
            }

            return tile.MoveCost;
        }

        private void GetWalkableNeighbors(Vector3Int coord, List<Vector3Int> results)
        {
            results.Clear();

            TryAddWalkableNeighborByColumn(results, coord, coord.x + 1, coord.z);
            TryAddWalkableNeighborByColumn(results, coord, coord.x - 1, coord.z);
            TryAddWalkableNeighborByColumn(results, coord, coord.x, coord.z + 1);
            TryAddWalkableNeighborByColumn(results, coord, coord.x, coord.z - 1);
        }

        private void TryAddWalkableNeighborByColumn(List<Vector3Int> results, Vector3Int fromCoord, int targetX, int targetZ)
        {
            if (!TryGetTopLogicTile(targetX, targetZ, out MapTileData targetTile))
            {
                return;
            }

            Vector3Int targetCoord = new Vector3Int(targetTile.X, targetTile.Y, targetTile.Z);

            if (!IsWalkable(targetCoord))
            {
                return;
            }

            int heightDelta = Mathf.Abs(targetCoord.y - fromCoord.y);

            if (heightDelta > MaxStepHeight)
            {
                return;
            }

            results.Add(targetCoord);
        }

        private bool TryGetTopLogicTile(int x, int z, out MapTileData tile)
        {
            tile = null;

            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, MapTileData> pair in tileMap)
            {
                Vector3Int coord = pair.Key;
                MapTileData currentTile = pair.Value;

                if (coord.x != x || coord.z != z)
                {
                    continue;
                }

                if (!MapTileRule.IsLogicTile(currentTile.Type))
                {
                    continue;
                }

                if (coord.y > topY)
                {
                    topY = coord.y;
                    tile = currentTile;
                }
            }

            return tile != null;
        }

        private PathNode GetOrCreateNode(Vector3Int coord, Dictionary<Vector3Int, PathNode> allNodes)
        {
            if (allNodes.TryGetValue(coord, out PathNode node))
            {
                return node;
            }

            node = new PathNode(coord);
            allNodes.Add(coord, node);

            return node;
        }

        private PathNode GetBestOpenNode(List<PathNode> openList)
        {
            PathNode bestNode = openList[0];

            for (int i = 1; i < openList.Count; i++)
            {
                PathNode node = openList[i];

                if (node.FCost < bestNode.FCost)
                {
                    bestNode = node;
                    continue;
                }

                if (node.FCost == bestNode.FCost && node.HCost < bestNode.HCost)
                {
                    bestNode = node;
                }
            }

            return bestNode;
        }

        private void BuildPath(PathNode endNode, List<Vector3Int> result)
        {
            result.Clear();

            PathNode currentNode = endNode;

            while (currentNode != null)
            {
                result.Add(currentNode.Coord);
                currentNode = currentNode.Parent;
            }

            result.Reverse();
        }

        private int GetHeuristicCost(Vector3Int from, Vector3Int to)
        {
            int dx = Mathf.Abs(from.x - to.x);
            int dy = Mathf.Abs(from.y - to.y);
            int dz = Mathf.Abs(from.z - to.z);

            return (dx + dy + dz) * 10;
        }

        private int GetHeightExtraCost(Vector3Int from, Vector3Int to)
        {
            int dy = to.y - from.y;

            if (dy <= 0)
            {
                return 0;
            }

            return dy * 5;
        }

        private sealed class PathNode
        {
            public Vector3Int Coord;
            public int GCost;
            public int HCost;
            public PathNode Parent;
            public bool Opened;
            public bool Closed;

            public int FCost
            {
                get
                {
                    return GCost + HCost;
                }
            }

            public PathNode(Vector3Int coord)
            {
                Coord = coord;
                GCost = int.MaxValue;
                HCost = 0;
                Parent = null;
                Opened = false;
                Closed = false;
            }
        }
    }
}