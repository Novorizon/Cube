using System;
using Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class ProductionPanelPrefabMigrator
    {
        private const string PrefabPath =
            "Assets/Arts/UI/Panels/Production/ProductionPanel.prefab";

        private const string FontPath =
            "Assets/Arts/Font/NotoSansSC-Regular SDF.asset";

        [MenuItem("Tools/Cube/UI/Convert Production Panel Text To TMP")]
        public static void ConvertToTmp()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"Unable to load prefab: {PrefabPath}");
            }

            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font == null)
                {
                    throw new InvalidOperationException($"Missing TMP font asset: {FontPath}");
                }

                Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
                int convertedCount = 0;
                for (int i = 0; i < legacyTexts.Length; i++)
                {
                    ConvertText(legacyTexts[i], font);
                    convertedCount++;
                }

                RemoveDynamicContentLocalization(root.transform);
                Validate(root);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[{nameof(ProductionPanelPrefabMigrator)}] Converted {convertedCount} legacy Text components to TMP: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConvertText(Text legacyText, TMP_FontAsset font)
        {
            if (legacyText == null)
            {
                return;
            }

            GameObject target = legacyText.gameObject;
            string value = legacyText.text;
            Color color = legacyText.color;
            float fontSize = legacyText.fontSize;
            bool resizeTextForBestFit = legacyText.resizeTextForBestFit;
            float resizeTextMinSize = legacyText.resizeTextMinSize;
            float resizeTextMaxSize = legacyText.resizeTextMaxSize;
            TextAnchor alignment = legacyText.alignment;
            FontStyle fontStyle = legacyText.fontStyle;
            bool supportRichText = legacyText.supportRichText;
            bool raycastTarget = legacyText.raycastTarget;
            bool maskable = legacyText.maskable;
            HorizontalWrapMode horizontalOverflow = legacyText.horizontalOverflow;
            VerticalWrapMode verticalOverflow = legacyText.verticalOverflow;

            WorldLocalizedText localized = target.GetComponent<WorldLocalizedText>();
            bool hadLocalization = localized != null;
            string localizationKey = string.Empty;
            string localizationFallback = string.Empty;
            if (localized != null)
            {
                SerializedObject serialized = new SerializedObject(localized);
                serialized.Update();
                localizationKey = serialized.FindProperty("key").stringValue;
                localizationFallback = serialized.FindProperty("fallback").stringValue;
            }

            UnityEngine.Object.DestroyImmediate(legacyText, true);

            TextMeshProUGUI tmpText = target.AddComponent<TextMeshProUGUI>();
            tmpText.font = font;
            tmpText.text = value;
            tmpText.color = color;
            tmpText.fontSize = fontSize;
            tmpText.enableAutoSizing = resizeTextForBestFit;
            tmpText.fontSizeMin = resizeTextMinSize;
            tmpText.fontSizeMax = resizeTextMaxSize;
            tmpText.alignment = ConvertAlignment(alignment);
            tmpText.fontStyle = ConvertFontStyle(fontStyle);
            tmpText.richText = supportRichText;
            tmpText.raycastTarget = raycastTarget;
            tmpText.maskable = maskable;
            tmpText.enableWordWrapping = horizontalOverflow == HorizontalWrapMode.Wrap;
            tmpText.overflowMode = verticalOverflow == VerticalWrapMode.Overflow
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;

            if (hadLocalization)
            {
                localized = target.GetComponent<WorldLocalizedText>() ??
                            target.AddComponent<WorldLocalizedText>();
                SerializedObject serialized = new SerializedObject(localized);
                serialized.FindProperty("targetText").objectReferenceValue = tmpText;
                serialized.FindProperty("legacyText").objectReferenceValue = null;
                serialized.FindProperty("key").stringValue = localizationKey;
                serialized.FindProperty("fallback").stringValue = localizationFallback;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(localized);
            }

            EditorUtility.SetDirty(tmpText);
        }

        private static void RemoveDynamicContentLocalization(Transform root)
        {
            Transform content = root.Find("Content");
            if (content == null)
            {
                throw new InvalidOperationException("Production panel is missing the Content node.");
            }

            WorldLocalizedText localized = content.GetComponent<WorldLocalizedText>();
            if (localized != null)
            {
                UnityEngine.Object.DestroyImmediate(localized, true);
            }
        }

        private static void Validate(GameObject root)
        {
            Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
            if (legacyTexts.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Production panel still contains {legacyTexts.Length} legacy Text components.");
            }

            Transform content = root.transform.Find("Content");
            if (content == null || content.GetComponent<TMP_Text>() == null)
            {
                throw new InvalidOperationException("Production panel Content is not using TMP.");
            }

            if (content.GetComponent<WorldLocalizedText>() != null)
            {
                throw new InvalidOperationException(
                    "Dynamic production Content must not have a static localization binding.");
            }
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.TopLeft;
            }
        }

        private static FontStyles ConvertFontStyle(FontStyle fontStyle)
        {
            switch (fontStyle)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }
    }
}
