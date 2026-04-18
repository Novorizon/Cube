#if UNITY_EDITOR
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public sealed class MapEditorWindow : OdinEditorWindow
{
    [MenuItem("Tools/TD/Map Editor")]
    private static void Open()
    {
        GetWindow<MapEditorWindow>().Show();
    }

    [Title("Map Editor")]
    [InfoBox("JSON 是地图真源。打开、保存、另存为都操作 json 文件。")]
    [SerializeField]
    private MapAuthoringRoot authoringRoot;

    [SerializeField, LabelText("当前文件")]
    private string currentFilePath = "Assets/Data/Maps/NewMap.json";

    [InlineProperty, HideLabel]
    [SerializeField]
    private MapJsonData currentMap = new MapJsonData();

    [Button("新建地图", ButtonSizes.Large)]
    private void NewMap()
    {
        currentMap = new MapJsonData();
        currentMap.mapId = "new_map";
        currentMap.mapName = "New Map";

        if (authoringRoot != null)
        {
            authoringRoot.RebuildFromJsonData(currentMap);
            EditorUtility.SetDirty(authoringRoot);
        }
    }

    [Button("从场景读取", ButtonSizes.Large)]
    private void ImportFromScene()
    {
        if (authoringRoot == null)
        {
            ShowMessage("缺少 MapAuthoringRoot");
            return;
        }

        MapJsonData data = authoringRoot.ExportToJsonData();
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
            return currentMap?.tiles?.ToArray() ?? System.Array.Empty<TileJsonData>();
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
