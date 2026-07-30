#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class ManagementMiniMapPrefabBuilder
    {
        private const string PrefabPath = "Assets/Arts/UI/Panels/World/MiniMapPanel.prefab";
        private const string PlayerIconPath = "Assets/Arts/UI/Icons/System/double_up_arrow_transparent.png";
        private const string NavigationIconPath = "Assets/Arts/UI/Icons/System/ui_td_system_check_icon.png";
        private const string DecorationIconPath = "Assets/Arts/UI/Icons/Structure/flower.png";
        private const string ResourceIconPath = "Assets/Arts/UI/Icons/Tool/Pickaxe.png";
        private const string BuildingIconPath = "Assets/Arts/UI/Icons/Structure/House.png";
        private const string InteractableIconPath =
            "Assets/Arts/UI/Icons/Skills/ui_td_skill_fireball_icon.png";

        [MenuItem("Tools/UI/Rebuild Management Mini Map Prefab")]
        public static void Rebuild()
        {
            GameObject root = null;
            try
            {
                root = CreateUiObject("MiniMapPanel", null, typeof(Image), typeof(ManagementMiniMapPanel));
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(1f, 0.5f);
                rootRect.anchorMax = new Vector2(1f, 0.5f);
                rootRect.pivot = new Vector2(1f, 0.5f);
                rootRect.anchoredPosition = new Vector2(-24f, 170f);
                rootRect.sizeDelta = new Vector2(288f, 288f);

                Image background = root.GetComponent<Image>();
                background.color = new Color(0.045f, 0.075f, 0.095f, 0.94f);
                background.raycastTarget = true;

                GameObject viewportObject = CreateUiObject("MapViewport", rootRect, typeof(Image), typeof(RectMask2D));
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                SetAnchoredRect(viewport, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(272f, 272f));
                Image viewportBackground = viewportObject.GetComponent<Image>();
                viewportBackground.color = new Color(0.015f, 0.028f, 0.038f, 1f);
                viewportBackground.raycastTarget = false;

                GameObject baseMapObject = CreateUiObject("BaseMap", viewport, typeof(RawImage));
                RectTransform baseMapRect = baseMapObject.GetComponent<RectTransform>();
                Stretch(baseMapRect);
                RawImage baseMap = baseMapObject.GetComponent<RawImage>();
                baseMap.color = Color.white;
                baseMap.raycastTarget = false;

                GameObject iconRootObject = CreateUiObject("IconRoot", viewport);
                RectTransform iconRoot = iconRootObject.GetComponent<RectTransform>();
                Stretch(iconRoot);

                Image iconTemplate = CreateMarkerImage(
                    "IconTemplate",
                    iconRoot,
                    new Vector2(12f, 12f),
                    Color.white,
                    true);
                iconTemplate.gameObject.SetActive(false);

                RectTransform cameraViewport = CreateCameraViewport(iconRoot);

                Image navigationMarker = CreateMarkerImage(
                    "NavigationMarker",
                    iconRoot,
                    new Vector2(11f, 11f),
                    new Color(1f, 0.66f, 0.16f, 0.95f),
                    false);
                navigationMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                navigationMarker.gameObject.SetActive(false);

                Image playerMarker = CreateMarkerImage(
                    "PlayerMarker",
                    iconRoot,
                    new Vector2(12f, 12f),
                    new Color(0.20f, 0.92f, 1f, 1f),
                    false);
                Image forward = CreateMarkerImage(
                    "Forward",
                    playerMarker.rectTransform,
                    new Vector2(3f, 9f),
                    new Color(0.86f, 1f, 1f, 1f),
                    false);
                forward.rectTransform.anchoredPosition = new Vector2(0f, 7f);
                playerMarker.gameObject.SetActive(false);

                SerializedObject serializedPanel = new SerializedObject(root.GetComponent<ManagementMiniMapPanel>());
                serializedPanel.FindProperty("mapViewport").objectReferenceValue = viewport;
                serializedPanel.FindProperty("baseMap").objectReferenceValue = baseMap;
                serializedPanel.FindProperty("iconRoot").objectReferenceValue = iconRoot;
                serializedPanel.FindProperty("iconTemplate").objectReferenceValue = iconTemplate;
                serializedPanel.FindProperty("playerMarker").objectReferenceValue = playerMarker;
                serializedPanel.FindProperty("navigationMarker").objectReferenceValue = navigationMarker;
                serializedPanel.FindProperty("cameraViewport").objectReferenceValue = cameraViewport;
                serializedPanel.FindProperty("playerDirectionIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(PlayerIconPath);
                serializedPanel.FindProperty("navigationIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(NavigationIconPath);
                serializedPanel.FindProperty("defaultDecorationIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(DecorationIconPath);
                serializedPanel.FindProperty("defaultResourceIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(ResourceIconPath);
                serializedPanel.FindProperty("defaultBuildingIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(BuildingIconPath);
                serializedPanel.FindProperty("defaultInteractableIcon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(InteractableIconPath);
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Management mini map prefab rebuilt: {PrefabPath}");
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static RectTransform CreateCameraViewport(RectTransform parent)
        {
            GameObject viewportObject = CreateUiObject("CameraViewport", parent, typeof(Image), typeof(Outline));
            RectTransform rect = viewportObject.GetComponent<RectTransform>();
            SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 30f));
            Image image = viewportObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.84f, 1f, 0.035f);
            image.raycastTarget = false;
            Outline outline = viewportObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.86f, 1f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            viewportObject.SetActive(false);
            return rect;
        }

        private static Image CreateMarkerImage(
            string name,
            RectTransform parent,
            Vector2 size,
            Color color,
            bool addOutline)
        {
            GameObject marker = CreateUiObject(
                name,
                parent,
                addOutline ? new[] { typeof(Image), typeof(Outline) } : new[] { typeof(Image) });
            RectTransform rect = marker.GetComponent<RectTransform>();
            SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image image = marker.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (addOutline)
            {
                Outline outline = marker.GetComponent<Outline>();
                outline.enabled = false;
            }
            return image;
        }

        private static GameObject CreateUiObject(string name, RectTransform parent, params System.Type[] components)
        {
            System.Type[] allComponents = new System.Type[components.Length + 2];
            allComponents[0] = typeof(RectTransform);
            allComponents[1] = typeof(CanvasRenderer);
            for (int i = 0; i < components.Length; i++)
            {
                allComponents[i + 2] = components[i];
            }

            GameObject result = new GameObject(name, allComponents);
            result.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = result.GetComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }
            return result;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}

#endif
