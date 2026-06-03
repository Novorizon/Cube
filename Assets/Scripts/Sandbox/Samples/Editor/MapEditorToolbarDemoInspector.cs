using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapEditorToolbarDemo))]
public class MapEditorToolbarDemoInspector : OdinEditor
{
    private int selectedTab;

    private GUIContent[] tabs;

    protected override void OnEnable()
    {
        base.OnEnable();

        tabs = new GUIContent[]
        {
            CreateIcon("Terrain Icon", "地形"),
            CreateIcon("TreeEditor.AddBranches", "树木"),
            CreateIcon("TreeEditor.Leaves", "草地"),
            CreateIcon("d_SettingsIcon", "设置"),
            CreateIcon("d_Refresh", "工具")
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawToolbar();

        EditorGUILayout.Space(8);

        switch (selectedTab)
        {
            case 0:
                DrawTerrainTab();
                break;

            case 1:
                DrawTreeTab();
                break;

            case 2:
                DrawGrassTab();
                break;

            case 3:
                DrawSettingsTab();
                break;

            case 4:
                DrawToolTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private GUIContent CreateIcon(string iconName, string tooltip)
    {
        GUIContent content = EditorGUIUtility.IconContent(iconName);
        content.tooltip = tooltip;

        if (content.image == null)
        {
            content.text = tooltip;
        }

        return content;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(28), GUILayout.Width(180));

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTerrainTab()
    {
        EditorGUILayout.LabelField("地形编辑", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("brushSize"), new GUIContent("笔刷大小"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"), new GUIContent("地形高度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainType"), new GUIContent("地形类型"));

        EditorGUILayout.Space();

        MapEditorToolbarDemo targetComponent = (MapEditorToolbarDemo)target;

        if (GUILayout.Button("绘制地形"))
        {
            targetComponent.PaintTerrain();
        }
    }

    private void DrawTreeTab()
    {
        EditorGUILayout.LabelField("树木编辑", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("treePrefab"), new GUIContent("树木 Prefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("treeDensity"), new GUIContent("树木密度"));

        EditorGUILayout.Space();

        MapEditorToolbarDemo targetComponent = (MapEditorToolbarDemo)target;

        if (GUILayout.Button("生成树木"))
        {
            targetComponent.GenerateTrees();
        }
    }

    private void DrawGrassTab()
    {
        EditorGUILayout.LabelField("草地编辑", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("grassPrefab"), new GUIContent("草 Prefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("grassDensity"), new GUIContent("草密度"));

        EditorGUILayout.Space();

        MapEditorToolbarDemo targetComponent = (MapEditorToolbarDemo)target;

        if (GUILayout.Button("生成草"))
        {
            targetComponent.GenerateGrass();
        }
    }

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("showGrid"), new GUIContent("显示网格"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSave"), new GUIContent("自动保存"));
    }

    private void DrawToolTab()
    {
        EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);

        MapEditorToolbarDemo targetComponent = (MapEditorToolbarDemo)target;

        if (GUILayout.Button("重新生成地图"))
        {
            targetComponent.RegenerateMap();
        }

        if (GUILayout.Button("打印当前配置"))
        {
            Debug.Log($"BrushSize: {targetComponent.brushSize}");
            Debug.Log($"Height: {targetComponent.height}");
            Debug.Log($"TerrainType: {targetComponent.terrainType}");
            Debug.Log($"ShowGrid: {targetComponent.showGrid}");
            Debug.Log($"AutoSave: {targetComponent.autoSave}");
        }
    }
}