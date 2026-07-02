using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 一张地图的完整导出数据。
    /// Width  对应 X 方向长度。
    /// Height 对应 Y 方向高度。
    /// Depth  对应 Z 方向长度。
    /// </summary>
    [Serializable]
    public class MapData
    {
        public int Id;
        public string Name;
        public string Description;

        public int Width;
        public int Height;
        public int Depth;

        public List<MapCellData> Cells = new List<MapCellData>();
        public List<MapObjectData> Objects = new List<MapObjectData>();
        public List<MapTileLogicDefaultData> TileLogicDefaults = new List<MapTileLogicDefaultData>();
        public List<MapOverlayLogicDefaultData> OverlayLogicDefaults = new List<MapOverlayLogicDefaultData>();

        /// <summary>
        /// 敌对 NPC 出生点。
        /// 数量规则：1~3 个。
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(Vector3IntJsonConverter))]
        public List<Vector3Int> SpawnPoints = new List<Vector3Int>();

        /// <summary>
        /// 是否已经设置玩家基地。
        /// 因为 Vector3Int 是 struct，不能用 null 表示未设置。
        /// </summary>
        public bool HasGoalPoint;

        /// <summary>
        /// 玩家基地坐标。
        /// HasGoalPoint 为 true 时有效。
        /// </summary>
        [JsonConverter(typeof(Vector3IntJsonConverter))]
        public Vector3Int GoalPoint;

        public MapData()
        {
        }

        public MapData(int id, string name, int width, int height, int depth)
        {
            Id = id;
            Name = name;
            Width = width;
            Height = height;
            Depth = depth;

            Cells = new List<MapCellData>();
            Objects = new List<MapObjectData>();
            TileLogicDefaults = new List<MapTileLogicDefaultData>();
            OverlayLogicDefaults = new List<MapOverlayLogicDefaultData>();
            SpawnPoints = new List<Vector3Int>();
            HasGoalPoint = false;
            GoalPoint = default;

            EnsureLogicDefaults();
        }

        public void EnsureRuntimeCollections()
        {
            if (Cells == null)
            {
                Cells = new List<MapCellData>();
            }

            if (SpawnPoints == null)
            {
                SpawnPoints = new List<Vector3Int>();
            }

            if (Objects == null)
            {
                Objects = new List<MapObjectData>();
            }

            EnsureLogicDefaults();
        }

        public void ApplyDefaultLogic(MapCellData cell)
        {
            if (cell == null)
            {
                return;
            }

            cell.EnsureLayers();

            if (cell.Overlay.Type != MapTileOverlay.None &&
                TryGetOverlayLogicDefault(cell.Overlay.Type, out MapOverlayLogicDefaultData overlayDefault))
            {
                cell.Walkable = overlayDefault.Walkable;
                cell.Buildable = overlayDefault.Buildable;
                cell.MoveCost = overlayDefault.MoveCost;
                return;
            }

            if (TryGetTileLogicDefault(cell.Type, out MapTileLogicDefaultData tileDefault))
            {
                cell.Walkable = tileDefault.Walkable;
                cell.Buildable = tileDefault.Buildable;
                cell.MoveCost = tileDefault.MoveCost;
                return;
            }

            cell.ApplyDefaultLogic();
        }

        public bool TryGetTileLogicDefault(MapTileType type, out MapTileLogicDefaultData result)
        {
            EnsureLogicDefaults();

            for (int i = 0; i < TileLogicDefaults.Count; i++)
            {
                MapTileLogicDefaultData current = TileLogicDefaults[i];
                if (current != null && current.Type == type)
                {
                    result = current;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public bool TryGetOverlayLogicDefault(MapTileOverlay overlay, out MapOverlayLogicDefaultData result)
        {
            EnsureLogicDefaults();

            for (int i = 0; i < OverlayLogicDefaults.Count; i++)
            {
                MapOverlayLogicDefaultData current = OverlayLogicDefaults[i];
                if (current != null && current.Overlay == overlay)
                {
                    result = current;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void EnsureLogicDefaults()
        {
            if (TileLogicDefaults == null)
            {
                TileLogicDefaults = new List<MapTileLogicDefaultData>();
            }

            if (OverlayLogicDefaults == null)
            {
                OverlayLogicDefaults = new List<MapOverlayLogicDefaultData>();
            }

            EnsureTileLogicDefault(MapTileType.Grass);
            EnsureTileLogicDefault(MapTileType.Hill);
            EnsureTileLogicDefault(MapTileType.Snow);
            EnsureTileLogicDefault(MapTileType.Water);
            EnsureTileLogicDefault(MapTileType.Road);
            EnsureTileLogicDefault(MapTileType.Bridge);
            EnsureTileLogicDefault(MapTileType.Soil);

            EnsureOverlayLogicDefault(MapTileOverlay.Bridge);
            EnsureOverlayLogicDefault(MapTileOverlay.Stair);
            EnsureOverlayLogicDefault(MapTileOverlay.Ramp);
        }

        private void EnsureTileLogicDefault(MapTileType type)
        {
            for (int i = 0; i < TileLogicDefaults.Count; i++)
            {
                MapTileLogicDefaultData current = TileLogicDefaults[i];
                if (current != null && current.Type == type)
                {
                    return;
                }
            }

            TileLogicDefaults.Add(MapTileLogicDefaultData.CreateRuleDefault(type));
        }

        private void EnsureOverlayLogicDefault(MapTileOverlay overlay)
        {
            for (int i = 0; i < OverlayLogicDefaults.Count; i++)
            {
                MapOverlayLogicDefaultData current = OverlayLogicDefaults[i];
                if (current != null && current.Overlay == overlay)
                {
                    return;
                }
            }

            OverlayLogicDefaults.Add(MapOverlayLogicDefaultData.CreateRuleDefault(overlay));
        }

        public MapCellData GetCell(int x, int y, int z)
        {
            if (Cells == null)
            {
                return null;
            }

            for (int i = 0; i < Cells.Count; i++)
            {
                MapCellData cell = Cells[i];

                if (cell == null)
                {
                    continue;
                }

                if (cell.X == x && cell.Y == y && cell.Z == z)
                {
                    return cell;
                }
            }

            return null;
        }

        public MapCellData GetCell(Vector3Int coord)
        {
            return GetCell(coord.x, coord.y, coord.z);
        }

        public List<MapObjectData> GetObjectsAt(Vector3Int coord, List<MapObjectData> results = null)
        {
            results ??= new List<MapObjectData>();
            results.Clear();

            if (Objects == null)
            {
                return results;
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                MapObjectData mapObject = Objects[i];
                if (mapObject != null && mapObject.Coord == coord)
                {
                    results.Add(mapObject);
                }
            }

            return results;
        }

        public bool HasSpawnPoint(Vector3Int coord)
        {
            if (SpawnPoints == null)
            {
                return false;
            }

            for (int i = 0; i < SpawnPoints.Count; i++)
            {
                if (SpawnPoints[i] == coord)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsGoalPoint(Vector3Int coord)
        {
            if (!HasGoalPoint)
            {
                return false;
            }

            return GoalPoint == coord;
        }

        public bool HasAnyPoint(Vector3Int coord)
        {
            if (HasSpawnPoint(coord))
            {
                return true;
            }

            if (IsGoalPoint(coord))
            {
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public class MapTileLogicDefaultData
    {
        public MapTileType Type;
        public bool Walkable;
        public bool Buildable;
        public int MoveCost;

        public static MapTileLogicDefaultData CreateRuleDefault(MapTileType type)
        {
            return new MapTileLogicDefaultData
            {
                Type = type,
                Walkable = MapTileRule.IsWalkable(type, MapTileOverlay.None),
                Buildable = MapTileRule.IsBuildable(type, MapTileOverlay.None),
                MoveCost = MapTileRule.GetDefaultMoveCost(type, MapTileOverlay.None)
            };
        }
    }

    [Serializable]
    public class MapOverlayLogicDefaultData
    {
        public MapTileOverlay Overlay;
        public bool Walkable;
        public bool Buildable;
        public int MoveCost;

        public static MapOverlayLogicDefaultData CreateRuleDefault(MapTileOverlay overlay)
        {
            return new MapOverlayLogicDefaultData
            {
                Overlay = overlay,
                Walkable = MapTileRule.IsWalkable(MapTileType.None, overlay),
                Buildable = MapTileRule.IsBuildable(MapTileType.None, overlay),
                MoveCost = MapTileRule.GetDefaultMoveCost(MapTileType.None, overlay)
            };
        }
    }

    public sealed class Vector3IntJsonConverter : JsonConverter<Vector3Int>
    {
        public override void WriteJson(JsonWriter writer, Vector3Int value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3Int ReadJson(JsonReader reader, Type objectType, Vector3Int existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return default;
            }

            JObject value = JObject.Load(reader);
            return new Vector3Int(
                value.Value<int>("x"),
                value.Value<int>("y"),
                value.Value<int>("z"));
        }
    }

    public sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return default;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                JArray array = JArray.Load(reader);
                return new Vector3(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f,
                    array.Count > 2 ? array[2].Value<float>() : 0f);
            }

            JObject value = JObject.Load(reader);
            return new Vector3(
                value.Value<float>("x"),
                value.Value<float>("y"),
                value.Value<float>("z"));
        }
    }
}
