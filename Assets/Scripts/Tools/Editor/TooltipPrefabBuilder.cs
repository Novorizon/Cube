#if UNITY_EDITOR
using System.IO;
using TMPro;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class TooltipPrefabBuilder
    {
        private const string PrefabPath = "Assets/Arts/UI/Panels/Common/Tooltip.prefab";

        [MenuItem("Tools/UI/Rebuild Tooltip Prefab")]
        public static void Rebuild()
        {
            string directory = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            GameObject root = null;
            try
            {
                root = CreateRoot();
                TooltipView view = root.GetComponent<TooltipView>();
                CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();

                GameObject header = CreateLayoutObject("Header", root.transform);
                HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
                headerLayout.padding = new RectOffset(0, 0, 0, 0);
                headerLayout.spacing = 12f;
                headerLayout.childAlignment = TextAnchor.UpperLeft;
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = false;
                headerLayout.childForceExpandHeight = false;

                ContentSizeFitter headerFitter = header.AddComponent<ContentSizeFitter>();
                headerFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                headerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                GameObject iconContainer = CreateLayoutObject("IconContainer", header.transform);
                LayoutElement iconLayout = iconContainer.AddComponent<LayoutElement>();
                iconLayout.minWidth = 56f;
                iconLayout.minHeight = 56f;
                iconLayout.preferredWidth = 56f;
                iconLayout.preferredHeight = 56f;
                iconLayout.flexibleWidth = 0f;
                iconLayout.flexibleHeight = 0f;

                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = 5;
                iconObject.transform.SetParent(iconContainer.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image icon = iconObject.GetComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                TMP_Text title = CreateText("Title", header.transform, 26f, FontStyles.Bold, new Color(1f, 0.90f, 0.62f, 1f));
                LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
                titleLayout.flexibleWidth = 1f;

                TMP_Text description = CreateText("Description", root.transform, 20f, FontStyles.Normal, new Color(0.96f, 0.92f, 0.82f, 1f));
                TMP_Text values = CreateText("Values", root.transform, 19f, FontStyles.Normal, new Color(0.94f, 0.88f, 0.72f, 1f));
                TMP_Text footer = CreateText("Footer", root.transform, 17f, FontStyles.Italic, new Color(0.74f, 0.70f, 0.62f, 1f));

                SerializedObject serializedView = new SerializedObject(view);
                serializedView.FindProperty("root").objectReferenceValue = root.GetComponent<RectTransform>();
                serializedView.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
                serializedView.FindProperty("iconContainer").objectReferenceValue = iconContainer;
                serializedView.FindProperty("icon").objectReferenceValue = icon;
                serializedView.FindProperty("titleText").objectReferenceValue = title;
                serializedView.FindProperty("descriptionText").objectReferenceValue = description;
                serializedView.FindProperty("valuesText").objectReferenceValue = values;
                serializedView.FindProperty("footerText").objectReferenceValue = footer;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Tooltip] Rebuilt prefab: {PrefabPath}");
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject CreateRoot()
        {
            GameObject root = new GameObject(
                "Tooltip",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(TooltipView));
            root.layer = 5;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(380f, 0f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.10f, 0.075f, 0.045f, 0.97f);
            background.raycastTarget = false;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Shadow shadow = root.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(4f, -4f);
            shadow.useGraphicAlpha = true;

            return root;
        }

        private static GameObject CreateLayoutObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            float fontSize,
            FontStyles fontStyle,
            Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = name;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
#endif
