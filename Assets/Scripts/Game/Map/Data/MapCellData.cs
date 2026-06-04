using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class MapCellData
    {
        public int X;
        public int Y;
        public int Z;

        public MapTileLayerData Tile = new MapTileLayerData();
        public MapOverlayLayerData Overlay = new MapOverlayLayerData();

        public bool Walkable;
        public bool Buildable;
        public int MoveCost;

        public Vector3Int Coord
        {
            get
            {
                return new Vector3Int(X, Y, Z);
            }
        }

        public MapTileType Type
        {
            get
            {
                EnsureLayers();
                return Tile.Type;
            }
            set
            {
                EnsureLayers();
                Tile.Type = value;
            }
        }

        public MapDirection TypeDirection
        {
            get
            {
                EnsureLayers();
                return Tile.Direction;
            }
            set
            {
                EnsureLayers();
                Tile.Direction = value == MapDirection.None ? MapDirection.North : value;
            }
        }

        public MapTileOverlay OverlayType
        {
            get
            {
                EnsureLayers();
                return Overlay.Type;
            }
            set
            {
                EnsureLayers();
                Overlay.Type = value;
            }
        }

        public MapDirection OverlayDirection
        {
            get
            {
                EnsureLayers();
                return Overlay.Direction;
            }
            set
            {
                EnsureLayers();
                Overlay.Direction = value;
            }
        }

        public MapCellData()
        {
        }

        public MapCellData(int x, int y, int z, MapTileType type)
        {
            X = x;
            Y = y;
            Z = z;
            Tile = new MapTileLayerData(type);
            Overlay = new MapOverlayLayerData();
            ApplyDefaultLogic();
        }

        public void EnsureLayers()
        {
            if (Tile == null)
            {
                Tile = new MapTileLayerData();
            }

            if (Overlay == null)
            {
                Overlay = new MapOverlayLayerData();
            }
        }

        public void ApplyDefaultLogicByType(MapTileType type)
        {
            EnsureLayers();
            Tile.Type = type;
            ApplyDefaultLogic();
        }

        public void ApplyDefaultLogic()
        {
            EnsureLayers();
            Walkable = MapTileRule.IsWalkable(Tile.Type, Overlay.Type);
            Buildable = MapTileRule.IsBuildable(Tile.Type, Overlay.Type);
            MoveCost = MapTileRule.GetDefaultMoveCost(Tile.Type, Overlay.Type);
        }
    }
}
