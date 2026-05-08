#if UNITY_EDITOR

using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UIPrefabGenerator
{
    private const string MainMenuPrefabPath = "Assets/Data/UI/Pages/MainMenuPage.prefab";

    [MenuItem("Tools/Cube/UI/Create Main Menu Page Prefab")]
    public static void CreateMainMenuPagePrefab()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/UI/Pages");

        GameObject root = new GameObject("MainMenuPage", typeof(RectTransform), typeof(CanvasRenderer), typeof(MainMenuPage));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);

        GameObject titleObject = CreateText("Title", root.transform, "Cube TD", 64, TextAnchor.MiddleCenter);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 140f);
        titleRect.sizeDelta = new Vector2(600f, 100f);

        GameObject buttonObject = CreateButton("EnterMapButton", root.transform, "½øÈëµØÍ¼");
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, 0f);
        buttonRect.sizeDelta = new Vector2(260f, 72f);

        MainMenuPage page = root.GetComponent<MainMenuPage>();
        SerializedObject serializedObject = new SerializedObject(page);
        serializedObject.FindProperty("enterMapButton").objectReferenceValue = buttonObject.GetComponent<Button>();
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"Create MainMenuPage prefab success: {MainMenuPrefabPath}");
    }

    private static GameObject CreateButton(string name, Transform parent, string text)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.45f, 0.95f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.45f, 0.95f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.55f, 1f, 1f);
        colors.pressedColor = new Color(0.1f, 0.3f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject textObject = CreateText("Text", buttonObject.transform, text, 32, TextAnchor.MiddleCenter);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        StretchFull(textRect);

        return buttonObject;
    }

    private static GameObject CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text textComponent = textObject.GetComponent<Text>();
        textComponent.text = text;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.raycastTarget = false;

        return textObject;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath);
        string name = System.IO.Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent))
        {
            parent = parent.Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

#endif