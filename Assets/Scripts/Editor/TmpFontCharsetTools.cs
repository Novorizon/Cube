using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class TmpFontCharsetTools
    {
        private const string FontAssetPath = "Assets/Arts/Font/NotoSansSC-Regular SDF.asset";
        private const string SourceFontPath = "Assets/Arts/Font/SourceFont/NotoSansSC-Regular.ttf";
        private const string CharsetPath = "Assets/Arts/Font/GameCommonCharset.txt";

        [MenuItem("Cube/Font/Apply Game Common Charset To NotoSansSC")]
        public static void ApplyGameCommonCharset()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Debug.LogError($"Font asset not found: {FontAssetPath}");
                return;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"Source font not found: {SourceFontPath}");
                return;
            }

            if (!File.Exists(CharsetPath))
            {
                Debug.LogError($"Charset file not found: {CharsetPath}");
                return;
            }

            string characters = File.ReadAllText(CharsetPath)
                .Where(c => !char.IsControl(c))
                .Distinct()
                .Aggregate(string.Empty, (current, c) => current + c);

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            serializedFontAsset.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            bool success = fontAsset.TryAddCharacters(characters, out string missingCharacters);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            if (success)
            {
                Debug.Log($"Applied charset to {FontAssetPath}. Character count: {characters.Length}");
                return;
            }

            Debug.LogWarning($"Applied charset with missing characters. Missing count: {missingCharacters.Length}, missing: {missingCharacters}");
        }
    }
}
