
#if UNITY_EDITOR

using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class MapEditorWindow : OdinEditorWindow
    {
        private enum BrushMode
        {
            Type,
            Overlay,
            Raise,
            Lower,
        }

        private enum TypeBrush
        {
            Grass = MapTileType.Grass,
            Hill = MapTileType.Hill,
            Snow = MapTileType.Snow,
            Water = MapTileType.Water,
            Road = MapTileType.Road,
        }

        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Cube/Configs/MapDecorationPrefabConfig.asset";
        private const string TerrainBlendConfigPath = "Assets/Data/Cube/Configs/MapTerrainBlendConfig.asset";
        private const string RootName = "MapRoot";

        private readonly Dictionary<Vector3Int, MapCellData> tileMap = new Dictionary<Vector3Int, MapCellData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Vector3Int, List<MapObjectData>> objectsByCoord = new Dictionary<Vector3Int, List<MapObjectData>>();
        private readonly HashSet<Vector3Int> paintedThisDrag = new HashSet<Vector3Int>();
        private readonly List<GameObject> markers = new List<GameObject>();
        private readonly List<GameObject> decorationObjects = new List<GameObject>();
        private readonly List<int> decorationIdOptions = new List<int>();
        private readonly List<string> decorationNameOptions = new List<string>();

        [TabGroup("Map"), LabelText("Map Id"), SerializeField]
        private int mapId = 1;

        [TabGroup("Map"), LabelText("Map Name"), SerializeField]
        private string mapName = "NewMap";

        [TabGroup("Map"), LabelText("Description"), TextArea, SerializeField]
        private string description = "Tower defense map";

        [TabGroup("Map"), LabelText("Width X"), MinValue(1), SerializeField]
        private int width = 12;

        [TabGroup("Map"), LabelText("Height Y"), MinValue(1), SerializeField]
        private int height = 1;

        [TabGroup("Map"), LabelText("Depth Z"), MinValue(1), SerializeField]
        private int depth = 12;

        [TabGroup("Map"), LabelText("Tile Size"), MinValue(0.1f), SerializeField]
        private float tileSize = 1f;

        [TabGroup("Map"), LabelText("Default Type"), EnumToggleButtons, SerializeField]
        private TypeBrush defaultTileType = TypeBrush.Grass;

        [TabGroup("Map"), LabelText("Prefab Config"), SerializeField]
        private MapTilePrefabConfig prefabConfig;

        [TabGroup("Map"), LabelText("Decoration Config"), SerializeField]
        private MapDecorationPrefabConfig decorationConfig;

        [TabGroup("Map"), LabelText("Terrain Blend Config"), SerializeField]
        private MapTerrainBlendConfig terrainBlendConfig;

        [TabGroup("Map"), LabelText("Preview Root"), ReadOnly, SerializeField]
        private Transform previewRoot;

        [TabGroup("Map"), LabelText("Debug Tile Picking"), SerializeField]
        private bool debugTilePicking;

        [HideInInspector, SerializeField]
        private TypeBrush brushTileType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay brushOverlay = MapTileOverlay.None;

        [TabGroup("Paint"), LabelText("Type Direction"), EnumToggleButtons, SerializeField]
        private MapDirection brushTypeDirection = MapDirection.North;

        [TabGroup("Paint"), LabelText("Overlay Direction"), EnumToggleButtons, SerializeField]
        private MapDirection brushOverlayDirection = MapDirection.North;

        [TabGroup("Paint"), OnInspectorGUI]
        private void DrawPaintBrushPreviews()
        {
            EditorGUILayout.HelpBox("Paint 页面说明：\n- 开关笔刷：开启后可在 Scene 里点击/拖动刷地块。\n- Brush Mode = Type 时，使用地块预览刷 Grass/Hill/Snow/Water/Road。\n- Brush Mode = Overlay 时，使用覆盖层预览刷 None/Bridge/Stair/Ramp。\n- Road 是地块 Type，不再作为 Overlay 使用。\n- 填充选中高度层使用当前 Type Brush。", MessageType.Info);

            DrawTypeBrushPreviewSelector();
            GUILayout.Space(6f);
            DrawOverlayBrushPreviewSelector();
            GUILayout.Space(8f);
            DrawPaintToolButtons();
        }

        private void DrawTypeBrushPreviewSelector()
        {
            EditorGUILayout.LabelField("Type Brush / 基础地块", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawTypeBrushPreviewButton(TypeBrush.Grass);
            DrawTypeBrushPreviewButton(TypeBrush.Hill);
            DrawTypeBrushPreviewButton(TypeBrush.Snow);
            DrawTypeBrushPreviewButton(TypeBrush.Water);
            DrawTypeBrushPreviewButton(TypeBrush.Road);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTypeBrushPreviewButton(TypeBrush type)
        {
            MapTileType mapTileType = ToMapTileType(type);
            if (DrawBrushPreviewButton(type.ToString(), GetPrefab(mapTileType), brushTileType == type, GetFallbackColor(mapTileType)))
            {
                brushTileType = type;
                if (brushMode != BrushMode.Type) brushMode = BrushMode.Type;
            }
        }

        private void DrawOverlayBrushPreviewSelector()
        {
            EditorGUILayout.LabelField("Overlay Brush / 覆盖层", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawOverlayBrushPreviewButton(MapTileOverlay.None);
            DrawOverlayBrushPreviewButton(MapTileOverlay.Bridge);
            DrawOverlayBrushPreviewButton(MapTileOverlay.Stair);
            DrawOverlayBrushPreviewButton(MapTileOverlay.Ramp);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverlayBrushPreviewButton(MapTileOverlay overlay)
        {
            if (DrawBrushPreviewButton(overlay.ToString(), GetOverlayPreviewPrefab(overlay), brushOverlay == overlay, GetOverlayFallbackColor(overlay)))
            {
                brushOverlay = overlay;
                if (brushMode != BrushMode.Overlay) brushMode = BrushMode.Overlay;
            }
        }

        private bool DrawBrushPreviewButton(string label, GameObject prefab, bool selected, Color fallbackColor)
        {
            Color oldBackgroundColor = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(104f));
            Texture2D preview = GetPrefabPreview(prefab);
            GUIContent previewContent = preview != null ? new GUIContent(preview) : new GUIContent(label);
            bool clicked = GUILayout.Button(previewContent, GUILayout.Width(92f), GUILayout.Height(72f));

            GUIStyle labelStyle = selected ? EditorStyles.boldLabel : EditorStyles.centeredGreyMiniLabel;
            Rect colorRect = GUILayoutUtility.GetRect(92f, 8f, GUILayout.Width(92f));
            EditorGUI.DrawRect(colorRect, fallbackColor);
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(92f));
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = oldBackgroundColor;
            return clicked;
        }

        private GameObject GetOverlayPreviewPrefab(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return GetPrefab(MapTileType.Bridge);

                default:
                    return null;
            }
        }

        private Color GetOverlayFallbackColor(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.None:
                    return new Color(0.25f, 0.25f, 0.25f);

                case MapTileOverlay.Bridge:
                    return GetFallbackColor(MapTileType.Bridge);

                case MapTileOverlay.Stair:
                    return new Color(0.75f, 0.62f, 0.42f);

                case MapTileOverlay.Ramp:
                    return new Color(0.65f, 0.55f, 0.35f);

                default:
                    return Color.magenta;
            }
        }

        private void DrawPaintToolButtons()
        {
            EditorGUILayout.LabelField("Paint Tools / 笔刷工具", EditorStyles.boldLabel);

            bool toggleBrush;
            bool fillLayer;
            bool clearOverlay;
            bool raise;
            bool lower;

            using (new EditorGUILayout.HorizontalScope())
            {
                toggleBrush = GUILayout.Button(brushEnabled ? "关闭笔刷\nToggle Off" : "开启笔刷\nToggle On", GUILayout.Width(118f), GUILayout.Height(44f));
                fillLayer = GUILayout.Button("填充当前高度层\nFill Y Layer", GUILayout.Width(138f), GUILayout.Height(44f));
                clearOverlay = GUILayout.Button("清除覆盖层\nClear Overlay", GUILayout.Width(118f), GUILayout.Height(44f));
                raise = DrawPaintModeButton("升高笔刷\nRaise", BrushMode.Raise, 96f);
                lower = DrawPaintModeButton("降低笔刷\nLower", BrushMode.Lower, 96f);
            }

            if (toggleBrush) ToggleBrush();
            if (fillLayer) FillSelectedLayer();
            if (clearOverlay) ClearOverlayBrushArea();
            if (raise) SetPaintBrushMode(BrushMode.Raise);
            if (lower) SetPaintBrushMode(BrushMode.Lower);
        }

        private bool DrawPaintModeButton(string label, BrushMode mode, float width)
        {
            Color oldBackgroundColor = GUI.backgroundColor;
            if (brushEnabled && brushMode == mode) GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
            bool clicked = GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(44f));
            GUI.backgroundColor = oldBackgroundColor;
            return clicked;
        }

        private void SetPaintBrushMode(BrushMode mode)
        {
            brushMode = mode;
            brushEnabled = true;
            paintedThisDrag.Clear();
            SceneView.RepaintAll();
        }

        [TabGroup("Paint"), LabelText("Brush Enabled"), SerializeField]
        private bool brushEnabled;

        [TabGroup("Paint"), LabelText("Brush Mode"), EnumToggleButtons, SerializeField]
        private BrushMode brushMode = BrushMode.Type;

        [TabGroup("Paint"), LabelText("Brush Size"), MinValue(1), MaxValue(9), SerializeField]
        private int brushSize = 1;

        [TabGroup("Paint"), LabelText("Skip Spawn/Goal"), SerializeField]
        private bool skipPointTiles = true;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Selected Coord")]
        private Vector3Int SelectedCoord => selectedCoord;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Has Selection")]
        private bool HasSelection => selectedTile != null;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Current Type")]
        private MapTileType SelectedCurrentType => selectedTile != null ? selectedTile.Type : MapTileType.None;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Current Overlay")]
        private MapTileOverlay SelectedCurrentOverlay => selectedTile != null ? selectedTile.Overlay.Type : MapTileOverlay.None;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Current Type Direction")]
        private MapDirection SelectedCurrentTypeDirection => selectedTile != null ? selectedTile.TypeDirection : MapDirection.None;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Current Overlay Direction")]
        private MapDirection SelectedCurrentOverlayDirection => selectedTile != null ? selectedTile.OverlayDirection : MapDirection.None;

        [HideInInspector, SerializeField]
        private TypeBrush selectedNewType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay selectedNewOverlay = MapTileOverlay.None;

        [TabGroup("Selection"), LabelText("New Type Direction"), EnumToggleButtons, SerializeField]
        private MapDirection selectedNewTypeDirection = MapDirection.North;

        [TabGroup("Selection"), LabelText("New Overlay Direction"), EnumToggleButtons, SerializeField]
        private MapDirection selectedNewOverlayDirection = MapDirection.North;

        [TabGroup("Selection"), OnInspectorGUI]
        private void DrawSelectionPreviewSelectors()
        {
            EditorGUILayout.HelpBox("Selection 页面说明：\n- 点击预览卡片选择要应用到当前地块的 Type 或 Overlay。\n- 点击 Apply Type To Selected / Apply Overlay To Selected 后才会修改当前地块。", MessageType.Info);

            DrawSelectionTypePreviewSelector();
            GUILayout.Space(6f);
            DrawSelectionOverlayPreviewSelector();
            GUILayout.Space(8f);
            DrawSelectionToolButtons();
        }

        private void DrawSelectionToolButtons()
        {
            bool applyType = false;
            bool applyOverlay = false;
            bool resetLogic = false;
            bool addAbove = false;
            bool remove = false;

            using (new EditorGUI.DisabledScope(!HasSelection))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    applyType = GUILayout.Button("应用 Type\nApply Type", GUILayout.Width(112f), GUILayout.Height(44f));
                    applyOverlay = GUILayout.Button("应用 Overlay\nApply Overlay", GUILayout.Width(128f), GUILayout.Height(44f));
                    resetLogic = GUILayout.Button("重置逻辑\nReset Logic", GUILayout.Width(112f), GUILayout.Height(44f));
                    addAbove = GUILayout.Button("上方加地块\nAdd Above", GUILayout.Width(116f), GUILayout.Height(44f));
                    remove = GUILayout.Button("删除地块\nRemove", GUILayout.Width(104f), GUILayout.Height(44f));
                }
            }

            if (applyType) ApplyTypeToSelected();
            if (applyOverlay) ApplyOverlayToSelected();
            if (resetLogic) ResetSelectedLogic();
            if (addAbove) AddTileAboveSelected();
            if (remove) RemoveSelectedTile();
        }

        private void DrawSelectionTypePreviewSelector()
        {
            EditorGUILayout.LabelField("New Type / 新基础地块", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSelectionTypePreviewButton(TypeBrush.Grass);
            DrawSelectionTypePreviewButton(TypeBrush.Hill);
            DrawSelectionTypePreviewButton(TypeBrush.Snow);
            DrawSelectionTypePreviewButton(TypeBrush.Water);
            DrawSelectionTypePreviewButton(TypeBrush.Road);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionTypePreviewButton(TypeBrush type)
        {
            MapTileType mapTileType = ToMapTileType(type);
            if (DrawBrushPreviewButton(type.ToString(), GetPrefab(mapTileType), selectedNewType == type, GetFallbackColor(mapTileType)))
            {
                selectedNewType = type;
            }
        }

        private void DrawSelectionOverlayPreviewSelector()
        {
            EditorGUILayout.LabelField("New Overlay / 新覆盖层", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSelectionOverlayPreviewButton(MapTileOverlay.None);
            DrawSelectionOverlayPreviewButton(MapTileOverlay.Bridge);
            DrawSelectionOverlayPreviewButton(MapTileOverlay.Stair);
            DrawSelectionOverlayPreviewButton(MapTileOverlay.Ramp);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionOverlayPreviewButton(MapTileOverlay overlay)
        {
            if (DrawBrushPreviewButton(overlay.ToString(), GetOverlayPreviewPrefab(overlay), selectedNewOverlay == overlay, GetOverlayFallbackColor(overlay)))
            {
                selectedNewOverlay = overlay;
            }
        }

        [TabGroup("Selection"), ShowInInspector, EnableIf("HasSelection")]
        private bool Walkable
        {
            get => selectedTile != null && selectedTile.Walkable;
            set { if (selectedTile != null) selectedTile.Walkable = value; }
        }

        [TabGroup("Selection"), ShowInInspector, EnableIf("HasSelection")]
        private bool Buildable
        {
            get => selectedTile != null && selectedTile.Buildable;
            set { if (selectedTile != null) selectedTile.Buildable = value; }
        }

        [TabGroup("Selection"), ShowInInspector, EnableIf("HasSelection"), MinValue(0)]
        private int MoveCost
        {
            get => selectedTile != null ? selectedTile.MoveCost : 0;
            set { if (selectedTile != null) selectedTile.MoveCost = Mathf.Max(0, value); }
        }

        [HideInInspector, SerializeField]
        private MapDecorationPrefabConfig decorationConfigInDecorationTab;

        [HideInInspector, SerializeField]
        private int selectedDecorationId;

        private string SelectedDecorationName => GetSelectedDecorationItem() != null ? GetSelectedDecorationItem().Name : "None";

        [HideInInspector, SerializeField]
        private Vector3 decorationLocalPosition = Vector3.zero;

        [HideInInspector, SerializeField]
        private Vector3 decorationLocalEuler = Vector3.zero;

        [HideInInspector, SerializeField]
        private Vector3 decorationLocalScale = Vector3.one;

        private int DecorationCount => currentMap != null && currentMap.Objects != null ? currentMap.Objects.Count : 0;
        private Vector2 decorationPreviewScroll;

        [TabGroup("Points"), LabelText("Spawn Points"), SerializeField]
        private List<Vector3Int> spawnPoints = new List<Vector3Int>();

        [TabGroup("Points"), LabelText("Has Goal"), SerializeField]
        private bool hasGoalPoint;

        [TabGroup("Points"), LabelText("Goal Point"), SerializeField]
        private Vector3Int goalPoint;

        [TabGroup("IO"), ShowInInspector, ReadOnly, LabelText("Tile Count")]
        private int TileCount => currentMap != null && currentMap.Cells != null ? currentMap.Cells.Count : 0;

        [TabGroup("IO"), ShowInInspector, ReadOnly, LabelText("Current Map")]
        private MapData currentMap;

        private MapCellData selectedTile;
        private Vector3Int selectedCoord;

        [MenuItem("Tools/Map/Map Editor")]
        public static void Open()
        {
            MapEditorWindow window = GetWindow<MapEditorWindow>();
            window.titleContent = new GUIContent("Map Editor");
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

        [TabGroup("Map"), OnInspectorGUI]
        private void DrawMapToolButtons()
        {
            bool createGrid;
            bool rebuild;
            bool clear;

            using (new EditorGUILayout.HorizontalScope())
            {
                createGrid = GUILayout.Button("创建地图\nCreate Grid", GUILayout.Width(128f), GUILayout.Height(44f));
                rebuild = GUILayout.Button("重建预览\nRebuild", GUILayout.Width(118f), GUILayout.Height(44f));
                clear = GUILayout.Button("清空地图\nClear", GUILayout.Width(108f), GUILayout.Height(44f));
            }

            if (createGrid) CreateGridMap();
            if (rebuild) RebuildPreview();
            if (clear) ClearMap();
        }

        private void CreateGridMap()
        {
            currentMap = new MapData(mapId, mapName, width, height, depth);
            currentMap.Description = description;
            tileMap.Clear();
            objectsByCoord.Clear();
            spawnPoints.Clear();
            currentMap.Objects.Clear();
            hasGoalPoint = false;
            goalPoint = default;
            selectedTile = null;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        AddTileNoCheck(ToMapTileType(defaultTileType), x, y, z);
                    }
                }
            }

            CreatePreviewObjects();
            Debug.Log($"Map created from origin (0,0,0), positive X/Y/Z. Size: {width}x{height}x{depth}");
        }

        private void RebuildPreview()
        {
            if (!EnsureMap()) return;
            RebuildTileIndex();
            CreatePreviewObjects();
        }

        private void ClearMap()
        {
            currentMap = null;
            selectedTile = null;
            tileMap.Clear();
            objectsByCoord.Clear();
            spawnPoints.Clear();
            hasGoalPoint = false;
            ClearPreviewObjects();
        }

        [TabGroup("Decoration"), OnInspectorGUI]
        private void DrawDecorationTab()
        {
            TryLoadDecorationConfig();
            EditorGUILayout.HelpBox("Decoration 页面说明：\n- 装饰物原始资源在 MapDecorationPrefabConfig.asset 的 Inspector 里维护，使用 Odin List。\n- 地图 JSON 在 Objects 里保存 ConfigId，不保存 prefab 路径。\n- 在这里选择装饰物并放到当前选中地块。\n- 删除当前格装饰只删除该地块上的装饰物，不影响 Type/Overlay。", MessageType.Info);

            float contentWidth = Mathf.Max(980f, position.width - 24f);
            float leftWidth = Mathf.Clamp(contentWidth * 0.46f, 620f, 760f);
            float rightWidth = Mathf.Max(340f, contentWidth - leftWidth - 12f);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(contentWidth)))
            {
                DrawDecorationPlacementPanel(leftWidth);
                GUILayout.Space(12f);
                DrawDecorationSourcePreviewPanel(rightWidth);
            }
        }

        private void DrawDecorationPlacementPanel(float panelWidth)
        {
            bool addDecoration = false;
            bool removeDecorations = false;
            bool createConfig;
            bool selectConfig = false;
            bool useDefaults;
            bool clearAll;

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth)))
            {
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 155f;

            EditorGUI.BeginChangeCheck();
            decorationConfigInDecorationTab = (MapDecorationPrefabConfig)EditorGUILayout.ObjectField("Decoration Config", decorationConfigInDecorationTab, typeof(MapDecorationPrefabConfig), false);
            if (EditorGUI.EndChangeCheck())
            {
                decorationConfig = decorationConfigInDecorationTab;
                if (decorationConfig != null) decorationConfig.RebuildCache();
            }

            DrawDecorationSelector();
            EditorGUILayout.LabelField("Selected Decoration", SelectedDecorationName);
            decorationLocalPosition = EditorGUILayout.Vector3Field("Local Position", decorationLocalPosition);
            decorationLocalEuler = EditorGUILayout.Vector3Field("Local Euler", decorationLocalEuler);
            decorationLocalScale = EditorGUILayout.Vector3Field("Local Scale", decorationLocalScale);
            EditorGUILayout.LabelField("Decoration Count", DecorationCount.ToString());

            using (new EditorGUI.DisabledScope(!HasSelection))
            {
                    addDecoration = GUILayout.Button("添加装饰到选中地块 / Add Decoration To Selected");
                    removeDecorations = GUILayout.Button("删除当前格装饰 / Remove Decorations At Selected");
            }

                createConfig = GUILayout.Button("创建装饰配置 / Create Decoration Config");
            using (new EditorGUI.DisabledScope(decorationConfig == null))
            {
                    selectConfig = GUILayout.Button("选中装饰配置资源 / Select Decoration Config Asset");
            }

                useDefaults = GUILayout.Button("使用选中项默认变换 / Use Selected Defaults");
                clearAll = GUILayout.Button("清空全部装饰 / Clear All Decorations");

            EditorGUIUtility.labelWidth = oldLabelWidth;
            }

            if (addDecoration) AddDecorationToSelected();
            if (removeDecorations) RemoveDecorationsAtSelected();
            if (createConfig) CreateDecorationConfig();
            if (selectConfig) Selection.activeObject = decorationConfig;
            if (useDefaults) UseSelectedDecorationDefaults();
            if (clearAll) ClearAllDecorations();
        }

        private void DrawDecorationSourcePreviewPanel(float panelWidth)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth));
            EditorGUILayout.LabelField("装饰物资源库预览 / Source Preview", EditorStyles.boldLabel);

            if (decorationConfig == null)
            {
                EditorGUILayout.HelpBox("No decoration config. Create or assign one on the left.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (decorationConfig.Items == null || decorationConfig.Items.Count == 0)
            {
                EditorGUILayout.HelpBox("No source items. Select the config asset and add items in its Inspector.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            decorationPreviewScroll = EditorGUILayout.BeginScrollView(decorationPreviewScroll);

            for (int i = 0; i < decorationConfig.Items.Count; i++)
            {
                MapDecorationPrefabConfig.DecorationPrefabItem item = decorationConfig.Items[i];
                if (item == null) continue;

                Rect rowRect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.MinHeight(96f));
                string itemName = string.IsNullOrEmpty(item.Name) ? "Unnamed" : item.Name;
                bool isSelected = item.Id == selectedDecorationId;

                Texture2D preview = GetDecorationPreview(item.Prefab);
                GUIContent previewContent = preview != null
                    ? new GUIContent(preview)
                    : new GUIContent("No\nPrefab");

                if (GUILayout.Button(previewContent, GUILayout.Width(86f), GUILayout.Height(86f)))
                {
                    selectedDecorationId = item.Id;
                    UseSelectedDecorationDefaults();
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{item.Id} - {itemName}", isSelected ? EditorStyles.boldLabel : EditorStyles.label);
                EditorGUILayout.LabelField("Category", string.IsNullOrEmpty(item.Category) ? "None" : item.Category);
                EditorGUILayout.ObjectField("Prefab", item.Prefab, typeof(GameObject), false);

                if (item.Prefab == null)
                {
                    EditorGUILayout.HelpBox("No prefab assigned in config asset.", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Select", GUILayout.Width(72f)))
                {
                    selectedDecorationId = item.Id;
                    UseSelectedDecorationDefaults();
                }

                EditorGUILayout.EndHorizontal();
                HandleDecorationPreviewRowClick(rowRect, item);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void HandleDecorationPreviewRowClick(Rect rowRect, MapDecorationPrefabConfig.DecorationPrefabItem item)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!rowRect.Contains(current.mousePosition))
            {
                return;
            }

            selectedDecorationId = item.Id;
            UseSelectedDecorationDefaults();
            current.Use();
            Repaint();
        }

        private Texture2D GetDecorationPreview(GameObject prefab)
        {
            return GetPrefabPreview(prefab);
        }

        private Texture2D GetPrefabPreview(GameObject prefab)
        {
            if (prefab == null) return null;

            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            return preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab);
        }

        private void DrawDecorationSelector()
        {
            BuildDecorationOptions();
            if (decorationIdOptions.Count == 0)
            {
                EditorGUILayout.Popup("Selected Decoration", 0, new[] { "None" });
                selectedDecorationId = 0;
                return;
            }

            int selectedIndex = Mathf.Max(0, decorationIdOptions.IndexOf(selectedDecorationId));
            int newIndex = EditorGUILayout.Popup("Selected Decoration", selectedIndex, decorationNameOptions.ToArray());
            selectedDecorationId = decorationIdOptions[Mathf.Clamp(newIndex, 0, decorationIdOptions.Count - 1)];
        }

        private void BuildDecorationOptions()
        {
            decorationIdOptions.Clear();
            decorationNameOptions.Clear();

            if (decorationConfig == null || decorationConfig.Items == null)
            {
                return;
            }

            for (int i = 0; i < decorationConfig.Items.Count; i++)
            {
                MapDecorationPrefabConfig.DecorationPrefabItem item = decorationConfig.Items[i];
                if (item == null || item.Id <= 0)
                {
                    continue;
                }

                decorationIdOptions.Add(item.Id);
                string name = string.IsNullOrEmpty(item.Name) ? "Unnamed" : item.Name;
                decorationNameOptions.Add($"{item.Id} - {name}");
            }
        }

        private void AddDecorationToSelected()
        {
            if (!EnsureMap()) return;
            TryLoadDecorationConfig();
            MapDecorationPrefabConfig.DecorationPrefabItem item = GetSelectedDecorationItem();

            if (item == null || item.Prefab == null)
            {
                EditorUtility.DisplayDialog("Decoration", "Please select a valid Decoration Id from the source list.", "OK");
                return;
            }

            currentMap.Objects.Add(new MapObjectData(item.Id, selectedCoord, decorationLocalPosition, decorationLocalEuler, decorationLocalScale));
            RebuildObjectIndex();
            RefreshDecorations();
        }

        private void CreateDecorationConfig()
        {
            MapDecorationPrefabConfig existingConfig = AssetDatabase.LoadAssetAtPath<MapDecorationPrefabConfig>(DecorationConfigPath);
            if (existingConfig != null)
            {
                decorationConfig = existingConfig;
                decorationConfigInDecorationTab = existingConfig;
                Selection.activeObject = existingConfig;
                return;
            }

            MapDecorationPrefabConfig newConfig = CreateInstance<MapDecorationPrefabConfig>();
            AssetDatabase.CreateAsset(newConfig, DecorationConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            decorationConfig = newConfig;
            decorationConfigInDecorationTab = newConfig;
            Selection.activeObject = newConfig;
        }

        private void UseSelectedDecorationDefaults()
        {
            MapDecorationPrefabConfig.DecorationPrefabItem item = GetSelectedDecorationItem();
            if (item == null) return;

            decorationLocalPosition = item.DefaultLocalPosition;
            decorationLocalEuler = item.DefaultLocalEuler;
            decorationLocalScale = item.DefaultLocalScale;
        }

        private void RemoveDecorationsAtSelected()
        {
            if (!EnsureMap()) return;
            currentMap.Objects.RemoveAll(decoration => decoration != null && decoration.Coord == selectedCoord);
            RebuildObjectIndex();
            RefreshDecorations();
        }

        private void ClearAllDecorations()
        {
            if (!EnsureMap()) return;
            if (!EditorUtility.DisplayDialog("Clear Decorations", "Clear all decorations in current map?", "Clear", "Cancel")) return;
            currentMap.Objects.Clear();
            RebuildObjectIndex();
            RefreshDecorations();
        }

        private void ToggleBrush()
        {
            brushEnabled = !brushEnabled;
            paintedThisDrag.Clear();
            SceneView.RepaintAll();
        }

        private void FillSelectedLayer()
        {
            if (!EnsureMap()) return;
            int y = HasSelection ? selectedCoord.y : 0;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    PaintTile(new Vector3Int(x, y, z), ToMapTileType(brushTileType), brushTypeDirection, false);
                }
            }
        }

        private void ClearOverlayBrushArea()
        {
            if (!HasSelection) return;
            MapTileOverlay oldOverlay = brushOverlay;
            brushOverlay = MapTileOverlay.None;
            PaintOverlayBrushAt(selectedCoord);
            brushOverlay = oldOverlay;
        }

        private void RaiseBrushAreaOnce()
        {
            if (!HasSelection) return;
            RaiseBrushAt(selectedCoord);
        }

        private void LowerBrushAreaOnce()
        {
            if (!HasSelection) return;
            LowerBrushAt(selectedCoord);
        }

        private void ApplyTypeToSelected()
        {
            PaintTile(selectedCoord, ToMapTileType(selectedNewType), selectedNewTypeDirection, true);
            SelectTile(selectedCoord);
        }

        private void ApplyOverlayToSelected()
        {
            PaintOverlay(selectedCoord, selectedNewOverlay, selectedNewOverlayDirection, true);
            SelectTile(selectedCoord);
        }

        private void ResetSelectedLogic()
        {
            if (selectedTile != null) selectedTile.ApplyDefaultLogic();
        }

        private void AddTileAboveSelected()
        {
            Vector3Int above = new Vector3Int(selectedCoord.x, selectedCoord.y + 1, selectedCoord.z);
            if (TryAddTile(above, ToMapTileType(selectedNewType), selectedNewTypeDirection, true)) SelectTile(above);
        }

        private void RemoveSelectedTile()
        {
            Vector3Int oldCoord = selectedCoord;
            if (!TryRemoveTile(oldCoord, true)) return;

            if (TryGetTopTile(oldCoord.x, oldCoord.z, out MapCellData topTile))
            {
                SelectTile(new Vector3Int(topTile.X, topTile.Y, topTile.Z));
            }
            else
            {
                selectedTile = null;
                selectedCoord = default;
            }
        }

        [TabGroup("Points"), OnInspectorGUI]
        private void DrawPointsToolButtons()
        {
            bool addSpawn = false;
            bool setGoal = false;
            bool applyPoints;
            bool clearPoints;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasSelection))
                {
                    addSpawn = GUILayout.Button("设为出生点\nAdd Spawn", GUILayout.Width(116f), GUILayout.Height(44f));
                    setGoal = GUILayout.Button("设为终点\nSet Goal", GUILayout.Width(108f), GUILayout.Height(44f));
                }

                applyPoints = GUILayout.Button("应用点位\nApply Points", GUILayout.Width(112f), GUILayout.Height(44f));
                clearPoints = GUILayout.Button("清空点位\nClear Points", GUILayout.Width(112f), GUILayout.Height(44f));
            }

            if (addSpawn) AddSelectedAsSpawn();
            if (setGoal) SetSelectedAsGoal();
            if (applyPoints) ApplyPointsToMap();
            if (clearPoints) ClearPoints();
        }

        private void AddSelectedAsSpawn()
        {
            if (!MapTileRule.IsValidMapPoint(selectedCoord, currentMap, out string reason))
            {
                EditorUtility.DisplayDialog("Invalid Spawn", reason, "OK");
                return;
            }

            if (!spawnPoints.Contains(selectedCoord)) spawnPoints.Add(selectedCoord);
            ApplyPointsToMap();
        }

        private void SetSelectedAsGoal()
        {
            if (!MapTileRule.IsValidMapPoint(selectedCoord, currentMap, out string reason))
            {
                EditorUtility.DisplayDialog("Invalid Goal", reason, "OK");
                return;
            }

            goalPoint = selectedCoord;
            hasGoalPoint = true;
            ApplyPointsToMap();
        }

        private void ApplyPointsToMap()
        {
            if (!EnsureMap()) return;
            currentMap.SpawnPoints.Clear();

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3Int point = spawnPoints[i];
                if (MapTileRule.IsValidMapPoint(point, currentMap, out _) && !currentMap.SpawnPoints.Contains(point))
                {
                    currentMap.SpawnPoints.Add(point);
                }
            }

            currentMap.HasGoalPoint = false;
            currentMap.GoalPoint = default;

            if (hasGoalPoint && MapTileRule.IsValidMapPoint(goalPoint, currentMap, out _))
            {
                currentMap.HasGoalPoint = true;
                currentMap.GoalPoint = goalPoint;
            }

            RefreshMarkers();
        }

        private void ClearPoints()
        {
            spawnPoints.Clear();
            hasGoalPoint = false;
            goalPoint = default;

            if (currentMap != null)
            {
                currentMap.SpawnPoints.Clear();
                currentMap.HasGoalPoint = false;
                currentMap.GoalPoint = default;
            }

            RefreshMarkers();
        }
        [TabGroup("IO"), OnInspectorGUI]
        private void DrawIOToolButtons()
        {
            bool import;
            bool export;
            bool validate;

            using (new EditorGUILayout.HorizontalScope())
            {
                import = GUILayout.Button("导入 Json\nImport", GUILayout.Width(112f), GUILayout.Height(44f));
                export = GUILayout.Button("导出 Json\nExport", GUILayout.Width(112f), GUILayout.Height(44f));
                validate = GUILayout.Button("校验地图\nValidate", GUILayout.Width(112f), GUILayout.Height(44f));
            }

            if (import) ImportJson();
            if (export) ExportJson();
            if (validate) ValidateCurrentMap();
        }

        private void ImportJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Map Json", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            MapData data = JsonConvert.DeserializeObject<MapData>(File.ReadAllText(path));
            if (data == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "Json did not contain valid MapData.", "OK");
                return;
            }

            data.EnsureRuntimeCollections();
            currentMap = data;
            PullSettingsFromMap();
            RebuildTileIndex();
            CreatePreviewObjects();
            Debug.Log($"Map imported: {path}");
        }

        private void ExportJson()
        {
            if (!EnsureMap()) return;
            ApplyPointsToMap();
            List<string> errors = ValidateMap(currentMap);

            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++) Debug.LogWarning(errors[i]);
                if (!EditorUtility.DisplayDialog("Map Validation", $"Found {errors.Count} issue(s). Export anyway?", "Export", "Cancel")) return;
            }

            string path = EditorUtility.SaveFilePanel("Export Map Json", Application.dataPath, currentMap.Id + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, JsonConvert.SerializeObject(currentMap, Formatting.Indented));
            AssetDatabase.Refresh();
            Debug.Log($"Map exported: {path}");
        }

        private void ValidateCurrentMap()
        {
            if (!EnsureMap()) return;
            List<string> errors = ValidateMap(currentMap);

            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Map Validation", "Map is valid.", "OK");
                return;
            }

            for (int i = 0; i < errors.Count; i++) Debug.LogWarning(errors[i]);
            EditorUtility.DisplayDialog("Map Validation", $"Found {errors.Count} issue(s). Check Console.", "OK");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (currentMap == null) return;
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.Layout)
            {
                if (brushEnabled)
                {
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                }

                return;
            }

            if (e.type == EventType.MouseUp)
            {
                paintedThisDrag.Clear();
                return;
            }

            DrawBrushGhost(e.mousePosition);

            if (brushEnabled && e.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }

            if (brushEnabled)
            {
                if (e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)) return;
                if (!TryPickTile(e.mousePosition, out Vector3Int coord, e.type == EventType.MouseDown)) return;

                ApplyBrushAt(coord);
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (e.button != 0 || e.type != EventType.MouseDown)
            {
                return;
            }

            if (!TryPickTile(e.mousePosition, out Vector3Int selectedCoord, true))
            {
                return;
            }

            SelectTile(selectedCoord);
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryPickTile(Vector2 mousePosition, out Vector3Int coord, bool logFailure = false)
        {
            coord = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            return TryPickTileByCollider(ray, out coord, logFailure);
        }

        private bool TryPickTileByCollider(Ray ray, out Vector3Int coord, bool logFailure)
        {
            coord = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                if (logFailure) LogTilePickFailure("raycast hit nothing.");
                return false;
            }

            float nearestDistance = float.MaxValue;
            bool hasCoord = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                if (!TryGetTileViewCoord(hit.collider.transform, out Vector3Int hitCoord))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                coord = hitCoord;
                hasCoord = true;
            }

            if (logFailure && !hasCoord)
            {
                LogTilePickFailure(BuildTilePickHitLog(hits));
            }

            return hasCoord;
        }

        private void LogTilePickFailure(string message)
        {
            if (!debugTilePicking)
            {
                return;
            }

            Debug.LogWarning($"[MapEditor] Tile pick failed: {message}");
        }

        private string BuildTilePickHitLog(RaycastHit[] hits)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("hit colliders, but no valid TileView with TileData was found:");

            int count = Mathf.Min(hits.Length, 8);
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                TileView.TryGetValidFrom(collider.transform, out TileView tileView);
                builder.Append(" [");
                builder.Append(collider.name);
                builder.Append(", path=");
                builder.Append(GetTransformPath(collider.transform));
                builder.Append(", hasValidTileView=");
                builder.Append(tileView != null);
                builder.Append("]");
            }

            return builder.ToString();
        }

        private string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private bool TryGetTileViewCoord(Transform start, out Vector3Int coord)
        {
            coord = default;

            if (!TileView.TryGetValidFrom(start, out TileView tileView))
            {
                return false;
            }

            Vector3Int tileCoord = tileView.Coord;
            if (!tileMap.ContainsKey(tileCoord))
            {
                return false;
            }

            coord = tileCoord;
            return true;
        }

        private void DrawBrushGhost(Vector2 mousePosition)
        {
            if (!brushEnabled) return;
            if (!TryPickTile(mousePosition, out Vector3Int center)) return;

            int radius = Mathf.Max(0, brushSize / 2);
            Color fillColor = GetBrushGhostColor();
            Color outlineColor = new Color(fillColor.r, fillColor.g, fillColor.b, 0.95f);
            fillColor.a = 0.24f;

            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector3Int coord = new Vector3Int(x, center.y, z);
                    if (!tileMap.ContainsKey(coord)) continue;
                    DrawTileGhost(coord, fillColor, outlineColor);
                }
            }

            Handles.Label(GetGhostLabelPosition(center), GetBrushGhostLabel());
        }

        private void DrawTileGhost(Vector3Int coord, Color fillColor, Color outlineColor)
        {
            Bounds bounds = GetTileGhostBounds(coord);
            float y = bounds.max.y + 0.015f;
            float halfX = bounds.size.x * 0.5f;
            float halfZ = bounds.size.z * 0.5f;
            Vector3 center = new Vector3(bounds.center.x, y, bounds.center.z);

            Vector3[] corners =
            {
                center + new Vector3(-halfX, 0f, -halfZ),
                center + new Vector3(-halfX, 0f, halfZ),
                center + new Vector3(halfX, 0f, halfZ),
                center + new Vector3(halfX, 0f, -halfZ)
            };

            Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
        }

        private Bounds GetTileGhostBounds(Vector3Int coord)
        {
            return GetGridTileBounds(coord);
        }

        private Bounds GetGridTileBounds(Vector3Int coord)
        {
            return new Bounds(GetWorldPosition(coord) + Vector3.up * (tileSize * 0.5f), new Vector3(tileSize, tileSize, tileSize));
        }

        private bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private Vector3 GetGhostLabelPosition(Vector3Int coord)
        {
            Bounds bounds = GetTileGhostBounds(coord);
            return new Vector3(bounds.center.x, bounds.max.y + 0.08f, bounds.center.z);
        }

        private Color GetBrushGhostColor()
        {
            switch (brushMode)
            {
                case BrushMode.Overlay:
                    return GetOverlayFallbackColor(brushOverlay);

                case BrushMode.Raise:
                    return new Color(0.35f, 0.9f, 0.35f);

                case BrushMode.Lower:
                    return new Color(1f, 0.55f, 0.25f);

                default:
                    return GetFallbackColor(ToMapTileType(brushTileType));
            }
        }

        private string GetBrushGhostLabel()
        {
            switch (brushMode)
            {
                case BrushMode.Overlay:
                    return $"Overlay: {brushOverlay}";

                case BrushMode.Raise:
                    return "Raise +1";

                case BrushMode.Lower:
                    return "Lower -1";

                default:
                    return $"Type: {brushTileType}";
            }
        }

        private void SelectTile(Vector3Int coord)
        {
            if (!tileMap.TryGetValue(coord, out selectedTile))
            {
                selectedTile = null;
                selectedCoord = default;
                return;
            }

            selectedCoord = coord;
            selectedNewType = ToTypeBrush(selectedTile.Type);
            selectedNewTypeDirection = selectedTile.TypeDirection == MapDirection.None ? MapDirection.North : selectedTile.TypeDirection;
            selectedNewOverlay = selectedTile.Overlay.Type;
            selectedNewOverlayDirection = selectedTile.OverlayDirection == MapDirection.None ? MapDirection.North : selectedTile.OverlayDirection;
        }

        private void ApplyBrushAt(Vector3Int center)
        {
            switch (brushMode)
            {
                case BrushMode.Overlay:
                    PaintOverlayBrushAt(center);
                    break;

                case BrushMode.Raise:
                    RaiseBrushAt(center);
                    break;

                case BrushMode.Lower:
                    LowerBrushAt(center);
                    break;

                default:
                    PaintBrushAt(center);
                    break;
            }
        }

        private void PaintBrushAt(Vector3Int center)
        {
            int radius = Mathf.Max(0, brushSize / 2);

            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector3Int coord = new Vector3Int(x, center.y, z);
                    if (paintedThisDrag.Contains(coord)) continue;
                    if (PaintTile(coord, ToMapTileType(brushTileType), brushTypeDirection, false)) paintedThisDrag.Add(coord);
                }
            }
        }

        private void PaintOverlayBrushAt(Vector3Int center)
        {
            int radius = Mathf.Max(0, brushSize / 2);

            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector3Int coord = new Vector3Int(x, center.y, z);
                    if (paintedThisDrag.Contains(coord)) continue;
                    if (PaintOverlay(coord, brushOverlay, brushOverlayDirection, false)) paintedThisDrag.Add(coord);
                }
            }
        }

        private void RaiseBrushAt(Vector3Int center)
        {
            int radius = Mathf.Max(0, brushSize / 2);

            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector3Int column = new Vector3Int(x, 0, z);
                    if (paintedThisDrag.Contains(column)) continue;

                    if (!TryGetTopTile(x, z, out MapCellData topTile)) continue;

                    Vector3Int above = new Vector3Int(x, topTile.Y + 1, z);
                    if (TryAddTile(above, ToMapTileType(brushTileType), brushTypeDirection, false)) paintedThisDrag.Add(column);
                }
            }
        }

        private void LowerBrushAt(Vector3Int center)
        {
            int radius = Mathf.Max(0, brushSize / 2);

            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector3Int column = new Vector3Int(x, 0, z);
                    if (paintedThisDrag.Contains(column)) continue;

                    if (!TryGetTopTile(x, z, out MapCellData topTile)) continue;
                    if (topTile.Y <= 0) continue;

                    Vector3Int coord = new Vector3Int(topTile.X, topTile.Y, topTile.Z);
                    if (TryRemoveTile(coord, false)) paintedThisDrag.Add(column);
                }
            }
        }

        private bool PaintTile(Vector3Int coord, MapTileType type, MapDirection typeDirection, bool showDialog)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                if (showDialog) EditorUtility.DisplayDialog("Paint Failed", "Only Grass, Hill, Snow, Water, and Road can be painted as Type.", "OK");
                return false;
            }

            if (!tileMap.TryGetValue(coord, out MapCellData tile)) return false;
            if (skipPointTiles && currentMap.HasAnyPoint(coord)) return false;
            MapDirection nextTypeDirection = typeDirection == MapDirection.None ? MapDirection.North : typeDirection;
            if (tile.Type == type && tile.TypeDirection == nextTypeDirection) return false;

            tile.ApplyDefaultLogicByType(type);
            tile.TypeDirection = nextTypeDirection;
            RecreatePreviewObject(coord);
            RefreshVisualAround(coord);
            RefreshMarkers();
            if (selectedTile == tile)
            {
                selectedNewType = ToTypeBrush(type);
                selectedNewTypeDirection = nextTypeDirection;
            }
            return true;
        }

        private void AddTileNoCheck(MapTileType type, int x, int y, int z)
        {
            AddTileNoCheck(type, MapDirection.North, x, y, z);
        }

        private void AddTileNoCheck(MapTileType type, MapDirection typeDirection, int x, int y, int z)
        {
            MapCellData tile = new MapCellData(x, y, z, type);
            tile.TypeDirection = typeDirection == MapDirection.None ? MapDirection.North : typeDirection;
            currentMap.Cells.Add(tile);
            tileMap[new Vector3Int(x, y, z)] = tile;
        }

        private bool TryAddTile(Vector3Int coord, MapTileType type, MapDirection typeDirection, bool showDialog)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", "Only Grass, Hill, Snow, Water, and Road can be added as Type.", "OK");
                return false;
            }

            if (coord.x < 0 || coord.x >= width || coord.z < 0 || coord.z >= depth || coord.y < 0)
            {
                if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", $"Coord is outside map range: {coord}", "OK");
                return false;
            }

            if (tileMap.ContainsKey(coord))
            {
                if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", $"Tile already exists: {coord}", "OK");
                return false;
            }

            if (coord.y > 0)
            {
                Vector3Int belowCoord = new Vector3Int(coord.x, coord.y - 1, coord.z);
                if (!tileMap.TryGetValue(belowCoord, out MapCellData belowTile))
                {
                    if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", $"Missing tile below: {belowCoord}", "OK");
                    return false;
                }

                if (!MapTileRule.CanPlaceOn(type, belowTile.Type))
                {
                    if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", $"{type} can not be placed on {belowTile.Type}.", "OK");
                    return false;
                }
            }

            AddTileNoCheck(type, typeDirection, coord.x, coord.y, coord.z);
            height = Mathf.Max(height, coord.y + 1);
            currentMap.Height = Mathf.Max(currentMap.Height, height);
            CreatePreviewObject(tileMap[coord]);
            RefreshVisualAround(coord);
            RefreshMarkers();
            return true;
        }

        private bool PaintOverlay(Vector3Int coord, MapTileOverlay overlay, MapDirection overlayDirection, bool showDialog)
        {
            if (!tileMap.TryGetValue(coord, out MapCellData tile))
            {
                return false;
            }

            if (skipPointTiles && currentMap.HasAnyPoint(coord))
            {
                return false;
            }

            MapDirection nextDirection = overlay == MapTileOverlay.None
                ? MapDirection.None
                : (overlayDirection == MapDirection.None ? MapDirection.North : overlayDirection);

            if (tile.Overlay.Type == overlay && tile.OverlayDirection == nextDirection)
            {
                return false;
            }

            tile.Overlay.Type = overlay;
            tile.OverlayDirection = nextDirection;
            tile.ApplyDefaultLogic();

            RecreatePreviewObject(coord);
            RefreshMarkers();
            return true;
        }

        private bool TryRemoveTile(Vector3Int coord, bool showDialog)
        {
            if (!tileMap.TryGetValue(coord, out MapCellData tile))
            {
                return false;
            }

            if (coord.y <= 0)
            {
                if (showDialog) EditorUtility.DisplayDialog("Remove Tile Failed", "The bottom layer can not be removed.", "OK");
                return false;
            }

            Vector3Int above = new Vector3Int(coord.x, coord.y + 1, coord.z);
            if (tileMap.ContainsKey(above))
            {
                if (showDialog) EditorUtility.DisplayDialog("Remove Tile Failed", $"Remove upper tile first: {above}", "OK");
                return false;
            }

            if (currentMap.HasAnyPoint(coord))
            {
                if (showDialog) EditorUtility.DisplayDialog("Remove Tile Failed", "Spawn/Goal point is on this tile.", "OK");
                return false;
            }

            currentMap.Cells.Remove(tile);
            tileMap.Remove(coord);

            if (tileObjects.TryGetValue(coord, out GameObject oldObject))
            {
                if (oldObject != null) DestroyImmediate(oldObject);
                tileObjects.Remove(coord);
            }

            RecalculateMapHeight();
            RefreshVisualAround(coord);
            RefreshMarkers();
            return true;
        }

        private bool TryGetTopTile(int x, int z, out MapCellData topTile)
        {
            topTile = null;
            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, MapCellData> pair in tileMap)
            {
                Vector3Int coord = pair.Key;
                if (coord.x != x || coord.z != z) continue;

                if (coord.y > topY)
                {
                    topY = coord.y;
                    topTile = pair.Value;
                }
            }

            return topTile != null;
        }

        private void RecalculateMapHeight()
        {
            int maxY = 0;

            foreach (Vector3Int coord in tileMap.Keys)
            {
                if (coord.y > maxY) maxY = coord.y;
            }

            height = Mathf.Max(1, maxY + 1);
            if (currentMap != null) currentMap.Height = height;
        }

        private void PullSettingsFromMap()
        {
            mapId = currentMap.Id;
            mapName = currentMap.Name;
            description = currentMap.Description;
            width = Mathf.Max(1, currentMap.Width);
            height = Mathf.Max(1, currentMap.Height);
            depth = Mathf.Max(1, currentMap.Depth);
            spawnPoints.Clear();
            if (currentMap.SpawnPoints != null) spawnPoints.AddRange(currentMap.SpawnPoints);
            hasGoalPoint = currentMap.HasGoalPoint;
            goalPoint = currentMap.GoalPoint;
            selectedTile = null;
        }

        private void RebuildTileIndex()
        {
            tileMap.Clear();
            objectsByCoord.Clear();
            if (currentMap == null) return;
            currentMap.EnsureRuntimeCollections();

            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData tile = currentMap.Cells[i];
                if (tile == null) continue;
                tileMap[new Vector3Int(tile.X, tile.Y, tile.Z)] = tile;
            }

            RebuildObjectIndex();
        }

        private void RebuildObjectIndex()
        {
            objectsByCoord.Clear();

            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                AddObjectToIndex(currentMap.Objects[i]);
            }
        }

        private void AddObjectToIndex(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return;
            }

            Vector3Int coord = mapObject.Coord;
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects))
            {
                objects = new List<MapObjectData>();
                objectsByCoord[coord] = objects;
            }

            objects.Add(mapObject);
        }

        private bool TryGetObjectsAt(Vector3Int coord, out IReadOnlyList<MapObjectData> objects)
        {
            if (objectsByCoord.TryGetValue(coord, out List<MapObjectData> result) && result.Count > 0)
            {
                objects = result;
                return true;
            }

            objects = null;
            return false;
        }

        private void CreatePreviewObjects()
        {
            ClearPreviewObjects();
            EnsurePreviewRoot();

            foreach (KeyValuePair<Vector3Int, MapCellData> pair in tileMap)
            {
                CreatePreviewObject(pair.Value);
            }

            RefreshAllVisuals();
            RefreshDecorations();
            RefreshMarkers();
        }

        private void EnsurePreviewRoot()
        {
            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null) DestroyImmediate(oldRoot);

            GameObject root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            previewRoot = root.transform;
        }

        private void CreatePreviewObject(MapCellData tile)
        {
            Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
            GameObject instance = CreateTileInstance(tile.Type);
            if (instance == null)
            {
                Debug.LogWarning($"[MapEditor] Missing tile prefab for {tile.Type}, Coord: {coord}");
                return;
            }

            instance.name = $"{tile.Type}_{tile.Overlay.Type}_{tile.X}_{tile.Y}_{tile.Z}";
            instance.transform.SetParent(previewRoot, false);
            instance.transform.position = GetWorldPosition(coord);
            instance.transform.localRotation = GetDirectionRotation(tile.TypeDirection);

            GameObject overlayVisual = CreateOverlayInstance(tile.Overlay.Type, tile.OverlayDirection);
            if (overlayVisual != null)
            {
                overlayVisual.name = $"Overlay_{tile.Overlay.Type}_{tile.OverlayDirection}";
                overlayVisual.transform.SetParent(instance.transform, false);
                overlayVisual.transform.localPosition = GetOverlayLocalPosition(tile.Overlay.Type);
                overlayVisual.transform.localRotation = Quaternion.Inverse(instance.transform.localRotation) * GetDirectionRotation(tile.OverlayDirection);
            }

            TileView tileView = TileView.InitializeHierarchy(instance, new TileData(tile));
            if (tileView == null)
            {
                Debug.LogWarning($"[MapEditor] Tile prefab root must contain TileView: {tile.Type}, Coord: {coord}, Instance: {instance.name}");
            }

            if (instance.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"[MapEditor] Tile prefab root should contain a Collider for picking: {tile.Type}, Coord: {coord}, Instance: {instance.name}");
            }

            tileObjects[coord] = instance;
            RefreshTerrainBlend(coord);
        }

        private GameObject CreateTileInstance(MapTileType type)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab != null)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                return prefabInstance != null ? prefabInstance : Instantiate(prefab);
            }

            return null;
        }

        private GameObject CreateOverlayInstance(MapTileOverlay overlay, MapDirection direction)
        {
            switch (overlay)
            {
                case MapTileOverlay.Road:
                    return CreateTileInstance(MapTileType.Road);

                case MapTileOverlay.Bridge:
                    return CreateTileInstance(MapTileType.Bridge);

                case MapTileOverlay.Stair:
                    return CreateOverlayFallback("Stair", new Color(0.75f, 0.62f, 0.42f));

                case MapTileOverlay.Ramp:
                    return CreateOverlayFallback("Ramp", new Color(0.65f, 0.55f, 0.35f));

                default:
                    return null;
            }
        }

        private GameObject CreateOverlayFallback(string name, Color color)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = name;
            fallback.transform.localScale = new Vector3(tileSize * 0.85f, 0.08f, tileSize * 0.85f);
            fallback.transform.localPosition = Vector3.zero;

            Renderer renderer = fallback.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            return fallback;
        }

        private Vector3 GetOverlayLocalPosition(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return Vector3.up * tileSize;

                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return Vector3.up * (tileSize * 0.5f);

                default:
                    return Vector3.zero;
            }
        }

        private Quaternion GetDirectionRotation(MapDirection direction)
        {
            switch (direction)
            {
                case MapDirection.East:
                    return Quaternion.Euler(0f, 90f, 0f);

                case MapDirection.South:
                    return Quaternion.Euler(0f, 180f, 0f);

                case MapDirection.West:
                    return Quaternion.Euler(0f, 270f, 0f);

                default:
                    return Quaternion.identity;
            }
        }

        private Color GetFallbackColor(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Grass: return new Color(0.25f, 0.65f, 0.25f);
                case MapTileType.Hill: return new Color(0.45f, 0.35f, 0.22f);
                case MapTileType.Water: return new Color(0.2f, 0.45f, 0.9f);
                case MapTileType.Snow: return new Color(0.85f, 0.9f, 0.95f);
                case MapTileType.Road: return new Color(0.45f, 0.4f, 0.35f);
                case MapTileType.Bridge: return new Color(0.55f, 0.36f, 0.18f);
                case MapTileType.Soil: return new Color(0.35f, 0.24f, 0.15f);
                default: return Color.magenta;
            }
        }

        private void RecreatePreviewObject(Vector3Int coord)
        {
            if (tileObjects.TryGetValue(coord, out GameObject oldObject))
            {
                if (oldObject != null) DestroyImmediate(oldObject);
                tileObjects.Remove(coord);
            }

            if (tileMap.TryGetValue(coord, out MapCellData tile)) CreatePreviewObject(tile);
        }

        private void RefreshAllVisuals()
        {
            List<Vector3Int> coords = new List<Vector3Int>(tileObjects.Keys);
            for (int i = 0; i < coords.Count; i++)
            {
                RefreshFlatTileVisual(coords[i]);
                RefreshTerrainBlend(coords[i]);
            }
        }

        private void RefreshVisualAround(Vector3Int coord)
        {
            RefreshFlatTileVisual(coord);
            RefreshFlatTileVisual(coord + Vector3Int.forward);
            RefreshFlatTileVisual(coord + Vector3Int.back);
            RefreshFlatTileVisual(coord + Vector3Int.left);
            RefreshFlatTileVisual(coord + Vector3Int.right);
            RefreshTerrainBlend(coord);
            RefreshTerrainBlend(coord + Vector3Int.forward);
            RefreshTerrainBlend(coord + Vector3Int.back);
            RefreshTerrainBlend(coord + Vector3Int.left);
            RefreshTerrainBlend(coord + Vector3Int.right);
        }

        private void RefreshFlatTileVisual(Vector3Int coord)
        {
            if (!tileObjects.TryGetValue(coord, out GameObject tileObject) || tileObject == null) return;
            Component visual = tileObject.GetComponentInChildren<FlatTileVisual>();
            if (visual == null) return;

            MethodInfo method = visual.GetType().GetMethod("Refresh", new[]
            {
                typeof(MapTileType), typeof(MapTileType), typeof(MapTileType), typeof(MapTileType), typeof(MapTileType)
            });

            if (method == null) return;

            method.Invoke(visual, new object[]
            {
                GetTileTypeOrNone(coord),
                GetTileTypeOrNone(coord + Vector3Int.forward),
                GetTileTypeOrNone(coord + Vector3Int.right),
                GetTileTypeOrNone(coord + Vector3Int.back),
                GetTileTypeOrNone(coord + Vector3Int.left)
            });
        }

        private void RefreshTerrainBlend(Vector3Int coord)
        {
            TryLoadTerrainBlendConfig();

            if (terrainBlendConfig == null)
            {
                return;
            }

            if (!tileObjects.TryGetValue(coord, out GameObject tileObject) || tileObject == null)
            {
                return;
            }

            MapTerrainBlendUtility.Apply(
                tileObject,
                terrainBlendConfig,
                GetTileTypeOrNone(coord),
                GetTileTypeOrNone(coord + Vector3Int.forward),
                GetTileTypeOrNone(coord + Vector3Int.right),
                GetTileTypeOrNone(coord + Vector3Int.back),
                GetTileTypeOrNone(coord + Vector3Int.left));
        }

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            return tileMap.TryGetValue(coord, out MapCellData tile) ? tile.Type : MapTileType.None;
        }

        private void RefreshDecorations()
        {
            RebuildObjectIndex();

            for (int i = decorationObjects.Count - 1; i >= 0; i--)
            {
                if (decorationObjects[i] != null) DestroyImmediate(decorationObjects[i]);
            }

            decorationObjects.Clear();
            if (currentMap == null || currentMap.Objects == null || previewRoot == null) return;

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                CreateDecorationObject(currentMap.Objects[i], i);
            }
        }

        private void CreateDecorationObject(MapObjectData decoration, int index)
        {
            if (decoration == null || decoration.ConfigId <= 0) return;
            if (!tileObjects.TryGetValue(decoration.Coord, out GameObject tileObject) || tileObject == null) return;

            GameObject prefab = GetDecorationPrefab(decoration);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab. Id: {decoration.ConfigId}");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) instance = Instantiate(prefab);

            instance.name = $"Decoration_{index}_{prefab.name}";
            instance.transform.SetParent(tileObject.transform, false);
            instance.transform.localPosition = decoration.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(decoration.LocalEuler);
            instance.transform.localScale = decoration.LocalScale;

            decorationObjects.Add(instance);
        }

        private void RefreshMarkers()
        {
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                if (markers[i] != null) DestroyImmediate(markers[i]);
            }

            markers.Clear();
            if (currentMap == null || previewRoot == null) return;

            if (currentMap.SpawnPoints != null)
            {
                for (int i = 0; i < currentMap.SpawnPoints.Count; i++)
                {
                    CreateMarker($"SpawnPoint_{i}", currentMap.SpawnPoints[i], Color.red);
                }
            }

            if (currentMap.HasGoalPoint) CreateMarker("GoalPoint", currentMap.GoalPoint, Color.green);
        }

        private void CreateMarker(string name, Vector3Int coord, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(previewRoot, false);
            marker.transform.position = GetWorldPosition(coord) + Vector3.up * 0.65f;
            marker.transform.localScale = Vector3.one * 0.35f;

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);

            markers.Add(marker);
        }

        private void ClearPreviewObjects()
        {
            if (previewRoot != null) DestroyImmediate(previewRoot.gameObject);
            tileObjects.Clear();
            decorationObjects.Clear();
            markers.Clear();
            previewRoot = null;
        }

        private Vector3 GetWorldPosition(Vector3Int coord)
        {
            return new Vector3(coord.x * tileSize, coord.y * tileSize, coord.z * tileSize);
        }

        private MapTileType GetSafeSurfaceType(MapTileType type)
        {
            return type == MapTileType.None || type == MapTileType.Soil ? MapTileType.Grass : type;
        }

        private MapTileType ToMapTileType(TypeBrush type)
        {
            return (MapTileType)type;
        }

        private TypeBrush ToTypeBrush(MapTileType type)
        {
            switch (type)
            {
                case MapTileType.Hill:
                    return TypeBrush.Hill;

                case MapTileType.Snow:
                    return TypeBrush.Snow;

                case MapTileType.Water:
                    return TypeBrush.Water;

                case MapTileType.Road:
                    return TypeBrush.Road;

                default:
                    return TypeBrush.Grass;
            }
        }

        private GameObject GetPrefab(MapTileType type)
        {
            TryLoadPrefabConfig();
            return prefabConfig != null ? prefabConfig.GetPrefab(type) : null;
        }

        private void TryLoadPrefabConfig()
        {
            if (prefabConfig == null) prefabConfig = AssetDatabase.LoadAssetAtPath<MapTilePrefabConfig>(PrefabConfigPath);
            if (prefabConfig != null) prefabConfig.RebuildCache();
            TryLoadTerrainBlendConfig();
            TryLoadDecorationConfig();
        }

        private void TryLoadTerrainBlendConfig()
        {
            if (terrainBlendConfig == null) terrainBlendConfig = AssetDatabase.LoadAssetAtPath<MapTerrainBlendConfig>(TerrainBlendConfigPath);
            if (terrainBlendConfig != null) terrainBlendConfig.RebuildCache();
        }

        private void TryLoadDecorationConfig()
        {
            if (decorationConfig == null) decorationConfig = AssetDatabase.LoadAssetAtPath<MapDecorationPrefabConfig>(DecorationConfigPath);
            if (decorationConfigInDecorationTab == null) decorationConfigInDecorationTab = decorationConfig;
            if (decorationConfig == null && decorationConfigInDecorationTab != null) decorationConfig = decorationConfigInDecorationTab;
            if (decorationConfigInDecorationTab == null && decorationConfig != null) decorationConfigInDecorationTab = decorationConfig;
            if (decorationConfig != null) decorationConfig.RebuildCache();
        }

        private MapDecorationPrefabConfig.DecorationPrefabItem GetSelectedDecorationItem()
        {
            TryLoadDecorationConfig();
            return decorationConfig != null ? decorationConfig.GetItem(selectedDecorationId) : null;
        }

        private GameObject GetDecorationPrefab(MapObjectData decoration)
        {
            TryLoadDecorationConfig();

            if (decorationConfig != null && decoration.ConfigId > 0)
            {
                GameObject prefab = decorationConfig.GetPrefab(decoration.ConfigId);
                if (prefab != null) return prefab;
            }

            return null;
        }

        private bool EnsureMap()
        {
            if (currentMap != null)
            {
                currentMap.EnsureRuntimeCollections();
                return true;
            }

            EditorUtility.DisplayDialog("No Map", "Create or import a map first.", "OK");
            return false;
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
            Dictionary<Vector3Int, MapCellData> temp = new Dictionary<Vector3Int, MapCellData>();

            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];
                if (tile == null)
                {
                    errors.Add($"Tile index {i} is null.");
                    continue;
                }

                Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
                if (temp.ContainsKey(coord))
                {
                    errors.Add($"Duplicate tile coord: {coord}");
                    continue;
                }

                temp.Add(coord, tile);

                if (tile.X < 0 || tile.X >= mapData.Width || tile.Z < 0 || tile.Z >= mapData.Depth)
                {
                    errors.Add($"Tile outside positive map range: {coord}");
                }

                if (tile.Type == MapTileType.Soil) errors.Add($"Soil is not used by this editor: {coord}");
                if (tile.Type != MapTileType.Soil && tile.Y < 0) errors.Add($"Non-soil tile must be y>=0: {coord}");
            }

            for (int i = 0; i < mapData.Cells.Count; i++)
            {
                MapCellData tile = mapData.Cells[i];
                if (tile == null || tile.Type == MapTileType.Soil) continue;

                if (tile.Y == 0) continue;

                Vector3Int below = new Vector3Int(tile.X, tile.Y - 1, tile.Z);
                if (!temp.TryGetValue(below, out MapCellData belowTile))
                {
                    errors.Add($"Tile missing support below: ({tile.X}, {tile.Y}, {tile.Z})");
                    continue;
                }

                if (!MapTileRule.CanPlaceOn(tile.Type, belowTile.Type))
                {
                    errors.Add($"Invalid stack. Below: {belowTile.Type}, Above: {tile.Type}, Coord: ({tile.X}, {tile.Y}, {tile.Z})");
                }
            }

            if (mapData.SpawnPoints == null || mapData.SpawnPoints.Count == 0) errors.Add("Map should have at least one spawn point.");
            if (!mapData.HasGoalPoint) errors.Add("Map should have one goal point.");
            return errors;
        }
    }
}

#endif
