using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class RuntimeMapAStarPathFinder
    {
        private const int MaxStepHeight = 1;
        private const int StraightCost = 10;
        private const int UphillExtraCost = 5;

        private readonly List<Vector3Int> neighbors = new List<Vector3Int>();

        public bool TryFindPath(Vector3Int start, Vector3Int goal, out List<Vector3Int> path)
        {
            path = null;

            if (!MapManager.Instance.IsWalkable(start))
            {
                Debug.LogWarning($"Find path failed. Start is not walkable: {start}");
                return false;
            }

            if (!MapManager.Instance.IsWalkable(goal))
            {
                Debug.LogWarning($"Find path failed. Goal is not walkable: {goal}");
                return false;
            }

            Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();
            List<Node> openList = new List<Node>();
            HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();

            Node startNode = GetOrCreateNode(allNodes, start);
            startNode.G = 0;
            startNode.H = GetHeuristicCost(start, goal);

            openList.Add(startNode);

            while (openList.Count > 0)
            {
                Node current = GetBestNode(openList);

                if (current.Coord == goal)
                {
                    path = BuildPath(current);
                    return true;
                }

                openList.Remove(current);
                closedSet.Add(current.Coord);

                GetRuntimeNeighbors(current.Coord, neighbors);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector3Int nextCoord = neighbors[i];

                    if (closedSet.Contains(nextCoord))
                    {
                        continue;
                    }

                    int moveCost = GetMoveCost(current.Coord, nextCoord);

                    if (moveCost == int.MaxValue)
                    {
                        continue;
                    }

                    int newG = current.G + moveCost;
                    Node nextNode = GetOrCreateNode(allNodes, nextCoord);

                    if (!openList.Contains(nextNode))
                    {
                        nextNode.G = newG;
                        nextNode.H = GetHeuristicCost(nextCoord, goal);
                        nextNode.Parent = current;
                        openList.Add(nextNode);
                    }
                    else if (newG < nextNode.G)
                    {
                        nextNode.G = newG;
                        nextNode.Parent = current;
                    }
                }
            }

            return false;
        }

        private void GetRuntimeNeighbors(Vector3Int coord, List<Vector3Int> results)
        {
            results.Clear();

            TryAddRuntimeNeighbor(coord, coord.x + 1, coord.z, results);
            TryAddRuntimeNeighbor(coord, coord.x - 1, coord.z, results);
            TryAddRuntimeNeighbor(coord, coord.x, coord.z + 1, results);
            TryAddRuntimeNeighbor(coord, coord.x, coord.z - 1, results);
        }

        private void TryAddRuntimeNeighbor(Vector3Int fromCoord, int x, int z, List<Vector3Int> results)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData))
            {
                return;
            }

            Vector3Int toCoord = tileData.Coord;

            if (!CanConnect(fromCoord, toCoord))
            {
                return;
            }

            if (!MapManager.Instance.IsWalkable(toCoord))
            {
                return;
            }

            results.Add(toCoord);
        }

        private bool CanConnect(Vector3Int fromCoord, Vector3Int toCoord)
        {
            int heightDelta = toCoord.y - fromCoord.y;

            if (heightDelta == 0)
            {
                return true;
            }

            if (Mathf.Abs(heightDelta) > MaxStepHeight)
            {
                return false;
            }

            if (!MapManager.Instance.TryGetTileData(fromCoord, out TileData fromTile) ||
                !MapManager.Instance.TryGetTileData(toCoord, out TileData toTile))
            {
                return false;
            }

            Vector3Int horizontalDirection = new Vector3Int(
                Mathf.Clamp(toCoord.x - fromCoord.x, -1, 1),
                0,
                Mathf.Clamp(toCoord.z - fromCoord.z, -1, 1));

            if (heightDelta > 0)
            {
                return AllowsHeightConnection(fromTile, horizontalDirection) ||
                       AllowsHeightConnection(toTile, horizontalDirection);
            }

            return AllowsHeightConnection(fromTile, -horizontalDirection) ||
                   AllowsHeightConnection(toTile, -horizontalDirection);
        }

        private bool AllowsHeightConnection(TileData tile, Vector3Int upDirection)
        {
            if (tile == null)
            {
                return false;
            }

            if (tile.Overlay != MapTileOverlay.Stair && tile.Overlay != MapTileOverlay.Ramp)
            {
                return false;
            }

            return GetDirectionVector(tile.Direction) == upDirection;
        }

        private Vector3Int GetDirectionVector(MapDirection direction)
        {
            switch (direction)
            {
                case MapDirection.North:
                    return Vector3Int.forward;

                case MapDirection.East:
                    return Vector3Int.right;

                case MapDirection.South:
                    return Vector3Int.back;

                case MapDirection.West:
                    return Vector3Int.left;

                default:
                    return Vector3Int.zero;
            }
        }

        private int GetMoveCost(Vector3Int from, Vector3Int to)
        {
            int cost = MapManager.Instance.GetMoveCost(to);

            if (cost == int.MaxValue)
            {
                return int.MaxValue;
            }

            if (cost <= 0)
            {
                cost = StraightCost;
            }

            if (to.y > from.y)
            {
                cost += UphillExtraCost;
            }

            return cost;
        }

        private int GetHeuristicCost(Vector3Int from, Vector3Int to)
        {
            int x = Mathf.Abs(from.x - to.x);
            int y = Mathf.Abs(from.y - to.y);
            int z = Mathf.Abs(from.z - to.z);

            return (x + y + z) * StraightCost;
        }

        private Node GetBestNode(List<Node> nodes)
        {
            Node best = nodes[0];

            for (int i = 1; i < nodes.Count; i++)
            {
                Node node = nodes[i];

                if (node.F < best.F)
                {
                    best = node;
                    continue;
                }

                if (node.F == best.F && node.H < best.H)
                {
                    best = node;
                }
            }

            return best;
        }

        private Node GetOrCreateNode(Dictionary<Vector3Int, Node> nodes, Vector3Int coord)
        {
            if (nodes.TryGetValue(coord, out Node node))
            {
                return node;
            }

            node = new Node(coord);
            nodes.Add(coord, node);

            return node;
        }

        private List<Vector3Int> BuildPath(Node endNode)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            Node current = endNode;

            while (current != null)
            {
                path.Add(current.Coord);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        private sealed class Node
        {
            public Vector3Int Coord;
            public int G;
            public int H;
            public Node Parent;

            public int F
            {
                get
                {
                    return G + H;
                }
            }

            public Node(Vector3Int coord)
            {
                Coord = coord;
            }
        }
    }
}
