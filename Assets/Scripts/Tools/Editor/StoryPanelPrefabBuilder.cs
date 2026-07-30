#if UNITY_EDITOR

using Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class StoryPanelPrefabBuilder
{
    private const string PrefabPath = StoryPanel.PrefabPath;
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color CardColor = new Color(0.12f, 0.095f, 0.075f, 0.96f);
    private static readonly Color TextColor = new Color(0.96f, 0.91f, 0.8f, 1f);
    private static readonly Color AccentColor = new Color(0.94f, 0.7f, 0.24f, 1f);

    [MenuItem("Tools/Story/Rebuild Story Panel Prefab")]
    public static void Build()
    {
        GameObject root = CreateUiObject("StoryPanel", null);
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            Image backgroundBlocker = root.AddComponent<Image>();
            backgroundBlocker.color = DimColor;
            backgroundBlocker.raycastTarget = true;

            GameObject illustrationRoot = CreateUiObject("IllustrationRoot", root.transform);
            Stretch(illustrationRoot.GetComponent<RectTransform>());
            Image illustrationBackground = illustrationRoot.AddComponent<Image>();
            illustrationBackground.color = Color.black;
            illustrationBackground.raycastTarget = false;

            GameObject viewport = CreateUiObject("Viewport", illustrationRoot.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();

            GameObject illustrationViewObject = CreateUiObject("IllustrationView", viewport.transform);
            RectTransform illustrationViewRect = illustrationViewObject.GetComponent<RectTransform>();
            illustrationViewRect.anchorMin = new Vector2(0.5f, 0.5f);
            illustrationViewRect.anchorMax = new Vector2(0.5f, 0.5f);
            illustrationViewRect.pivot = new Vector2(0.5f, 0.5f);
            illustrationViewRect.sizeDelta = new Vector2(1920f, 1080f);
            RawImage illustrationView = illustrationViewObject.AddComponent<RawImage>();
            illustrationView.color = Color.white;
            illustrationView.raycastTarget = false;
            illustrationView.uvRect = new Rect(0f, 0f, 1f, 1f);
            AspectRatioFitter illustrationAspectFitter = illustrationViewObject.AddComponent<AspectRatioFitter>();
            illustrationAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            illustrationAspectFitter.aspectRatio = 16f / 9f;

            StoryMotionPlayer motionPlayer = illustrationRoot.AddComponent<StoryMotionPlayer>();
            SerializedObject motionSerialized = new SerializedObject(motionPlayer);
            motionSerialized.FindProperty("view").objectReferenceValue = illustrationView;
            motionSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject storyCard = CreateUiObject("StoryCard", root.transform);
            RectTransform cardRect = storyCard.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.anchoredPosition = new Vector2(0f, 54f);
            cardRect.sizeDelta = new Vector2(1120f, 280f);
            Image cardImage = storyCard.AddComponent<Image>();
            cardImage.color = CardColor;
            cardImage.raycastTarget = true;
            Outline cardOutline = storyCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.78f, 0.58f, 0.26f, 0.65f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            TMP_Text titleText = CreateText("Title", storyCard.transform, 32f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetOffsets(titleText.rectTransform, new Vector2(40f, 204f), new Vector2(-180f, -24f));

            TMP_Text bodyText = CreateText("Body", storyCard.transform, 25f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetOffsets(bodyText.rectTransform, new Vector2(40f, 78f), new Vector2(-40f, -82f));

            TMP_Text progressText = CreateText("Progress", storyCard.transform, 19f, FontStyles.Normal, TextAlignmentOptions.Right);
            progressText.color = new Color(0.82f, 0.74f, 0.62f, 1f);
            SetOffsets(progressText.rectTransform, new Vector2(900f, 214f), new Vector2(-40f, -24f));

            Button continueButton = CreateButton("ContinueButton", storyCard.transform, "Continue");
            RectTransform continueRect = continueButton.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(1f, 0f);
            continueRect.anchorMax = new Vector2(1f, 0f);
            continueRect.pivot = new Vector2(1f, 0f);
            continueRect.anchoredPosition = new Vector2(-40f, 24f);
            continueRect.sizeDelta = new Vector2(220f, 58f);

            GuideOverlay guideOverlay = BuildGuideOverlay(root.transform);

            StoryPanel panel = root.AddComponent<StoryPanel>();
            SerializedObject panelSerialized = new SerializedObject(panel);
            SetObject(panelSerialized, "backgroundBlocker", backgroundBlocker);
            SetObject(panelSerialized, "illustrationRoot", illustrationRoot);
            SetObject(panelSerialized, "illustrationView", illustrationView);
            SetObject(panelSerialized, "illustrationAspectFitter", illustrationAspectFitter);
            SetObject(panelSerialized, "motionPlayer", motionPlayer);
            SetObject(panelSerialized, "storyCard", storyCard);
            SetObject(panelSerialized, "titleText", titleText);
            SetObject(panelSerialized, "bodyText", bodyText);
            SetObject(panelSerialized, "progressText", progressText);
            SetObject(panelSerialized, "continueButton", continueButton);
            SetObject(panelSerialized, "guideOverlay", guideOverlay);
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();

            illustrationRoot.SetActive(false);
            guideOverlay.gameObject.SetActive(false);

            string directory = System.IO.Path.GetDirectoryName(PrefabPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt StoryPanel prefab: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    public static void BuildForBatch()
    {
        Build();
    }

    private static GuideOverlay BuildGuideOverlay(Transform parent)
    {
        GameObject rootObject = CreateUiObject("GuideOverlay", parent);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        Stretch(root);

        RectTransform topBlocker = CreateGuideRect("TopBlocker", rootObject.transform, DimColor, true);
        RectTransform bottomBlocker = CreateGuideRect("BottomBlocker", rootObject.transform, DimColor, true);
        RectTransform leftBlocker = CreateGuideRect("LeftBlocker", rootObject.transform, DimColor, true);
        RectTransform rightBlocker = CreateGuideRect("RightBlocker", rootObject.transform, DimColor, true);
        RectTransform centerBlocker = CreateGuideRect("CenterBlocker", rootObject.transform, Color.clear, true);
        RectTransform focusTop = CreateGuideRect("FocusTop", rootObject.transform, AccentColor, false);
        RectTransform focusBottom = CreateGuideRect("FocusBottom", rootObject.transform, AccentColor, false);
        RectTransform focusLeft = CreateGuideRect("FocusLeft", rootObject.transform, AccentColor, false);
        RectTransform focusRight = CreateGuideRect("FocusRight", rootObject.transform, AccentColor, false);

        GameObject hintPanelObject = CreateUiObject("HintPanel", rootObject.transform);
        RectTransform hintPanel = hintPanelObject.GetComponent<RectTransform>();
        hintPanel.anchorMin = new Vector2(0.5f, 0.5f);
        hintPanel.anchorMax = new Vector2(0.5f, 0.5f);
        hintPanel.pivot = new Vector2(0.5f, 0.5f);
        hintPanel.sizeDelta = new Vector2(600f, 100f);
        Image hintBackground = hintPanelObject.AddComponent<Image>();
        hintBackground.color = CardColor;
        hintBackground.raycastTarget = false;
        Outline hintOutline = hintPanelObject.AddComponent<Outline>();
        hintOutline.effectColor = AccentColor;
        hintOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text hintText = CreateText("HintText", hintPanelObject.transform, 24f, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(hintText.rectTransform, new Vector2(24f, 14f), new Vector2(-24f, -14f));

        GuideOverlay overlay = rootObject.AddComponent<GuideOverlay>();
        SerializedObject serialized = new SerializedObject(overlay);
        SetObject(serialized, "root", root);
        SetObject(serialized, "topBlocker", topBlocker);
        SetObject(serialized, "bottomBlocker", bottomBlocker);
        SetObject(serialized, "leftBlocker", leftBlocker);
        SetObject(serialized, "rightBlocker", rightBlocker);
        SetObject(serialized, "centerBlocker", centerBlocker);
        SetObject(serialized, "focusTop", focusTop);
        SetObject(serialized, "focusBottom", focusBottom);
        SetObject(serialized, "focusLeft", focusLeft);
        SetObject(serialized, "focusRight", focusRight);
        SetObject(serialized, "hintPanel", hintPanel);
        SetObject(serialized, "hintText", hintText);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return overlay;
    }

    private static RectTransform CreateGuideRect(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = TextColor;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string labelText)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.72f, 0.47f, 0.15f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText("Text", go.transform, 23f, FontStyles.Bold, TextAlignmentOptions.Center);
        label.text = labelText;
        Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new System.InvalidOperationException($"Missing serialized property: {propertyName}");
        }

        property.objectReferenceValue = value;
    }

    private static void SetOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

#endif
