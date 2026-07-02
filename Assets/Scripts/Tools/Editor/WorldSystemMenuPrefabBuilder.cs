using Game;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class WorldSystemMenuPrefabBuilder
    {
        private const string PanelsFolder = "Assets/Arts/UI/Panels";
        private const string MenuPanelPath = PanelsFolder + "/MenuPanel.prefab";
        private static readonly Dictionary<Transform, float> NextElementY = new Dictionary<Transform, float>();

        [MenuItem("Tools/Game UI/Rebuild System Menu Panels")]
        public static void Rebuild()
        {
            EnsureMenuPanelBindings();
            CreateSoundPanel();
            CreateLanguagePanel();
            CreateSavePanel();
            CreateGmPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Game UI/Rebuild GM Panel Only")]
        public static void RebuildGmPanelOnly()
        {
            CreateGmPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureMenuPanelBindings()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MenuPanelPath);
            try
            {
                WorldMenuPanel panel = root.GetComponent<WorldMenuPanel>();
                if (panel == null)
                {
                    panel = root.AddComponent<WorldMenuPanel>();
                }

                Transform content = root.transform.Find("Content/Scroll View/Viewport/Content");
                if (content == null)
                {
                    Debug.LogError($"MenuPanel button content not found: {MenuPanelPath}");
                    return;
                }

                Button soundButton = EnsureButton(content, "Sound", "Sound");
                Button languageButton = EnsureButton(content, "Language", "Language");
                Button followRoleButton = EnsureButton(content, "FollowRole", "FollowRole");
                Button saveButton = EnsureButton(content, "Save", "Save");
                Button gmButton = EnsureButton(content, "GM", "GM");
                Button closeButton = EnsureButton(content, "Close", "Close");

                SerializedObject serialized = new SerializedObject(panel);
                SetObject(serialized, "soundButton", soundButton);
                SetObject(serialized, "languageButton", languageButton);
                SetObject(serialized, "saveButton", saveButton);
                SetObject(serialized, "gmButton", gmButton);
                SetObject(serialized, "closeButton", closeButton);
                SetObject(serialized, "cameraModeButton", followRoleButton);
                SetObject(serialized, "cameraModeButtonText", followRoleButton.GetComponentInChildren<TMP_Text>(true));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, MenuPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateSoundPanel()
        {
            GameObject root = CreatePanelRoot("SoundPanel", typeof(WorldSoundPanel), "Sound");
            Transform content = root.transform.Find("Content");
            TMP_Text volumeText = CreateText(content, "VolumeText", "Volume: 100%", 24, 42f);
            Button decreaseButton = CreateButton(content, "Decrease", "Volume -");
            Button increaseButton = CreateButton(content, "Increase", "Volume +");
            Button muteButton = CreateButton(content, "Mute", "Mute");
            TMP_Text muteButtonText = muteButton.GetComponentInChildren<TMP_Text>(true);
            Button closeButton = CreateButton(content, "Close", "Close");

            SerializedObject serialized = new SerializedObject(root.GetComponent<WorldSoundPanel>());
            SetObject(serialized, "closeButton", closeButton);
            SetObject(serialized, "decreaseButton", decreaseButton);
            SetObject(serialized, "increaseButton", increaseButton);
            SetObject(serialized, "muteButton", muteButton);
            SetObject(serialized, "volumeText", volumeText);
            SetObject(serialized, "muteButtonText", muteButtonText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, WorldSoundPanel.PrefabPath);
        }

        private static void CreateLanguagePanel()
        {
            GameObject root = CreatePanelRoot("LanguagePanel", typeof(WorldLanguagePanel), "Language");
            Transform content = root.transform.Find("Content");
            TMP_Text languageText = CreateText(content, "LanguageText", "Current: Chinese", 24, 42f);
            Button chineseButton = CreateButton(content, "Chinese", "Chinese");
            Button englishButton = CreateButton(content, "English", "English");
            Button closeButton = CreateButton(content, "Close", "Close");

            SerializedObject serialized = new SerializedObject(root.GetComponent<WorldLanguagePanel>());
            SetObject(serialized, "closeButton", closeButton);
            SetObject(serialized, "englishButton", englishButton);
            SetObject(serialized, "chineseButton", chineseButton);
            SetObject(serialized, "languageText", languageText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, WorldLanguagePanel.PrefabPath);
        }

        private static void CreateSavePanel()
        {
            GameObject root = CreatePanelRoot("SavePanel", typeof(WorldSavePanel), "Save");
            Transform content = root.transform.Find("Content");
            TMP_Text statusText = CreateText(content, "StatusText", "Ready", 24, 42f);
            Button saveButton = CreateButton(content, "Save", "Save");
            Button closeButton = CreateButton(content, "Close", "Close");

            SerializedObject serialized = new SerializedObject(root.GetComponent<WorldSavePanel>());
            SetObject(serialized, "closeButton", closeButton);
            SetObject(serialized, "saveButton", saveButton);
            SetObject(serialized, "statusText", statusText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, WorldSavePanel.PrefabPath);
        }

        private static void CreateGmPanel()
        {
            GameObject root = new GameObject("GmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(WorldGmPanel));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(560f, 620f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.97f, 0.91f, 0.8f, 0.98f);
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;

            GameObject header = CreateGmObject(root.transform, "Header", 20f, 18f, 520f, 58f, typeof(CanvasRenderer), typeof(Image));
            Image headerImage = header.GetComponent<Image>();
            headerImage.color = new Color(0.24f, 0.17f, 0.1f, 0.96f);
            headerImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            headerImage.type = Image.Type.Sliced;

            CreateGmText(header.transform, "Title", "GM", 18f, 0f, 120f, 58f, 30, TextAlignmentOptions.Left, Color.white, "ui.gm.title");
            TMP_Text statusText = CreateGmText(header.transform, "StatusText", "Ready", 190f, 0f, 160f, 58f, 22, TextAlignmentOptions.Center, new Color(0.55f, 1f, 0.25f, 1f));
            Button closeButton = CreateGmButton(header.transform, "Close", "X", 468f, 8f, 42f, 42f, 26, new Color(0.72f, 0.55f, 0.35f, 1f));

            GameObject tabs = CreateGmObject(root.transform, "Tabs", 20f, 86f, 520f, 42f, typeof(RectTransform));
            Button commonTabButton = CreateGmButton(tabs.transform, "CommonTab", "Common", 0f, 0f, 170f, 42f, 20, new Color(0.98f, 0.94f, 0.86f, 1f), "ui.gm.tab.common");
            Button resourcesTabButton = CreateGmButton(tabs.transform, "ResourcesTab", "Resources", 175f, 0f, 170f, 42f, 20, new Color(0.94f, 0.88f, 0.78f, 1f), "ui.gm.tab.resources");
            Button farmingTabButton = CreateGmButton(tabs.transform, "FarmingTab", "Farming", 350f, 0f, 170f, 42f, 20, new Color(0.94f, 0.88f, 0.78f, 1f), "ui.gm.tab.farming");

            GameObject commonRoot = CreateGmPage(root.transform, "CommonRoot");
            Button starterPackButton = CreateGmCommonButton(commonRoot.transform, "StarterPack", "Starter Pack", "ui.gm.button.starter_pack", 0, 0);
            Button addWoodButton = CreateGmCommonButton(commonRoot.transform, "AddWood", "Wood +100", "ui.gm.button.wood_100", 1, 0);
            Button addStoneButton = CreateGmCommonButton(commonRoot.transform, "AddStone", "Stone +100", "ui.gm.button.stone_100", 2, 0);
            Button addGoldButton = CreateGmCommonButton(commonRoot.transform, "AddGold", "Gold +100", "ui.gm.button.gold_100", 0, 1);
            Button addFoodButton = CreateGmCommonButton(commonRoot.transform, "AddFood", "Food +100", "ui.gm.button.food_100", 1, 1);
            Button addAllSeedsButton = CreateGmCommonButton(commonRoot.transform, "AddAllSeeds", "All Seeds +100", "ui.gm.button.all_seeds_100", 2, 1);
            Button addAllResourcesButton = CreateGmCommonButton(commonRoot.transform, "AddAllResources", "All Resources +1000", "ui.gm.button.all_resources_1000", 0, 2);
            Button addAllCropsButton = CreateGmCommonButton(commonRoot.transform, "AddAllCrops", "All Crops +1000", "ui.gm.button.all_crops_1000", 1, 2);

            GameObject resourcesRoot = CreateGmPage(root.transform, "ResourcesRoot");
            CreateGmText(resourcesRoot.transform, "ResourcesTitle", "Resources", 0f, 0f, 520f, 30f, 22, TextAlignmentOptions.Left, new Color(0.2f, 0.2f, 0.2f, 1f), "ui.gm.section.resources");
            WorldGmItemRowView[] resourceRows =
            {
                CreateGmItemRow(resourcesRoot.transform, 44f, ItemIds.Wood, "Wood", "item.wood"),
                CreateGmItemRow(resourcesRoot.transform, 84f, ItemIds.Stone, "Stone", "item.stone"),
                CreateGmItemRow(resourcesRoot.transform, 124f, ItemIds.Gold, "Gold", "item.gold"),
                CreateGmItemRow(resourcesRoot.transform, 164f, ItemIds.CopperOre, "Copper Ore", "item.copper_ore"),
                CreateGmItemRow(resourcesRoot.transform, 204f, ItemIds.IronOre, "Iron Ore", "item.iron_ore"),
                CreateGmItemRow(resourcesRoot.transform, 244f, ItemIds.Food, "Food", "item.food"),
                CreateGmItemRow(resourcesRoot.transform, 284f, ItemIds.Plank, "Plank", "item.plank"),
                CreateGmItemRow(resourcesRoot.transform, 324f, ItemIds.CopperIngot, "Copper Ingot", "item.copper_ingot"),
                CreateGmItemRow(resourcesRoot.transform, 364f, ItemIds.IronIngot, "Iron Ingot", "item.iron_ingot"),
            };

            GameObject farmingRoot = CreateGmPage(root.transform, "FarmingRoot");
            farmingRoot.SetActive(false);
            CreateGmText(farmingRoot.transform, "SeedsTitle", "Seeds", 0f, 0f, 520f, 30f, 22, TextAlignmentOptions.Left, new Color(0.2f, 0.2f, 0.2f, 1f), "ui.gm.section.seeds");
            WorldGmItemRowView[] seedRows =
            {
                CreateGmItemRow(farmingRoot.transform, 44f, ItemIds.WheatSeed, "Wheat Seed", "item.wheat_seed"),
                CreateGmItemRow(farmingRoot.transform, 84f, ItemIds.TomatoSeed, "Tomato Seed", "item.tomato_seed"),
                CreateGmItemRow(farmingRoot.transform, 124f, ItemIds.HerbSeed, "Herb Seed", "item.herb_seed"),
                CreateGmItemRow(farmingRoot.transform, 164f, ItemIds.FlowerSeed, "Flower Seed", "item.flower_seed"),
            };

            CreateGmText(farmingRoot.transform, "CropsTitle", "Crops", 0f, 216f, 520f, 30f, 22, TextAlignmentOptions.Left, new Color(0.2f, 0.2f, 0.2f, 1f), "ui.gm.section.crops");
            WorldGmItemRowView[] cropRows =
            {
                CreateGmItemRow(farmingRoot.transform, 260f, ItemIds.Wheat, "Wheat", "item.wheat"),
                CreateGmItemRow(farmingRoot.transform, 300f, ItemIds.Tomato, "Tomato", "item.tomato"),
                CreateGmItemRow(farmingRoot.transform, 340f, ItemIds.Herb, "Herb", "item.herb"),
                CreateGmItemRow(farmingRoot.transform, 380f, ItemIds.Flower, "Flower", "item.flower"),
            };

            resourcesRoot.SetActive(false);

            GameObject footer = CreateGmObject(root.transform, "Footer", 20f, 552f, 520f, 48f, typeof(CanvasRenderer), typeof(Image));
            Image footerImage = footer.GetComponent<Image>();
            footerImage.color = new Color(0.25f, 0.18f, 0.11f, 0.95f);
            footerImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            footerImage.type = Image.Type.Sliced;
            Button refreshButton = CreateGmButton(footer.transform, "Refresh", "Refresh UI", 384f, 7f, 126f, 34f, 18, new Color(0.72f, 0.55f, 0.35f, 1f), "ui.gm.button.refresh_ui");

            SerializedObject serialized = new SerializedObject(root.GetComponent<WorldGmPanel>());
            SetObject(serialized, "closeButton", closeButton);
            SetObject(serialized, "refreshButton", refreshButton);
            SetObject(serialized, "commonTabButton", commonTabButton);
            SetObject(serialized, "resourcesTabButton", resourcesTabButton);
            SetObject(serialized, "farmingTabButton", farmingTabButton);
            SetObject(serialized, "commonRoot", commonRoot);
            SetObject(serialized, "resourcesRoot", resourcesRoot);
            SetObject(serialized, "farmingRoot", farmingRoot);
            SetObject(serialized, "starterPackButton", starterPackButton);
            SetObject(serialized, "addGoldButton", addGoldButton);
            SetObject(serialized, "addWoodButton", addWoodButton);
            SetObject(serialized, "addStoneButton", addStoneButton);
            SetObject(serialized, "addFoodButton", addFoodButton);
            SetObject(serialized, "addAllResourcesButton", addAllResourcesButton);
            SetObject(serialized, "addAllSeedsButton", addAllSeedsButton);
            SetObject(serialized, "addAllCropsButton", addAllCropsButton);
            SetObjectArray(serialized, "resourceRows", resourceRows);
            SetObjectArray(serialized, "seedRows", seedRows);
            SetObjectArray(serialized, "cropRows", cropRows);
            SetObject(serialized, "statusText", statusText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, WorldGmPanel.PrefabPath);
        }

        private static GameObject CreateGmPage(Transform parent, string name)
        {
            GameObject page = CreateGmObject(parent, name, 20f, 144f, 520f, 392f, typeof(CanvasRenderer), typeof(Image));
            Image image = page.GetComponent<Image>();
            image.color = new Color(1f, 0.97f, 0.9f, 0.75f);
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            return page;
        }

        private static Button CreateGmCommonButton(Transform parent, string name, string text, string localizationKey, int column, int row)
        {
            const float width = 160f;
            const float height = 72f;
            const float gapX = 20f;
            const float gapY = 18f;
            float x = column * (width + gapX);
            float y = row * (height + gapY);
            return CreateGmButton(parent, name, text, x, y, width, height, 19, new Color(0.99f, 0.96f, 0.88f, 1f), localizationKey);
        }

        private static WorldGmItemRowView CreateGmItemRow(Transform parent, float y, int itemId, string displayName, string displayKey)
        {
            GameObject row = CreateGmObject(parent, displayName.Replace(" ", string.Empty) + "Row", 0f, y, 520f, 34f, typeof(CanvasRenderer), typeof(Image), typeof(WorldGmItemRowView));
            Image rowImage = row.GetComponent<Image>();
            rowImage.color = new Color(0.99f, 0.96f, 0.88f, 0.92f);
            rowImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            rowImage.type = Image.Type.Sliced;

            TMP_Text nameText = CreateGmText(row.transform, "Name", displayName, 14f, 0f, 150f, 34f, 18, TextAlignmentOptions.Left, new Color(0.18f, 0.18f, 0.18f, 1f), displayKey);
            TMP_Text countText = CreateGmText(row.transform, "Count", "0", 168f, 0f, 66f, 34f, 17, TextAlignmentOptions.Right, new Color(0.18f, 0.32f, 0.12f, 1f));
            Button add10Button = CreateGmButton(row.transform, "Add10", "+10", 250f, 4f, 78f, 26f, 15, new Color(0.94f, 0.9f, 0.82f, 1f));
            Button add100Button = CreateGmButton(row.transform, "Add100", "+100", 336f, 4f, 78f, 26f, 15, new Color(0.94f, 0.9f, 0.82f, 1f));
            Button add1000Button = CreateGmButton(row.transform, "Add1000", "+1000", 422f, 4f, 86f, 26f, 15, new Color(0.94f, 0.9f, 0.82f, 1f));

            WorldGmItemRowView view = row.GetComponent<WorldGmItemRowView>();
            view.Configure(itemId, displayName, displayKey, nameText, countText, add10Button, add100Button, add1000Button);
            return view;
        }

        private static GameObject CreateGmObject(Transform parent, string name, float x, float y, float width, float height, params System.Type[] componentTypes)
        {
            List<System.Type> types = new List<System.Type> { typeof(RectTransform) };
            if (componentTypes != null)
            {
                for (int i = 0; i < componentTypes.Length; i++)
                {
                    if (componentTypes[i] != typeof(RectTransform))
                    {
                        types.Add(componentTypes[i]);
                    }
                }
            }

            GameObject go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return go;
        }

        private static TMP_Text CreateGmText(Transform parent, string name, string text, float x, float y, float width, float height, int fontSize, TextAlignmentOptions alignment, Color color, string localizationKey = null)
        {
            GameObject go = CreateGmObject(parent, name, x, y, width, height, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Font/NotoSansSC-Regular SDF.asset");
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            if (!string.IsNullOrEmpty(localizationKey))
            {
                WorldLocalizedText localizedText = go.AddComponent<WorldLocalizedText>();
                localizedText.Configure(label, localizationKey, text);
            }

            return label;
        }

        private static Button CreateGmButton(Transform parent, string name, string text, float x, float y, float width, float height, int fontSize, Color? backgroundColor = null, string localizationKey = null)
        {
            GameObject go = CreateGmObject(parent, name, x, y, width, height, typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Image image = go.GetComponent<Image>();
            image.color = backgroundColor ?? Color.white;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            CreateGmText(go.transform, "Text (TMP)", text, 0f, 0f, width, height, fontSize, TextAlignmentOptions.Center, new Color(0.18f, 0.16f, 0.12f, 1f), localizationKey);
            return button;
        }

        private static GameObject CreatePanelRoot(string name, System.Type panelType, string title)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), panelType);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 520f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.98f, 0.92f, 0.82f, 0.96f);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(28f, 24f);
            contentRect.offsetMax = new Vector2(-28f, -24f);

            NextElementY[content.transform] = 0f;
            CreateText(content.transform, "Title", title, 26, 54f);
            return root;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            PlaceElement(go.GetComponent<RectTransform>(), parent, height);

            TMP_Text label = go.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button EnsureButton(Transform parent, string name, string text)
        {
            Transform child = parent.Find(name);
            if (child != null && child.TryGetComponent(out Button existing))
            {
                TMP_Text existingText = existing.GetComponentInChildren<TMP_Text>(true);
                if (existingText != null)
                {
                    existingText.text = text;
                }

                return existing;
            }

            return CreateButton(parent, name, text);
        }

        private static Button CreateButton(Transform parent, string name, string text)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            PlaceElement(go.GetComponent<RectTransform>(), parent, 48f);

            Image image = go.GetComponent<Image>();
            image.color = Color.white;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            GameObject textGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text label = textGo.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = 24;
            label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return button;
        }

        private static void PlaceElement(RectTransform rect, Transform parent, float height)
        {
            float y = NextElementY.TryGetValue(parent, out float currentY) ? currentY : 0f;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(0f, height);
            NextElementY[parent] = y + height + 10f;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Serialized property not found: {propertyName}");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedObject serialized, string propertyName, Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Serialized property not found: {propertyName}");
                return;
            }

            int length = values != null ? values.Length : 0;
            property.arraySize = length;
            for (int i = 0; i < length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
