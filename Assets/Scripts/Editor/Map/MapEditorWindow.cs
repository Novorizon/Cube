#if UNITY_EDITOR
using System;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Odin 地图编辑窗口。
/// 
/// 功能：
/// - 新建 3D 体素地图
/// - 从场景读取
/// - 从 JSON 打开
/// - 保存
/// - 另存为
/// - 重建场景
/// </summary>
public sealed class MapEditorWindow : OdinEditorWindow
{
    [MenuItem("Tools/TD/Map Editor")]
    private static void Open()
    {
        GetWindow<MapEditorWindow>().Show();
    }

    [Title("Map Editor")]
    [InfoBox("JSON 是地图真源。3D 体素地图中，每个 (x,y,z) 都是一个真实 cube。")]
    [SerializeField]
    private MapAuthoringRoot authoringRoot;

    [SerializeField, LabelText("当前文件")]
    private string currentFilePath = "Assets/Data/Maps/NewMap.json";

    [Title("新建地图参数")]
    [SerializeField, LabelText("宽度 X")]
    private int newMapWidth = 8;

    [SerializeField, LabelText("高度 Y")]
    private int newMapHeight = 1;

    [SerializeField, LabelText("深度 Z")]
    private int newMapDepth = 8;

    [SerializeField, LabelText("默认地块类型")]
    private TileType defaultTileType = TileType.Grass;

    [Title("当前地图数据")]
    [InlineProperty, HideLabel]
    [SerializeField]
    private MapJsonData currentMap = new MapJsonData();

    [Button("新建地图", ButtonSizes.Large)]
    private void NewMap()
    {
        currentMap = new MapJsonData
        {
            version = 1,
            mapId = "new_map",
            mapName = "New Map",
            width = Mathf.Max(1, newMapWidth),
            height = Mathf.Max(1, newMapHeight),
            depth = Mathf.Max(1, newMapDepth)
        };

        if (authoringRoot == null)
        {
            ShowMessage("缺少 MapAuthoringRoot");
            return;
        }

        authoringRoot.CreateNewMap(
            currentMap.width,
            currentMap.height,
            currentMap.depth,
            defaultTileType);

        EditorUtility.SetDirty(authoringRoot);

        ImportFromScene();
        ShowMessage("已新建地图");
    }

    [Button("从场景读取", ButtonSizes.Large)]
    private void ImportFromScene()
    {
        if (authoringRoot == null)
        {
            ShowMessage("缺少 MapAuthoringRoot");
            return;
        }

        int width = Mathf.Max(1, currentMap.width);
        int height = Mathf.Max(1, currentMap.height);
        int depth = Mathf.Max(1, currentMap.depth);

        MapJsonData data = authoringRoot.ExportToJsonData(width, height, depth);

        data.version = currentMap.version <= 0 ? 1 : currentMap.version;
        data.mapId = string.IsNullOrWhiteSpace(currentMap.mapId) ? "new_map" : currentMap.mapId;
        data.mapName = string.IsNullOrWhiteSpace(currentMap.mapName) ? "New Map" : currentMap.mapName;

        currentMap = data;
        ShowMessage("已从场景读取地图");
    }

    [Button("重建场景", ButtonSizes.Large)]
    private void RebuildScene()
    {
        if (authoringRoot == null)
        {
            ShowMessage("缺少 MapAuthoringRoot");
            return;
        }

        authoringRoot.RebuildFromJsonData(currentMap);
        EditorUtility.SetDirty(authoringRoot);
        ShowMessage("已重建场景");
    }

    [Button("打开 JSON", ButtonSizes.Large)]
    private void OpenJson()
    {
        string path = EditorUtility.OpenFilePanel("Open Map Json", "Assets/Data/Maps", "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        currentFilePath = ToProjectRelativePath(path);
        currentMap = MapJsonIO.LoadFromFile(path);

        if (authoringRoot != null)
        {
            authoringRoot.RebuildFromJsonData(currentMap);
            EditorUtility.SetDirty(authoringRoot);
        }

        ShowMessage("已打开 JSON");
    }

    [Button("保存", ButtonSizes.Large)]
    private void Save()
    {
        ImportFromScene();

        if (!MapEditorValidator.ValidateForSave(currentMap, currentFilePath, out string error))
        {
            EditorUtility.DisplayDialog("保存失败", error, "确定");
            return;
        }

        string absolutePath = ToAbsolutePath(currentFilePath);
        MapJsonIO.SaveToFile(absolutePath, currentMap, true);
        AssetDatabase.Refresh();

        ShowMessage("已保存");
    }

    [Button("另存为", ButtonSizes.Large)]
    private void SaveAs()
    {
        ImportFromScene();

        string path = EditorUtility.SaveFilePanel(
            "Save Map Json",
            "Assets/Data/Maps",
            string.IsNullOrWhiteSpace(currentMap.mapId) ? "new_map" : currentMap.mapId,
            "json");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        currentFilePath = ToProjectRelativePath(path);

        if (!MapEditorValidator.ValidateForSave(currentMap, currentFilePath, out string error))
        {
            EditorUtility.DisplayDialog("保存失败", error, "确定");
            return;
        }

        MapJsonIO.SaveToFile(path, currentMap, true);
        AssetDatabase.Refresh();

        ShowMessage("已另存为");
    }

    [ShowInInspector, TableList(AlwaysExpanded = true, IsReadOnly = true)]
    private TileJsonData[] TilePreview
    {
        get
        {
            return currentMap?.tiles?.ToArray() ?? Array.Empty<TileJsonData>();
        }
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");
        string full = Path.GetFullPath(absolutePath).Replace("\\", "/");

        if (full.StartsWith(projectPath))
        {
            return full.Substring(projectPath.Length + 1);
        }

        return absolutePath;
    }

    private static string ToAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectPath, path);
    }

    private void ShowMessage(string message)
    {
        ShowNotification(new GUIContent(message));
    }
}
#endif