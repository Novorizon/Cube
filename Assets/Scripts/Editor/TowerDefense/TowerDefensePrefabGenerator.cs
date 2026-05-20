using System;
using System.Reflection;
using Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class TowerDefensePrefabGenerator
    {
        private const string UiRoot = "Assets/Arts/UI/TowerDefense";
        private const string PrefabRoot = UiRoot + "/Prefabs";
        private const string ConfigPath = UiRoot + "/TowerDefenseUIConfig.asset";

        [MenuItem("Tools/TowerDefense UI/Generate Prefabs")]
        public static void GeneratePrefabs()
        {
            EnsureFolders();
            ConfigureSprites();

            SkillSlotView skillSlotPrefab = CreateSkillSlotPrefab();
            TowerBuildCardView towerCardPrefab = CreateTowerBuildCardPrefab();
            Image miniMapIconPrefab = CreateMiniMapIconPrefab();
            TdUiConfig config = CreateConfigAsset();
            CreateBattleHudPrefab(config, skillSlotPrefab, towerCardPrefab, miniMapIconPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Tower Defense UI prefabs generated. Prefabs: " + PrefabRoot);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Arts/UI");
            EnsureFolder(UiRoot);
            EnsureFolder(PrefabRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ConfigureSprites()
        {
            string[] slicedPaths =
            {
                "Modules/TopStatusBar/ui_td_top_status_bar_bg.png",
                "Modules/SkillPanel/ui_td_skill_panel_bg.png",
                "Modules/TowerInfo/ui_td_tower_info_panel_bg.png",
                "Modules/TowerInfo/ui_td_tower_info_header_bg.png",
                "Modules/TowerInfo/ui_td_tower_info_stat_row_bg.png",
                "Modules/BuildPanel/ui_td_build_panel_bg.png",
                "Modules/BuildPanel/ui_td_tower_card_bg_normal.png",
                "Modules/BuildPanel/ui_td_tower_card_bg_selected.png",
                "Modules/BuildPanel/ui_td_tower_card_bg_disabled.png",
                "Modules/BuildPanel/ui_td_tower_card_price_bg.png",
                "Modules/BattleControl/ui_td_control_panel_bg.png",
                "Modules/BattleControl/ui_td_speed_button_normal.png",
                "Modules/BattleControl/ui_td_speed_button_selected.png",
                "Modules/BattleControl/ui_td_speed_button_disabled.png",
                "Common/Buttons/ui_td_button_green_normal.png",
                "Common/Buttons/ui_td_button_green_pressed.png",
                "Common/Buttons/ui_td_button_green_disabled.png",
                "Common/Buttons/ui_td_button_orange_normal.png",
                "Common/Buttons/ui_td_button_orange_pressed.png",
                "Common/Buttons/ui_td_button_orange_disabled.png",
                "Common/Buttons/ui_td_button_white_normal.png",
                "Common/Buttons/ui_td_button_white_pressed.png",
                "Common/Buttons/ui_td_button_white_disabled.png",
                "Common/Buttons/ui_td_button_yellow_normal.png",
                "Common/Buttons/ui_td_button_yellow_pressed.png",
                "Common/Buttons/ui_td_button_icon_round_normal.png",
                "Common/Buttons/ui_td_button_icon_round_pressed.png",
                "Common/Buttons/ui_td_button_icon_round_disabled.png",
                "Common/Buttons/ui_td_toggle_track_off.png",
                "Common/Buttons/ui_td_toggle_track_on.png",
                "Common/Bars/ui_td_bar_hp_bg.png",
                "Common/Bars/ui_td_bar_hp_fill_green.png",
                "Common/Bars/ui_td_bar_small_bg.png",
                "Common/Bars/ui_td_bar_small_fill_green.png"
            };

            string[] simplePaths =
            {
                "Modules/SkillPanel/ui_td_skill_slot_frame_normal.png",
                "Modules/SkillPanel/ui_td_skill_slot_frame_selected.png",
                "Modules/SkillPanel/ui_td_skill_slot_frame_disabled.png",
                "Modules/SkillPanel/ui_td_skill_slot_inner_dark.png",
                "Modules/MiniMap/ui_td_minimap_frame.png",
                "Modules/MiniMap/ui_td_minimap_inner_bg.png",
                "Modules/MiniMap/ui_td_minimap_path_dot.png",
                "Modules/MiniMap/ui_td_minimap_path_line.png",
                "Modules/MiniMap/ui_td_minimap_enemy_dot.png",
                "Modules/MiniMap/ui_td_minimap_tower_dot.png",
                "Modules/MiniMap/ui_td_minimap_base_icon.png",
                "Modules/MiniMap/ui_td_minimap_player_marker.png",
                "Common/Badges/ui_td_badge_count_bg.png",
                "Common/States/ui_td_state_cooldown_mask.png",
                "Common/States/ui_td_state_disabled_mask.png",
                "Common/States/ui_td_state_selected_glow.png",
                "Common/States/ui_td_state_highlight_glow.png",
                "Common/States/ui_td_state_price_not_enough_mask.png",
                "Common/Frames/ui_td_frame_icon_square_normal.png",
                "Common/Frames/ui_td_frame_icon_square_selected.png",
                "Common/Frames/ui_td_frame_icon_square_disabled.png",
                "Common/Frames/ui_td_frame_icon_round.png",
                "Common/Dividers/ui_td_divider_vertical.png",
                "Common/Dividers/ui_td_divider_horizontal.png",
                "Common/Bars/ui_td_bar_hp_gloss.png",
                "Common/Buttons/ui_td_toggle_knob.png",
                "Icons/Skills/ui_td_skill_fireball_icon.png",
                "Icons/Skills/ui_td_skill_freeze_icon.png",
                "Icons/Skills/ui_td_skill_lightning_icon.png",
                "Icons/Skills/ui_td_skill_bomb_icon.png",
                "Icons/Towers/ui_td_tower_arrow_icon.png",
                "Icons/Towers/ui_td_tower_cannon_icon.png",
                "Icons/Towers/ui_td_tower_ice_icon.png",
                "Icons/Towers/ui_td_tower_fire_icon.png",
                "Icons/Stats/ui_td_stat_attack_icon.png",
                "Icons/Stats/ui_td_stat_range_icon.png",
                "Icons/Stats/ui_td_stat_speed_icon.png",
                "Icons/Stats/ui_td_stat_level_icon.png",
                "Icons/Stats/ui_td_stat_sell_icon.png",
                "Icons/System/ui_td_system_heart_icon.png",
                "Icons/System/ui_td_system_coin_icon.png",
                "Icons/System/ui_td_system_coin_small_icon.png",
                "Icons/System/ui_td_system_skull_icon.png",
                "Icons/System/ui_td_system_upgrade_arrow_icon.png",
                "Icons/System/ui_td_system_pause_icon.png",
                "Icons/System/ui_td_system_setting_icon.png",
                "Icons/System/ui_td_system_close_icon.png",
                "Icons/System/ui_td_system_check_icon.png"
            };

            for (int i = 0; i < slicedPaths.Length; i++)
            {
                ConfigureSprite(UiRoot + "/" + slicedPaths[i], true);
            }

            for (int i = 0; i < simplePaths.Length; i++)
            {
                ConfigureSprite(UiRoot + "/" + simplePaths[i], false);
            }
        }

        private static void ConfigureSprite(string path, bool sliced)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Missing UI sprite: " + path);
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.alphaIsTransparency == false)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (sliced)
            {
                Vector4 border = new Vector4(24f, 24f, 24f, 24f);
                if (importer.spriteBorder != border)
                {
                    importer.spriteBorder = border;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static SkillSlotView CreateSkillSlotPrefab()
        {
            GameObject root = CreateUiObject("SkillSlot", null, new Vector2(76f, 76f));
            SetAnchored(root.transform as RectTransform, Vector2.zero, new Vector2(76f, 76f));

            Image frame = AddImage(root, "Frame", SpriteOf("Modules/SkillPanel/ui_td_skill_slot_frame_normal.png"), Image.Type.Simple, true);
            Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);

            Image inner = AddImage(root, "Inner", SpriteOf("Modules/SkillPanel/ui_td_skill_slot_inner_dark.png"), Image.Type.Simple, false);
            Stretch(inner.rectTransform, 10f, 10f, 10f, 10f);

            Image icon = AddImage(root, "Icon", null, Image.Type.Simple, false);
            SetAnchored(icon.rectTransform, Vector2.zero, new Vector2(48f, 48f));

            Image selectedFrame = AddImage(root, "SelectedFrame", SpriteOf("Modules/SkillPanel/ui_td_skill_slot_frame_selected.png"), Image.Type.Simple, false);
            Stretch(selectedFrame.rectTransform, -4f, -4f, -4f, -4f);
            selectedFrame.gameObject.SetActive(false);

            Image cooldownMask = AddImage(root, "CooldownMask", SpriteOf("Common/States/ui_td_state_cooldown_mask.png"), Image.Type.Simple, false);
            Stretch(cooldownMask.rectTransform, 8f, 8f, 8f, 8f);
            cooldownMask.gameObject.SetActive(false);

            Image disabledMask = AddImage(root, "DisabledMask", SpriteOf("Common/States/ui_td_state_disabled_mask.png"), Image.Type.Simple, false);
            Stretch(disabledMask.rectTransform, 8f, 8f, 8f, 8f);
            disabledMask.gameObject.SetActive(false);

            GameObject badge = CreateUiObject("CountBadge", root.transform, new Vector2(30f, 30f));
            RectTransform badgeRect = badge.transform as RectTransform;
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-6f, -6f);
            badgeRect.sizeDelta = new Vector2(30f, 30f);

            Image badgeBg = badge.AddComponent<Image>();
            badgeBg.sprite = SpriteOf("Common/Badges/ui_td_badge_count_bg.png");
            badgeBg.raycastTarget = false;

            TMP_Text countText = AddText(badge, "CountText", "0", 18, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(countText.rectTransform, 0f, 0f, 0f, 0f);

            TMP_Text nameText = AddText(root, "NameText", string.Empty, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            nameText.gameObject.SetActive(false);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = frame;
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            SkillSlotView view = root.AddComponent<SkillSlotView>();
            SetField(view, "button", button);
            SetField(view, "iconImage", icon);
            SetField(view, "nameText", nameText);
            SetField(view, "countText", countText);
            SetField(view, "canvasGroup", canvasGroup);

            return SavePrefab<SkillSlotView>(root, PrefabRoot + "/SkillSlot.prefab");
        }

        private static TowerBuildCardView CreateTowerBuildCardPrefab()
        {
            GameObject root = CreateUiObject("TowerBuildCard", null, new Vector2(132f, 142f));
            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/BuildPanel/ui_td_tower_card_bg_normal.png");
            bg.type = Image.Type.Sliced;

            Image selected = AddImage(root, "SelectedFrame", SpriteOf("Modules/BuildPanel/ui_td_tower_card_bg_selected.png"), Image.Type.Sliced, false);
            Stretch(selected.rectTransform, 0f, 0f, 0f, 0f);
            selected.gameObject.SetActive(false);

            Image icon = AddImage(root, "TowerIcon", null, Image.Type.Simple, false);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -26f);
            iconRect.sizeDelta = new Vector2(82f, 70f);
            icon.preserveAspect = true;

            TMP_Text nameText = AddText(root, "TowerNameText", "Tower", 18, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(8f, -28f);
            nameRect.offsetMax = new Vector2(-8f, -8f);

            GameObject price = CreateUiObject("Price", root.transform, new Vector2(86f, 28f));
            RectTransform priceRect = price.transform as RectTransform;
            priceRect.anchorMin = new Vector2(0.5f, 0f);
            priceRect.anchorMax = new Vector2(0.5f, 0f);
            priceRect.pivot = new Vector2(0.5f, 0f);
            priceRect.anchoredPosition = new Vector2(0f, 10f);
            priceRect.sizeDelta = new Vector2(86f, 28f);
            Image priceBg = price.AddComponent<Image>();
            priceBg.sprite = SpriteOf("Modules/BuildPanel/ui_td_tower_card_price_bg.png");
            priceBg.type = Image.Type.Sliced;

            Image coin = AddImage(price, "CoinIcon", SpriteOf("Icons/System/ui_td_system_coin_small_icon.png"), Image.Type.Simple, false);
            RectTransform coinRect = coin.rectTransform;
            coinRect.anchorMin = new Vector2(0f, 0.5f);
            coinRect.anchorMax = new Vector2(0f, 0.5f);
            coinRect.pivot = new Vector2(0f, 0.5f);
            coinRect.anchoredPosition = new Vector2(8f, 0f);
            coinRect.sizeDelta = new Vector2(20f, 20f);
            coin.preserveAspect = true;

            TMP_Text costText = AddText(price, "CostText", "0", 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RectTransform costRect = costText.rectTransform;
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(1f, 1f);
            costRect.offsetMin = new Vector2(32f, 0f);
            costRect.offsetMax = new Vector2(-6f, 0f);

            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            TowerBuildCardView view = root.AddComponent<TowerBuildCardView>();
            SetField(view, "button", button);
            SetField(view, "iconImage", icon);
            SetField(view, "selectedFrame", selected);
            SetField(view, "nameText", nameText);
            SetField(view, "costText", costText);

            return SavePrefab<TowerBuildCardView>(root, PrefabRoot + "/TowerBuildCard.prefab");
        }

        private static Image CreateMiniMapIconPrefab()
        {
            GameObject root = CreateUiObject("MiniMapIcon", null, new Vector2(12f, 12f));
            Image image = root.AddComponent<Image>();
            image.sprite = SpriteOf("Modules/MiniMap/ui_td_minimap_enemy_dot.png");
            image.raycastTarget = false;
            return SavePrefab<Image>(root, PrefabRoot + "/MiniMapIcon.prefab");
        }

        private static TdUiConfig CreateConfigAsset()
        {
            TdUiConfig config = AssetDatabase.LoadAssetAtPath<TdUiConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TdUiConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.Towers = new[]
            {
                new TdTowerUiConfig { Id = 1001, Name = "箭塔", Icon = SpriteOf("Icons/Towers/ui_td_tower_arrow_icon.png"), Cost = 150 },
                new TdTowerUiConfig { Id = 1002, Name = "加农炮塔", Icon = SpriteOf("Icons/Towers/ui_td_tower_cannon_icon.png"), Cost = 250 },
                new TdTowerUiConfig { Id = 1003, Name = "冰塔", Icon = SpriteOf("Icons/Towers/ui_td_tower_ice_icon.png"), Cost = 200 },
                new TdTowerUiConfig { Id = 1004, Name = "火塔", Icon = SpriteOf("Icons/Towers/ui_td_tower_fire_icon.png"), Cost = 250 }
            };

            config.Skills = new[]
            {
                new TdSkillUiConfig { Id = 2001, Name = "火球", Icon = SpriteOf("Icons/Skills/ui_td_skill_fireball_icon.png"), Count = 3 },
                new TdSkillUiConfig { Id = 2002, Name = "冰冻", Icon = SpriteOf("Icons/Skills/ui_td_skill_freeze_icon.png"), Count = 2 },
                new TdSkillUiConfig { Id = 2003, Name = "闪电", Icon = SpriteOf("Icons/Skills/ui_td_skill_lightning_icon.png"), Count = 2 },
                new TdSkillUiConfig { Id = 2004, Name = "炸弹", Icon = SpriteOf("Icons/Skills/ui_td_skill_bomb_icon.png"), Count = 1 }
            };

            EditorUtility.SetDirty(config);
            return config;
        }

        private static void CreateBattleHudPrefab(TdUiConfig config, SkillSlotView skillSlotPrefab, TowerBuildCardView towerCardPrefab, Image miniMapIconPrefab)
        {
            GameObject root = CreateUiObject("BattleHud", null, Vector2.zero);
            RectTransform rootRect = root.transform as RectTransform;
            Stretch(rootRect, 0f, 0f, 0f, 0f);
            BattleHudController controller = root.AddComponent<BattleHudController>();

            StatusPanel statusPanel = CreateTopStatusBar(root.transform);
            CreateTopRightButtons(root.transform);
            SkillPanel skillPanel = CreateSkillPanel(root.transform, skillSlotPrefab);
            MiniMapPanel miniMapPanel = CreateMiniMapPanel(root.transform, miniMapIconPrefab);
            TowerInfoPanel towerInfoPanel = CreateTowerInfoPanel(root.transform);
            BuildTowerPanel buildTowerPanel = CreateBuildTowerPanel(root.transform, towerCardPrefab);
            BattleControlPanel battleControlPanel = CreateBattleControlPanel(root.transform);

            SetField(controller, "config", config);
            SetField(controller, "statusPanel", statusPanel);
            SetField(controller, "buildTowerPanel", buildTowerPanel);
            SetField(controller, "towerInfoPanel", towerInfoPanel);
            SetField(controller, "skillPanel", skillPanel);
            SetField(controller, "battleControlPanel", battleControlPanel);
            SetField(controller, "miniMapPanel", miniMapPanel);

            SavePrefab<BattleHudController>(root, PrefabRoot + "/BattleHud.prefab");
        }

        private static StatusPanel CreateTopStatusBar(Transform parent)
        {
            GameObject root = CreateUiObject("TopStatusBar", parent, new Vector2(840f, 74f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(18f, -18f);
            rootRect.sizeDelta = new Vector2(840f, 74f);

            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/TopStatusBar/ui_td_top_status_bar_bg.png");
            bg.type = Image.Type.Sliced;

            StatusPanel panel = root.AddComponent<StatusPanel>();
            AddStatusLifeGroup(root, panel);
            AddStatusGoldGroup(root, panel);
            AddStatusWaveGroup(root, panel);
            AddStatusEnemyGroup(root, panel);
            AddDivider(root, "Divider1", 272f);
            AddDivider(root, "Divider2", 438f);
            AddDivider(root, "Divider3", 606f);
            return panel;
        }

        private static void AddStatusLifeGroup(GameObject root, StatusPanel panel)
        {
            Image icon = AddImage(root, "HeartIcon", SpriteOf("Icons/System/ui_td_system_heart_icon.png"), Image.Type.Simple, false);
            SetTopLeft(icon.rectTransform, 24f, -13f, 48f, 48f);

            TMP_Text title = AddText(root, "LifeTitle", "基地生命", 18, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(title.rectTransform, 82f, -8f, 150f, 24f);

            GameObject hpBarObj = CreateUiObject("HpBar", root.transform, new Vector2(180f, 24f));
            SetTopLeft(hpBarObj.transform as RectTransform, 82f, -38f, 180f, 24f);
            UIProgressBar hpBar = hpBarObj.AddComponent<UIProgressBar>();
            Image barBg = hpBarObj.AddComponent<Image>();
            barBg.sprite = SpriteOf("Common/Bars/ui_td_bar_hp_bg.png");
            barBg.type = Image.Type.Sliced;

            Image fill = AddImage(hpBarObj, "Fill", SpriteOf("Common/Bars/ui_td_bar_hp_fill_green.png"), Image.Type.Filled, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);

            Image gloss = AddImage(hpBarObj, "Gloss", SpriteOf("Common/Bars/ui_td_bar_hp_gloss.png"), Image.Type.Simple, false);
            Stretch(gloss.rectTransform, 3f, 3f, 3f, 3f);

            TMP_Text valueText = AddText(hpBarObj, "ValueText", "3000/3000", 15, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(valueText.rectTransform, 0f, 0f, 0f, 0f);
            SetField(hpBar, "fillImage", fill);
            SetField(hpBar, "valueText", valueText);
            SetField(panel, "baseLifeBar", hpBar);
            SetField(panel, "baseLifeText", valueText);
        }

        private static void AddStatusGoldGroup(GameObject root, StatusPanel panel)
        {
            Image icon = AddImage(root, "CoinIcon", SpriteOf("Icons/System/ui_td_system_coin_icon.png"), Image.Type.Simple, false);
            SetTopLeft(icon.rectTransform, 296f, -14f, 46f, 46f);

            TMP_Text text = AddText(root, "GoldText", "金币: 1250", 24, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(text.rectTransform, 352f, -20f, 130f, 40f);
            SetField(panel, "goldText", text);
        }

        private static void AddStatusWaveGroup(GameObject root, StatusPanel panel)
        {
            TMP_Text text = AddText(root, "WaveText", "波次: 0", 26, FontStyles.Bold, TextAlignmentOptions.Center);
            SetTopLeft(text.rectTransform, 456f, -17f, 150f, 40f);
            SetField(panel, "waveText", text);
        }

        private static void AddStatusEnemyGroup(GameObject root, StatusPanel panel)
        {
            Image icon = AddImage(root, "SkullIcon", SpriteOf("Icons/System/ui_td_system_skull_icon.png"), Image.Type.Simple, false);
            SetTopLeft(icon.rectTransform, 624f, -16f, 42f, 42f);

            TMP_Text text = AddText(root, "EnemyText", "敌人: 0", 22, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(text.rectTransform, 674f, -20f, 150f, 40f);
            SetField(panel, "enemyText", text);
        }

        private static SkillPanel CreateSkillPanel(Transform parent, SkillSlotView slotPrefab)
        {
            GameObject root = CreateUiObject("SkillPanel", parent, new Vector2(102f, 420f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0.5f);
            rootRect.anchoredPosition = new Vector2(18f, 0f);
            rootRect.sizeDelta = new Vector2(102f, 420f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/SkillPanel/ui_td_skill_panel_bg.png");
            bg.type = Image.Type.Sliced;

            GameObject content = CreateUiObject("Content", root.transform, new Vector2(76f, 356f));
            RectTransform contentRect = content.transform as RectTransform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(76f, 356f);
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            SkillPanel panel = root.AddComponent<SkillPanel>();
            SetField(panel, "contentRoot", contentRect);
            SetField(panel, "slotPrefab", slotPrefab);
            return panel;
        }

        private static MiniMapPanel CreateMiniMapPanel(Transform parent, Image iconPrefab)
        {
            GameObject root = CreateUiObject("MiniMapPanel", parent, new Vector2(220f, 178f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-18f, -18f);
            rootRect.sizeDelta = new Vector2(220f, 178f);

            Image frame = AddImage(root, "Frame", SpriteOf("Modules/MiniMap/ui_td_minimap_frame.png"), Image.Type.Simple, false);
            Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);

            Image inner = AddImage(root, "InnerBg", SpriteOf("Modules/MiniMap/ui_td_minimap_inner_bg.png"), Image.Type.Simple, false);
            Stretch(inner.rectTransform, 15f, 15f, 15f, 15f);

            GameObject mapRoot = CreateUiObject("MapRoot", root.transform, new Vector2(190f, 148f));
            Stretch(mapRoot.transform as RectTransform, 15f, 15f, 15f, 15f);

            MiniMapPanel panel = root.AddComponent<MiniMapPanel>();
            SetField(panel, "mapRoot", mapRoot.transform as RectTransform);
            SetField(panel, "iconPrefab", iconPrefab);
            SetField(panel, "enemySprite", SpriteOf("Modules/MiniMap/ui_td_minimap_enemy_dot.png"));
            SetField(panel, "towerSprite", SpriteOf("Modules/MiniMap/ui_td_minimap_tower_dot.png"));
            SetField(panel, "baseSprite", SpriteOf("Modules/MiniMap/ui_td_minimap_base_icon.png"));
            SetField(panel, "pathSprite", SpriteOf("Modules/MiniMap/ui_td_minimap_path_dot.png"));
            SetField(panel, "playerSprite", SpriteOf("Modules/MiniMap/ui_td_minimap_player_marker.png"));
            return panel;
        }

        private static TowerInfoPanel CreateTowerInfoPanel(Transform parent)
        {
            GameObject root = CreateUiObject("TowerInfoPanel", parent, new Vector2(300f, 360f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(1f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.anchoredPosition = new Vector2(-18f, -40f);
            rootRect.sizeDelta = new Vector2(300f, 360f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/TowerInfo/ui_td_tower_info_panel_bg.png");
            bg.type = Image.Type.Sliced;
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();

            Image iconFrame = AddImage(root, "TowerIconFrame", SpriteOf("Common/Frames/ui_td_frame_icon_round.png"), Image.Type.Simple, false);
            SetTopLeft(iconFrame.rectTransform, 24f, -24f, 82f, 82f);
            Image towerIcon = AddImage(root, "TowerIcon", null, Image.Type.Simple, false);
            SetTopLeft(towerIcon.rectTransform, 34f, -34f, 62f, 62f);
            towerIcon.preserveAspect = true;

            TMP_Text nameText = AddText(root, "TowerNameText", "塔", 24, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(nameText.rectTransform, 120f, -28f, 150f, 34f);
            TMP_Text levelText = AddText(root, "LevelText", "等级 1", 20, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(levelText.rectTransform, 120f, -66f, 150f, 30f);

            AddDivider(root, "HeaderDivider", 116f, true);
            TMP_Text attackText = CreateStatRow(root, "AttackRow", 132f, SpriteOf("Icons/Stats/ui_td_stat_attack_icon.png"), "攻击力", out TMP_Text attackAddText);
            TMP_Text rangeText = CreateStatRow(root, "RangeRow", 178f, SpriteOf("Icons/Stats/ui_td_stat_range_icon.png"), "攻击范围", out _);
            TMP_Text speedText = CreateStatRow(root, "SpeedRow", 224f, SpriteOf("Icons/Stats/ui_td_stat_speed_icon.png"), "攻击速度", out _);

            Button upgradeButton = CreateButton(root, "UpgradeButton", SpriteOf("Common/Buttons/ui_td_button_green_normal.png"), new Vector2(92f, 56f), new Vector2(-62f, 34f), "升级", 22);
            RectTransform upgradeRect = upgradeButton.transform as RectTransform;
            upgradeRect.anchorMin = new Vector2(0.5f, 0f);
            upgradeRect.anchorMax = new Vector2(0.5f, 0f);
            Image upgradeCoin = AddImage(upgradeButton.gameObject, "CoinIcon", SpriteOf("Icons/System/ui_td_system_coin_small_icon.png"), Image.Type.Simple, false);
            SetAnchored(upgradeCoin.rectTransform, new Vector2(-22f, -14f), new Vector2(18f, 18f));
            TMP_Text upgradeCostText = AddText(upgradeButton.gameObject, "CostText", "0", 16, FontStyles.Bold, TextAlignmentOptions.Center);
            SetAnchored(upgradeCostText.rectTransform, new Vector2(12f, -14f), new Vector2(48f, 20f));

            Button sellButton = CreateButton(root, "SellButton", SpriteOf("Common/Buttons/ui_td_button_orange_normal.png"), new Vector2(92f, 56f), new Vector2(62f, 34f), "出售", 22);
            RectTransform sellRect = sellButton.transform as RectTransform;
            sellRect.anchorMin = new Vector2(0.5f, 0f);
            sellRect.anchorMax = new Vector2(0.5f, 0f);
            Image sellCoin = AddImage(sellButton.gameObject, "CoinIcon", SpriteOf("Icons/System/ui_td_system_coin_small_icon.png"), Image.Type.Simple, false);
            SetAnchored(sellCoin.rectTransform, new Vector2(-22f, -14f), new Vector2(18f, 18f));
            TMP_Text sellGoldText = AddText(sellButton.gameObject, "PriceText", "0", 16, FontStyles.Bold, TextAlignmentOptions.Center);
            SetAnchored(sellGoldText.rectTransform, new Vector2(12f, -14f), new Vector2(48f, 20f));

            TowerInfoPanel panel = root.AddComponent<TowerInfoPanel>();
            SetField(panel, "canvasGroup", canvasGroup);
            SetField(panel, "towerIconImage", towerIcon);
            SetField(panel, "towerNameText", nameText);
            SetField(panel, "levelText", levelText);
            SetField(panel, "attackText", attackText);
            SetField(panel, "attackAddText", attackAddText);
            SetField(panel, "rangeText", rangeText);
            SetField(panel, "speedText", speedText);
            SetField(panel, "upgradeCostText", upgradeCostText);
            SetField(panel, "sellGoldText", sellGoldText);
            SetField(panel, "upgradeButton", upgradeButton);
            SetField(panel, "sellButton", sellButton);
            return panel;
        }

        private static TMP_Text CreateStatRow(GameObject parent, string name, float top, Sprite iconSprite, string label, out TMP_Text addText)
        {
            GameObject row = CreateUiObject(name, parent.transform, new Vector2(252f, 34f));
            SetTopLeft(row.transform as RectTransform, 24f, -top, 252f, 34f);
            Image bg = row.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/TowerInfo/ui_td_tower_info_stat_row_bg.png");
            bg.type = Image.Type.Sliced;
            Image icon = AddImage(row, "Icon", iconSprite, Image.Type.Simple, false);
            SetTopLeft(icon.rectTransform, 8f, -5f, 24f, 24f);
            TMP_Text labelText = AddText(row, "LabelText", label, 18, FontStyles.Normal, TextAlignmentOptions.Left);
            SetTopLeft(labelText.rectTransform, 40f, -2f, 100f, 30f);
            TMP_Text valueText = AddText(row, "ValueText", "0", 18, FontStyles.Bold, TextAlignmentOptions.Right);
            SetTopLeft(valueText.rectTransform, 142f, -2f, 64f, 30f);
            addText = AddText(row, "AddText", string.Empty, 16, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(addText.rectTransform, 212f, -2f, 38f, 30f);
            addText.color = new Color(0.25f, 0.68f, 0.22f, 1f);
            return valueText;
        }

        private static BuildTowerPanel CreateBuildTowerPanel(Transform parent, TowerBuildCardView cardPrefab)
        {
            GameObject root = CreateUiObject("BuildTowerPanel", parent, new Vector2(620f, 162f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 18f);
            rootRect.sizeDelta = new Vector2(620f, 162f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/BuildPanel/ui_td_build_panel_bg.png");
            bg.type = Image.Type.Sliced;

            GameObject content = CreateUiObject("Content", root.transform, new Vector2(560f, 142f));
            RectTransform contentRect = content.transform as RectTransform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(560f, 142f);
            HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            BuildTowerPanel panel = root.AddComponent<BuildTowerPanel>();
            SetField(panel, "contentRoot", contentRect);
            SetField(panel, "cardPrefab", cardPrefab);
            return panel;
        }

        private static BattleControlPanel CreateBattleControlPanel(Transform parent)
        {
            GameObject root = CreateUiObject("BattleControlPanel", parent, new Vector2(274f, 108f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-18f, 18f);
            rootRect.sizeDelta = new Vector2(274f, 108f);
            Image bg = root.AddComponent<Image>();
            bg.sprite = SpriteOf("Modules/BattleControl/ui_td_control_panel_bg.png");
            bg.type = Image.Type.Sliced;

            Button speed1 = CreateButton(root, "Speed1Button", SpriteOf("Modules/BattleControl/ui_td_speed_button_selected.png"), new Vector2(62f, 34f), new Vector2(-88f, 54f), "x1", 20);
            Button speed2 = CreateButton(root, "Speed2Button", SpriteOf("Modules/BattleControl/ui_td_speed_button_normal.png"), new Vector2(62f, 34f), new Vector2(-18f, 54f), "x2", 20);
            Button speed3 = CreateButton(root, "Speed3Button", SpriteOf("Modules/BattleControl/ui_td_speed_button_normal.png"), new Vector2(62f, 34f), new Vector2(52f, 54f), "x3", 20);

            Toggle toggle = CreateToggle(root, "AutoNextWaveToggle");
            RectTransform toggleRect = toggle.transform as RectTransform;
            toggleRect.anchorMin = new Vector2(1f, 0f);
            toggleRect.anchorMax = new Vector2(1f, 0f);
            toggleRect.pivot = new Vector2(1f, 0f);
            toggleRect.anchoredPosition = new Vector2(-18f, 16f);
            toggleRect.sizeDelta = new Vector2(90f, 36f);

            TMP_Text label = AddText(root, "AutoNextWaveText", "自动下一波", 18, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.offsetMin = new Vector2(24f, 16f);
            labelRect.offsetMax = new Vector2(-112f, 52f);

            BattleControlPanel panel = root.AddComponent<BattleControlPanel>();
            SetField(panel, "speed1Button", speed1);
            SetField(panel, "speed2Button", speed2);
            SetField(panel, "speed3Button", speed3);
            SetField(panel, "autoNextWaveToggle", toggle);
            return panel;
        }

        private static void CreateTopRightButtons(Transform parent)
        {
            GameObject root = CreateUiObject("TopRightButtons", parent, new Vector2(140f, 62f));
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-252f, -24f);
            rootRect.sizeDelta = new Vector2(140f, 62f);

            Button pause = CreateIconButton(root, "PauseButton", new Vector2(-38f, -31f), SpriteOf("Icons/System/ui_td_system_pause_icon.png"));
            Button setting = CreateIconButton(root, "SettingButton", new Vector2(30f, -31f), SpriteOf("Icons/System/ui_td_system_setting_icon.png"));
            pause.name = "PauseButton";
            setting.name = "SettingButton";
        }

        private static Button CreateIconButton(GameObject parent, string name, Vector2 anchoredPosition, Sprite iconSprite)
        {
            Button button = CreateButton(parent, name, SpriteOf("Common/Buttons/ui_td_button_icon_round_normal.png"), new Vector2(58f, 58f), anchoredPosition, string.Empty, 18);
            Image icon = AddImage(button.gameObject, "Icon", iconSprite, Image.Type.Simple, false);
            SetAnchored(icon.rectTransform, Vector2.zero, new Vector2(30f, 30f));
            icon.preserveAspect = true;
            Transform text = button.transform.Find("Text");
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
            return button;
        }

        private static Toggle CreateToggle(GameObject parent, string name)
        {
            GameObject root = CreateUiObject(name, parent.transform, new Vector2(90f, 36f));
            Toggle toggle = root.AddComponent<Toggle>();
            Image track = AddImage(root, "Track", SpriteOf("Common/Buttons/ui_td_toggle_track_on.png"), Image.Type.Sliced, true);
            Stretch(track.rectTransform, 0f, 0f, 0f, 0f);
            Image knob = AddImage(root, "Knob", SpriteOf("Common/Buttons/ui_td_toggle_knob.png"), Image.Type.Simple, false);
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = new Vector2(1f, 0.5f);
            knobRect.anchorMax = new Vector2(1f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.anchoredPosition = new Vector2(-18f, 0f);
            knobRect.sizeDelta = new Vector2(34f, 34f);
            toggle.targetGraphic = track;
            toggle.graphic = knob;
            toggle.isOn = true;
            return toggle;
        }

        private static Button CreateButton(GameObject parent, string name, Sprite sprite, Vector2 size, Vector2 anchoredPosition, string text, int fontSize)
        {
            GameObject root = CreateUiObject(name, parent.transform, size);
            RectTransform rect = root.transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image bg = root.AddComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            TMP_Text label = AddText(root, "Text", text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            return button;
        }

        private static void AddDivider(GameObject parent, string name, float left)
        {
            Image divider = AddImage(parent, name, SpriteOf("Common/Dividers/ui_td_divider_vertical.png"), Image.Type.Simple, false);
            SetTopLeft(divider.rectTransform, left, -12f, 2f, 50f);
        }

        private static void AddDivider(GameObject parent, string name, float top, bool horizontal)
        {
            Image divider = AddImage(parent, name, SpriteOf("Common/Dividers/ui_td_divider_horizontal.png"), Image.Type.Simple, false);
            SetTopLeft(divider.rectTransform, 24f, -top, 252f, 2f);
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.transform as RectTransform;
            rect.sizeDelta = size;
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }
            return gameObject;
        }

        private static Image AddImage(GameObject parent, string name, Sprite sprite, Image.Type type, bool raycastTarget)
        {
            GameObject gameObject = CreateUiObject(name, parent.transform, Vector2.zero);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TMP_Text AddText(GameObject parent, string name, string value, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateUiObject(name, parent.transform, Vector2.zero);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = new Color(0.22f, 0.15f, 0.1f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static Sprite SpriteOf(string relativePath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/" + relativePath);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Debug.LogWarning($"Missing field {target.GetType().Name}.{fieldName}");
                return;
            }

            field.SetValue(target, value);
            EditorUtility.SetDirty(target as UnityEngine.Object);
        }

        private static T SavePrefab<T>(GameObject root, string path) where T : UnityEngine.Object
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<T>();
        }
    }
}