using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A* pathfinder based on serialized MapData.
    /// It uses MapPathConnectionRules so editor validation and runtime pathfinding can share the same tile connection rules.
    /// </summary>
    public sealed class MapDataAStarPathFinder
    {
        private readonly Dictionary<Vector3Int, MapCellData> tileMap = new Dictionary<Vector3Int, MapCellData>();
        private readonly Dictionary<Vector3Int, List<MapObjectData>> objectsByCoord = new Dictionary<Vector3Int, List<MapObjectData>>();
        private readonly Dictionary<Vector2Int, MapCellData> topLogicTileMap = new Dictionary<Vector2Int, MapCellData>();
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
            RebuildObjectIndex(mapData);

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
            PathNodeHeap openHeap = new PathNodeHeap();

            PathNode startNode = GetOrCreateNode(start, allNodes);
            startNode.GCost = 0;
            startNode.HCost = GetHeuristicCost(start, goal);
            startNode.Opened = true;

            openHeap.Add(startNode);

            while (openHeap.Count > 0)
            {
                PathNode currentNode = openHeap.RemoveFirst();

                if (currentNode.Coord == goal)
                {
                    BuildPath(currentNode, result);
                    return true;
                }

                currentNode.Opened = false;
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
                            openHeap.Add(neighborNode);
                        }
                        else
                        {
                            openHeap.Update(neighborNode);
                        }
                    }
                }
            }

            return false;
        }

        private void RebuildTileIndex(MapData mapData)
        {
            tileMap.Clear();
            topLogicTileMap.Clear();

            if (mapData == null || mapData.Cells == null)
            {
                return;
            }

            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];

                if (tile == null)
                {
                    continue;
                }

                tile.EnsureLayers();

                Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
                tileMap[coord] = tile;

                if (!MapTileRule.IsLogicTile(tile.Type))
                {
                    continue;
                }

                Vector2Int column = new Vector2Int(coord.x, coord.z);
                if (!topLogicTileMap.TryGetValue(column, out MapCellData topTile) || coord.y > topTile.Y)
                {
                    topLogicTileMap[column] = tile;
                }
            }
        }

        private void RebuildObjectIndex(MapData mapData)
        {
            objectsByCoord.Clear();

            if (mapData == null || mapData.Objects == null)
            {
                return;
            }

            for (int i = 0; i < mapData.Objects.Count; i++)
            {
                MapObjectData mapObject = mapData.Objects[i];
                if (mapObject == null)
                {
                    continue;
                }

                GetMapObjectFootprintSize(mapObject, out int sizeX, out int sizeZ);
                for (int offsetX = 0; offsetX < sizeX; offsetX++)
                {
                    for (int offsetZ = 0; offsetZ < sizeZ; offsetZ++)
                    {
                        Vector3Int coord = new Vector3Int(
                            mapObject.X + offsetX,
                            mapObject.Y,
                            mapObject.Z + offsetZ);
                        if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects))
                        {
                            objects = new List<MapObjectData>();
                            objectsByCoord[coord] = objects;
                        }

                        objects.Add(mapObject);
                    }
                }
            }
        }

        private bool IsWalkable(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapCellData tile))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tile.Type))
            {
                return false;
            }

            if (!tile.Walkable)
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            if (HasMoveBlockingObject(coord))
            {
                return false;
            }

            return true;
        }

        private bool HasMoveBlockingObject(Vector3Int coord)
        {
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject != null && mapObject.BlocksMove)
                {
                    return true;
                }
            }

            return false;
        }

        private static void GetMapObjectFootprintSize(MapObjectData mapObject, out int sizeX, out int sizeZ)
        {
            sizeX = 1;
            sizeZ = 1;
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Building)
            {
                return;
            }

            if (DataManager.Instance.WorldBuilding == null ||
                !DataManager.Instance.WorldBuilding.TryGet(mapObject.ConfigId, out WorldBuildingConfig config) ||
                config == null)
            {
                return;
            }

            sizeX = WorldBuildingFootprint.GetSizeX(config);
            sizeZ = WorldBuildingFootprint.GetSizeZ(config);
        }

        private bool IsExposed(Vector3Int coord)
        {
            Vector3Int aboveCoord = new Vector3Int(coord.x, coord.y + 1, coord.z);
            return !tileMap.ContainsKey(aboveCoord);
        }

        private int GetMoveCost(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapCellData tile))
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
            if (!TryGetTopLogicTile(targetX, targetZ, out MapCellData targetTile))
            {
                return;
            }

            Vector3Int targetCoord = new Vector3Int(targetTile.X, targetTile.Y, targetTile.Z);

            if (!IsWalkable(targetCoord))
            {
                return;
            }

            if (!CanConnect(fromCoord, targetCoord))
            {
                return;
            }

            results.Add(targetCoord);
        }

        private bool CanConnect(Vector3Int fromCoord, Vector3Int targetCoord)
        {
            if (!tileMap.TryGetValue(fromCoord, out MapCellData fromTile) ||
                !tileMap.TryGetValue(targetCoord, out MapCellData targetTile))
            {
                return false;
            }

            return MapPathConnectionRules.CanConnect(fromTile, targetTile);
        }
        private bool TryGetTopLogicTile(int x, int z, out MapCellData tile)
        {
            return topLogicTileMap.TryGetValue(new Vector2Int(x, z), out tile);
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
            return 0;
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
            public int HeapIndex;

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
                HeapIndex = -1;
            }
        }

        private sealed class PathNodeHeap
        {
            private readonly List<PathNode> items = new List<PathNode>();

            public int Count => items.Count;

            public void Add(PathNode node)
            {
                node.HeapIndex = items.Count;
                items.Add(node);
                SortUp(node);
            }

            public PathNode RemoveFirst()
            {
                PathNode first = items[0];
                int lastIndex = items.Count - 1;
                PathNode last = items[lastIndex];
                items.RemoveAt(lastIndex);

                if (items.Count > 0)
                {
                    items[0] = last;
                    last.HeapIndex = 0;
                    SortDown(last);
                }

                first.HeapIndex = -1;
                return first;
            }

            public void Update(PathNode node)
            {
                SortUp(node);
            }

            private void SortDown(PathNode node)
            {
                while (true)
                {
                    int leftChildIndex = node.HeapIndex * 2 + 1;
                    int rightChildIndex = node.HeapIndex * 2 + 2;

                    if (leftChildIndex >= items.Count)
                    {
                        return;
                    }

                    int bestChildIndex = leftChildIndex;
                    if (rightChildIndex < items.Count && IsBetter(items[rightChildIndex], items[leftChildIndex]))
                    {
                        bestChildIndex = rightChildIndex;
                    }

                    if (!IsBetter(items[bestChildIndex], node))
                    {
                        return;
                    }

                    Swap(node, items[bestChildIndex]);
                }
            }

            private void SortUp(PathNode node)
            {
                while (node.HeapIndex > 0)
                {
                    int parentIndex = (node.HeapIndex - 1) / 2;
                    PathNode parent = items[parentIndex];

                    if (!IsBetter(node, parent))
                    {
                        return;
                    }

                    Swap(node, parent);
                }
            }

            private static bool IsBetter(PathNode left, PathNode right)
            {
                if (left.FCost != right.FCost)
                {
                    return left.FCost < right.FCost;
                }

                return left.HCost < right.HCost;
            }

            private void Swap(PathNode left, PathNode right)
            {
                items[left.HeapIndex] = right;
                items[right.HeapIndex] = left;
                int leftIndex = left.HeapIndex;
                left.HeapIndex = right.HeapIndex;
                right.HeapIndex = leftIndex;
            }
        }
    }
}
