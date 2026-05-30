
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
        }

        private const string PrefabConfigPath = "Assets/Data/Cube/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Cube/Configs/MapDecorationPrefabConfig.asset";
        private const string RootName = "MapRoot";

        private readonly Dictionary<Vector3Int, MapTileData> tileMap = new Dictionary<Vector3Int, MapTileData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();
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

        [TabGroup("Map"), LabelText("Preview Root"), ReadOnly, SerializeField]
        private Transform previewRoot;

        [HideInInspector, SerializeField]
        private TypeBrush brushTileType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay brushOverlay = MapTileOverlay.Road;

        [TabGroup("Paint"), LabelText("Direction"), EnumToggleButtons, SerializeField]
        private MapDirection brushDirection = MapDirection.North;

        [TabGroup("Paint"), OnInspectorGUI]
        private void DrawPaintBrushPreviews()
        {
            EditorGUILayout.HelpBox("Paint 页面说明：\n- 开关笔刷：开启后可在 Scene 里点击/拖动刷地块。\n- Brush Mode = Type 时，使用基础地块预览刷 Grass/Hill/Snow/Water。\n- Brush Mode = Overlay 时，使用覆盖层预览刷 None/Road/Bridge/Stair/Ramp。\n- 填充选中高度层使用当前 Type Brush。", MessageType.Info);

            DrawTypeBrushPreviewSelector();
            GUILayout.Space(6f);
            DrawOverlayBrushPreviewSelector();
        }

        private void DrawTypeBrushPreviewSelector()
        {
            EditorGUILayout.LabelField("Type Brush / 基础地块", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawTypeBrushPreviewButton(TypeBrush.Grass);
            DrawTypeBrushPreviewButton(TypeBrush.Hill);
            DrawTypeBrushPreviewButton(TypeBrush.Snow);
            DrawTypeBrushPreviewButton(TypeBrush.Water);
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
            DrawOverlayBrushPreviewButton(MapTileOverlay.Road);
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
                case MapTileOverlay.Road:
                    return GetPrefab(MapTileType.Road);

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

                case MapTileOverlay.Road:
                    return GetFallbackColor(MapTileType.Road);

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
        private MapTileOverlay SelectedCurrentOverlay => selectedTile != null ? selectedTile.Overlay : MapTileOverlay.None;

        [TabGroup("Selection"), ShowInInspector, ReadOnly, LabelText("Current Direction")]
        private MapDirection SelectedCurrentDirection => selectedTile != null ? selectedTile.Direction : MapDirection.None;

        [HideInInspector, SerializeField]
        private TypeBrush selectedNewType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay selectedNewOverlay = MapTileOverlay.None;

        [TabGroup("Selection"), LabelText("New Direction"), EnumToggleButtons, SerializeField]
        private MapDirection selectedNewDirection = MapDirection.North;

        [TabGroup("Selection"), OnInspectorGUI]
        private void DrawSelectionPreviewSelectors()
        {
            EditorGUILayout.HelpBox("Selection 页面说明：\n- 点击预览卡片选择要应用到当前地块的 Type 或 Overlay。\n- 点击 Apply Type To Selected / Apply Overlay To Selected 后才会修改当前地块。", MessageType.Info);

            DrawSelectionTypePreviewSelector();
            GUILayout.Space(6f);
            DrawSelectionOverlayPreviewSelector();
        }

        private void DrawSelectionTypePreviewSelector()
        {
            EditorGUILayout.LabelField("New Type / 新基础地块", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSelectionTypePreviewButton(TypeBrush.Grass);
            DrawSelectionTypePreviewButton(TypeBrush.Hill);
            DrawSelectionTypePreviewButton(TypeBrush.Snow);
            DrawSelectionTypePreviewButton(TypeBrush.Water);
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
            DrawSelectionOverlayPreviewButton(MapTileOverlay.Road);
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

        private int DecorationCount => currentMap != null && currentMap.Decorations != null ? currentMap.Decorations.Count : 0;
        private Vector2 decorationPreviewScroll;

        [TabGroup("Points"), LabelText("Spawn Points"), SerializeField]
        private List<Vector3Int> spawnPoints = new List<Vector3Int>();

        [TabGroup("Points"), LabelText("Has Goal"), SerializeField]
        private bool hasGoalPoint;

        [TabGroup("Points"), LabelText("Goal Point"), SerializeField]
        private Vector3Int goalPoint;

        [TabGroup("IO"), ShowInInspector, ReadOnly, LabelText("Tile Count")]
        private int TileCount => currentMap != null && currentMap.Tiles != null ? currentMap.Tiles.Count : 0;

        [TabGroup("IO"), ShowInInspector, ReadOnly, LabelText("Current Map")]
        private MapData currentMap;

        private MapTileData selectedTile;
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
        [TabGroup("Map"), Button("Create Grid Map", ButtonSizes.Large), GUIColor(0.35f, 0.85f, 0.45f)]
        private void CreateGridMap()
        {
            currentMap = new MapData(mapId, mapName, width, height, depth);
            currentMap.Description = description;
            tileMap.Clear();
            spawnPoints.Clear();
            currentMap.Decorations.Clear();
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

        [TabGroup("Map"), Button("Rebuild Preview"), GUIColor(0.45f, 0.7f, 1f)]
        private void RebuildPreview()
        {
            if (!EnsureMap()) return;
            RebuildTileIndex();
            CreatePreviewObjects();
        }

        [TabGroup("Map"), Button("Clear Map"), GUIColor(1f, 0.55f, 0.35f)]
        private void ClearMap()
        {
            currentMap = null;
            selectedTile = null;
            tileMap.Clear();
            spawnPoints.Clear();
            hasGoalPoint = false;
            ClearPreviewObjects();
        }

        [TabGroup("Decoration"), OnInspectorGUI]
        private void DrawDecorationTab()
        {
            TryLoadDecorationConfig();
            EditorGUILayout.HelpBox("Decoration 页面说明：\n- 装饰物原始资源在 MapDecorationPrefabConfig.asset 的 Inspector 里维护，使用 Odin List。\n- 地图 JSON 只保存 DecorationId，不保存 prefab 路径。\n- 在这里选择装饰物并放到当前选中地块。\n- 删除当前格装饰只删除该地块上的装饰物，不影响 Type/Overlay。", MessageType.Info);

            float contentWidth = Mathf.Max(980f, position.width - 24f);
            float leftWidth = Mathf.Clamp(contentWidth * 0.46f, 620f, 760f);
            float rightWidth = Mathf.Max(340f, contentWidth - leftWidth - 12f);

            EditorGUILayout.BeginHorizontal(GUILayout.Width(contentWidth));
            DrawDecorationPlacementPanel(leftWidth);
            GUILayout.Space(12f);
            DrawDecorationSourcePreviewPanel(rightWidth);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDecorationPlacementPanel(float panelWidth)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth));
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
                if (GUILayout.Button("添加装饰到选中地块 / Add Decoration To Selected")) AddDecorationToSelected();
                if (GUILayout.Button("删除当前格装饰 / Remove Decorations At Selected")) RemoveDecorationsAtSelected();
            }

            if (GUILayout.Button("创建装饰配置 / Create Decoration Config")) CreateDecorationConfig();
            using (new EditorGUI.DisabledScope(decorationConfig == null))
            {
                if (GUILayout.Button("选中装饰配置资源 / Select Decoration Config Asset")) Selection.activeObject = decorationConfig;
            }

            if (GUILayout.Button("使用选中项默认变换 / Use Selected Defaults")) UseSelectedDecorationDefaults();
            if (GUILayout.Button("清空全部装饰 / Clear All Decorations")) ClearAllDecorations();

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.EndVertical();
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

            currentMap.Decorations.Add(new MapDecorationData(item.Id, selectedCoord, decorationLocalPosition, decorationLocalEuler, decorationLocalScale));
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
            currentMap.Decorations.RemoveAll(decoration => decoration != null && decoration.Coord == selectedCoord);
            RefreshDecorations();
        }

        private void ClearAllDecorations()
        {
            if (!EnsureMap()) return;
            if (!EditorUtility.DisplayDialog("Clear Decorations", "Clear all decorations in current map?", "Clear", "Cancel")) return;
            currentMap.Decorations.Clear();
            RefreshDecorations();
        }

        [TabGroup("Paint"), Button("开关笔刷 / Toggle Brush"), GUIColor(0.45f, 0.75f, 1f)]
        private void ToggleBrush()
        {
            brushEnabled = !brushEnabled;
            paintedThisDrag.Clear();
            SceneView.RepaintAll();
        }

        [TabGroup("Paint"), Button("填充选中高度层 / Fill Selected Y Layer"), GUIColor(0.55f, 0.8f, 1f)]
        private void FillSelectedLayer()
        {
            if (!EnsureMap()) return;
            int y = HasSelection ? selectedCoord.y : 0;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    PaintTile(new Vector3Int(x, y, z), ToMapTileType(brushTileType), false);
                }
            }
        }

        [TabGroup("Paint"), Button("清除笔刷区域覆盖层 / Clear Overlay Brush Area"), GUIColor(0.7f, 0.7f, 0.7f)]
        private void ClearOverlayBrushArea()
        {
            if (!HasSelection) return;
            MapTileOverlay oldOverlay = brushOverlay;
            brushOverlay = MapTileOverlay.None;
            PaintOverlayBrushAt(selectedCoord);
            brushOverlay = oldOverlay;
        }

        [TabGroup("Paint"), Button("笔刷区域升高一层 / Raise Brush Area Once"), GUIColor(0.6f, 0.9f, 0.55f)]
        private void RaiseBrushAreaOnce()
        {
            if (!HasSelection) return;
            RaiseBrushAt(selectedCoord);
        }

        [TabGroup("Paint"), Button("笔刷区域降低一层 / Lower Brush Area Once"), GUIColor(1f, 0.65f, 0.45f)]
        private void LowerBrushAreaOnce()
        {
            if (!HasSelection) return;
            LowerBrushAt(selectedCoord);
        }

        [TabGroup("Selection"), Button("Apply Type To Selected"), EnableIf("HasSelection"), GUIColor(0.35f, 0.85f, 1f)]
        private void ApplyTypeToSelected()
        {
            PaintTile(selectedCoord, ToMapTileType(selectedNewType), true);
            SelectTile(selectedCoord);
        }

        [TabGroup("Selection"), Button("Apply Overlay To Selected"), EnableIf("HasSelection"), GUIColor(0.75f, 0.85f, 1f)]
        private void ApplyOverlayToSelected()
        {
            PaintOverlay(selectedCoord, selectedNewOverlay, selectedNewDirection, true);
            SelectTile(selectedCoord);
        }

        [TabGroup("Selection"), Button("Reset Selected Logic"), EnableIf("HasSelection")]
        private void ResetSelectedLogic()
        {
            if (selectedTile != null) selectedTile.ApplyDefaultLogic();
        }

        [TabGroup("Selection"), Button("Add Tile Above Selected"), EnableIf("HasSelection"), GUIColor(0.6f, 0.9f, 0.55f)]
        private void AddTileAboveSelected()
        {
            Vector3Int above = new Vector3Int(selectedCoord.x, selectedCoord.y + 1, selectedCoord.z);
            if (TryAddTile(above, ToMapTileType(selectedNewType), true)) SelectTile(above);
        }

        [TabGroup("Selection"), Button("Remove Selected Tile"), EnableIf("HasSelection"), GUIColor(1f, 0.65f, 0.45f)]
        private void RemoveSelectedTile()
        {
            Vector3Int oldCoord = selectedCoord;
            if (!TryRemoveTile(oldCoord, true)) return;

            if (TryGetTopTile(oldCoord.x, oldCoord.z, out MapTileData topTile))
            {
                SelectTile(new Vector3Int(topTile.X, topTile.Y, topTile.Z));
            }
            else
            {
                selectedTile = null;
                selectedCoord = default;
            }
        }

        [TabGroup("Points"), Button("Add Selected As Spawn"), EnableIf("HasSelection"), GUIColor(1f, 0.55f, 0.45f)]
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

        [TabGroup("Points"), Button("Set Selected As Goal"), EnableIf("HasSelection"), GUIColor(0.45f, 1f, 0.55f)]
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

        [TabGroup("Points"), Button("Apply Points"), GUIColor(0.55f, 0.9f, 0.65f)]
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

        [TabGroup("Points"), Button("Clear Points"), GUIColor(1f, 0.7f, 0.45f)]
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
        [TabGroup("IO"), Button("Import Json", ButtonSizes.Large), GUIColor(0.45f, 0.7f, 1f)]
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

        [TabGroup("IO"), Button("Export Json", ButtonSizes.Large), GUIColor(0.35f, 0.6f, 1f)]
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

        [TabGroup("IO"), Button("Validate Map"), GUIColor(0.7f, 0.7f, 1f)]
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
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                paintedThisDrag.Clear();
                return;
            }

            if (e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)) return;
            if (!TryPickTile(e.mousePosition, out Vector3Int coord)) return;

            if (brushEnabled)
            {
                ApplyBrushAt(coord);
            }
            else if (e.type == EventType.MouseDown)
            {
                SelectTile(coord);
            }

            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryPickTile(Vector2 mousePosition, out Vector3Int coord)
        {
            coord = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return false;

            TileView tileView = hit.collider.GetComponentInParent<TileView>();
            if (tileView == null) return false;

            coord = tileView.Coord;
            return true;
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
            selectedNewOverlay = selectedTile.Overlay;
            selectedNewDirection = selectedTile.Direction == MapDirection.None ? MapDirection.North : selectedTile.Direction;
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
                    if (PaintTile(coord, ToMapTileType(brushTileType), false)) paintedThisDrag.Add(coord);
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
                    if (PaintOverlay(coord, brushOverlay, brushDirection, false)) paintedThisDrag.Add(coord);
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

                    if (!TryGetTopTile(x, z, out MapTileData topTile)) continue;

                    Vector3Int above = new Vector3Int(x, topTile.Y + 1, z);
                    if (TryAddTile(above, ToMapTileType(brushTileType), false)) paintedThisDrag.Add(column);
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

                    if (!TryGetTopTile(x, z, out MapTileData topTile)) continue;
                    if (topTile.Y <= 0) continue;

                    Vector3Int coord = new Vector3Int(topTile.X, topTile.Y, topTile.Z);
                    if (TryRemoveTile(coord, false)) paintedThisDrag.Add(column);
                }
            }
        }

        private bool PaintTile(Vector3Int coord, MapTileType type, bool showDialog)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                if (showDialog) EditorUtility.DisplayDialog("Paint Failed", "Only Grass, Hill, Snow, and Water can be painted as Type.", "OK");
                return false;
            }

            if (!tileMap.TryGetValue(coord, out MapTileData tile)) return false;
            if (skipPointTiles && currentMap.HasAnyPoint(coord)) return false;
            if (tile.Type == type) return false;

            tile.ApplyDefaultLogicByType(type);
            RecreatePreviewObject(coord);
            RefreshVisualAround(coord);
            RefreshMarkers();
            if (selectedTile == tile) selectedNewType = ToTypeBrush(type);
            return true;
        }
        private void AddTileNoCheck(MapTileType type, int x, int y, int z)
        {
            MapTileData tile = new MapTileData(x, y, z, type);
            currentMap.Tiles.Add(tile);
            tileMap[new Vector3Int(x, y, z)] = tile;
        }

        private bool TryAddTile(Vector3Int coord, MapTileType type, bool showDialog)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                if (showDialog) EditorUtility.DisplayDialog("Add Tile Failed", "Only Grass, Hill, Snow, and Water can be added as Type.", "OK");
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
                if (!tileMap.TryGetValue(belowCoord, out MapTileData belowTile))
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

            AddTileNoCheck(type, coord.x, coord.y, coord.z);
            height = Mathf.Max(height, coord.y + 1);
            currentMap.Height = Mathf.Max(currentMap.Height, height);
            CreatePreviewObject(tileMap[coord]);
            RefreshVisualAround(coord);
            RefreshMarkers();
            return true;
        }

        private bool PaintOverlay(Vector3Int coord, MapTileOverlay overlay, MapDirection direction, bool showDialog)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
            {
                return false;
            }

            if (skipPointTiles && currentMap.HasAnyPoint(coord))
            {
                return false;
            }

            MapDirection nextDirection = RequiresDirection(overlay) ? direction : MapDirection.None;

            if (tile.Overlay == overlay && tile.Direction == nextDirection)
            {
                return false;
            }

            tile.Overlay = overlay;
            tile.Direction = nextDirection;
            tile.ApplyDefaultLogic();

            RecreatePreviewObject(coord);
            RefreshMarkers();
            return true;
        }

        private bool RequiresDirection(MapTileOverlay overlay)
        {
            return overlay == MapTileOverlay.Stair || overlay == MapTileOverlay.Ramp;
        }

        private bool TryRemoveTile(Vector3Int coord, bool showDialog)
        {
            if (!tileMap.TryGetValue(coord, out MapTileData tile))
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

            currentMap.Tiles.Remove(tile);
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

        private bool TryGetTopTile(int x, int z, out MapTileData topTile)
        {
            topTile = null;
            int topY = int.MinValue;

            foreach (KeyValuePair<Vector3Int, MapTileData> pair in tileMap)
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
            if (currentMap == null) return;
            currentMap.EnsureRuntimeCollections();

            for (int i = 0; i < currentMap.Tiles.Count; i++)
            {
                MapTileData tile = currentMap.Tiles[i];
                if (tile == null) continue;
                tileMap[new Vector3Int(tile.X, tile.Y, tile.Z)] = tile;
            }
        }

        private void CreatePreviewObjects()
        {
            ClearPreviewObjects();
            EnsurePreviewRoot();

            foreach (KeyValuePair<Vector3Int, MapTileData> pair in tileMap)
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

        private void CreatePreviewObject(MapTileData tile)
        {
            Vector3Int coord = new Vector3Int(tile.X, tile.Y, tile.Z);
            GameObject instance = new GameObject($"{tile.Type}_{tile.Overlay}_{tile.X}_{tile.Y}_{tile.Z}");

            instance.transform.SetParent(previewRoot, false);
            instance.transform.position = GetWorldPosition(coord);

            GameObject typeVisual = CreateTileInstance(tile.Type);
            if (typeVisual != null)
            {
                typeVisual.name = $"Type_{tile.Type}";
                typeVisual.transform.SetParent(instance.transform, false);
                typeVisual.transform.localPosition = Vector3.zero;
            }

            GameObject overlayVisual = CreateOverlayInstance(tile.Overlay, tile.Direction);
            if (overlayVisual != null)
            {
                overlayVisual.name = $"Overlay_{tile.Overlay}_{tile.Direction}";
                overlayVisual.transform.SetParent(instance.transform, false);
                overlayVisual.transform.localPosition = Vector3.zero;
                overlayVisual.transform.localRotation = GetDirectionRotation(tile.Direction);
            }

            TileView tileView = instance.GetComponent<TileView>();
            if (tileView == null) tileView = instance.AddComponent<TileView>();
            tileView.Initialize(new TileData(tile));
            EnsurePickingCollider(instance);
            tileObjects[coord] = instance;
        }

        private GameObject CreateTileInstance(MapTileType type)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab != null)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                return prefabInstance != null ? prefabInstance : Instantiate(prefab);
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.transform.localScale = new Vector3(tileSize, 0.18f, tileSize);
            Renderer renderer = fallback.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = GetFallbackColor(type);
                renderer.sharedMaterial = material;
            }

            return fallback;
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
            fallback.transform.localPosition = Vector3.up * 0.54f;

            Renderer renderer = fallback.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            return fallback;
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

        private void EnsurePickingCollider(GameObject instance)
        {
            if (instance == null) return;

            Collider collider = instance.GetComponent<Collider>();
            if (collider == null) collider = instance.AddComponent<BoxCollider>();

            if (collider is BoxCollider boxCollider)
            {
                boxCollider.center = Vector3.zero;
                boxCollider.size = new Vector3(tileSize, Mathf.Max(0.2f, tileSize * 0.25f), tileSize);
            }

            collider.enabled = true;
            collider.isTrigger = false;
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

            if (tileMap.TryGetValue(coord, out MapTileData tile)) CreatePreviewObject(tile);
        }

        private void RefreshAllVisuals()
        {
            List<Vector3Int> coords = new List<Vector3Int>(tileObjects.Keys);
            for (int i = 0; i < coords.Count; i++) RefreshFlatTileVisual(coords[i]);
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

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            return tileMap.TryGetValue(coord, out MapTileData tile) ? tile.Type : MapTileType.None;
        }

        private void RefreshDecorations()
        {
            for (int i = decorationObjects.Count - 1; i >= 0; i--)
            {
                if (decorationObjects[i] != null) DestroyImmediate(decorationObjects[i]);
            }

            decorationObjects.Clear();
            if (currentMap == null || currentMap.Decorations == null || previewRoot == null) return;

            for (int i = 0; i < currentMap.Decorations.Count; i++)
            {
                CreateDecorationObject(currentMap.Decorations[i], i);
            }
        }

        private void CreateDecorationObject(MapDecorationData decoration, int index)
        {
            if (decoration == null || decoration.DecorationId <= 0) return;
            if (!tileObjects.TryGetValue(decoration.Coord, out GameObject tileObject) || tileObject == null) return;

            GameObject prefab = GetDecorationPrefab(decoration);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab. Id: {decoration.DecorationId}");
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
            TryLoadDecorationConfig();
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

        private GameObject GetDecorationPrefab(MapDecorationData decoration)
        {
            TryLoadDecorationConfig();

            if (decorationConfig != null && decoration.DecorationId > 0)
            {
                GameObject prefab = decorationConfig.GetPrefab(decoration.DecorationId);
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
            Dictionary<Vector3Int, MapTileData> temp = new Dictionary<Vector3Int, MapTileData>();

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];
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

            for (int i = 0; i < mapData.Tiles.Count; i++)
            {
                MapTileData tile = mapData.Tiles[i];
                if (tile == null || tile.Type == MapTileType.Soil) continue;

                if (tile.Y == 0) continue;

                Vector3Int below = new Vector3Int(tile.X, tile.Y - 1, tile.Z);
                if (!temp.TryGetValue(below, out MapTileData belowTile))
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
