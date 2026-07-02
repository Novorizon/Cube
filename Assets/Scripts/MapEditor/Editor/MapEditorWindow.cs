
#if UNITY_EDITOR

using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public partial class MapEditorWindow : OdinEditorWindow
    {
        private enum BrushMode
        {
            Type,
            Overlay,
            Raise,
            Lower,
        }

        private enum MainTab
        {
            Map,
            Paint,
            Points,
            Decoration,
            Resources,
        }

        private enum TypeBrush
        {
            Grass = MapTileType.Grass,
            Hill = MapTileType.Hill,
            Snow = MapTileType.Snow,
            Water = MapTileType.Water,
            Road = MapTileType.Road,
        }

        private sealed class MapDataReadOnlyView
        {
            private readonly MapData mapData;

            public MapDataReadOnlyView(MapData mapData)
            {
                this.mapData = mapData;
            }

            [ShowInInspector, ReadOnly, LabelText("Cells")]
            public List<MapCellData> Cells => mapData.Cells;

            [ShowInInspector, ReadOnly, LabelText("Objects")]
            public List<MapObjectData> Objects => mapData.Objects;

            [ShowInInspector, ReadOnly, LabelText("Tile Logic Defaults")]
            public List<MapTileLogicDefaultData> TileLogicDefaults => mapData.TileLogicDefaults;

            [ShowInInspector, ReadOnly, LabelText("Overlay Logic Defaults")]
            public List<MapOverlayLogicDefaultData> OverlayLogicDefaults => mapData.OverlayLogicDefaults;

            [ShowInInspector, ReadOnly, LabelText("Spawn Points")]
            public List<Vector3Int> SpawnPoints => mapData.SpawnPoints;

            [ShowInInspector, ReadOnly, LabelText("Has Goal Point")]
            public bool HasGoalPoint => mapData.HasGoalPoint;

            [ShowInInspector, ReadOnly, LabelText("Goal Point")]
            public Vector3Int GoalPoint => mapData.GoalPoint;
        }

        private sealed class WorldResourceEditorItem
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("resourceType")]
            public int ResourceType { get; set; }

            [JsonProperty("interactionType")]
            public int InteractionType { get; set; }

            [JsonProperty("gatherConfigId")]
            public int GatherConfigId { get; set; }

            [JsonProperty("pickupRewardGroupId")]
            public int PickupRewardGroupId { get; set; }

            [JsonProperty("mineBuildingId")]
            public int MineBuildingId { get; set; }

            [JsonProperty("prefabLocation")]
            public string PrefabLocation { get; set; }

            [JsonProperty("iconLocation")]
            public string IconLocation { get; set; }

            [JsonProperty("blocksBuild")]
            public bool BlocksBuild { get; set; }

            [JsonProperty("blocksMove")]
            public bool BlocksMove { get; set; }

            [JsonProperty("enable")]
            public bool Enable { get; set; } = true;
        }

        [TabGroup("Tabs", "Map", false, 0), OnInspectorGUI, PropertyOrder(-1000)]
        private void DrawMapTab()
        {
            DrawMapToolButtons();
        }

        private const string PrefabConfigPath = "Assets/Data/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Configs/MapDecorationPrefabConfig.asset";
        private const string WorldResourceJsonPath = "Assets/Data/Json/tbworldresource.json";
        private const string RootName = "MapRoot";
        private const float RightDockPanelWidth = 360f;
        private const float MiddlePanelWidth = 430f;
        private const float DecorationSourcePreviewPanelWidth = 360f;
        private const float DecorationColumnGap = 12f;

        protected virtual string EditorTitle => "Map Editor";
        protected virtual bool UsesFixedValidationMode => false;
        protected virtual MapEditorValidationMode DefaultValidationMode => MapEditorValidationMode.World;
        protected virtual bool SupportsPointsTab => true;
        protected virtual bool SupportsResourcesTab => true;

        private readonly Dictionary<Vector3Int, MapCellData> tileMap = new Dictionary<Vector3Int, MapCellData>();
        private readonly Dictionary<Vector3Int, GameObject> tileObjects = new Dictionary<Vector3Int, GameObject>();
        private readonly Dictionary<Vector3Int, List<MapObjectData>> objectsByCoord = new Dictionary<Vector3Int, List<MapObjectData>>();
        private readonly HashSet<Vector3Int> paintedThisDrag = new HashSet<Vector3Int>();
        private readonly List<GameObject> markers = new List<GameObject>();
        private readonly List<GameObject> decorationObjects = new List<GameObject>();
        private readonly List<GameObject> objectPreviewObjects = new List<GameObject>();
        private readonly List<int> decorationIdOptions = new List<int>();
        private readonly List<string> decorationNameOptions = new List<string>();
        private readonly List<WorldResourceEditorItem> worldResourceItems = new List<WorldResourceEditorItem>();
        private readonly List<int> worldResourceIdOptions = new List<int>();
        private readonly List<string> worldResourceNameOptions = new List<string>();
        private Vector2 logicDefaultsPreviewScroll;
        private bool showCurrentMapData;
        private PropertyTree currentMapPropertyTree;
        private MapData currentMapPropertyTreeTarget;
        private MapDataReadOnlyView currentMapReadOnlyView;

        [SerializeField, HideInInspector]
        private MainTab activeMainTab = MainTab.Map;

        [SerializeField, HideInInspector]
        private MapEditorValidationMode validationMode = MapEditorValidationMode.World;

        [HideInInspector, SerializeField]
        private int mapId = 1;

        [HideInInspector, SerializeField]
        private string mapName = "NewMap";

        [HideInInspector, SerializeField]
        private string description = "Tower defense map";

        [HideInInspector, SerializeField]
        private int width = 12;

        [HideInInspector, SerializeField]
        private int height = 1;

        [HideInInspector, SerializeField]
        private int depth = 12;

        [HideInInspector, SerializeField]
        private float tileSize = 1f;

        [HideInInspector, SerializeField]
        private TypeBrush defaultTileType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTilePrefabConfig prefabConfig;

        [HideInInspector, SerializeField]
        private MapDecorationPrefabConfig decorationConfig;

        [HideInInspector, SerializeField]
        private Transform previewRoot;

        [HideInInspector, SerializeField]
        private TypeBrush brushTileType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay brushOverlay = MapTileOverlay.None;

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Type Direction"), EnumToggleButtons, SerializeField]
        private MapDirection brushTypeDirection = MapDirection.North;

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Overlay Direction"), EnumToggleButtons, SerializeField]
        private MapDirection brushOverlayDirection = MapDirection.North;

        [TabGroup("Tabs", "Paint", false, 1), OnInspectorGUI]
        private void DrawPaintBrushPreviews()
        {
            DrawTypeBrushPreviewSelector();
            GUILayout.Space(6f);
            DrawOverlayBrushPreviewSelector();
            GUILayout.Space(8f);
            DrawPaintToolButtons();
        }

        private void DrawTypeBrushPreviewSelector()
        {
            EditorGUILayout.LabelField("Type Brush", EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField("Overlay Brush", EditorStyles.boldLabel);
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
            const float outerWidth = 78f;
            const float previewWidth = 68f;
            const float previewHeight = 58f;

            Color oldBackgroundColor = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(outerWidth));
            Texture2D preview = GetPrefabPreview(prefab);
            GUIContent previewContent = preview != null ? new GUIContent(preview) : new GUIContent(label);
            bool clicked = GUILayout.Button(previewContent, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));

            GUIStyle labelStyle = selected ? EditorStyles.boldLabel : EditorStyles.centeredGreyMiniLabel;
            Rect colorRect = GUILayoutUtility.GetRect(previewWidth, 8f, GUILayout.Width(previewWidth));
            EditorGUI.DrawRect(colorRect, fallbackColor);
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.Width(previewWidth));
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
            EditorGUILayout.LabelField("Paint Tools", EditorStyles.boldLabel);

            bool toggleBrush;
            bool fillLayer;
            bool clearOverlay;
            bool raise;
            bool lower;

            using (new EditorGUILayout.HorizontalScope())
            {
                toggleBrush = GUILayout.Button(brushEnabled ? "Toggle Off" : "Toggle On", GUILayout.Width(118f), GUILayout.Height(44f));
                fillLayer = GUILayout.Button("Fill Y Layer", GUILayout.Width(138f), GUILayout.Height(44f));
                clearOverlay = GUILayout.Button("Clear Overlay", GUILayout.Width(118f), GUILayout.Height(44f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                raise = DrawPaintModeButton("Raise", BrushMode.Raise, 96f);
                lower = DrawPaintModeButton("Lower", BrushMode.Lower, 96f);
            }

            if (toggleBrush) ToggleBrush();
            if (fillLayer) FillSelectedLayer();
            if (clearOverlay) ClearOverlayBrushArea();
            if (raise) SetPaintBrushMode(BrushMode.Raise);
            if (lower) SetPaintBrushMode(BrushMode.Lower);
        }

        private void DrawPaintControlPanel()
        {
            EditorGUILayout.LabelField("Type Direction", EditorStyles.boldLabel);
            brushTypeDirection = DrawDirectionButtons(brushTypeDirection);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Overlay Direction", EditorStyles.boldLabel);
            brushOverlayDirection = DrawDirectionButtons(brushOverlayDirection);

            GUILayout.Space(8f);
            brushEnabled = EditorGUILayout.Toggle("Brush Enabled", brushEnabled);
            brushMode = (BrushMode)EditorGUILayout.EnumPopup("Brush Mode", brushMode);
            brushSize = Mathf.Clamp(EditorGUILayout.IntField("Brush Size", brushSize), 1, 9);
            skipPointTiles = EditorGUILayout.Toggle("Skip Spawn/Goal", skipPointTiles);
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

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Brush Enabled"), SerializeField]
        private bool brushEnabled;

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Brush Mode"), EnumToggleButtons, SerializeField]
        private BrushMode brushMode = BrushMode.Type;

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Brush Size"), MinValue(1), MaxValue(9), SerializeField]
        private int brushSize = 1;

        [TabGroup("Tabs", "Paint", false, 1), LabelText("Skip Spawn/Goal"), SerializeField]
        private bool skipPointTiles = true;

        private bool HasSelection => selectedTile != null;

        [HideInInspector, SerializeField]
        private TypeBrush selectedNewType = TypeBrush.Grass;

        [HideInInspector, SerializeField]
        private MapTileOverlay selectedNewOverlay = MapTileOverlay.None;

        [HideInInspector, SerializeField]
        private MapDirection selectedNewTypeDirection = MapDirection.North;

        [HideInInspector, SerializeField]
        private MapDirection selectedNewOverlayDirection = MapDirection.North;

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

        [HideInInspector, SerializeField]
        private int selectedWorldResourceId;

        [HideInInspector, SerializeField]
        private Vector3 resourceLocalPosition = Vector3.zero;

        [HideInInspector, SerializeField]
        private Vector3 resourceLocalEuler = Vector3.zero;

        [HideInInspector, SerializeField]
        private Vector3 resourceLocalScale = Vector3.one;

        [HideInInspector, SerializeField]
        private bool resourceBlocksBuild = true;

        [HideInInspector, SerializeField]
        private bool resourceBlocksMove = true;

        private int DecorationCount => CountObjects(MapObjectType.Decoration);
        private int ResourceObjectCount => CountObjects(MapObjectType.Resource);
        private Vector2 decorationPreviewScroll;
        private Vector2 resourcePreviewScroll;

        [TabGroup("Tabs", "Points", false, 2), LabelText("Spawn Points"), SerializeField]
        private List<Vector3Int> spawnPoints = new List<Vector3Int>();

        [TabGroup("Tabs", "Points", false, 2), LabelText("Has Goal"), SerializeField]
        private bool hasGoalPoint;

        [TabGroup("Tabs", "Points", false, 2), LabelText("Goal Point"), SerializeField]
        private Vector3Int goalPoint;

        private int TileCount => currentMap != null && currentMap.Cells != null ? currentMap.Cells.Count : 0;

        private int ObjectCount => currentMap != null && currentMap.Objects != null ? currentMap.Objects.Count : 0;

        private int CountObjects(MapObjectType objectType)
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                MapObjectData mapObject = currentMap.Objects[i];
                if (mapObject != null && mapObject.ObjectType == objectType)
                {
                    count++;
                }
            }

            return count;
        }

        private MapData currentMap;

        private MapCellData selectedTile;
        private Vector3Int selectedCoord;

        private void DrawRightDockSelectionPanel()
        {
            EditorGUILayout.LabelField("Selected Cell", EditorStyles.boldLabel);

            if (!HasSelection || selectedTile == null)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            EditorGUILayout.LabelField("Coord", FormatCoord(selectedCoord));
            EditorGUILayout.LabelField("Tile", $"{selectedTile.Type} / {selectedTile.TypeDirection}");
            EditorGUILayout.LabelField("Overlay", $"{selectedTile.Overlay.Type} / {selectedTile.OverlayDirection}");

            EditorGUI.BeginChangeCheck();
            bool walkable = EditorGUILayout.Toggle("Walkable", selectedTile.Walkable);
            bool buildable = EditorGUILayout.Toggle("Buildable", selectedTile.Buildable);
            int moveCost = Mathf.Max(0, EditorGUILayout.IntField("Move Cost", selectedTile.MoveCost));
            if (EditorGUI.EndChangeCheck())
            {
                selectedTile.Walkable = walkable;
                selectedTile.Buildable = buildable;
                selectedTile.MoveCost = moveCost;
                Repaint();
            }

            GUILayout.Space(8f);
            DrawRightDockGrassVisualEditor();
            GUILayout.Space(8f);
            DrawRightDockSelectionEditors();
            GUILayout.Space(8f);
            DrawRightDockSelectedCellObjectsPanel();
        }

        private void DrawRightDockGrassVisualEditor()
        {
            if (selectedTile.Type != MapTileType.Grass)
            {
                return;
            }

            EditorGUILayout.LabelField("Grass Visual", EditorStyles.boldLabel);

            bool enabled = selectedTile.GrassVisual != null;
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle("Override", enabled);
            if (EditorGUI.EndChangeCheck())
            {
                selectedTile.GrassVisual = enabled ? MapGrassVisualData.CreateDefault() : null;
                ApplyGrassVisualToPreview(selectedCoord);
                Repaint();
                SceneView.RepaintAll();
            }

            MapGrassVisualData visual = selectedTile.GrassVisual;
            if (visual == null)
            {
                EditorGUILayout.HelpBox("Disabled: this cell uses the shared Grass material defaults.", MessageType.None);
                return;
            }

            EditorGUI.BeginChangeCheck();
            Color baseGreen = EditorGUILayout.ColorField("Base Green", visual.BaseGreen);
            Color darkGreen = EditorGUILayout.ColorField("Dark Green", visual.DarkGreen);
            Color lightGreen = EditorGUILayout.ColorField("Light Green", visual.LightGreen);
            float variationStrength = EditorGUILayout.Slider("Variation Strength", visual.VariationStrength, 0f, 1f);
            float variationScale = EditorGUILayout.Slider("Variation Scale", visual.VariationScale, 0.25f, 8f);
            float variationSoftness = EditorGUILayout.Slider("Variation Softness", visual.VariationSoftness, 0.01f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                visual.BaseGreen = baseGreen;
                visual.DarkGreen = darkGreen;
                visual.LightGreen = lightGreen;
                visual.VariationStrength = Mathf.Clamp01(variationStrength);
                visual.VariationScale = Mathf.Clamp(variationScale, 0.25f, 8f);
                visual.VariationSoftness = Mathf.Clamp(variationSoftness, 0.01f, 1f);
                ApplyGrassVisualToPreview(selectedCoord);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawRightDockSelectionEditors()
        {
            EditorGUILayout.LabelField("New Type Direction", EditorStyles.boldLabel);
            selectedNewTypeDirection = DrawDirectionButtons(selectedNewTypeDirection);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("New Type", EditorStyles.boldLabel);
            DrawRightDockTypeGrid();

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("New Overlay Direction", EditorStyles.boldLabel);
            selectedNewOverlayDirection = DrawDirectionButtons(selectedNewOverlayDirection);

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("New Overlay", EditorStyles.boldLabel);
            DrawRightDockOverlayGrid();

            GUILayout.Space(8f);
            DrawRightDockActionButtons();
        }

        private MapDirection DrawDirectionButtons(MapDirection current)
        {
            MapDirection result = current;

            using (new EditorGUILayout.HorizontalScope())
            {
                result = DrawDirectionButton("None", MapDirection.None, result);
                result = DrawDirectionButton("N", MapDirection.North, result);
                result = DrawDirectionButton("E", MapDirection.East, result);
                result = DrawDirectionButton("S", MapDirection.South, result);
                result = DrawDirectionButton("W", MapDirection.West, result);
            }

            return result;
        }

        private MapDirection DrawDirectionButton(string label, MapDirection value, MapDirection current)
        {
            Color oldColor = GUI.backgroundColor;
            if (current == value)
            {
                GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Width(value == MapDirection.None ? 58f : 42f), GUILayout.Height(24f)))
            {
                current = value;
            }

            GUI.backgroundColor = oldColor;
            return current;
        }

        private void DrawRightDockTypeGrid()
        {
            TypeBrush[] types =
            {
                TypeBrush.Grass,
                TypeBrush.Hill,
                TypeBrush.Snow,
                TypeBrush.Water,
                TypeBrush.Road
            };

            for (int i = 0; i < types.Length; i += 2)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawRightDockTypeButton(types[i]);
                    if (i + 1 < types.Length)
                    {
                        DrawRightDockTypeButton(types[i + 1]);
                    }
                }
            }
        }

        private void DrawRightDockTypeButton(TypeBrush type)
        {
            MapTileType mapTileType = ToMapTileType(type);
            if (DrawSmallPreviewButton(type.ToString(), GetPrefab(mapTileType), selectedNewType == type, GetFallbackColor(mapTileType)))
            {
                selectedNewType = type;
            }
        }

        private void DrawRightDockOverlayGrid()
        {
            MapTileOverlay[] overlays =
            {
                MapTileOverlay.None,
                MapTileOverlay.Bridge,
                MapTileOverlay.Stair,
                MapTileOverlay.Ramp
            };

            for (int i = 0; i < overlays.Length; i += 2)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawRightDockOverlayButton(overlays[i]);
                    if (i + 1 < overlays.Length)
                    {
                        DrawRightDockOverlayButton(overlays[i + 1]);
                    }
                }
            }
        }

        private void DrawRightDockOverlayButton(MapTileOverlay overlay)
        {
            if (DrawSmallPreviewButton(overlay.ToString(), GetOverlayPreviewPrefab(overlay), selectedNewOverlay == overlay, GetOverlayFallbackColor(overlay)))
            {
                selectedNewOverlay = overlay;
            }
        }

        private bool DrawSmallPreviewButton(string label, GameObject prefab, bool selected, Color fallbackColor)
        {
            const float buttonWidth = 154f;
            const float buttonHeight = 48f;
            Rect rect = GUILayoutUtility.GetRect(buttonWidth, buttonHeight, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));

            Color background = selected ? new Color(0.20f, 0.33f, 0.40f, 1f) : new Color(0.27f, 0.27f, 0.27f, 1f);
            EditorGUI.DrawRect(rect, background);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            Rect previewRect = new Rect(rect.x + 5f, rect.y + 5f, 38f, 38f);
            Rect colorRect = new Rect(previewRect.x, previewRect.yMax - 5f, previewRect.width, 5f);
            Rect labelRect = new Rect(previewRect.xMax + 8f, rect.y + 14f, rect.width - previewRect.width - 18f, 20f);

            Texture2D preview = GetPrefabPreview(prefab);
            if (preview != null)
            {
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(previewRect, label, EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUI.DrawRect(colorRect, fallbackColor);
            GUI.Label(labelRect, label, selected ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            Event current = Event.current;
            if (current != null && current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                current.Use();
                Repaint();
                return true;
            }

            return false;
        }

        private void DrawRightDockActionButtons()
        {
            using (new EditorGUI.DisabledScope(!HasSelection))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Type", GUILayout.Height(30f))) ApplyTypeToSelected();
                    if (GUILayout.Button("Apply Overlay", GUILayout.Height(30f))) ApplyOverlayToSelected();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset Logic", GUILayout.Height(30f))) ResetSelectedLogic();
                    if (GUILayout.Button("Add Above", GUILayout.Height(30f))) AddTileAboveSelected();
                }

                if (GUILayout.Button("Remove Tile", GUILayout.Height(30f)))
                {
                    RemoveSelectedTile();
                }
            }
        }

        private void DrawRightDockSelectedCellObjectsPanel()
        {
            if (!TryGetObjectsAt(selectedCoord, out IReadOnlyList<MapObjectData> objects) || objects == null || objects.Count == 0)
            {
                EditorGUILayout.LabelField("Objects On Cell", "0");
                return;
            }

            EditorGUILayout.LabelField("Objects On Cell", objects.Count.ToString(), EditorStyles.boldLabel);
            TryLoadDecorationConfig();

            MapObjectData objectToDelete = null;
            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject == null)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"#{i + 1} {mapObject.ObjectType}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Object Id", mapObject.ObjectId.ToString());
                    EditorGUILayout.LabelField("Config", mapObject.ConfigId.ToString());
                    EditorGUILayout.LabelField("Name", GetObjectDisplayName(mapObject));
                    if (mapObject.ObjectType == MapObjectType.Resource)
                    {
                        EditorGUILayout.LabelField("Resource Mode", GetObjectResourceSummary(mapObject));
                    }
                    EditorGUILayout.LabelField("Blocks", $"Build {mapObject.BlocksBuild} | Move {mapObject.BlocksMove}");
                    EditorGUILayout.LabelField("Pos", FormatVector(mapObject.LocalPosition));
                    EditorGUILayout.LabelField("Rot", FormatVector(mapObject.LocalEuler));
                    EditorGUILayout.LabelField("Scale", FormatVector(mapObject.LocalScale));

                    if (GUILayout.Button("Delete"))
                    {
                        objectToDelete = mapObject;
                    }
                }
            }

            if (objectToDelete != null)
            {
                DeleteMapObject(objectToDelete);
            }
        }

        private string GetObjectDisplayName(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return "None";
            }

            if (mapObject.ObjectType == MapObjectType.Decoration && decorationConfig != null)
            {
                MapDecorationPrefabConfig.DecorationPrefabItem item = decorationConfig.GetItem(mapObject.ConfigId);
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        return item.Name;
                    }

                    if (item.Prefab != null)
                    {
                        return item.Prefab.name;
                    }
                }
            }

            if (mapObject.ObjectType == MapObjectType.Resource)
            {
                WorldResourceEditorItem item = GetWorldResourceItem(mapObject.ConfigId);
                if (item != null && !string.IsNullOrEmpty(item.Name))
                {
                    return item.Name;
                }
            }

            return $"Object {mapObject.ObjectId}";
        }

        private string GetObjectResourceSummary(MapObjectData mapObject)
        {
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource)
            {
                return "None";
            }

            WorldResourceEditorItem item = GetWorldResourceItem(mapObject.ConfigId);
            return item != null ? GetWorldResourceInteractionSummary(item) : "Missing Config";
        }

        private void DeleteMapObject(MapObjectData mapObject)
        {
            if (currentMap == null || currentMap.Objects == null || mapObject == null)
            {
                return;
            }

            currentMap.Objects.Remove(mapObject);
            RebuildObjectIndex();
            RefreshDecorations();
            RefreshMapObjectPreviews();
            Repaint();
            SceneView.RepaintAll();
        }

        private static string FormatCoord(Vector3Int coord)
        {
            return $"{coord.x}, {coord.y}, {coord.z}";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.##}, {value.y:0.##}, {value.z:0.##}";
        }

        public static void Open()
        {
            WorldMapEditorWindow.OpenWorldMapEditor();
        }

        protected override void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawMainEditorColumns();
                }

                DrawRightDockPreviewPanel();
            }
        }

        private void DrawMainEditorColumns()
        {
            float contentWidth = Mathf.Max(720f, position.width - RightDockPanelWidth - 18f);
            float middleWidth = GetMiddlePanelWidth(contentWidth);
            float leftWidth = middleWidth > 0f
                ? Mathf.Max(360f, contentWidth - middleWidth - DecorationColumnGap)
                : contentWidth;

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(contentWidth), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth), GUILayout.MinWidth(360f), GUILayout.ExpandHeight(true)))
                {
                    DrawMainTabToolbar();
                    DrawActiveLeftPanel(leftWidth);
                }

                if (middleWidth > 0f)
                {
                    GUILayout.Space(DecorationColumnGap);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(middleWidth), GUILayout.MinWidth(middleWidth), GUILayout.MaxWidth(middleWidth), GUILayout.ExpandHeight(true)))
                    {
                        DrawActiveMiddlePanel(middleWidth);
                    }
                }
            }
        }

        private float GetMiddlePanelWidth(float contentWidth)
        {
            return Mathf.Min(MiddlePanelWidth, Mathf.Max(360f, contentWidth - 360f - DecorationColumnGap));
        }

        private void DrawActiveLeftPanel(float panelWidth)
        {
            switch (activeMainTab)
            {
                case MainTab.Map:
                    DrawMapControlPanel();
                    break;

                case MainTab.Paint:
                    DrawPaintControlPanel();
                    break;

                case MainTab.Points:
                    DrawPointsPanel();
                    break;

                case MainTab.Decoration:
                    DrawDecorationPlacementPanel(panelWidth);
                    break;

                case MainTab.Resources:
                    DrawObjectPlacementPanel(panelWidth);
                    break;
            }
        }

        private void DrawActiveMiddlePanel(float panelWidth)
        {
            switch (activeMainTab)
            {
                case MainTab.Map:
                    DrawMapLogicDefaultsPanel(panelWidth);
                    break;

                case MainTab.Paint:
                    DrawPaintBrushPreviews();
                    break;

                case MainTab.Points:
                    DrawPointsPreviewPanel(panelWidth);
                    break;

                case MainTab.Decoration:
                    DrawDecorationSourcePreviewPanel(panelWidth);
                    break;

                case MainTab.Resources:
                    DrawObjectSourcePreviewPanel(panelWidth);
                    break;
            }
        }

        private void DrawRightDockPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(RightDockPanelWidth), GUILayout.ExpandHeight(true)))
            {
                SirenixEditorGUI.BeginBox();
                DrawRightDockSelectionPanel();
                GUILayout.FlexibleSpace();
                SirenixEditorGUI.EndBox();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent(EditorTitle);
            ApplyFixedEditorMode();
            TryLoadPrefabConfig();
            if (SupportsResourcesTab)
            {
                TryLoadWorldResourceItems();
            }

            SceneView.duringSceneGui += OnSceneGUI;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneView.duringSceneGui -= OnSceneGUI;
            paintedThisDrag.Clear();
        }

        private void DrawMapToolButtons()
        {
            float contentWidth = Mathf.Max(760f, position.width - RightDockPanelWidth - 36f);
            float rightWidth = Mathf.Min(430f, Mathf.Max(380f, contentWidth * 0.48f));
            float leftWidth = Mathf.Max(360f, contentWidth - rightWidth - DecorationColumnGap);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(contentWidth)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth), GUILayout.MinWidth(leftWidth), GUILayout.MaxWidth(leftWidth)))
                {
                    DrawMapControlPanel();
                }

                GUILayout.Space(DecorationColumnGap);
                DrawMapLogicDefaultsPanel(rightWidth);
            }
        }

        private void DrawMapControlPanel()
        {
            DrawMapSettingsFields();
            GUILayout.Space(4f);
            DrawMapReferenceFields();
            GUILayout.Space(4f);

            bool createGrid;
            bool rebuild;
            bool clear;
            bool import;
            bool export;
            bool validate;

            using (new EditorGUILayout.HorizontalScope())
            {
                createGrid = GUILayout.Button("Create Grid", GUILayout.Width(128f), GUILayout.Height(44f));
                rebuild = GUILayout.Button("Rebuild", GUILayout.Width(118f), GUILayout.Height(44f));
                clear = GUILayout.Button("Clear", GUILayout.Width(108f), GUILayout.Height(44f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                import = GUILayout.Button("Import Json", GUILayout.Width(128f), GUILayout.Height(44f));
                export = GUILayout.Button("Export Json", GUILayout.Width(118f), GUILayout.Height(44f));
                validate = GUILayout.Button("Validate", GUILayout.Width(108f), GUILayout.Height(44f));
            }

            if (createGrid) CreateGridMap();
            if (rebuild) RebuildPreview();
            if (clear) ClearMap();
            if (import) QueueImportJson();
            if (export) ExportJson();
            if (validate) ValidateCurrentMap();

            GUILayout.Space(8f);
            DrawMapLogicApplyButtons();
            GUILayout.Space(8f);
            DrawMapStatusFields();
        }

        private void DrawMapSettingsFields()
        {
            DrawFixedIntField("Map Id", ref mapId, 1);
            DrawFixedTextField("Map Name", ref mapName);
            DrawFixedTextArea("Description", ref description);
            DrawFixedIntField("Width X", ref width, 1);
            DrawFixedIntField("Height Y", ref height, 1);
            DrawFixedIntField("Depth Z", ref depth, 1);
            DrawFixedFloatField("Tile Size", ref tileSize, 0.1f);
            DrawFixedDefaultTypeField();
            DrawValidationModeField();
        }

        private void DrawValidationModeField()
        {
            if (UsesFixedValidationMode)
            {
                validationMode = DefaultValidationMode;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.EnumPopup("Validation Mode", validationMode, GUILayout.Width(382f));
                }

                return;
            }

            validationMode = (MapEditorValidationMode)EditorGUILayout.EnumPopup("Validation Mode", validationMode, GUILayout.Width(382f));
        }

        private void DrawFixedTextField(string label, ref string value)
        {
            const float labelWidth = 112f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                value = EditorGUILayout.TextField(value, GUILayout.Width(fieldWidth));
            }
        }

        private void DrawFixedTextArea(string label, ref string value)
        {
            const float labelWidth = 112f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                value = EditorGUILayout.TextArea(value, GUILayout.Width(fieldWidth), GUILayout.Height(42f));
            }
        }

        private void DrawFixedIntField(string label, ref int value, int minValue)
        {
            const float labelWidth = 112f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                value = Mathf.Max(minValue, EditorGUILayout.IntField(value, GUILayout.Width(fieldWidth)));
            }
        }

        private void DrawFixedFloatField(string label, ref float value, float minValue)
        {
            const float labelWidth = 112f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                value = Mathf.Max(minValue, EditorGUILayout.FloatField(value, GUILayout.Width(fieldWidth)));
            }
        }

        private void DrawFixedDefaultTypeField()
        {
            const float labelWidth = 112f;
            const float buttonWidth = 54f;
            TypeBrush[] options =
            {
                TypeBrush.Grass,
                TypeBrush.Hill,
                TypeBrush.Snow,
                TypeBrush.Water,
                TypeBrush.Road
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Default Type", GUILayout.Width(labelWidth));
                for (int i = 0; i < options.Length; i++)
                {
                    TypeBrush option = options[i];
                    Color oldBackgroundColor = GUI.backgroundColor;
                    if (defaultTileType == option)
                    {
                        GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                    }

                    if (GUILayout.Button(option.ToString(), GUILayout.Width(buttonWidth), GUILayout.Height(20f)))
                    {
                        defaultTileType = option;
                    }

                    GUI.backgroundColor = oldBackgroundColor;
                }
            }
        }

        private void DrawMapReferenceFields()
        {
            DrawFixedObjectField("Prefab Config", ref prefabConfig);
            DrawFixedObjectField("Decoration Config", ref decorationConfig);

            using (new EditorGUI.DisabledScope(true))
            {
                DrawFixedObjectField("Preview Root", ref previewRoot);
            }
        }

        private void DrawFixedObjectField<T>(string label, ref T value) where T : UnityEngine.Object
        {
            const float labelWidth = 112f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                value = (T)EditorGUILayout.ObjectField(value, typeof(T), false, GUILayout.Width(fieldWidth));
            }
        }

        private void DrawMapStatusFields()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                DrawFixedReadOnlyTextField("Tile Count", TileCount.ToString());
                DrawFixedReadOnlyTextField("Object Count", ObjectCount.ToString());
            }

            DrawCurrentMapDataFoldout();
        }

        private void DrawFixedReadOnlyTextField(string label, string value)
        {
            const float labelWidth = 156f;
            const float fieldWidth = 270f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
                EditorGUILayout.TextField(value, GUILayout.Width(fieldWidth));
            }
        }

        private void DrawCurrentMapDataFoldout()
        {
            showCurrentMapData = EditorGUILayout.Foldout(showCurrentMapData, "Current Map", true);
            if (!showCurrentMapData)
            {
                return;
            }

            if (currentMap == null)
            {
                EditorGUILayout.HelpBox("Current map is null.", MessageType.Info);
                return;
            }

            if (currentMapPropertyTree == null || currentMapPropertyTreeTarget != currentMap)
            {
                currentMapReadOnlyView = new MapDataReadOnlyView(currentMap);
                currentMapPropertyTree = PropertyTree.Create(currentMapReadOnlyView);
                currentMapPropertyTreeTarget = currentMap;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                currentMapPropertyTree.Draw(false);
            }
        }

        private void CreateGridMap()
        {
            currentMap = new MapData(mapId, mapName, width, height, depth);
            currentMapPropertyTree = null;
            currentMapPropertyTreeTarget = null;
            currentMapReadOnlyView = null;
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
            currentMapPropertyTree = null;
            currentMapPropertyTreeTarget = null;
            currentMapReadOnlyView = null;
            selectedTile = null;
            tileMap.Clear();
            objectsByCoord.Clear();
            spawnPoints.Clear();
            hasGoalPoint = false;
            ClearPreviewObjects();
        }

        [TabGroup("Tabs", "Decoration", false, 3), OnInspectorGUI]
        private void DrawDecorationTab()
        {
            TryLoadDecorationConfig();

            float contentWidth = Mathf.Max(760f, position.width - RightDockPanelWidth - 36f);
            float rightWidth = Mathf.Min(DecorationSourcePreviewPanelWidth, Mathf.Max(320f, contentWidth * 0.45f));
            float leftWidth = Mathf.Max(360f, contentWidth - rightWidth - DecorationColumnGap);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(contentWidth)))
            {
                DrawDecorationPlacementPanel(leftWidth);
                GUILayout.Space(DecorationColumnGap);
                DrawDecorationSourcePreviewPanel(rightWidth);
            }
        }

        [TabGroup("Tabs", "Resources", false, 4), OnInspectorGUI]
        private void DrawResourcesTab()
        {
            TryLoadWorldResourceItems();

            float contentWidth = Mathf.Max(760f, position.width - RightDockPanelWidth - 36f);
            float rightWidth = Mathf.Min(DecorationSourcePreviewPanelWidth, Mathf.Max(320f, contentWidth * 0.45f));
            float leftWidth = Mathf.Max(360f, contentWidth - rightWidth - DecorationColumnGap);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(contentWidth)))
            {
                DrawObjectPlacementPanel(leftWidth);
                GUILayout.Space(DecorationColumnGap);
                DrawObjectSourcePreviewPanel(rightWidth);
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
                GUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!HasSelection))
                    {
                        addDecoration = GUILayout.Button("Add", GUILayout.Width(124f), GUILayout.Height(40f));
                        removeDecorations = GUILayout.Button("Remove", GUILayout.Width(124f), GUILayout.Height(40f));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    createConfig = GUILayout.Button("Create Config", GUILayout.Width(124f), GUILayout.Height(40f));
                    using (new EditorGUI.DisabledScope(decorationConfig == null))
                    {
                        selectConfig = GUILayout.Button("Select Config", GUILayout.Width(124f), GUILayout.Height(40f));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    useDefaults = GUILayout.Button("Use Defaults", GUILayout.Width(124f), GUILayout.Height(40f));
                    clearAll = GUILayout.Button("Clear All", GUILayout.Width(124f), GUILayout.Height(40f));
                }

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
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth));
            EditorGUILayout.LabelField("Decoration Source Preview", EditorStyles.boldLabel);

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

            float listWidth = panelWidth - 24f;
            decorationPreviewScroll = EditorGUILayout.BeginScrollView(decorationPreviewScroll, false, true, GUILayout.Width(panelWidth));

            for (int i = 0; i < decorationConfig.Items.Count; i++)
            {
                MapDecorationPrefabConfig.DecorationPrefabItem item = decorationConfig.Items[i];
                if (item == null) continue;
                DrawDecorationSourcePreviewRow(item, listWidth);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDecorationSourcePreviewRow(MapDecorationPrefabConfig.DecorationPrefabItem item, float rowWidth)
        {
            const float rowHeight = 84f;
            Rect rowRect = GUILayoutUtility.GetRect(rowWidth, rowHeight, GUILayout.Width(rowWidth), GUILayout.Height(rowHeight));
            rowRect = new Rect(rowRect.x + 2f, rowRect.y + 2f, rowRect.width - 4f, rowRect.height - 4f);

            bool isSelected = item.Id == selectedDecorationId;
            Color background = isSelected ? new Color(0.20f, 0.33f, 0.40f, 1f) : new Color(0.24f, 0.24f, 0.24f, 1f);
            EditorGUI.DrawRect(rowRect, background);
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);

            Rect radioRect = new Rect(rowRect.x + 8f, rowRect.y + 31f, 18f, 18f);
            Rect previewRect = new Rect(radioRect.xMax + 4f, rowRect.y + 9f, 62f, 62f);
            Rect textRect = new Rect(previewRect.xMax + 8f, rowRect.y + 8f, rowRect.width - previewRect.width - radioRect.width - 34f, 22f);
            Rect objectFieldRect = new Rect(textRect.x, rowRect.y + 42f, Mathf.Min(170f, textRect.width), 18f);

            GUI.Label(radioRect, isSelected ? "*" : "o", EditorStyles.boldLabel);

            GUI.Box(previewRect, GUIContent.none);
            Texture2D preview = GetDecorationPreview(item.Prefab);
            if (preview != null)
            {
                GUI.DrawTexture(new Rect(previewRect.x + 3f, previewRect.y + 3f, previewRect.width - 6f, previewRect.height - 6f), preview, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(previewRect, "No\nPrefab", EditorStyles.centeredGreyMiniLabel);
            }

            string itemName = string.IsNullOrEmpty(item.Name) ? "Unnamed" : item.Name;
            GUI.Label(textRect, $"{item.Id} - {itemName}", isSelected ? EditorStyles.boldLabel : EditorStyles.label);

            EditorGUI.ObjectField(objectFieldRect, GUIContent.none, item.Prefab, typeof(GameObject), false);
            EditorGUIUtility.AddCursorRect(objectFieldRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !rowRect.Contains(current.mousePosition))
            {
                return;
            }

            SelectDecorationSourceItem(item);

            if (objectFieldRect.Contains(current.mousePosition))
            {
                LocateDecorationPrefab(item.Prefab);
            }

            current.Use();
            Repaint();
        }

        private static string GetPrefabAssetPath(GameObject prefab)
        {
            if (prefab == null)
            {
                return "Missing prefab";
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            return string.IsNullOrEmpty(path) ? "Scene object, not an asset prefab" : path;
        }

        private void SelectDecorationSourceItem(MapDecorationPrefabConfig.DecorationPrefabItem item)
        {
            selectedDecorationId = item.Id;
            UseSelectedDecorationDefaults();
        }

        private static void LocateDecorationPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
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

            decorationLocalPosition = GetDecorationDefaultLocalPosition(item);
            decorationLocalEuler = item.DefaultLocalEuler;
            decorationLocalScale = item.DefaultLocalScale;
        }

        private Vector3 GetDecorationDefaultLocalPosition(MapDecorationPrefabConfig.DecorationPrefabItem item)
        {
            if (item == null)
            {
                return Vector3.up * tileSize;
            }

            if (item.DefaultLocalPosition == Vector3.zero)
            {
                return Vector3.up * tileSize;
            }

            return item.DefaultLocalPosition;
        }

        private void RemoveDecorationsAtSelected()
        {
            if (!EnsureMap()) return;
            currentMap.Objects.RemoveAll(decoration =>
                decoration != null &&
                decoration.ObjectType == MapObjectType.Decoration &&
                decoration.Coord == selectedCoord);
            RebuildObjectIndex();
            RefreshDecorations();
        }

        private void ClearAllDecorations()
        {
            if (!EnsureMap()) return;
            if (!EditorUtility.DisplayDialog("Clear Decorations", "Clear all decorations in current map?", "Clear", "Cancel")) return;
            currentMap.Objects.RemoveAll(decoration => decoration != null && decoration.ObjectType == MapObjectType.Decoration);
            RebuildObjectIndex();
            RefreshDecorations();
        }

        private void DrawObjectPlacementPanel(float panelWidth)
        {
            bool addResource = false;
            bool removeResources = false;
            bool useDefaults;
            bool reloadTypes;
            bool clearResources;

            TryLoadWorldResourceItems();

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth)))
            {
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 155f;

                EditorGUILayout.LabelField("World Resources", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Object Type", MapObjectType.Resource.ToString());
                DrawWorldResourceSelector();
                EditorGUILayout.LabelField("Selected Resource", GetSelectedWorldResourceName());
                EditorGUILayout.LabelField("Interaction", GetWorldResourceInteractionSummary(GetSelectedWorldResourceItem()));
                resourceLocalPosition = EditorGUILayout.Vector3Field("Local Position", resourceLocalPosition);
                resourceLocalEuler = EditorGUILayout.Vector3Field("Local Euler", resourceLocalEuler);
                resourceLocalScale = EditorGUILayout.Vector3Field("Local Scale", resourceLocalScale);
                resourceBlocksBuild = EditorGUILayout.Toggle("Blocks Build", resourceBlocksBuild);
                resourceBlocksMove = EditorGUILayout.Toggle("Blocks Move", resourceBlocksMove);
                EditorGUILayout.LabelField("Resource Count", ResourceObjectCount.ToString());
                GUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!HasSelection))
                    {
                        addResource = GUILayout.Button("Add", GUILayout.Width(124f), GUILayout.Height(40f));
                        removeResources = GUILayout.Button("Remove", GUILayout.Width(124f), GUILayout.Height(40f));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    useDefaults = GUILayout.Button("Use Defaults", GUILayout.Width(124f), GUILayout.Height(40f));
                    reloadTypes = GUILayout.Button("Reload", GUILayout.Width(124f), GUILayout.Height(40f));
                }

                clearResources = GUILayout.Button("Clear Resources", GUILayout.Width(124f), GUILayout.Height(40f));

                EditorGUIUtility.labelWidth = oldLabelWidth;
            }

            if (addResource) AddResourceObjectToSelected();
            if (removeResources) RemoveResourceObjectsAtSelected();
            if (useDefaults) UseSelectedResourceDefaults();
            if (reloadTypes) TryLoadWorldResourceItems(true);
            if (clearResources) ClearResourceObjects();
        }

        private void DrawObjectSourcePreviewPanel(float panelWidth)
        {
            TryLoadWorldResourceItems();
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth)))
            {
                EditorGUILayout.LabelField("Resource Types", EditorStyles.boldLabel);

                if (worldResourceItems.Count == 0)
                {
                    EditorGUILayout.HelpBox("No resource types found. Generate Luban json first.", MessageType.Info);
                    return;
                }

                float listWidth = panelWidth - 24f;
                using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(resourcePreviewScroll, false, true, GUILayout.Width(panelWidth)))
                {
                    resourcePreviewScroll = scroll.scrollPosition;

                    for (int i = 0; i < worldResourceItems.Count; i++)
                    {
                        DrawWorldResourcePreviewRow(worldResourceItems[i], listWidth);
                    }
                }
            }
        }

        private void DrawWorldResourcePreviewRow(WorldResourceEditorItem item, float rowWidth)
        {
            if (item == null) return;

            const float rowHeight = 116f;
            Rect rowRect = GUILayoutUtility.GetRect(rowWidth, rowHeight, GUILayout.Width(rowWidth), GUILayout.Height(rowHeight));
            rowRect = new Rect(rowRect.x + 2f, rowRect.y + 2f, rowRect.width - 4f, rowRect.height - 4f);

            bool isSelected = item.Id == selectedWorldResourceId;
            Color background = isSelected ? new Color(0.20f, 0.33f, 0.40f, 1f) : new Color(0.24f, 0.24f, 0.24f, 1f);
            EditorGUI.DrawRect(rowRect, background);
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);

            GameObject prefab = GetWorldResourcePrefab(item);
            Rect radioRect = new Rect(rowRect.x + 8f, rowRect.y + 41f, 18f, 18f);
            Rect previewRect = new Rect(radioRect.xMax + 4f, rowRect.y + 10f, 70f, 70f);
            Rect titleRect = new Rect(previewRect.xMax + 8f, rowRect.y + 8f, rowRect.width - previewRect.width - radioRect.width - 34f, 22f);
            Rect infoRect = new Rect(titleRect.x, titleRect.yMax + 3f, titleRect.width, 18f);
            Rect interactionRect = new Rect(titleRect.x, infoRect.yMax + 1f, titleRect.width, 18f);
            Rect objectFieldRect = new Rect(titleRect.x, interactionRect.yMax + 2f, Mathf.Min(230f, titleRect.width), 18f);
            Rect pathRect = new Rect(titleRect.x, objectFieldRect.yMax + 2f, titleRect.width, 18f);

            GUI.Label(radioRect, isSelected ? "*" : "o", EditorStyles.boldLabel);

            GUI.Box(previewRect, GUIContent.none);
            Texture2D preview = GetWorldResourcePreview(prefab);
            if (preview != null)
            {
                GUI.DrawTexture(new Rect(previewRect.x + 3f, previewRect.y + 3f, previewRect.width - 6f, previewRect.height - 6f), preview, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(new Rect(previewRect.x + 3f, previewRect.y + 3f, previewRect.width - 6f, previewRect.height - 6f), GetWorldResourceColor(item));
                GUI.Label(previewRect, "No\nPrefab", EditorStyles.centeredGreyMiniLabel);
            }

            GUI.Label(titleRect, $"{item.Id} - {GetWorldResourceName(item)}", isSelected ? EditorStyles.boldLabel : EditorStyles.label);
            GUI.Label(infoRect, $"Type {GetWorldResourceCategoryName(item)} | Build {item.BlocksBuild} | Move {item.BlocksMove}", EditorStyles.miniLabel);
            GUI.Label(interactionRect, GetWorldResourceInteractionSummary(item), EditorStyles.miniLabel);
            EditorGUI.ObjectField(objectFieldRect, GUIContent.none, prefab, typeof(GameObject), false);
            GUI.Label(pathRect, GetWorldResourcePrefabPath(item, prefab), EditorStyles.miniLabel);

            EditorGUIUtility.AddCursorRect(objectFieldRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !rowRect.Contains(current.mousePosition))
            {
                return;
            }

            SelectWorldResourceItem(item);
            if (objectFieldRect.Contains(current.mousePosition))
            {
                LocateWorldResourcePrefab(prefab);
            }

            current.Use();
            Repaint();
        }

        private static Texture2D GetWorldResourcePreview(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            return preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab);
        }

        private static string GetWorldResourcePrefabPath(WorldResourceEditorItem item, GameObject prefab)
        {
            if (item == null)
            {
                return "Prefab: None";
            }

            string assetPath = GetPrefabAssetPath(prefab);
            if (!string.IsNullOrEmpty(item.PrefabLocation) && item.PrefabLocation != assetPath)
            {
                return $"Excel: {item.PrefabLocation}";
            }

            return $"Path: {assetPath}";
        }

        private void SelectWorldResourceItem(WorldResourceEditorItem item)
        {
            if (item == null)
            {
                return;
            }

            selectedWorldResourceId = item.Id;
            UseSelectedResourceDefaults();
        }

        private static void LocateWorldResourcePrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private void DrawWorldResourceSelector()
        {
            BuildWorldResourceOptions();
            if (worldResourceIdOptions.Count == 0)
            {
                EditorGUILayout.Popup("Resource Type", 0, new[] { "None" });
                selectedWorldResourceId = 0;
                return;
            }

            int selectedIndex = Mathf.Max(0, worldResourceIdOptions.IndexOf(selectedWorldResourceId));
            int newIndex = EditorGUILayout.Popup("Resource Type", selectedIndex, worldResourceNameOptions.ToArray());
            selectedWorldResourceId = worldResourceIdOptions[Mathf.Clamp(newIndex, 0, worldResourceIdOptions.Count - 1)];
        }

        private void BuildWorldResourceOptions()
        {
            worldResourceIdOptions.Clear();
            worldResourceNameOptions.Clear();
            TryLoadWorldResourceItems();

            for (int i = 0; i < worldResourceItems.Count; i++)
            {
                WorldResourceEditorItem item = worldResourceItems[i];
                if (item == null || item.Id <= 0) continue;
                worldResourceIdOptions.Add(item.Id);
                worldResourceNameOptions.Add($"{item.Id} - {GetWorldResourceName(item)}");
            }
        }

        private void AddResourceObjectToSelected()
        {
            if (!EnsureMap()) return;
            if (!HasSelection)
            {
                EditorUtility.DisplayDialog("World Object", "Select a cell first.", "OK");
                return;
            }

            WorldResourceEditorItem item = GetSelectedWorldResourceItem();
            if (item == null)
            {
                EditorUtility.DisplayDialog("World Object", "Please select a valid Resource Type.", "OK");
                return;
            }

            if (GetWorldResourcePrefab(item) == null)
            {
                EditorUtility.DisplayDialog("World Object", $"Missing prefab for resource: {item.Id} - {GetWorldResourceName(item)}", "OK");
                return;
            }

            MapObjectData mapObject = new MapObjectData(
                CreateNextMapObjectId(),
                MapObjectType.Resource,
                item.Id,
                selectedCoord,
                resourceLocalPosition,
                resourceLocalEuler,
                resourceLocalScale)
            {
                BlocksBuild = resourceBlocksBuild,
                BlocksMove = resourceBlocksMove,
            };

            currentMap.Objects.Add(mapObject);
            RebuildObjectIndex();
            RefreshMapObjectPreviews();
            Repaint();
            SceneView.RepaintAll();
        }

        private void RemoveResourceObjectsAtSelected()
        {
            if (!EnsureMap()) return;
            currentMap.Objects.RemoveAll(mapObject =>
                mapObject != null &&
                mapObject.ObjectType == MapObjectType.Resource &&
                mapObject.Coord == selectedCoord);
            RebuildObjectIndex();
            RefreshMapObjectPreviews();
            Repaint();
            SceneView.RepaintAll();
        }

        private void ClearResourceObjects()
        {
            if (!EnsureMap()) return;
            if (!EditorUtility.DisplayDialog("Clear Resources", "Clear all resource objects in current map?", "Clear", "Cancel")) return;
            currentMap.Objects.RemoveAll(mapObject => mapObject != null && mapObject.ObjectType == MapObjectType.Resource);
            RebuildObjectIndex();
            RefreshMapObjectPreviews();
            Repaint();
            SceneView.RepaintAll();
        }

        private void UseSelectedResourceDefaults()
        {
            WorldResourceEditorItem item = GetSelectedWorldResourceItem();
            resourceLocalPosition = Vector3.up * tileSize;
            resourceLocalEuler = Vector3.zero;
            resourceLocalScale = Vector3.one;

            if (item == null) return;
            resourceBlocksBuild = item.BlocksBuild;
            resourceBlocksMove = item.BlocksMove;
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
            if (selectedTile != null) selectedTile.ApplyDefaultLogic(currentMap);
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

        [TabGroup("Tabs", "Points", false, 2), OnInspectorGUI]
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
                    addSpawn = GUILayout.Button("Add Spawn", GUILayout.Width(116f), GUILayout.Height(44f));
                    setGoal = GUILayout.Button("Set Goal", GUILayout.Width(108f), GUILayout.Height(44f));
                }

                applyPoints = GUILayout.Button("Apply Points", GUILayout.Width(112f), GUILayout.Height(44f));
                clearPoints = GUILayout.Button("Clear Points", GUILayout.Width(112f), GUILayout.Height(44f));
            }

            if (addSpawn) AddSelectedAsSpawn();
            if (setGoal) SetSelectedAsGoal();
            if (applyPoints) ApplyPointsToMap();
            if (clearPoints) ClearPoints();
        }

        private void DrawPointsPanel()
        {
            EditorGUILayout.LabelField("Spawn Points", EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    spawnPoints[i] = EditorGUILayout.Vector3IntField($"#{i}", spawnPoints[i]);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        removeIndex = i;
                    }
                }
            }

            if (removeIndex >= 0)
            {
                spawnPoints.RemoveAt(removeIndex);
            }

            if (GUILayout.Button("Add Spawn Entry", GUILayout.Width(132f), GUILayout.Height(38f)))
            {
                spawnPoints.Add(default);
            }

            GUILayout.Space(8f);
            hasGoalPoint = EditorGUILayout.Toggle("Has Goal", hasGoalPoint);
            using (new EditorGUI.DisabledScope(!hasGoalPoint))
            {
                goalPoint = EditorGUILayout.Vector3IntField("Goal Point", goalPoint);
            }

            GUILayout.Space(8f);
            DrawPointsToolButtons();
        }

        private void DrawPointsPreviewPanel(float panelWidth)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Point Preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Spawn Count", spawnPoints.Count);
                EditorGUILayout.Toggle("Has Goal", hasGoalPoint);
                EditorGUILayout.Vector3IntField("Goal Point", goalPoint);
            }

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Spawn Points", EditorStyles.miniBoldLabel);

            if (spawnPoints.Count == 0)
            {
                EditorGUILayout.HelpBox("No spawn points.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    EditorGUILayout.Vector3IntField($"#{i}", spawnPoints[i]);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
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
        private void DrawMapLogicDefaultsPanel(float panelWidth)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.MaxWidth(panelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Logic Defaults Source Preview", EditorStyles.boldLabel);

            if (currentMap == null)
            {
                EditorGUILayout.HelpBox("Create or import a map first.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            currentMap.EnsureRuntimeCollections();
            float listWidth = panelWidth - 24f;
            logicDefaultsPreviewScroll = EditorGUILayout.BeginScrollView(logicDefaultsPreviewScroll, false, true, GUILayout.Width(panelWidth), GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("Tile Type Defaults", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < currentMap.TileLogicDefaults.Count; i++)
            {
                MapTileLogicDefaultData item = currentMap.TileLogicDefaults[i];
                if (item == null) continue;
                DrawTileLogicDefaultPreviewRow(item, listWidth);
            }

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Overlay Defaults", EditorStyles.miniBoldLabel);

            for (int i = 0; i < currentMap.OverlayLogicDefaults.Count; i++)
            {
                MapOverlayLogicDefaultData item = currentMap.OverlayLogicDefaults[i];
                if (item == null) continue;
                DrawOverlayLogicDefaultPreviewRow(item, listWidth);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawMapLogicApplyButtons()
        {
            EditorGUILayout.LabelField("Logic Defaults", EditorStyles.boldLabel);

            if (currentMap == null)
            {
                EditorGUILayout.HelpBox("Create or import a map first.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasSelection))
                {
                    if (GUILayout.Button("Selected", GUILayout.Width(104f), GUILayout.Height(38f))) ApplyDefaultLogicToSelected();
                    if (GUILayout.Button("Same Type", GUILayout.Width(104f), GUILayout.Height(38f))) ApplyDefaultLogicToSameType();
                }

                if (GUILayout.Button("All Cells", GUILayout.Width(104f), GUILayout.Height(38f))) ApplyDefaultLogicToAllCells();
            }
        }

        private void DrawTileLogicDefaultPreviewRow(MapTileLogicDefaultData item, float rowWidth)
        {
            DrawLogicDefaultPreviewRow(
                item.Type.ToString(),
                GetPrefab(item.Type),
                GetFallbackColor(item.Type),
                ref item.Walkable,
                ref item.Buildable,
                ref item.MoveCost,
                rowWidth);
        }

        private void DrawOverlayLogicDefaultPreviewRow(MapOverlayLogicDefaultData item, float rowWidth)
        {
            DrawLogicDefaultPreviewRow(
                item.Overlay.ToString(),
                GetOverlayPreviewPrefab(item.Overlay),
                GetOverlayFallbackColor(item.Overlay),
                ref item.Walkable,
                ref item.Buildable,
                ref item.MoveCost,
                rowWidth);
        }

        private void DrawLogicDefaultPreviewRow(string label, GameObject prefab, Color fallbackColor, ref bool walkable, ref bool buildable, ref int moveCost, float rowWidth)
        {
            const float rowHeight = 92f;
            Rect rowRect = GUILayoutUtility.GetRect(rowWidth, rowHeight, GUILayout.Width(rowWidth), GUILayout.Height(rowHeight));
            rowRect = new Rect(rowRect.x + 2f, rowRect.y + 2f, rowRect.width - 4f, rowRect.height - 4f);

            EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.24f, 0.24f, 1f));
            GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);

            Rect previewRect = new Rect(rowRect.x + 8f, rowRect.y + 17f, 56f, 56f);
            Rect colorRect = new Rect(previewRect.x, previewRect.yMax - 5f, previewRect.width, 5f);
            Rect nameRect = new Rect(previewRect.xMax + 10f, rowRect.y + 14f, 110f, 18f);
            Rect objectFieldRect = new Rect(nameRect.x, rowRect.y + 42f, 150f, 18f);
            Rect walkRect = new Rect(objectFieldRect.xMax + 12f, rowRect.y + 14f, 62f, 18f);
            Rect buildRect = new Rect(walkRect.x, rowRect.y + 36f, 62f, 18f);
            Rect costLabelRect = new Rect(walkRect.x, rowRect.y + 60f, 34f, 18f);
            Rect costRect = new Rect(costLabelRect.xMax + 4f, rowRect.y + 58f, 48f, 18f);

            GUI.Box(previewRect, GUIContent.none);
            Texture2D preview = GetPrefabPreview(prefab);
            if (preview != null)
            {
                GUI.DrawTexture(new Rect(previewRect.x + 3f, previewRect.y + 3f, previewRect.width - 6f, previewRect.height - 6f), preview, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(previewRect, "No\nPrefab", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUI.DrawRect(colorRect, fallbackColor);
            GUI.Label(nameRect, label, EditorStyles.boldLabel);
            EditorGUI.ObjectField(objectFieldRect, GUIContent.none, prefab, typeof(GameObject), false);
            walkable = EditorGUI.ToggleLeft(walkRect, "Walk", walkable);
            buildable = EditorGUI.ToggleLeft(buildRect, "Build", buildable);
            GUI.Label(costLabelRect, "Cost");
            moveCost = Mathf.Max(0, EditorGUI.IntField(costRect, moveCost));
        }

        private void ApplyDefaultLogicToSelected()
        {
            if (selectedTile == null || currentMap == null) return;
            selectedTile.ApplyDefaultLogic(currentMap);
            RefreshAfterLogicApply(1);
        }

        private void ApplyDefaultLogicToSameType()
        {
            if (selectedTile == null || currentMap == null || currentMap.Cells == null) return;

            MapTileType type = selectedTile.Type;
            int count = 0;
            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData tile = currentMap.Cells[i];
                if (tile == null || tile.Type != type) continue;
                tile.ApplyDefaultLogic(currentMap);
                count++;
            }

            RefreshAfterLogicApply(count);
        }

        private void ApplyDefaultLogicToAllCells()
        {
            if (currentMap == null || currentMap.Cells == null) return;

            int count = 0;
            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData tile = currentMap.Cells[i];
                if (tile == null) continue;
                tile.ApplyDefaultLogic(currentMap);
                count++;
            }

            RefreshAfterLogicApply(count);
        }

        private void RefreshAfterLogicApply(int count)
        {
            RefreshMarkers();
            Repaint();
            SceneView.RepaintAll();
            Debug.Log($"Applied map logic defaults to {count} cell(s).");
        }

        private void QueueImportJson()
        {
            EditorApplication.delayCall -= ImportJson;
            EditorApplication.delayCall += ImportJson;
        }

        private void ImportJson()
        {
            try
            {
                string path = MapJsonService.OpenImportPanel();
                if (string.IsNullOrEmpty(path)) return;

                MapData data = MapJsonService.Load(path);
                if (data == null)
                {
                    EditorUtility.DisplayDialog("Import Failed", "Json did not contain valid MapData.", "OK");
                    return;
                }

                data.EnsureRuntimeCollections();
                if (!ApplyEditorModeConstraints(data, true, "Import"))
                {
                    return;
                }

                currentMap = data;
                currentMapPropertyTree = null;
                currentMapPropertyTreeTarget = null;
                currentMapReadOnlyView = null;
                PullSettingsFromMap();
                RebuildTileIndex();
                CreatePreviewObjects();
                Repaint();
                SceneView.RepaintAll();
                Debug.Log($"Map imported: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
            }
        }

        private void ExportJson()
        {
            if (!EnsureMap()) return;
            if (SupportsPointsTab)
            {
                ApplyPointsToMap();
            }

            if (!ApplyEditorModeConstraints(currentMap, true, "Export"))
            {
                return;
            }

            List<string> errors = MapEditorValidator.Validate(currentMap, validationMode, ValidateMapObject);
            AppendEditorModeValidationErrors(currentMap, errors);

            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++) Debug.LogWarning(errors[i]);
                if (!EditorUtility.DisplayDialog("Map Validation", $"Found {errors.Count} issue(s). Export anyway?", "Export", "Cancel")) return;
            }

            string path = MapJsonService.GetDefaultMapJsonPath(currentMap.Id);
            if (!MapJsonService.ConfirmOverwrite(path))
            {
                return;
            }

            MapJsonService.Save(currentMap, path);
            Debug.Log($"Map exported: {path}");
        }

        private void ValidateCurrentMap()
        {
            if (!EnsureMap()) return;
            List<string> errors = MapEditorValidator.Validate(currentMap, validationMode, ValidateMapObject);
            AppendEditorModeValidationErrors(currentMap, errors);

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

            return TryPickTileByCollider(ray, out coord);
        }

        private bool TryPickTileByCollider(Ray ray, out Vector3Int coord)
        {
            coord = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
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

            return hasCoord;
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

            tile.ApplyDefaultLogicByType(type, currentMap);
            tile.TypeDirection = nextTypeDirection;
            if (type != MapTileType.Grass)
            {
                tile.GrassVisual = null;
            }
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
            tile.ApplyDefaultLogic(currentMap);
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
            tile.ApplyDefaultLogic(currentMap);

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
            RefreshMapObjectPreviews();
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

            ApplyGrassVisualToPreviewObject(tile, instance);
            tileObjects[coord] = instance;
        }

        private void ApplyGrassVisualToPreview(Vector3Int coord)
        {
            if (!tileObjects.TryGetValue(coord, out GameObject tileObject) || tileObject == null)
            {
                return;
            }

            if (!tileMap.TryGetValue(coord, out MapCellData tile))
            {
                return;
            }

            ApplyGrassVisualToPreviewObject(tile, tileObject);
        }

        private void ApplyGrassVisualToPreviewObject(MapCellData tile, GameObject tileObject)
        {
            if (tile == null || tileObject == null)
            {
                return;
            }

            GrassTileMaterialOverride grassVisual = tileObject.GetComponent<GrassTileMaterialOverride>();
            if (grassVisual == null)
            {
                return;
            }

            MapGrassVisualData visualData = tile.Type == MapTileType.Grass ? tile.GrassVisual : null;
            grassVisual.ApplyVisualData(visualData);
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
                Material material = new Material(Shader.Find("CubeTD/LowPolyTextured") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
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
            if (decoration == null || decoration.ObjectType != MapObjectType.Decoration || decoration.ConfigId <= 0) return;
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

        private void RefreshMapObjectPreviews()
        {
            for (int i = objectPreviewObjects.Count - 1; i >= 0; i--)
            {
                if (objectPreviewObjects[i] != null) DestroyImmediate(objectPreviewObjects[i]);
            }

            objectPreviewObjects.Clear();
            if (currentMap == null || currentMap.Objects == null || previewRoot == null) return;

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                CreateResourceObjectPreview(currentMap.Objects[i], i);
            }
        }

        private void CreateResourceObjectPreview(MapObjectData mapObject, int index)
        {
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource || mapObject.ConfigId <= 0) return;
            if (!tileObjects.TryGetValue(mapObject.Coord, out GameObject tileObject) || tileObject == null) return;

            WorldResourceEditorItem item = GetWorldResourceItem(mapObject.ConfigId);
            GameObject prefab = GetWorldResourcePrefab(item);
            GameObject instance = prefab != null ? PrefabUtility.InstantiatePrefab(prefab) as GameObject : null;
            if (instance == null && prefab != null) instance = Instantiate(prefab);
            if (instance == null) instance = CreateResourceFallbackObject(item);
            if (instance == null) return;

            instance.name = $"Resource_{index}_{GetWorldResourceName(item)}";
            instance.transform.SetParent(tileObject.transform, false);
            instance.transform.localPosition = mapObject.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(mapObject.LocalEuler);
            instance.transform.localScale = mapObject.LocalScale;

            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);

            objectPreviewObjects.Add(instance);
        }

        private GameObject CreateResourceFallbackObject(WorldResourceEditorItem item)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.transform.localScale = new Vector3(tileSize * 0.45f, tileSize * 0.55f, tileSize * 0.45f);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("CubeTD/LowPolyTextured") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.color = GetWorldResourceColor(item);
                renderer.sharedMaterial = material;
            }

            return marker;
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
                Material material = new Material(Shader.Find("CubeTD/LowPolyTextured") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
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
            objectPreviewObjects.Clear();
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

        private void TryLoadWorldResourceItems(bool forceReload = false)
        {
            if (!forceReload && worldResourceItems.Count > 0)
            {
                return;
            }

            worldResourceItems.Clear();

            if (!File.Exists(WorldResourceJsonPath))
            {
                return;
            }

            try
            {
                List<WorldResourceEditorItem> items = JsonConvert.DeserializeObject<List<WorldResourceEditorItem>>(File.ReadAllText(WorldResourceJsonPath));
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        WorldResourceEditorItem item = items[i];
                        if (item == null || item.Id <= 0 || !item.Enable)
                        {
                            continue;
                        }

                        worldResourceItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MapEditor] Failed to load world resources from {WorldResourceJsonPath}: {ex.Message}");
            }

            if (selectedWorldResourceId <= 0 && worldResourceItems.Count > 0)
            {
                selectedWorldResourceId = worldResourceItems[0].Id;
                UseSelectedResourceDefaults();
            }
        }

        private WorldResourceEditorItem GetSelectedWorldResourceItem()
        {
            return GetWorldResourceItem(selectedWorldResourceId);
        }

        private WorldResourceEditorItem GetWorldResourceItem(int id)
        {
            TryLoadWorldResourceItems();

            for (int i = 0; i < worldResourceItems.Count; i++)
            {
                WorldResourceEditorItem item = worldResourceItems[i];
                if (item != null && item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        private string GetSelectedWorldResourceName()
        {
            return GetWorldResourceName(GetSelectedWorldResourceItem());
        }

        private static string GetWorldResourceName(WorldResourceEditorItem item)
        {
            if (item == null)
            {
                return "None";
            }

            return string.IsNullOrEmpty(item.Name) ? $"Resource {item.Id}" : item.Name;
        }

        private static string GetWorldResourceCategoryName(WorldResourceEditorItem item)
        {
            if (item == null)
            {
                return "None";
            }

            return ((WorldResourceCategory)item.ResourceType).ToString();
        }

        private static string GetWorldResourceInteractionSummary(WorldResourceEditorItem item)
        {
            if (item == null)
            {
                return "None";
            }

            WorldResourceInteractionType interactionType = (WorldResourceInteractionType)item.InteractionType;
            switch (interactionType)
            {
                case WorldResourceInteractionType.Pickup:
                    return $"Pickup | Reward {item.PickupRewardGroupId}";

                case WorldResourceInteractionType.Gather:
                    return $"Gather | Config {item.GatherConfigId}";

                case WorldResourceInteractionType.MineTarget:
                    return $"Mine Target | Building {item.MineBuildingId}";

                default:
                    return "None";
            }
        }

        private static GameObject GetWorldResourcePrefab(WorldResourceEditorItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.PrefabLocation))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(item.PrefabLocation);
        }

        private static Color GetWorldResourceColor(WorldResourceEditorItem item)
        {
            if (item == null)
            {
                return new Color(0.55f, 0.55f, 0.55f);
            }

            switch ((WorldResourceCategory)item.ResourceType)
            {
                case WorldResourceCategory.Tree:
                    return new Color(0.18f, 0.62f, 0.24f);

                case WorldResourceCategory.Stone:
                    return new Color(0.50f, 0.50f, 0.52f);

                case WorldResourceCategory.Ore:
                    return GetWorldResourceName(item).ToLowerInvariant().Contains("iron")
                        ? new Color(0.60f, 0.58f, 0.55f)
                        : new Color(0.76f, 0.42f, 0.22f);

                case WorldResourceCategory.Plant:
                    return new Color(0.36f, 0.76f, 0.36f);

                default:
                    return new Color(0.55f, 0.55f, 0.55f);
            }
        }

        private int CreateNextMapObjectId()
        {
            int maxObjectId = 0;

            if (currentMap != null && currentMap.Objects != null)
            {
                for (int i = 0; i < currentMap.Objects.Count; i++)
                {
                    MapObjectData mapObject = currentMap.Objects[i];
                    if (mapObject != null && mapObject.ObjectId > maxObjectId)
                    {
                        maxObjectId = mapObject.ObjectId;
                    }
                }
            }

            return maxObjectId + 1;
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

        private void ValidateMapObject(MapObjectData mapObject, List<string> errors)
        {
            if (mapObject == null || errors == null)
            {
                return;
            }

            if (mapObject.ObjectType == MapObjectType.Resource)
            {
                WorldResourceEditorItem resourceItem = GetWorldResourceItem(mapObject.ConfigId);
                if (resourceItem == null)
                {
                    errors.Add($"Resource object missing world_resource config. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
                    return;
                }

                ValidateWorldResourceObject(mapObject, resourceItem, errors);

                if (GetWorldResourcePrefab(resourceItem) == null)
                {
                    errors.Add($"Resource object missing prefab. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}, Prefab: {resourceItem.PrefabLocation}");
                }
            }
            else if (mapObject.ObjectType == MapObjectType.Decoration && GetDecorationPrefab(mapObject) == null)
            {
                errors.Add($"Decoration object missing prefab. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
            }
        }

        private static void ValidateWorldResourceObject(MapObjectData mapObject, WorldResourceEditorItem resourceItem, List<string> errors)
        {
            if (mapObject == null || resourceItem == null || errors == null)
            {
                return;
            }

            switch ((WorldResourceInteractionType)resourceItem.InteractionType)
            {
                case WorldResourceInteractionType.Pickup:
                    if (resourceItem.PickupRewardGroupId <= 0)
                    {
                        errors.Add($"Pickup resource missing reward group. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
                    }
                    break;

                case WorldResourceInteractionType.Gather:
                    if (resourceItem.GatherConfigId <= 0)
                    {
                        errors.Add($"Gather resource missing gather config id. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
                    }
                    break;

                case WorldResourceInteractionType.MineTarget:
                    if (resourceItem.MineBuildingId <= 0)
                    {
                        errors.Add($"Mine target resource missing mine building id. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
                    }
                    break;

                default:
                    errors.Add($"Resource object missing interaction type. ObjectId: {mapObject.ObjectId}, Config: {mapObject.ConfigId}");
                    break;
            }
        }
    }
}

#endif

