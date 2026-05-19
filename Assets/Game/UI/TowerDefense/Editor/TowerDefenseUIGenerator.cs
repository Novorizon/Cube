#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class TowerDefenseUIGenerator
    {
        private const string SpriteDir = "Assets/GameRes/UI/TowerDefense/Sprites";
        private const string PrefabDir = "Assets/GameRes/Prefabs/UI/TowerDefense";
        private const string ConfigPath = "Assets/GameRes/UI/TowerDefense/TowerDefenseUIConfig.asset";

        [MenuItem("Tools/Game/Tower Defense UI/Generate All UI Assets")]
        public static void GenerateAll()
        {
            EnsureFolders();
            TdUiConfig config = CreateConfig();
            GameObject cardPrefab = CreateTowerBuildCardPrefab();
            GameObject skillPrefab = CreateSkillSlotPrefab();
            GameObject hpPrefab = CreateWorldHpBarPrefab();
            CreateBattleHudPrefab(config, cardPrefab.GetComponent<TowerBuildCardView>(), skillPrefab.GetComponent<SkillSlotView>());
            PrefabUtility.SaveAsPrefabAsset(hpPrefab, $"{PrefabDir}/WorldHpBar.prefab");
            Object.DestroyImmediate(cardPrefab);
            Object.DestroyImmediate(skillPrefab);
            Object.DestroyImmediate(hpPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Tower Defense UI generated. Prefabs: " + PrefabDir);
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/GameRes", "Prefabs");
            CreateFolder("Assets/GameRes/Prefabs", "UI");
            CreateFolder("Assets/GameRes/Prefabs/UI", "TowerDefense");
            CreateFolder("Assets/GameRes/UI", "TowerDefense");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static TdUiConfig CreateConfig()
        {
            TdUiConfig config = AssetDatabase.LoadAssetAtPath<TdUiConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TdUiConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            config.Towers = new[]
            {
                new TdTowerUiConfig { Id = 1001, Name = "箭塔", Icon = LoadSprite("icon_arrowtower"), Cost = 150 },
                new TdTowerUiConfig { Id = 1002, Name = "加农炮塔", Icon = LoadSprite("icon_cannon"), Cost = 250 },
                new TdTowerUiConfig { Id = 1003, Name = "冰塔", Icon = LoadSprite("icon_ice"), Cost = 200 },
                new TdTowerUiConfig { Id = 1004, Name = "火塔", Icon = LoadSprite("icon_fire"), Cost = 250 }
            };
            config.Skills = new[]
            {
                new TdSkillUiConfig { Id = 2001, Name = "火球", Icon = LoadSprite("icon_fire"), Count = 3 },
                new TdSkillUiConfig { Id = 2002, Name = "冰冻", Icon = LoadSprite("icon_ice"), Count = 2 },
                new TdSkillUiConfig { Id = 2003, Name = "闪电", Icon = LoadSprite("icon_lightning"), Count = 2 },
                new TdSkillUiConfig { Id = 2004, Name = "炸弹", Icon = LoadSprite("icon_bomb"), Count = 1 }
            };
            EditorUtility.SetDirty(config);
            return config;
        }

        private static GameObject CreateBattleHudPrefab(TdUiConfig config, TowerBuildCardView cardPrefab, SkillSlotView skillPrefab)
        {
            GameObject root = NewUIObject("BattleHud", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672f, 941f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            BattleHudController controller = root.AddComponent<BattleHudController>();

            StatusPanel status = CreateStatusPanel(root.transform);
            SkillPanel skills = CreateSkillPanel(root.transform, skillPrefab);
            MiniMapPanel minimap = CreateMiniMap(root.transform);
            TowerInfoPanel towerInfo = CreateTowerInfoPanel(root.transform);
            BuildTowerPanel build = CreateBuildPanel(root.transform, cardPrefab);
            BattleControlPanel controls = CreateBattleControlPanel(root.transform);

            SetPrivate(controller, "config", config);
            SetPrivate(controller, "statusPanel", status);
            SetPrivate(controller, "buildTowerPanel", build);
            SetPrivate(controller, "towerInfoPanel", towerInfo);
            SetPrivate(controller, "skillPanel", skills);
            SetPrivate(controller, "battleControlPanel", controls);
            SetPrivate(controller, "miniMapPanel", minimap);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/BattleHud.prefab");
            Object.DestroyImmediate(root);
            return root;
        }

        private static StatusPanel CreateStatusPanel(Transform parent)
        {
            GameObject go = NewPanel("TopStatusBar", parent, new Vector2(0f, 1f), new Vector2(0.66f, 1f), new Vector2(8f, -8f), new Vector2(-300f, -96f));
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 10, 10);
            layout.spacing = 28f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            StatusPanel panel = go.AddComponent<StatusPanel>();

            UIProgressBar hp = CreateHpBlock(go.transform);
            TMP_Text gold = CreateStatusTextBlock(go.transform, LoadSprite("icon_coin"), "金币", "1250");
            TMP_Text wave = CreateSingleStatusText(go.transform, "第 3/10 波", 220f);
            TMP_Text enemy = CreateStatusTextBlock(go.transform, LoadSprite("icon_skull"), "剩余敌人", "28/36");
            Button pause = CreateIconButton("PauseButton", parent, LoadSprite("icon_pause"), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -20f), new Vector2(72f, 72f));
            Button setting = CreateIconButton("SettingButton", parent, LoadSprite("icon_gear"), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-66f, -20f), new Vector2(72f, 72f));
            SetPrivate(panel, "baseLifeBar", hp);
            SetPrivate(panel, "goldText", gold);
            SetPrivate(panel, "waveText", wave);
            SetPrivate(panel, "enemyText", enemy);
            return panel;
        }

        private static UIProgressBar CreateHpBlock(Transform parent)
        {
            GameObject block = NewUIObject("HpBlock", parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300f, 76f));
            HorizontalLayoutGroup layout = block.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            Image icon = CreateImage("HeartIcon", block.transform, LoadSprite("icon_heart"));
            icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
            GameObject barRoot = NewUIObject("HpBar", block.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(210f, 42f));
            Image bg = barRoot.AddComponent<Image>();
            bg.sprite = LoadSprite("slot_soft");
            bg.type = Image.Type.Sliced;
            GameObject fillGo = NewUIObject("Fill", barRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.35f, 0.85f, 0.22f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            TMP_Text text = CreateText("Value", barRoot.transform, "3000/3000", 22, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            UIProgressBar bar = barRoot.AddComponent<UIProgressBar>();
            SetPrivate(bar, "fillImage", fill);
            SetPrivate(bar, "valueText", text);
            return bar;
        }

        private static TMP_Text CreateStatusTextBlock(Transform parent, Sprite iconSprite, string label, string value)
        {
            GameObject block = NewUIObject(label + "Block", parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(180f, 76f));
            HorizontalLayoutGroup layout = block.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            Image icon = CreateImage("Icon", block.transform, iconSprite);
            icon.rectTransform.sizeDelta = new Vector2(54f, 54f);
            TMP_Text text = CreateText("Text", block.transform, label + "\n" + value, 24, TextAlignmentOptions.Left);
            return text;
        }

        private static TMP_Text CreateSingleStatusText(Transform parent, string value, float width)
        {
            TMP_Text text = CreateText("WaveText", parent, value, 30, TextAlignmentOptions.Center);
            text.rectTransform.sizeDelta = new Vector2(width, 76f);
            return text;
        }

        private static SkillPanel CreateSkillPanel(Transform parent, SkillSlotView slotPrefab)
        {
            GameObject go = NewPanel("SkillPanel", parent, new Vector2(0f, 0.36f), new Vector2(0f, 0.79f), new Vector2(18f, 0f), new Vector2(150f, 0f));
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            SkillPanel panel = go.AddComponent<SkillPanel>();
            SetPrivate(panel, "contentRoot", go.GetComponent<RectTransform>());
            SetPrivate(panel, "slotPrefab", slotPrefab);
            return panel;
        }

        private static MiniMapPanel CreateMiniMap(Transform parent)
        {
            GameObject go = NewPanel("MiniMapPanel", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-286f, -120f), new Vector2(260f, 220f));
            GameObject mapRoot = NewUIObject("MapRoot", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image bg = mapRoot.AddComponent<Image>();
            bg.sprite = LoadSprite("icon_minimap");
            bg.type = Image.Type.Sliced;
            MiniMapPanel panel = go.AddComponent<MiniMapPanel>();
            Image dot = NewUIObject("IconPrefab", mapRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f)).AddComponent<Image>();
            dot.sprite = LoadSprite("badge");
            dot.gameObject.SetActive(false);
            SetPrivate(panel, "mapRoot", mapRoot.GetComponent<RectTransform>());
            SetPrivate(panel, "iconPrefab", dot);
            return panel;
        }

        private static TowerInfoPanel CreateTowerInfoPanel(Transform parent)
        {
            GameObject go = NewPanel("TowerInfoPanel", parent, new Vector2(1f, 0.25f), new Vector2(1f, 0.78f), new Vector2(-380f, 0f), new Vector2(350f, 0f));
            CanvasGroup group = go.AddComponent<CanvasGroup>();
            TowerInfoPanel panel = go.AddComponent<TowerInfoPanel>();
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 14f;

            Image icon = CreateImage("TowerIcon", go.transform, LoadSprite("icon_cannon"));
            icon.rectTransform.sizeDelta = new Vector2(96f, 96f);
            TMP_Text name = CreateText("TowerName", go.transform, "加农炮塔", 32, TextAlignmentOptions.Center);
            TMP_Text level = CreateText("Level", go.transform, "等级 2", 24, TextAlignmentOptions.Center);
            TMP_Text attack = CreateText("Attack", go.transform, "攻击力        72", 24, TextAlignmentOptions.Left);
            TMP_Text add = CreateText("AttackAdd", attack.transform, "+12", 22, TextAlignmentOptions.Right);
            add.color = new Color(0.15f, 0.65f, 0.15f, 1f);
            TMP_Text range = CreateText("Range", go.transform, "攻击范围      4.5 格", 24, TextAlignmentOptions.Left);
            TMP_Text speed = CreateText("Speed", go.transform, "攻击速度      1.2 秒", 24, TextAlignmentOptions.Left);
            GameObject buttons = NewUIObject("Buttons", go.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300f, 82f));
            HorizontalLayoutGroup bl = buttons.AddComponent<HorizontalLayoutGroup>();
            bl.spacing = 16f;
            Button upgrade = CreateTextButton("UpgradeButton", buttons.transform, "升级\n600", LoadSprite("button_green"), new Vector2(138f, 76f));
            Button sell = CreateTextButton("SellButton", buttons.transform, "出售\n187", LoadSprite("button_red"), new Vector2(138f, 76f));
            TMP_Text upgradeText = upgrade.GetComponentInChildren<TMP_Text>();
            TMP_Text sellText = sell.GetComponentInChildren<TMP_Text>();

            SetPrivate(panel, "canvasGroup", group);
            SetPrivate(panel, "towerIconImage", icon);
            SetPrivate(panel, "towerNameText", name);
            SetPrivate(panel, "levelText", level);
            SetPrivate(panel, "attackText", attack);
            SetPrivate(panel, "attackAddText", add);
            SetPrivate(panel, "rangeText", range);
            SetPrivate(panel, "speedText", speed);
            SetPrivate(panel, "upgradeCostText", upgradeText);
            SetPrivate(panel, "sellGoldText", sellText);
            SetPrivate(panel, "upgradeButton", upgrade);
            SetPrivate(panel, "sellButton", sell);
            return panel;
        }

        private static BuildTowerPanel CreateBuildPanel(Transform parent, TowerBuildCardView cardPrefab)
        {
            GameObject go = NewPanel("BuildTowerPanel", parent, new Vector2(0.28f, 0f), new Vector2(0.72f, 0f), new Vector2(0f, 16f), new Vector2(0f, 172f));
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            BuildTowerPanel panel = go.AddComponent<BuildTowerPanel>();
            SetPrivate(panel, "contentRoot", go.GetComponent<RectTransform>());
            SetPrivate(panel, "cardPrefab", cardPrefab);
            return panel;
        }

        private static BattleControlPanel CreateBattleControlPanel(Transform parent)
        {
            GameObject go = NewPanel("BattleControlPanel", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 24f), new Vector2(260f, 130f));
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 10f;
            GameObject speedRoot = NewUIObject("SpeedRoot", go.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(230f, 52f));
            HorizontalLayoutGroup hl = speedRoot.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8f;
            Button b1 = CreateTextButton("Speed1", speedRoot.transform, "x1", LoadSprite("button_yellow"), new Vector2(70f, 48f));
            Button b2 = CreateTextButton("Speed2", speedRoot.transform, "x2", LoadSprite("panel_white"), new Vector2(70f, 48f));
            Button b3 = CreateTextButton("Speed3", speedRoot.transform, "x3", LoadSprite("panel_white"), new Vector2(70f, 48f));
            GameObject toggleGo = NewUIObject("AutoNextWaveToggle", go.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(230f, 50f));
            Toggle toggle = toggleGo.AddComponent<Toggle>();
            TMP_Text label = CreateText("Label", toggleGo.transform, "自动下一波", 22, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            Image bg = CreateImage("ToggleBg", toggleGo.transform, LoadSprite("button_green"));
            bg.rectTransform.anchorMin = new Vector2(0.72f, 0.12f);
            bg.rectTransform.anchorMax = new Vector2(1f, 0.88f);
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;
            Image check = CreateImage("Checkmark", bg.transform, LoadSprite("badge"));
            check.rectTransform.anchorMin = new Vector2(0.55f, 0.1f);
            check.rectTransform.anchorMax = new Vector2(0.95f, 0.9f);
            check.rectTransform.offsetMin = Vector2.zero;
            check.rectTransform.offsetMax = Vector2.zero;
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            BattleControlPanel panel = go.AddComponent<BattleControlPanel>();
            SetPrivate(panel, "speed1Button", b1);
            SetPrivate(panel, "speed2Button", b2);
            SetPrivate(panel, "speed3Button", b3);
            SetPrivate(panel, "autoNextWaveToggle", toggle);
            return panel;
        }

        private static GameObject CreateTowerBuildCardPrefab()
        {
            GameObject go = NewPanel("TowerBuildCard", null, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(150f, 140f));
            TowerBuildCardView view = go.AddComponent<TowerBuildCardView>();
            Button button = go.AddComponent<Button>();
            CanvasGroup canvasGroup = go.AddComponent<CanvasGroup>();
            TMP_Text name = CreateText("Name", go.transform, "箭塔", 22, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            Image icon = CreateImage("Icon", go.transform, LoadSprite("icon_arrowtower"));
            icon.rectTransform.anchorMin = new Vector2(0.18f, 0.27f);
            icon.rectTransform.anchorMax = new Vector2(0.82f, 0.74f);
            icon.rectTransform.offsetMin = Vector2.zero;
            icon.rectTransform.offsetMax = Vector2.zero;
            TMP_Text cost = CreateText("Cost", go.transform, "150", 24, TextAlignmentOptions.Center);
            cost.rectTransform.anchorMin = new Vector2(0f, 0f);
            cost.rectTransform.anchorMax = new Vector2(1f, 0.28f);
            Image selected = CreateImage("SelectedFrame", go.transform, LoadSprite("slot_selected"));
            selected.rectTransform.anchorMin = Vector2.zero;
            selected.rectTransform.anchorMax = Vector2.one;
            selected.rectTransform.offsetMin = Vector2.zero;
            selected.rectTransform.offsetMax = Vector2.zero;
            selected.gameObject.SetActive(false);
            SetPrivate(view, "button", button);
            SetPrivate(view, "iconImage", icon);
            SetPrivate(view, "selectedFrame", selected);
            SetPrivate(view, "nameText", name);
            SetPrivate(view, "costText", cost);
            SetPrivate(view, "canvasGroup", canvasGroup);
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/TowerBuildCard.prefab");
            return go;
        }

        private static GameObject CreateSkillSlotPrefab()
        {
            GameObject go = NewPanel("SkillSlot", null, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(104f, 116f));
            SkillSlotView view = go.AddComponent<SkillSlotView>();
            Button button = go.AddComponent<Button>();
            CanvasGroup canvasGroup = go.AddComponent<CanvasGroup>();
            Image icon = CreateImage("Icon", go.transform, LoadSprite("icon_fire"));
            icon.rectTransform.anchorMin = new Vector2(0.18f, 0.32f);
            icon.rectTransform.anchorMax = new Vector2(0.82f, 0.95f);
            TMP_Text name = CreateText("Name", go.transform, "火球", 18, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0.32f);
            TMP_Text count = CreateText("Count", go.transform, "3", 20, TextAlignmentOptions.Center);
            count.rectTransform.anchorMin = new Vector2(0.68f, 0.68f);
            count.rectTransform.anchorMax = new Vector2(1.06f, 1.06f);
            SetPrivate(view, "button", button);
            SetPrivate(view, "iconImage", icon);
            SetPrivate(view, "nameText", name);
            SetPrivate(view, "countText", count);
            SetPrivate(view, "canvasGroup", canvasGroup);
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/SkillSlot.prefab");
            return go;
        }

        private static GameObject CreateWorldHpBarPrefab()
        {
            GameObject go = NewUIObject("WorldHpBar", null, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 42f));
            WorldHpBarView view = go.AddComponent<WorldHpBarView>();
            TMP_Text name = CreateText("Name", go.transform, "敌人", 16, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            GameObject barRoot = NewUIObject("HpBar", go.transform, new Vector2(0f, 0f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);
            Image bg = barRoot.AddComponent<Image>();
            bg.sprite = LoadSprite("panel_white");
            bg.type = Image.Type.Sliced;
            GameObject fillGo = NewUIObject("Fill", barRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.92f, 0.18f, 0.12f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            UIProgressBar bar = barRoot.AddComponent<UIProgressBar>();
            SetPrivate(bar, "fillImage", fill);
            SetPrivate(view, "hpBar", bar);
            SetPrivate(view, "nameText", name);
            SetPrivate(view, "rectTransform", go.GetComponent<RectTransform>());
            return go;
        }

        private static Button CreateIconButton(string name, Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = NewPanel(name, parent, anchorMin, anchorMax, anchoredPosition, size);
            Button button = go.AddComponent<Button>();
            Image icon = CreateImage("Icon", go.transform, sprite);
            icon.rectTransform.anchorMin = new Vector2(0.18f, 0.18f);
            icon.rectTransform.anchorMax = new Vector2(0.82f, 0.82f);
            icon.rectTransform.offsetMin = Vector2.zero;
            icon.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static Button CreateTextButton(string name, Transform parent, string text, Sprite sprite, Vector2 size)
        {
            GameObject go = NewUIObject(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, size);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            Button button = go.AddComponent<Button>();
            TMP_Text label = CreateText("Text", go.transform, text, 24, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static GameObject NewPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = NewUIObject(name, parent, anchorMin, anchorMax, anchoredPosition, size);
            Image image = go.AddComponent<Image>();
            image.sprite = LoadSprite("panel_soft");
            image.type = Image.Type.Sliced;
            return go;
        }

        private static GameObject NewUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = NewUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, int size, TextAlignmentOptions alignment)
        {
            GameObject go = NewUIObject(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(160f, 42f));
            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = new Color(0.22f, 0.16f, 0.12f, 1f);
            tmp.alignment = alignment;
            tmp.enableAutoSizing = false;
            return tmp;
        }

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                EditorUtility.SetDirty((Object)target);
            }
        }
    }
}
#endif
