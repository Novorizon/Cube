using Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class WorldMainPanelPrefabTool
    {
        private const string SaveCurrentMenuPath = "Tools/World/UI/Save Current WorldMainPanel Prefab";
        private const string RebuildPrefabMenuPath = "Tools/World/UI/Rebuild WorldMainPanel Prefab Layout";

        [MenuItem(SaveCurrentMenuPath)]
        public static void SaveCurrentWorldMainPanelPrefab()
        {
            WorldMainPanel panel = WorldMainPanel.Instance;
            if (panel == null)
            {
                panel = Object.FindFirstObjectByType<WorldMainPanel>(FindObjectsInactive.Include);
            }

            if (panel == null)
            {
                Debug.LogWarning("[World UI] WorldMainPanel not found in the current scene.");
                return;
            }

            EnsureFolder("Assets/Arts");
            EnsureFolder("Assets/Arts/UI");
            EnsureFolder("Assets/Arts/UI/Management");
            EnsureFolder("Assets/Arts/UI/Management/Prefabs");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, WorldMainPanel.PrefabPath, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError($"[World UI] Failed to save prefab: {WorldMainPanel.PrefabPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[World UI] Saved prefab: {WorldMainPanel.PrefabPath}");
        }

        [MenuItem(SaveCurrentMenuPath, true)]
        public static bool CanSaveCurrentWorldMainPanelPrefab()
        {
            return WorldMainPanel.Instance != null ||
                   Object.FindFirstObjectByType<WorldMainPanel>(FindObjectsInactive.Include) != null;
        }

        [MenuItem(RebuildPrefabMenuPath)]
        public static void RebuildWorldMainPanelPrefabLayout()
        {
            EnsureFolder("Assets/Arts");
            EnsureFolder("Assets/Arts/UI");
            EnsureFolder("Assets/Arts/UI/Management");
            EnsureFolder("Assets/Arts/UI/Management/Prefabs");

            GameObject root = new GameObject("WorldMainPanel");

            RectTransform rectTransform = root.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            CreateDefaultLayout(rectTransform);
            root.AddComponent<WorldMainPanel>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, WorldMainPanel.PrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || savedPrefab == null)
            {
                Debug.LogError($"[World UI] Failed to rebuild prefab: {WorldMainPanel.PrefabPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log($"[World UI] Rebuilt prefab layout: {WorldMainPanel.PrefabPath}");
        }

        private static void CreateDefaultLayout(Transform root)
        {
            GameObject hud = CreatePanelObject("Hud", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(620f, 220f));
            VerticalLayoutGroup hudLayout = hud.AddComponent<VerticalLayoutGroup>();
            hudLayout.padding = new RectOffset(18, 18, 14, 14);
            hudLayout.spacing = 8f;
            hudLayout.childControlWidth = true;
            hudLayout.childControlHeight = false;
            hudLayout.childForceExpandWidth = true;
            hudLayout.childForceExpandHeight = false;

            AddLayout(CreateText("Title", hud.transform, "World", 24, TextAnchor.MiddleLeft, Color.white).gameObject, 34f);
            AddLayout(CreateText("Status", hud.transform, "Map 0   Base Not Built\nLMB select/build/farm   RMB move   WASD camera   Wheel height", 18, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.92f)).gameObject, 50f);

            Text resources = CreateText("Resources", hud.transform, "Gold 0   Wood 0   Stone 0   Food 0\nCopper 0   Iron 0\nWheat 0   Tomato 0   Herb 0   Flower 0", 18, TextAnchor.UpperLeft, Color.white);
            resources.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayout(resources.gameObject, 92f);

            GameObject buildPanel = CreatePanelObject("BuildPanel", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(440f, -40f));
            VerticalLayoutGroup buildLayout = buildPanel.AddComponent<VerticalLayoutGroup>();
            buildLayout.padding = new RectOffset(14, 14, 14, 14);
            buildLayout.spacing = 10f;
            buildLayout.childControlWidth = true;
            buildLayout.childControlHeight = false;
            buildLayout.childForceExpandWidth = true;
            buildLayout.childForceExpandHeight = false;

            AddLayout(CreateText("Title", buildPanel.transform, "Buildings", 24, TextAnchor.MiddleLeft, Color.white).gameObject, 34f);
            AddLayout(CreateText("Selected", buildPanel.transform, "Selected: None", 18, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.92f)).gameObject, 28f);
            CreateButtonShell(buildPanel.transform, "CancelBuild", "Cancel Build", new Color(0.24f, 0.24f, 0.25f, 0.94f));
            CreateScrollView(buildPanel.transform);
        }

        private static GameObject CreatePanelObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.055f, 0.065f, 0.075f, 0.88f);

            return panel;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return text;
        }

        private static GameObject CreateButtonShell(Transform parent, string name, string label, Color color)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;

            buttonObject.AddComponent<Button>();
            AddLayout(buttonObject, 48f);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return buttonObject;
        }

        private static void CreateScrollView(Transform parent)
        {
            GameObject scrollObject = new GameObject("ScrollView");
            scrollObject.transform.SetParent(parent, false);

            RectTransform scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            Image scrollBackground = scrollObject.AddComponent<Image>();
            scrollBackground.color = new Color(0.035f, 0.043f, 0.052f, 0.74f);

            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 220f;
            scrollLayout.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 26f;

            GameObject viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(6f, 6f);
            viewportRect.offsetMax = new Vector2(-6f, -6f);

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            Mask mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content");
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
        }

        private static void AddLayout(GameObject gameObject, float preferredHeight)
        {
            LayoutElement layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            int slashIndex = assetPath.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return;
            }

            string parent = assetPath.Substring(0, slashIndex);
            string name = assetPath.Substring(slashIndex + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
