using System;
using Newtonsoft.Json;
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
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public MapGrassVisualData GrassVisual;

        public bool Walkable;
        public bool Buildable;
        public int MoveCost;

        [JsonIgnore]
        public Vector3Int Coord
        {
            get
            {
                return new Vector3Int(X, Y, Z);
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
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

        [JsonIgnore]
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

        [JsonIgnore]
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

        public void ApplyDefaultLogicByType(MapTileType type, MapData mapData)
        {
            EnsureLayers();
            Tile.Type = type;
            ApplyDefaultLogic(mapData);
        }

        public void ApplyDefaultLogic()
        {
            EnsureLayers();
            Walkable = MapTileRule.IsWalkable(Tile.Type, Overlay.Type);
            Buildable = MapTileRule.IsBuildable(Tile.Type, Overlay.Type);
            MoveCost = MapTileRule.GetDefaultMoveCost(Tile.Type, Overlay.Type);
        }

        public void ApplyDefaultLogic(MapData mapData)
        {
            if (mapData == null)
            {
                ApplyDefaultLogic();
                return;
            }

            mapData.ApplyDefaultLogic(this);
        }
    }

    [Serializable]
    public class MapGrassVisualData
    {
        public float BaseR = 0.43f;
        public float BaseG = 0.66f;
        public float BaseB = 0.09f;

        public float DarkR = 0.34f;
        public float DarkG = 0.56f;
        public float DarkB = 0.055f;

        public float LightR = 0.56f;
        public float LightG = 0.76f;
        public float LightB = 0.15f;

        public float VariationStrength = 0.12f;
        public float VariationScale = 1.35f;
        public float VariationSoftness = 0.72f;

        [JsonIgnore]
        public Color BaseGreen
        {
            get
            {
                return new Color(BaseR, BaseG, BaseB, 1f);
            }
            set
            {
                BaseR = value.r;
                BaseG = value.g;
                BaseB = value.b;
            }
        }

        [JsonIgnore]
        public Color DarkGreen
        {
            get
            {
                return new Color(DarkR, DarkG, DarkB, 1f);
            }
            set
            {
                DarkR = value.r;
                DarkG = value.g;
                DarkB = value.b;
            }
        }

        [JsonIgnore]
        public Color LightGreen
        {
            get
            {
                return new Color(LightR, LightG, LightB, 1f);
            }
            set
            {
                LightR = value.r;
                LightG = value.g;
                LightB = value.b;
            }
        }

        public static MapGrassVisualData CreateDefault()
        {
            return new MapGrassVisualData();
        }
    }
}
