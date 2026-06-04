using UnityEngine;

namespace Game
{
    /// <summary>
    /// Runtime tile data created from a serialized map cell.
    /// </summary>
    public sealed class TileData
    {
        private readonly MapCellData mapCellData;
        private Tower tower;

        public TileData(MapCellData mapCellData)
        {
            this.mapCellData = mapCellData;
            this.mapCellData?.EnsureLayers();
        }

        public MapCellData MapCellData
        {
            get
            {
                return mapCellData;
            }
        }

        public int X => mapCellData.X;
        public int Y => mapCellData.Y;
        public int Z => mapCellData.Z;
        public Vector3Int Coord => new Vector3Int(X, Y, Z);
        public MapTileType Type => mapCellData.Tile.Type;
        public MapDirection TypeDirection => mapCellData.Tile.Direction;
        public bool Walkable => mapCellData.Walkable;
        public MapTileOverlay Overlay => mapCellData.Overlay.Type;
        public MapDirection OverlayDirection => mapCellData.Overlay.Direction;
        public bool Buildable => mapCellData.Buildable;
        public int MoveCost => mapCellData.MoveCost;
        public bool HasTower => tower != null;
        public Tower Tower => tower;

        public bool IsRuntimeWalkable
        {
            get
            {
                if (!Walkable)
                {
                    return false;
                }

                if (HasTower)
                {
                    return false;
                }

                return true;
            }
        }

        public bool IsRuntimeBuildable
        {
            get
            {
                if (!Buildable)
                {
                    return false;
                }

                if (HasTower)
                {
                    return false;
                }

                return true;
            }
        }

        public bool TrySetTower(Tower tower)
        {
            if (tower == null)
            {
                return false;
            }

            if (HasTower)
            {
                return false;
            }

            this.tower = tower;
            return true;
        }

        public void ClearTower()
        {
            tower = null;
        }
    }
}
