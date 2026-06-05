using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public interface IMapOverlayConnectionRule
    {
        MapTileOverlay Overlay { get; }

        bool CanConnect(MapPathCellInfo from, MapPathCellInfo to, MapPathCellInfo overlayCell);
    }

    public static class MapPathConnectionRules
    {
        private static readonly List<IMapOverlayConnectionRule> OverlayRules = new List<IMapOverlayConnectionRule>();

        static MapPathConnectionRules()
        {
            ResetDefaultOverlayRules();
        }

        public static void ResetDefaultOverlayRules()
        {
            OverlayRules.Clear();
            RegisterOverlayRule(new StairOverlayConnectionRule());
            RegisterOverlayRule(new RampOverlayConnectionRule());
            RegisterOverlayRule(new BridgeOverlayConnectionRule());
        }

        public static void RegisterOverlayRule(IMapOverlayConnectionRule rule, bool replaceExisting = true)
        {
            if (rule == null)
            {
                return;
            }

            if (replaceExisting)
            {
                UnregisterOverlayRule(rule.Overlay);
            }

            OverlayRules.Add(rule);
        }

        public static void UnregisterOverlayRule(MapTileOverlay overlay)
        {
            for (int i = OverlayRules.Count - 1; i >= 0; i--)
            {
                if (OverlayRules[i].Overlay == overlay)
                {
                    OverlayRules.RemoveAt(i);
                }
            }
        }

        public static bool CanConnect(MapCellData from, MapCellData to)
        {
            if (from == null || to == null)
            {
                return false;
            }

            return CanConnect(MapPathCellInfo.From(from), MapPathCellInfo.From(to));
        }

        public static bool CanConnect(TileData from, TileData to)
        {
            if (from == null || to == null)
            {
                return false;
            }

            return CanConnect(MapPathCellInfo.From(from), MapPathCellInfo.From(to));
        }

        public static bool CanConnect(MapPathCellInfo from, MapPathCellInfo to)
        {
            if (!IsHorizontalNeighbor(from.Coord, to.Coord))
            {
                return false;
            }

            int heightDelta = to.Coord.y - from.Coord.y;
            if (heightDelta == 0)
            {
                return true;
            }

            return CanOverlayConnect(from, to, from) ||
                   CanOverlayConnect(from, to, to);
        }

        public static Vector3Int GetDirectionVector(MapDirection direction)
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

        private static bool CanOverlayConnect(MapPathCellInfo from, MapPathCellInfo to, MapPathCellInfo overlayCell)
        {
            if (overlayCell.Overlay == MapTileOverlay.None)
            {
                return false;
            }

            for (int i = 0; i < OverlayRules.Count; i++)
            {
                IMapOverlayConnectionRule rule = OverlayRules[i];
                if (rule.Overlay != overlayCell.Overlay)
                {
                    continue;
                }

                if (rule.CanConnect(from, to, overlayCell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHorizontalNeighbor(Vector3Int from, Vector3Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dz = Mathf.Abs(to.z - from.z);
            return dx + dz == 1;
        }
    }

    internal abstract class DirectedHeightOverlayConnectionRule : IMapOverlayConnectionRule
    {
        private readonly int maxHeightDelta;

        public abstract MapTileOverlay Overlay { get; }

        protected DirectedHeightOverlayConnectionRule(int maxHeightDelta)
        {
            this.maxHeightDelta = maxHeightDelta;
        }

        public bool CanConnect(MapPathCellInfo from, MapPathCellInfo to, MapPathCellInfo overlayCell)
        {
            int heightDelta = Mathf.Abs(to.Coord.y - from.Coord.y);
            if (heightDelta <= 0 || heightDelta > maxHeightDelta)
            {
                return false;
            }

            Vector3Int horizontalDirection = new Vector3Int(
                Mathf.Clamp(to.Coord.x - from.Coord.x, -1, 1),
                0,
                Mathf.Clamp(to.Coord.z - from.Coord.z, -1, 1));

            if (horizontalDirection == Vector3Int.zero)
            {
                return false;
            }

            Vector3Int overlayForward = MapPathConnectionRules.GetDirectionVector(overlayCell.OverlayDirection);
            if (overlayForward == Vector3Int.zero)
            {
                return false;
            }

            if (overlayCell.Coord == from.Coord)
            {
                return overlayForward == horizontalDirection;
            }

            if (overlayCell.Coord == to.Coord)
            {
                return overlayForward == -horizontalDirection;
            }

            return false;
        }
    }

    internal sealed class StairOverlayConnectionRule : DirectedHeightOverlayConnectionRule
    {
        public override MapTileOverlay Overlay
        {
            get
            {
                return MapTileOverlay.Stair;
            }
        }

        public StairOverlayConnectionRule()
            : base(1)
        {
        }
    }

    internal sealed class RampOverlayConnectionRule : DirectedHeightOverlayConnectionRule
    {
        public override MapTileOverlay Overlay
        {
            get
            {
                return MapTileOverlay.Ramp;
            }
        }

        public RampOverlayConnectionRule()
            : base(1)
        {
        }
    }

    internal sealed class BridgeOverlayConnectionRule : IMapOverlayConnectionRule
    {
        public MapTileOverlay Overlay
        {
            get
            {
                return MapTileOverlay.Bridge;
            }
        }

        public bool CanConnect(MapPathCellInfo from, MapPathCellInfo to, MapPathCellInfo overlayCell)
        {
            return from.Coord.y == to.Coord.y;
        }
    }
}
