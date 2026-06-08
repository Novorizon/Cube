using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class MapPathFinder
    {
        private const int UphillExtraCost = 5;
        private readonly List<Vector3Int> neighbors = new List<Vector3Int>();

        public bool TryFindPath(Vector3Int start, Vector3Int goal, out List<Vector3Int> path)
        {
            path = null;

            if (!MapManager.Instance.IsWalkable(start) || !MapManager.Instance.IsWalkable(goal))
            {
                Debug.LogWarning($"Find path failed. Start: {start}, Goal: {goal}");
                return false;
            }

            Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();
            NodeHeap openHeap = new NodeHeap();

            Node startNode = GetOrCreateNode(allNodes, start);
            startNode.G = 0;
            startNode.H = GetHeuristicCost(start, goal);
            startNode.Opened = true;
            openHeap.Add(startNode);

            while (openHeap.Count > 0)
            {
                Node current = openHeap.RemoveFirst();

                if (current.Coord == goal)
                {
                    path = BuildPath(current);
                    return true;
                }

                current.Opened = false;
                current.Closed = true;
                GetNeighbors(current.Coord, neighbors);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector3Int nextCoord = neighbors[i];

                    int moveCost = GetMoveCost(current.Coord, nextCoord);

                    if (moveCost == int.MaxValue)
                    {
                        continue;
                    }

                    int newG = current.G + moveCost;
                    Node nextNode = GetOrCreateNode(allNodes, nextCoord);

                    if (nextNode.Closed)
                    {
                        continue;
                    }

                    if (!nextNode.Opened)
                    {
                        nextNode.G = newG;
                        nextNode.H = GetHeuristicCost(nextCoord, goal);
                        nextNode.Parent = current;
                        nextNode.Opened = true;
                        openHeap.Add(nextNode);
                    }
                    else if (newG < nextNode.G)
                    {
                        nextNode.G = newG;
                        nextNode.Parent = current;
                        openHeap.Update(nextNode);
                    }
                }
            }

            return false;
        }

        private void GetNeighbors(Vector3Int coord, List<Vector3Int> results)
        {
            results.Clear();
            TryAddNeighbor(coord, coord.x + 1, coord.z, results);
            TryAddNeighbor(coord, coord.x - 1, coord.z, results);
            TryAddNeighbor(coord, coord.x, coord.z + 1, results);
            TryAddNeighbor(coord, coord.x, coord.z - 1, results);
        }

        private void TryAddNeighbor(Vector3Int fromCoord, int x, int z, List<Vector3Int> results)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData))
            {
                return;
            }

            Vector3Int toCoord = tileData.Coord;

            if (!CanConnect(fromCoord, toCoord) || !MapManager.Instance.IsWalkable(toCoord))
            {
                return;
            }

            results.Add(toCoord);
        }

        private bool CanConnect(Vector3Int fromCoord, Vector3Int toCoord)
        {
            if (!MapManager.Instance.TryGetTileData(fromCoord, out TileData fromTile) ||
                !MapManager.Instance.TryGetTileData(toCoord, out TileData toTile))
            {
                return false;
            }

            return MapPathConnectionRules.CanConnect(fromTile, toTile);
        }

        private int GetMoveCost(Vector3Int from, Vector3Int to)
        {
            int cost = MapManager.Instance.GetMoveCost(to);

            if (cost == int.MaxValue)
            {
                return int.MaxValue;
            }

            if (to.y > from.y)
            {
                cost += UphillExtraCost;
            }

            return cost;
        }

        private int GetHeuristicCost(Vector3Int from, Vector3Int to)
        {
            return 0;
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
            public bool Opened;
            public bool Closed;
            public int HeapIndex;
            public int F => G + H;

            public Node(Vector3Int coord)
            {
                Coord = coord;
                HeapIndex = -1;
            }
        }

        private sealed class NodeHeap
        {
            private readonly List<Node> items = new List<Node>();

            public int Count => items.Count;

            public void Add(Node node)
            {
                node.HeapIndex = items.Count;
                items.Add(node);
                SortUp(node);
            }

            public Node RemoveFirst()
            {
                Node first = items[0];
                int lastIndex = items.Count - 1;
                Node last = items[lastIndex];
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

            public void Update(Node node)
            {
                SortUp(node);
            }

            private void SortDown(Node node)
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

            private void SortUp(Node node)
            {
                while (node.HeapIndex > 0)
                {
                    int parentIndex = (node.HeapIndex - 1) / 2;
                    Node parent = items[parentIndex];

                    if (!IsBetter(node, parent))
                    {
                        return;
                    }

                    Swap(node, parent);
                }
            }

            private static bool IsBetter(Node left, Node right)
            {
                if (left.F != right.F)
                {
                    return left.F < right.F;
                }

                return left.H < right.H;
            }

            private void Swap(Node left, Node right)
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
