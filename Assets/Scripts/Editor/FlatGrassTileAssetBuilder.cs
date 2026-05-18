#if UNITY_EDITOR

using Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class BattleToastPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Arts/UI/Toasts";
        private const string PrefabPath = PrefabFolder + "/BattleToast.prefab";

        [MenuItem("Tools/Cube/UI/Create BattleToast Prefab From Selected Sprite")]
        public static void CreateBattleToastPrefabFromSelectedSprite()
        {
            EnsureFolder("Assets/Arts");
            EnsureFolder("Assets/Arts/UI");
            EnsureFolder(PrefabFolder);

            Sprite selectedSprite = Selection.activeObject as Sprite;

            if (selectedSprite == null)
            {
                Texture2D texture = Selection.activeObject as Texture2D;

                if (texture != null)
                {
                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    selectedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                }
            }

            GameObject root = CreatePrefabRoot(selectedSprite);

            GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (oldPrefab != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;

            Debug.Log($"Create BattleToast prefab success: {PrefabPath}");
        }

        private static GameObject CreatePrefabRoot(Sprite selectedSprite)
        {
            GameObject root = new GameObject("BattleToast", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            BattleToast battleToast = root.AddComponent<BattleToast>();

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(Image));
            content.transform.SetParent(root.transform, false);

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(360f, 90f);
            contentRect.localScale = Vector3.one;

            Image contentImage = content.GetComponent<Image>();
            contentImage.raycastTarget = false;
            contentImage.type = Image.Type.Simple;
            contentImage.preserveAspect = false;

            if (selectedSprite != null)
            {
                contentImage.sprite = selectedSprite;
                contentImage.color = Color.white;
            }
            else
            {
                contentImage.color = new Color(0f, 0f, 0f, 0.45f);
            }

            GameObject textObject = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(content.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(36f, 14f);
            textRect.offsetMax = new Vector2(-36f, -14f);
            textRect.localScale = Vector3.one;

            TextMeshProUGUI messageText = textObject.GetComponent<TextMeshProUGUI>();
            messageText.text = "½ð±Ò²»×ã";
            messageText.fontSize = 26f;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.color = new Color(0.33f, 0.28f, 0.22f, 1f);
            messageText.raycastTarget = false;
            messageText.enableWordWrapping = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                messageText.font = TMP_Settings.defaultFontAsset;
            }

            messageText.outlineWidth = 0.18f;
            messageText.outlineColor = new Color(1f, 1f, 1f, 0.7f);

            BindReferences(battleToast, messageText, canvasGroup, contentRect);

            return root;
        }

        private static void BindReferences(BattleToast battleToast, TMP_Text messageText, CanvasGroup canvasGroup, RectTransform contentRect)
        {
            SerializedObject serializedObject = new SerializedObject(battleToast);

            serializedObject.FindProperty("messageText").objectReferenceValue = messageText;
            serializedObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("contentRoot").objectReferenceValue = contentRect;

            serializedObject.FindProperty("duration").floatValue = 1.2f;
            serializedObject.FindProperty("startY").floatValue = 0f;
            serializedObject.FindProperty("floatDistance").floatValue = 0f;
            serializedObject.FindProperty("fadeInTime").floatValue = 0.10f;
            serializedObject.FindProperty("fadeOutTime").floatValue = 0.20f;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battleToast);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int lastSlashIndex = folderPath.LastIndexOf('/');

            if (lastSlashIndex <= 0)
            {
                return;
            }

            string parentFolder = folderPath.Substring(0, lastSlashIndex);
            string folderName = folderPath.Substring(lastSlashIndex + 1);

            EnsureFolder(parentFolder);
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }
}

#endif