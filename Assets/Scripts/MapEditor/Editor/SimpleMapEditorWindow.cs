#if UNITY_EDITOR

using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class SimpleMapEditorWindow : OdinEditorWindow
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();

        [Title("Map Settings")]

        [LabelText("地图名称")]
        [SerializeField]
        private string mapName = "NewMap";

        [LabelText("地图Id")]
        [SerializeField]
        private int id = 0;

        [LabelText("地图描述")]
        [SerializeField]
        private string description = "这是地图描述";

        [LabelText("宽度 X")]
        [MinValue(1)]
        [SerializeField]
        private int width = 10;

        [LabelText("高度 Y")]
        [MinValue(1)]
        [SerializeField]
        private int height = 3;

        [LabelText("深度 Z")]
        [MinValue(1)]
        [SerializeField]
        private int depth = 10;

        [Title("Random Generate")]

        [LabelText("基础层默认地块")]
        [SerializeField]
        private MapTileType defaultSurfaceType = MapTileType.Grass;

        [LabelText("随机生成水")]
        [SerializeField]
        private bool randomWater = true;

        [LabelText("水概率")]
        [Range(0f, 1f)]
        [SerializeField]
        private float waterRate = 0.08f;

        [LabelText("随机生成雪")]
        [SerializeField]
        private bool randomSnow = true;

        [LabelText("雪概率")]
        [Range(0f, 1f)]
        [SerializeField]
        private float snowRate = 0.08f;

        [LabelText("随机生成山")]
        [SerializeField]
        private bool randomHill = true;

        [LabelText("山概率")]
        [Range(0f, 1f)]
        [SerializeField]
        private float hillRate = 0.12f;

        [LabelText("继续叠高概率")]
        [Range(0f, 1f)]
        [SerializeField]
        private float stackContinueRate = 0.35f;

        [Title("Manual Tile Edit")]

        [LabelText("选择地块类型")]
        [SerializeField]
        private MapTileType selectedTileType = MapTileType.Grass;

        [LabelText("编辑坐标 X")]
        [SerializeField]
        private int editX = 0;

        [LabelText("编辑坐标 Y")]
        [SerializeField]
        private int editY = 0;

        [LabelText("编辑坐标 Z")]
        [SerializeField]
        private int editZ = 0;

        [Title("Map Points - Drag TileView Here")]

        [LabelText("出生点地块对象")]
        [SerializeField]
        private List<TileView> spawns = new List<TileView>();

        [LabelText("玩家基地地块对象")]
        [SerializeField]
        private TileView goal;

        [ShowInInspector]
        [ReadOnly]
        [LabelText("当前出生点")]
        private List<Vector3Int> CurrentSpawnPoints
        {
            get
            {
                if (currentMap == null || currentMap.SpawnPoints == null)
                {
                    return new List<Vector3Int>();
                }

                return currentMap.SpawnPoints;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("是否已设置玩家基地")]
        private bool HasGoalPoint
        {
            get
            {
                if (currentMap == null)
                {
                    return false;
                }

                return currentMap.HasGoalPoint;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("当前玩家基地")]
        private Vector3Int CurrentGoalPoint
        {
            get
            {
                if (currentMap == null || !currentMap.HasGoalPoint)
                {
                    return default;
                }

                return currentMap.GoalPoint;
            }
        }

        [Title("Preview Prefabs")]

        [LabelText("地图根节点")]
        [SerializeField]
        private Transform previewRoot;

        [Title("Prefab Config")]

        [LabelText("地块 Prefab 配置")]
        [SerializeField]
        private MapTilePrefabConfig prefabConfig;

        [LabelText("格子尺寸")]
        [SerializeField]
        private float tileSize = 1f;

        [Title("Current Map")]

        [ShowInInspector]
        [ReadOnly]
        [LabelText("是否已创建地图")]
        private bool HasMap
        {
            get
            {
                return currentMap != null;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("地块数量")]
        private int TileCount
        {
            get
            {
                if (currentMap == null || currentMap.Tiles == null)
                {
                    return 0;
                }

                return currentMap.Tiles.Count;
            }
        }

        [ShowInInspector]
        [ReadOnly]
        [LabelText("当前地图数据")]
        private MapData currentMap;

        [MenuItem("Tools/Cube/Simple Map Editor")]
        public static void Open()
        {
            SimpleMapEditorWindow window = GetWindow<SimpleMapEditorWindow>();
            window.titleContent = new GUIContent("Simple Map Editor");
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            TryLoadPrefabConfig();
        }

        private void TryLoadPrefabConfig()
        {
            if (prefabConfig != null)
            {
                return;
            }

            prefabConfig = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(PrefabConfigPath);

            if (prefabConfig == null)
            {
                Debug.LogWarning($"MapTilePrefabConfig not found. Path: {PrefabConfigPath}");
            }
        }

        [Button("创建规则地图", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        private void CreateMap()
        {
            currentMap = new MapData(id, mapName, width, height, depth);
            currentMap.Description = description;
            currentMap.SpawnPoints = new List<Vector3Int>();
            currentMap.HasGoalPoint = false;
            currentMap.GoalPoint = default;

            tileMap.Clear();

            if (spawns == null)
            {
                spawns = new List<TileView>();
            }

            spawns.Clear();
            goal = null;

            CreateSoilLayer();
            CreateSurfaceLayer();
            CreateStackLayers();

            CreatePreviewObjects();

            Debug.Log($"Create map success. Name: {mapName}, Size: {width}x{height}x{depth}, Tiles: {currentMap.Tiles.Count}");
        }

        private void CreateSoilLayer()
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    TryAddTileInternal(MapTileType.Soil, x, -1, z, false);
                }
            }
        }

        private void CreateSurfaceLayer()
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    MapTileType surfaceType = PickSurfaceTileType();
                    TryAddTileInternal(surfaceType, x, 0, z, false);
                }
            }
        }

        private void CreateStackLayers()
        {
            if (height <= 1)
            {
                return;
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 1; y < height; y++)
                    {
                        if (UnityEngine.Random.value > stackContinueRate)
                        {
                            break;
                        }

                        if (!TryGetTile(x, y - 1, z, out MapTileData belowTile))
                        {
                            break;
                        }

                        MapTileType nextType = PickTileAbove(belowTile.Type);

                        if (nextType == MapTileType.None)
                        {
                            break;
                        }

                        bool added = TryAddTileInternal(nextType, x, y, z, false);

                        if (!added)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private MapTileType PickSurfaceTileType()
        {
            float value = UnityEngine.Random.value;

            if (randomWater && value < waterRate)
            {
                return MapTileType.Water;
            }

            value -= waterRate;

            if (randomHill && value < hillRate)
            {
                return MapTileType.Hill;
            }

            value -= hillRate;

            if (randomSnow && value < snowRate)
            {
                return MapTileType.Snow;
            }

            if (defaultSurfaceType == MapTileType.None || defaultSurfaceType == MapTileType.Soil)
            {
                return MapTileType.Grass;
            }

            return defaultSurfaceType;
        }

        private MapTileType PickTileAbove(MapTileType belowType)
        {
            List<MapTileType> candidates = GetAboveCandidates(belowType);

            if (candidates.Count == 0)
            {
                return MapTileType.None;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[index];
        }

        private List<MapTileType> GetAboveCandidates(MapTileType belowType)
        {
            List<MapTileType> candidates = new List<MapTileType>();

            TryAddAboveCandidate(candidates, belowType, MapTileType.Grass);
            TryAddAboveCandidate(candidates, belowType, MapTileType.Hill);
            TryAddAboveCandidate(candidates, belowType, MapTileType.Snow);
            TryAddAboveCandidate(candidates, belowType, MapTileType.Water);

            return candidates;
        }

        private void TryAddAboveCandidate(List<MapTileType> candidates, MapTileType belowType, MapTileType aboveType)
        {
            if (!MapTileRule.CanHaveTileAbove(belowType, aboveType))
            {
                return;
            }

            if (aboveType == MapTileType.Water)
            {
                return;
            }

            if (aboveType == MapTileType.Hill && !randomHill)
            {
                return;
            }

            if (aboveType == MapTileType.Snow && !randomSnow)
            {
                return;
            }

            candidates.Add(aboveType);
        }

        [Button("添加/放置地块")]
        [GUIColor(0.4f, 0.8f, 1.0f)]
        private void AddSelectedTile()
        {
            if (!EnsureMap())
            {
                return;
            }

            bool added = TryAddTileInternal(selectedTileType, editX, editY, editZ, true);

            if (added)
            {
                CreatePreviewObjects();
            }
        }

        [Button("删除坐标地块")]
        [GUIColor(1.0f, 0.5f, 0.3f)]
        private void RemoveTileAtCoord()
        {
            if (!EnsureMap())
            {
                return;
            }

            bool removed = TryRemoveTileInternal(editX, editY, editZ, true);

            if (removed)
            {
                CreatePreviewObjects();
            }
        }

        [Button("删除顶部地块")]
        [GUIColor(1.0f, 0.7f, 0.3f)]
        private void RemoveTopTileAtXZ()
        {
            if (!EnsureMap())
            {
                return;
            }

            if (!TryGetTopTile(editX, editZ, out MapTileData topTile))
            {
                Debug.LogWarning($"Remove top tile failed. No tile at column: X:{editX}, Z:{editZ}");
                return;
            }

            bool removed = TryRemoveTileInternal(topTile.X, topTile.Y, topTile.Z, true);

            if (removed)
            {
                CreatePreviewObjects();
            }
        }

        [Button("应用拖拽点位到 MapData", ButtonSizes.Large)]
        [GUIColor(0.4f, 1.0f, 0.6f)]
        private void ApplyDraggedPointObjectsToMap()
        {
            if (!EnsureMap())
            {
                return;
            }

            bool success = SyncDraggedPointViewsToMapData(true);

            if (!success)
            {
                return;
            }

            RefreshPointPreviewObjectsOnly();

            EditorUtility.DisplayDialog("Apply Points Success", "点位应用成功。", "OK");
            Debug.Log("Apply dragged point views to MapData success.");
        }

        private bool SyncDraggedPointViewsToMapData(bool showDialog)
        {
            if (currentMap == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Sync Failed", "请先创建或导入地图。", "OK");
                }

                return false;
            }

            currentMap.EnsureRuntimeCollections();

            currentMap.SpawnPoints.Clear();
            currentMap.HasGoalPoint = false;
            currentMap.GoalPoint = default;

            List<string> errors = new List<string>();

            TryApplySpawnTileViews(spawns, errors);
            TryApplyGoalTileView(goal, errors);

            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    Debug.LogWarning(errors[i]);
                }

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Sync Points Warning", $"点位同步失败，有 {errors.Count} 个问题，请查看 Console。", "OK");
                }

                return false;
            }

            return true;
        }

        private bool HasDraggedPointInput()
        {
            if (goal != null)
            {
                return true;
            }

            if (spawns == null)
            {
                return false;
            }

            for (int i = 0; i < spawns.Count; i++)
            {
                if (spawns[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryApplySpawnTileViews(List<TileView> spawnTileViews, List<string> errors)
        {
            if (spawnTileViews == null)
            {
                errors.Add("Spawn tile view list is null.");
                return;
            }

            if (spawnTileViews.Count < 1)
            {
                errors.Add("Spawn point count must be at least 1.");
                return;
            }

            if (spawnTileViews.Count > 3)
            {
                errors.Add("Spawn point count can not exceed 3.");
                return;
            }

            for (int i = 0; i < spawnTileViews.Count; i++)
            {
                TileView tileView = spawnTileViews[i];

                if (tileView == null)
                {
                    errors.Add($"SpawnPoint {i + 1} is null.");
                    continue;
                }

                Vector3Int coord = tileView.Coord;

                if (!CanPlaceSpawnPoint(coord, out string reason))
                {
                    errors.Add($"SpawnPoint {i + 1} invalid. Coord: {coord}, Reason: {reason}");
                    continue;
                }

                currentMap.SpawnPoints.Add(coord);
            }
        }

        private void TryApplyGoalTileView(TileView goalTileView, List<string> errors)
        {
            if (goalTileView == null)
            {
                errors.Add("GoalPoint is empty.");
                return;
            }

            Vector3Int coord = goalTileView.Coord;

            if (!CanPlaceGoalPoint(coord, out string reason))
            {
                errors.Add($"GoalPoint invalid. Coord: {coord}, Reason: {reason}");
                return;
            }

            currentMap.GoalPoint = coord;
            currentMap.HasGoalPoint = true;
        }

        private bool CanPlaceSpawnPoint(Vector3Int coord, out string reason)
        {
            reason = string.Empty;

            if (currentMap.SpawnPoints.Count >= 3)
            {
                reason = "spawn point count can not exceed 3";
                return false;
            }

            if (currentMap.HasSpawnPoint(coord))
            {
                reason = "duplicate spawn point";
                return false;
            }

            if (currentMap.HasGoalPoint && currentMap.GoalPoint == coord)
            {
                reason = "spawn point overlaps goal point";
                return false;
            }

            if (!MapTileRule.IsValidMapPoint(coord, currentMap, out reason))
            {
                return false;
            }

            return true;
        }

        private bool CanPlaceGoalPoint(Vector3Int coord, out string reason)
        {
            reason = string.Empty;

            if (currentMap.HasSpawnPoint(coord))
            {
                reason = "goal point overlaps spawn point";
                return false;
            }

            if (!MapTileRule.IsValidMapPoint(coord, currentMap, out reason))
            {
                return false;
            }

            return true;
        }

        [Button("清空拖拽点位")]
        [GUIColor(0.7f, 0.7f, 0.7f)]
        private void ClearDraggedPointViews()
        {
            if (spawns != null)
            {
                spawns.Clear();
            }

            goal = null;
        }

        [Button("清空 MapData 点位")]
        [GUIColor(1.0f, 0.7f, 0.5f)]
        private void ClearMapDataPoints()
        {
            if (!EnsureMap())
            {
                return;
            }

            currentMap.SpawnPoints.Clear();
            currentMap.HasGoalPoint = false;
            currentMap.GoalPoint = default;

            RefreshPointPreviewObjectsOnly();

            Debug.Log("Clear MapData points.");
        }

        [Button("检查地图合法性")]
        [GUIColor(0.7f, 0.7f, 1.0f)]
        private void ValidateCurrentMap()
        {
            if (!EnsureMap())
            {
                return;
            }

            if (HasDraggedPointInput())
            {
                bool syncSuccess = SyncDraggedPointViewsToMapData(false);

                if (!syncSuccess)
                {
                    EditorUtility.DisplayDialog("Validate Failed", "点位同步失败，请检查出生点和玩家基地拖拽对象。", "OK");
                    return;
                }
            }

            RebuildTileIndex();

            List<string> errors = ValidateMap(currentMap);

            if (errors.Count == 0)
            {
                Debug.Log("Map validate success. No rule errors.");
                EditorUtility.DisplayDialog("Validate Success", "地图规则检查通过。", "OK");
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogWarning(errors[i]);
            }

            EditorUtility.DisplayDialog("Validate Failed", $"地图存在 {errors.Count} 个规则问题，请查看 Console。", "OK");
        }

        private bool TryAddTileInternal(MapTileType type, int x, int y, int z, bool showDialog)
        {
            if (currentMap == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Add Failed", "请先创建或导入地图。", "OK");
                }

                return false;
            }

            currentMap.EnsureRuntimeCollections();

            if (!MapTileRule.CanPlaceTile(type, x, y, z, currentMap))
            {
                string message = $"Can not place tile. Type: {type}, Coord: {x}, {y}, {z}";

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Add Failed", message, "OK");
                }

                Debug.LogWarning(message);
                return false;
            }

            MapTileData tile = new MapTileData(x, y, z, type);
            currentMap.Tiles.Add(tile);

            Vector3Int key = new Vector3Int(x, y, z);
            tileMap[key] = tile;

            return true;
        }

        private bool TryRemoveTileInternal(int x, int y, int z, bool showDialog)
        {
            if (currentMap == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Remove Failed", "请先创建或导入地图。", "OK");
                }

                return false;
            }

            currentMap.EnsureRuntimeCollections();

            Vector3Int key = new Vector3Int(x, y, z);

            if (!tileMap.TryGetValue(key, out MapTileData tile))
            {
                string message = $"Remove failed. Tile not found. Coord: {x}, {y}, {z}";

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Remove Failed", message, "OK");
                }

                Debug.LogWarning(message);
                return false;
            }

            if (!MapTileRule.CanRemoveTile(x, y, z, currentMap))
            {
                string message = $"Remove failed. Tile has upper tile or violates rule. Type: {tile.Type}, Coord: {x}, {y}, {z}";

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Remove Failed", message, "OK");
                }

                Debug.LogWarning(message);
                return false;
            }

            if (currentMap.HasAnyPoint(key))
            {
                string message = $"Remove failed. Tile has spawn point or goal point. Coord: {key}";

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Remove Failed", message, "OK");
                }

                Debug.LogWarning(message);
                return false;
            }

            currentMap.Tiles.Remove(tile);
            tileMap.Remove(key);

            if (tileObjects.TryGetValue(key, out GameObject tileObject))
            {
                if (tileObject != null)
                {
                    DestroyImmediate(tileObject);
                }

                tileObjects.Remove(key);
            }

            return true;
        }

        private bool EnsureMap()
        {
            if (currentMap != null)
            {
                currentMap.EnsureRuntimeCollections();

                if (spawns == null)
                {
                    spawns = new List<TileView>();
                }

                return true;
            }

            EditorUtility.DisplayDialog("No Map", "请先创建或导入地图。", "OK");
            return false;
        }

        private bool TryGetTile(int x, int y, int z, out MapTileData tile)
        {
            Vector3Int key = new Vector3Int(x, y, z);
            return tileMap.TryGetValue(key, out tile);
        }

        private bool TryGetTopTile(int x, int z, out MapTileData tile)
        {
            tile = null;
            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, MapTileData> pair in tileMap)
            {
                Vector3Int coord = pair.Key;

                if (coord.x != x || coord.z != z)
                {
                    continue;
                }

                if (coord.y > topY)
                {
                    topY = coord.y;
                    tile = pair.Value;
                }
            }

            return tile != null;
        }

        private List<string> ValidateMap(MapData mapData)
        {
            List<string> errors = new List<string>();

            if (mapData == null)
            {
                errors.Add("MapData is null.");
                return errors;
            }

            mapData.EnsureRuntimeCollections();

            Dictionary<Vector3Int, MapTileData> tempMap = new Dictionary<Vector3Int, MapTileData>();

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];

                if (tile == null)
                {
                    errors.Add($"Tile index {i} is null.");
                    continue;
                }

                Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);

                if (tempMap.ContainsKey(key))
                {
                    errors.Add($"Duplicate tile coord. Coord: {key}");
                    continue;
                }

                tempMap[key] = tile;
            }

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                if (tile.Type == MapTileType.None)
                {
                    errors.Add($"Invalid None tile. Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                if (tile.Type == MapTileType.Soil)
                {
                    if (tile.Y != -1)
                    {
                        errors.Add($"Soil must be at y = -1. Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    }

                    continue;
                }

                if (tile.Y < 0)
                {
                    errors.Add($"Non-Soil tile y must be >= 0. Type: {tile.Type}, Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                Vector3Int belowKey = new Vector3Int(tile.X, tile.Y - 1, tile.Z);

                if (!tempMap.TryGetValue(belowKey, out MapTileData belowTile))
                {
                    errors.Add($"Tile missing below support. Type: {tile.Type}, Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                if (!MapTileRule.CanPlaceOn(tile.Type, belowTile.Type))
                {
                    errors.Add($"Invalid stack. Below: {belowTile.Type}, Above: {tile.Type}, Coord: {tile.X}, {tile.Y}, {tile.Z}");
                }
            }

            ValidateMapPoints(mapData, errors);

            return errors;
        }

        private void ValidateMapPoints(MapData mapData, List<string> errors)
        {
            if (mapData.SpawnPoints == null)
            {
                errors.Add("SpawnPoints is null.");
                return;
            }

            if (mapData.SpawnPoints.Count < 1)
            {
                errors.Add("Map must have at least 1 spawn point.");
            }

            if (mapData.SpawnPoints.Count > 3)
            {
                errors.Add("Map can have at most 3 spawn points.");
            }

            if (!mapData.HasGoalPoint)
            {
                errors.Add("Map must have 1 goal point.");
            }

            HashSet<Vector3Int> spawnSet = new HashSet<Vector3Int>();

            for (int i = 0; i < mapData.SpawnPoints.Count; i++)
            {
                Vector3Int spawnCoord = mapData.SpawnPoints[i];

                if (spawnSet.Contains(spawnCoord))
                {
                    errors.Add($"Duplicate spawn point. Coord: {spawnCoord}");
                    continue;
                }

                spawnSet.Add(spawnCoord);

                if (!MapTileRule.IsValidMapPoint(spawnCoord, mapData, out string reason))
                {
                    errors.Add($"Invalid spawn point. Coord: {spawnCoord}, Reason: {reason}");
                }

                if (mapData.HasGoalPoint && mapData.GoalPoint == spawnCoord)
                {
                    errors.Add($"Spawn point overlaps goal point. Coord: {spawnCoord}");
                }
            }

            if (mapData.HasGoalPoint)
            {
                if (!MapTileRule.IsValidMapPoint(mapData.GoalPoint, mapData, out string reason))
                {
                    errors.Add($"Invalid goal point. Coord: {mapData.GoalPoint}, Reason: {reason}");
                }
            }
        }

        private void RebuildTileIndex()
        {
            tileMap.Clear();

            if (currentMap == null || currentMap.Tiles == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                tile.ApplyDefaultLogicByType(tile.Type);

                Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);
                tileMap[key] = tile;
            }
        }

        private void CreatePreviewObjects()
        {
            if (currentMap == null)
            {
                Debug.LogWarning("Current map is null.");
                return;
            }

            ClearPreviewObjects();

            GameObject rootObject = GameObject.Find("MapRoot");

            if (rootObject != null)
            {
                DestroyImmediate(rootObject);
            }

            rootObject = new GameObject("MapRoot");
            rootObject.transform.position = Vector3.zero;
            previewRoot = rootObject.transform;

            tileObjects.Clear();

            RebuildTileIndex();

            if (currentMap.Tiles == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                CreatePreviewObject(tile);
            }

            CreatePointPreviewObjects();

            Debug.Log($"Create preview objects success. Count: {tileObjects.Count}");
        }

        private void CreatePreviewObject(MapTileData tile)
        {
            Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);

            GameObject prefab = GetPrefab(tile.Type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {tile.Type}");
                return;
            }

            Vector3 position = GetWorldPosition(tile.X, tile.Y, tile.Z);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
            {
                instance = Instantiate(prefab);
            }

            instance.name = $"{tile.Type}_{tile.X}_{tile.Y}_{tile.Z}";
            instance.transform.SetParent(previewRoot, false);
            instance.transform.position = position;

            TileView tileView = instance.GetComponent<TileView>();

            if (tileView == null)
            {
                tileView = instance.AddComponent<TileView>();
            }

            tileView.Initialize(new TileData(tile));

            tileObjects[key] = instance;
        }

        private void CreatePointPreviewObjects()
        {
            if (currentMap == null || previewRoot == null)
            {
                return;
            }

            if (currentMap.SpawnPoints != null)
            {
                for (int i = 0; i < currentMap.SpawnPoints.Count; i++)
                {
                    CreatePointPreviewObject(currentMap.SpawnPoints[i], $"SpawnPoint_{i}", Color.red);
                }
            }

            if (currentMap.HasGoalPoint)
            {
                CreatePointPreviewObject(currentMap.GoalPoint, "GoalPoint", Color.green);
            }
        }

        private void RefreshPointPreviewObjectsOnly()
        {
            if (previewRoot == null)
            {
                return;
            }

            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = previewRoot.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                if (IsPointPreviewObject(child.gameObject))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            CreatePointPreviewObjects();
        }

        private bool IsPointPreviewObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (gameObject.name.StartsWith("SpawnPoint_", StringComparison.Ordinal))
            {
                return true;
            }

            if (gameObject.name == "GoalPoint")
            {
                return true;
            }

            return false;
        }

        private void CreatePointPreviewObject(Vector3Int coord, string objectName, Color color)
        {
            GameObject pointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointObject.name = objectName;
            pointObject.transform.SetParent(previewRoot, false);
            pointObject.transform.position = GetWorldPosition(coord.x, coord.y, coord.z) + Vector3.up * 0.6f;
            pointObject.transform.localScale = Vector3.one * 0.35f;

            Renderer renderer = pointObject.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }
        }

        private Vector3 GetWorldPosition(int x, int y, int z)
        {
            return new Vector3(x * tileSize, y * tileSize, z * tileSize);
        }

        private void ClearPreviewObjects()
        {
            if (previewRoot == null)
            {
                tileObjects.Clear();
                return;
            }

            for (int i = previewRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = previewRoot.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
            }

            tileObjects.Clear();
        }

        private GameObject GetPrefab(MapTileType type)
        {
            if (prefabConfig == null)
            {
                TryLoadPrefabConfig();
            }

            if (prefabConfig == null)
            {
                Debug.LogWarning("Prefab config is null.");
                return null;
            }

            GameObject prefab = prefabConfig.GetPrefab(type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {type}");
            }

            return prefab;
        }

        [Button("导出 Json", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.6f, 1.0f)]
        private void ExportJson()
        {
            if (currentMap == null)
            {
                EditorUtility.DisplayDialog("Export Failed", "请先创建地图。", "OK");
                return;
            }

            if (HasDraggedPointInput())
            {
                bool syncSuccess = SyncDraggedPointViewsToMapData(false);

                if (!syncSuccess)
                {
                    EditorUtility.DisplayDialog("Export Failed", "点位同步失败，请检查出生点和玩家基地拖拽对象。", "OK");
                    return;
                }
            }

            RebuildTileIndex();

            List<string> errors = ValidateMap(currentMap);

            if (errors.Count > 0)
            {
                bool continueExport = EditorUtility.DisplayDialog("Map Invalid", $"地图存在 {errors.Count} 个规则问题，是否仍然导出？", "Export", "Cancel");

                for (int i = 0; i < errors.Count; i++)
                {
                    Debug.LogWarning(errors[i]);
                }

                if (!continueExport)
                {
                    return;
                }
            }

            string path = EditorUtility.SaveFilePanel("Export Map Json", Application.dataPath, currentMap.Id + ".json", "json");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };

            string json = JsonConvert.SerializeObject(currentMap, settings);
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();

            Debug.Log($"Export map json success: {path}");
        }

        [Button("清空当前地图")]
        [GUIColor(1.0f, 0.6f, 0.3f)]
        private void ClearMap()
        {
            currentMap = null;
            tileMap.Clear();

            if (spawns != null)
            {
                spawns.Clear();
            }

            goal = null;

            ClearPreviewObjects();

            Debug.Log("Clear current map.");
        }

        [Button("导入 Json 并生成地图", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.7f, 1.0f)]
        private void ImportJsonAndCreateMap()
        {
            string path = EditorUtility.OpenFilePanel("Import Map Json", Application.dataPath, "json");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                MapData mapData = JsonConvert.DeserializeObject<MapData>(json);

                if (mapData == null)
                {
                    EditorUtility.DisplayDialog("Import Failed", "Json 解析失败，MapData 为空。", "OK");
                    return;
                }

                mapData.EnsureRuntimeCollections();

                currentMap = mapData;

                id = currentMap.Id;
                mapName = currentMap.Name;
                description = currentMap.Description;
                width = currentMap.Width;
                height = currentMap.Height;
                depth = currentMap.Depth;

                if (spawns == null)
                {
                    spawns = new List<TileView>();
                }

                spawns.Clear();
                goal = null;

                RebuildTileIndex();

                List<string> errors = ValidateMap(currentMap);

                if (errors.Count > 0)
                {
                    for (int i = 0; i < errors.Count; i++)
                    {
                        Debug.LogWarning(errors[i]);
                    }

                    EditorUtility.DisplayDialog("Import Warning", $"地图导入成功，但存在 {errors.Count} 个规则问题，请查看 Console。", "OK");
                }

                CreatePreviewObjects();

                Debug.Log($"Import map success. Path: {path}, Name: {currentMap.Name}, Size: {currentMap.Width}x{currentMap.Height}x{currentMap.Depth}, Tiles: {currentMap.Tiles.Count}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Import Failed", exception.Message, "OK");
            }
        }
    }
}

#endif