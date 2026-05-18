#if UNITY_EDITOR

using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class SimpleFlatMapEditorWindow : OdinEditorWindow
    {
        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();

        private readonly MapDataAStarPathFinder pathFinder = new MapDataAStarPathFinder();
        private readonly List<Vector3Int> pathBuffer = new List<Vector3Int>();
        private readonly List<GameObject> pathPreviewObjects = new List<GameObject>();

        private readonly HashSet<Vector3Int> paintedThisDrag = new HashSet<Vector3Int>();

        [Title("Flat Map Settings")]

        [LabelText("地图名称")]
        [SerializeField]
        private string mapName = "NewFlatMap";

        [LabelText("地图Id")]
        [SerializeField]
        private int id = 1;

        [LabelText("地图描述")]
        [SerializeField]
        private string description = "这是平面格子地图";

        [LabelText("宽度 X")]
        [MinValue(1)]
        [SerializeField]
        private int width = 12;

        [LabelText("深度 Z")]
        [MinValue(1)]
        [SerializeField]
        private int depth = 12;

        [LabelText("格子尺寸")]
        [SerializeField]
        private float tileSize = 1f;

        [Title("Manual Paint")]

        [LabelText("选择地块类型")]
        [SerializeField]
        private MapTileType selectedTileType = MapTileType.Grass;

        [LabelText("编辑坐标 X")]
        [SerializeField]
        private int editX = 0;

        [LabelText("编辑坐标 Z")]
        [SerializeField]
        private int editZ = 0;

        [Title("Brush Mode")]

        [LabelText("开启鼠标笔刷")]
        [SerializeField]
        private bool brushMode = false;

        [LabelText("笔刷尺寸")]
        [MinValue(1)]
        [MaxValue(7)]
        [SerializeField]
        private int brushSize = 1;

        [LabelText("刷到出生点/基地时跳过")]
        [SerializeField]
        private bool skipPointTilesWhenBrushPainting = true;

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

        [Title("Preview")]

        [LabelText("地图根节点")]
        [SerializeField]
        private Transform previewRoot;

        [LabelText("地块 Prefab 配置")]
        [SerializeField]
        private MapTilePrefabConfig prefabConfig;

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

        [MenuItem("Tools/Cube/Simple Flat Map Editor")]
        public static void Open()
        {
            SimpleFlatMapEditorWindow window = GetWindow<SimpleFlatMapEditorWindow>();
            window.titleContent = new GUIContent("Simple Flat Map Editor");
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            TryLoadPrefabConfig();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            SceneView.duringSceneGui -= OnSceneGUI;
            paintedThisDrag.Clear();
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

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!brushMode)
            {
                return;
            }

            if (currentMap == null)
            {
                return;
            }

            Event currentEvent = Event.current;

            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                paintedThisDrag.Clear();
                currentEvent.Use();
                return;
            }

            if (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag)
            {
                return;
            }

            if (currentEvent.button != 0)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return;
            }

            TileView tileView = hit.collider.GetComponentInParent<TileView>();

            if (tileView == null)
            {
                return;
            }

            Vector3Int coord = tileView.Coord;

            PaintBrushAt(coord.x, coord.z, selectedTileType);

            currentEvent.Use();
            SceneView.RepaintAll();
        }

        private void PaintBrushAt(int centerX, int centerZ, MapTileType type)
        {
            int size = Mathf.Max(1, brushSize);
            int radius = size / 2;

            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    Vector3Int coord = new Vector3Int(x, 0, z);

                    if (paintedThisDrag.Contains(coord))
                    {
                        continue;
                    }

                    bool painted = PaintTile(x, z, type, false);

                    if (painted)
                    {
                        paintedThisDrag.Add(coord);
                    }
                }
            }
        }

        [Button("创建平面地图 Soil + Grass", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.9f, 0.4f)]
        private void CreateFlatMap()
        {
            currentMap = new MapData(id, mapName, width, 1, depth);
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

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    AddTileNoCheck(MapTileType.Soil, x, -1, z);
                    AddTileNoCheck(MapTileType.Grass, x, 0, z);
                }
            }

            CreatePreviewObjects();

            Debug.Log($"Create flat map success. Name: {mapName}, Size: {width}x{depth}, Tiles: {currentMap.Tiles.Count}");
        }

        [Button("按当前宽度/深度调整地图", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.8f, 1.0f)]
        private void ResizeFlatMap()
        {
            if (!EnsureMap())
            {
                return;
            }

            if (width < 1 || depth < 1)
            {
                EditorUtility.DisplayDialog("Resize Failed", "宽度和深度必须 >= 1。", "OK");
                return;
            }

            bool hasPointOutside = HasPointOutside(width, depth);

            if (hasPointOutside)
            {
                bool confirm = EditorUtility.DisplayDialog("Resize Warning", "裁剪区域包含出生点或基地，继续会清理这些点位。是否继续？", "Resize", "Cancel");

                if (!confirm)
                {
                    return;
                }
            }

            RemoveTilesOutside(width, depth);
            RemovePointsOutside(width, depth);
            AddMissingFlatTiles(width, depth);

            currentMap.Width = width;
            currentMap.Height = 1;
            currentMap.Depth = depth;

            RebuildTileIndex();
            CreatePreviewObjects();

            Debug.Log($"Resize flat map success. Size: {width}x{depth}");
        }

        private bool HasPointOutside(int newWidth, int newDepth)
        {
            if (currentMap == null)
            {
                return false;
            }

            if (currentMap.SpawnPoints != null)
            {
                for (int i = 0; i < currentMap.SpawnPoints.Count; i++)
                {
                    Vector3Int coord = currentMap.SpawnPoints[i];

                    if (coord.x < 0 || coord.x >= newWidth || coord.z < 0 || coord.z >= newDepth)
                    {
                        return true;
                    }
                }
            }

            if (currentMap.HasGoalPoint)
            {
                Vector3Int coord = currentMap.GoalPoint;

                if (coord.x < 0 || coord.x >= newWidth || coord.z < 0 || coord.z >= newDepth)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveTilesOutside(int newWidth, int newDepth)
        {
            for (int i = currentMap.Tiles.Count - 1; i >= 0; i--)
            {
                MapTileData tile = currentMap.Tiles[i];

                if (tile == null)
                {
                    currentMap.Tiles.RemoveAt(i);
                    continue;
                }

                if (tile.X < 0 || tile.X >= newWidth || tile.Z < 0 || tile.Z >= newDepth)
                {
                    Vector3Int key = new Vector3Int(tile.X, tile.Y, tile.Z);
                    currentMap.Tiles.RemoveAt(i);
                    tileMap.Remove(key);

                    if (tileObjects.TryGetValue(key, out GameObject oldObject))
                    {
                        if (oldObject != null)
                        {
                            DestroyImmediate(oldObject);
                        }

                        tileObjects.Remove(key);
                    }
                }
            }
        }

        private void RemovePointsOutside(int newWidth, int newDepth)
        {
            if (currentMap.SpawnPoints != null)
            {
                for (int i = currentMap.SpawnPoints.Count - 1; i >= 0; i--)
                {
                    Vector3Int coord = currentMap.SpawnPoints[i];

                    if (coord.x < 0 || coord.x >= newWidth || coord.z < 0 || coord.z >= newDepth)
                    {
                        currentMap.SpawnPoints.RemoveAt(i);
                    }
                }
            }

            if (currentMap.HasGoalPoint)
            {
                Vector3Int coord = currentMap.GoalPoint;

                if (coord.x < 0 || coord.x >= newWidth || coord.z < 0 || coord.z >= newDepth)
                {
                    currentMap.HasGoalPoint = false;
                    currentMap.GoalPoint = default;
                    goal = null;
                }
            }
        }

        private void AddMissingFlatTiles(int newWidth, int newDepth)
        {
            RebuildTileIndex();

            for (int z = 0; z < newDepth; z++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    Vector3Int soilCoord = new Vector3Int(x, -1, z);
                    Vector3Int surfaceCoord = new Vector3Int(x, 0, z);

                    if (!tileMap.ContainsKey(soilCoord))
                    {
                        AddTileNoCheck(MapTileType.Soil, x, -1, z);
                    }

                    if (!tileMap.ContainsKey(surfaceCoord))
                    {
                        AddTileNoCheck(MapTileType.Grass, x, 0, z);
                    }
                }
            }
        }

        private void AddTileNoCheck(MapTileType type, int x, int y, int z)
        {
            MapTileData tile = new MapTileData(x, y, z, type);
            tile.ApplyDefaultLogicByType(type);

            currentMap.Tiles.Add(tile);

            Vector3Int key = new Vector3Int(x, y, z);
            tileMap[key] = tile;
        }

        [Button("切换笔刷模式")]
        [GUIColor(0.6f, 0.8f, 1.0f)]
        private void ToggleBrushMode()
        {
            brushMode = !brushMode;
            paintedThisDrag.Clear();

            Debug.Log($"Brush mode: {brushMode}");
        }

        [Button("刷当前坐标地块")]
        [GUIColor(0.4f, 0.9f, 1.0f)]
        private void PaintSelectedTile()
        {
            if (!EnsureMap())
            {
                return;
            }

            PaintTile(editX, editZ, selectedTileType, true);
        }

        private bool PaintTile(int x, int z, MapTileType type, bool showDialog)
        {
            if (type == MapTileType.None || type == MapTileType.Soil)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Paint Failed", "平面编辑器只能刷逻辑地块，不能刷 None 或 Soil。", "OK");
                }

                return false;
            }

            if (x < 0 || x >= width || z < 0 || z >= depth)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Paint Failed", $"坐标越界。X: {x}, Z: {z}", "OK");
                }

                return false;
            }

            Vector3Int coord = new Vector3Int(x, 0, z);

            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Paint Failed", $"找不到 y=0 地块。Coord: {coord}", "OK");
                }

                return false;
            }

            if (currentMap.HasAnyPoint(coord))
            {
                if (!showDialog && skipPointTilesWhenBrushPainting)
                {
                    return false;
                }

                if (showDialog)
                {
                    bool confirm = EditorUtility.DisplayDialog("Paint Warning", "这个格子是出生点或基地，修改类型可能导致点位非法。是否继续？", "Paint", "Cancel");

                    if (!confirm)
                    {
                        return false;
                    }
                }
            }

            if (tile.Type == type)
            {
                return false;
            }

            tile.Type = type;
            tile.ApplyDefaultLogicByType(type);

            RecreatePreviewObject(coord);
            RefreshVisualAround(coord);
            RefreshPointPreviewObjectsOnly();

            Debug.Log($"Paint tile success. Coord: {coord}, Type: {type}");
            return true;
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

            if (coord.y != 0)
            {
                reason = "flat map point must be at y = 0";
                return false;
            }

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

            if (coord.y != 0)
            {
                reason = "flat map point must be at y = 0";
                return false;
            }

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

            List<string> errors = ValidateFlatMap(currentMap);

            if (errors.Count == 0)
            {
                Debug.Log("Flat map validate success. No rule errors.");
                EditorUtility.DisplayDialog("Validate Success", "地图规则检查通过。", "OK");
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogWarning(errors[i]);
            }

            EditorUtility.DisplayDialog("Validate Failed", $"地图存在 {errors.Count} 个规则问题，请查看 Console。", "OK");
        }

        [Button("检查出生点到基地是否有路", ButtonSizes.Large)]
        [GUIColor(0.3f, 1.0f, 0.8f)]
        private void CheckSpawnToGoalPaths()
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
                    EditorUtility.DisplayDialog("Path Check Failed", "点位同步失败，请检查出生点和玩家基地拖拽对象。", "OK");
                    return;
                }
            }

            RebuildTileIndex();

            List<string> mapErrors = ValidateFlatMap(currentMap);

            if (mapErrors.Count > 0)
            {
                for (int i = 0; i < mapErrors.Count; i++)
                {
                    Debug.LogWarning(mapErrors[i]);
                }

                EditorUtility.DisplayDialog("Path Check Failed", $"地图规则不合法，存在 {mapErrors.Count} 个问题，请先修复。", "OK");
                return;
            }

            ClearPathPreviewObjects();

            bool allSuccess = true;
            List<string> pathErrors = new List<string>();

            for (int i = 0; i < currentMap.SpawnPoints.Count; i++)
            {
                Vector3Int spawnCoord = currentMap.SpawnPoints[i];
                Vector3Int goalCoord = currentMap.GoalPoint;

                bool success = pathFinder.TryFindPath(currentMap, spawnCoord, goalCoord, pathBuffer);

                if (!success)
                {
                    allSuccess = false;
                    string message = $"Path not found. SpawnIndex: {i}, Spawn: {spawnCoord}, Goal: {goalCoord}";
                    pathErrors.Add(message);
                    Debug.LogWarning(message);
                    continue;
                }

                Debug.Log($"Path found. SpawnIndex: {i}, Spawn: {spawnCoord}, Goal: {goalCoord}, Count: {pathBuffer.Count}");

                CreatePathPreviewObjects(pathBuffer, i);
            }

            if (!allSuccess)
            {
                EditorUtility.DisplayDialog("Path Check Failed", $"有 {pathErrors.Count} 个出生点无法到达基地，请查看 Console。", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Path Check Success", $"全部 {currentMap.SpawnPoints.Count} 个出生点都可以到达基地。", "OK");
        }

        [Button("清空寻路预览")]
        [GUIColor(0.7f, 0.7f, 0.7f)]
        private void ClearPathPreview()
        {
            ClearPathPreviewObjects();
        }

        private List<string> ValidateFlatMap(MapData mapData)
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

            for (int z = 0; z < mapData.Depth; z++)
            {
                for (int x = 0; x < mapData.Width; x++)
                {
                    Vector3Int soilCoord = new Vector3Int(x, -1, z);
                    Vector3Int surfaceCoord = new Vector3Int(x, 0, z);

                    if (!tempMap.TryGetValue(soilCoord, out MapTileData soilTile))
                    {
                        errors.Add($"Missing soil tile. Coord: {soilCoord}");
                    }
                    else if (soilTile.Type != MapTileType.Soil)
                    {
                        errors.Add($"Tile at y=-1 must be Soil. Coord: {soilCoord}, Type: {soilTile.Type}");
                    }

                    if (!tempMap.TryGetValue(surfaceCoord, out MapTileData surfaceTile))
                    {
                        errors.Add($"Missing surface tile. Coord: {surfaceCoord}");
                    }
                    else if (surfaceTile.Type == MapTileType.None || surfaceTile.Type == MapTileType.Soil)
                    {
                        errors.Add($"Invalid surface tile. Coord: {surfaceCoord}, Type: {surfaceTile.Type}");
                    }
                }
            }

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];

                if (tile == null)
                {
                    continue;
                }

                if (tile.Y != -1 && tile.Y != 0)
                {
                    errors.Add($"Flat map only allows y=-1 or y=0. Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                if (tile.Y == -1 && tile.Type != MapTileType.Soil)
                {
                    errors.Add($"Flat map y=-1 must be Soil. Coord: {tile.X}, {tile.Y}, {tile.Z}, Type: {tile.Type}");
                    continue;
                }

                if (tile.Y == 0 && tile.Type == MapTileType.Soil)
                {
                    errors.Add($"Flat map y=0 can not be Soil. Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                if (tile.X < 0 || tile.X >= mapData.Width || tile.Z < 0 || tile.Z >= mapData.Depth)
                {
                    errors.Add($"Tile outside map size. Coord: {tile.X}, {tile.Y}, {tile.Z}");
                    continue;
                }

                tile.ApplyDefaultLogicByType(tile.Type);
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

                if (spawnCoord.y != 0)
                {
                    errors.Add($"Spawn point must be at y=0. Coord: {spawnCoord}");
                    continue;
                }

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
                if (mapData.GoalPoint.y != 0)
                {
                    errors.Add($"Goal point must be at y=0. Coord: {mapData.GoalPoint}");
                }
                else if (!MapTileRule.IsValidMapPoint(mapData.GoalPoint, mapData, out string reason))
                {
                    errors.Add($"Invalid goal point. Coord: {mapData.GoalPoint}, Reason: {reason}");
                }
            }
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
            pathPreviewObjects.Clear();

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

            RefreshAllFlatTileVisuals();
            CreatePointPreviewObjects();

            Debug.Log($"Create flat preview objects success. Count: {tileObjects.Count}");
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

        private void RecreatePreviewObject(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                return;
            }

            if (tileObjects.TryGetValue(coord, out GameObject oldObject))
            {
                if (oldObject != null)
                {
                    DestroyImmediate(oldObject);
                }

                tileObjects.Remove(coord);
            }

            CreatePreviewObject(tile);
        }

        private void RefreshAllFlatTileVisuals()
        {
            foreach (KeyValuePair<Vector3Int, GameObject> pair in tileObjects)
            {
                RefreshFlatTileVisual(pair.Key);
            }
        }

        private void RefreshVisualAround(Vector3Int coord)
        {
            RefreshFlatTileVisual(coord);
            RefreshFlatTileVisual(coord + Vector3Int.forward);
            RefreshFlatTileVisual(coord + Vector3Int.back);
            RefreshFlatTileVisual(coord + Vector3Int.left);
            RefreshFlatTileVisual(coord + Vector3Int.right);
        }

        private void RefreshFlatTileVisual(Vector3Int coord)
        {
            if (!tileObjects.TryGetValue(coord, out GameObject tileObject))
            {
                return;
            }

            if (tileObject == null)
            {
                return;
            }

            Component visual = tileObject.GetComponent("FlatTileVisual");

            if (visual == null)
            {
                return;
            }

            MethodInfo refreshMethod = visual.GetType().GetMethod("Refresh", new Type[]
            {
                typeof(MapTileType),
                typeof(MapTileType),
                typeof(MapTileType),
                typeof(MapTileType),
                typeof(MapTileType)
            });

            if (refreshMethod == null)
            {
                return;
            }

            MapTileType centerType = GetTileTypeOrNone(coord);
            MapTileType northType = GetTileTypeOrNone(coord + Vector3Int.forward);
            MapTileType eastType = GetTileTypeOrNone(coord + Vector3Int.right);
            MapTileType southType = GetTileTypeOrNone(coord + Vector3Int.back);
            MapTileType westType = GetTileTypeOrNone(coord + Vector3Int.left);

            refreshMethod.Invoke(visual, new object[]
            {
                centerType,
                northType,
                eastType,
                southType,
                westType
            });
        }

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                return MapTileType.None;
            }

            return tile.Type;
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

        private void CreatePathPreviewObjects(List<Vector3Int> path, int pathIndex)
        {
            if (path == null || path.Count == 0)
            {
                return;
            }

            if (previewRoot == null)
            {
                return;
            }

            for (int i = 0; i < path.Count; i++)
            {
                Vector3Int coord = path[i];

                GameObject pointObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pointObject.name = $"PathPreview_{pathIndex}_{i}";
                pointObject.transform.SetParent(previewRoot, false);
                pointObject.transform.position = GetWorldPosition(coord.x, coord.y, coord.z) + Vector3.up * 0.25f;
                pointObject.transform.localScale = Vector3.one * 0.22f;

                Renderer renderer = pointObject.GetComponent<Renderer>();

                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    material.color = Color.cyan;
                    renderer.sharedMaterial = material;
                }

                pathPreviewObjects.Add(pointObject);
            }
        }

        private void ClearPathPreviewObjects()
        {
            for (int i = pathPreviewObjects.Count - 1; i >= 0; i--)
            {
                GameObject pathObject = pathPreviewObjects[i];

                if (pathObject != null)
                {
                    DestroyImmediate(pathObject);
                }
            }

            pathPreviewObjects.Clear();

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

                if (child.name.StartsWith("PathPreview_", StringComparison.Ordinal))
                {
                    DestroyImmediate(child.gameObject);
                }
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
                pathPreviewObjects.Clear();
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
            pathPreviewObjects.Clear();
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

            List<string> errors = ValidateFlatMap(currentMap);

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

            Debug.Log($"Export flat map json success: {path}");
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

            string json = File.ReadAllText(path);
            MapData data = JsonConvert.DeserializeObject<MapData>(json);

            if (data == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "Json 解析失败。", "OK");
                return;
            }

            data.EnsureRuntimeCollections();

            currentMap = data;
            id = currentMap.Id;
            mapName = currentMap.Name;
            description = currentMap.Description;
            width = Mathf.Max(1, currentMap.Width);
            depth = Mathf.Max(1, currentMap.Depth);

            if (spawns == null)
            {
                spawns = new List<TileView>();
            }

            spawns.Clear();
            goal = null;

            RebuildTileIndex();
            CreatePreviewObjects();

            Debug.Log($"Import flat map success: {path}");
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

            Debug.Log("Clear current flat map.");
        }
    }
}

#endif