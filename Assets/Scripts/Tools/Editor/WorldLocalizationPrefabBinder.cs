using Game;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class WorldLocalizationPrefabBinder
    {
        private static readonly HashSet<string> DynamicObjectNames = new HashSet<string>
        {
            "FollowRole",
            "VolumeText",
            "Mute",
            "StatusText",
            "LanguageText",
            "GoldText",
            "WaveText",
            "EnemyText",
            "ValueText",
            "Count",
            "CountText",
            "CostText",
            "Price",
            "PriceText",
            "LevelText",
            "NameText",
            "DescriptionText",
            "AddText",
            "TowerNameText",
            "LabelText",
        };

        private static readonly Dictionary<string, TextBinding> CurrentTextBindings = new Dictionary<string, TextBinding>
        {
            { "Menu", new TextBinding("ui.menu.title", "Menu") },
            { "Sound", new TextBinding("ui.menu.sound", "Sound") },
            { "Language", new TextBinding("ui.menu.language", "Language") },
            { "Save", new TextBinding("ui.menu.save", "Save") },
            { "GM", new TextBinding("ui.menu.gm", "GM") },
            { "Close", new TextBinding("ui.common.close", "Close") },
            { "Volume -", new TextBinding("ui.sound.decrease", "Volume -") },
            { "Volume +", new TextBinding("ui.sound.increase", "Volume +") },
            { "Chinese", new TextBinding("ui.language.chinese", "Chinese") },
            { "English", new TextBinding("ui.language.english", "English") },
            { "Tech Tree", new TextBinding("ui.tech.panel.title", "Tech Tree") },
            { "科技树", new TextBinding("ui.tech.panel.title", "科技树") },
            { "Build", new TextBinding("ui.build.icon_fallback", "Build") },
            { "Build Panel", new TextBinding("ui.build.icon_fallback", "Build") },
            { "Building", new TextBinding("ui.build.tab.building", "Building") },
            { "Buildings", new TextBinding("ui.production.group.buildings", "Buildings") },
            { "Decor", new TextBinding("ui.build.tab.decoration", "Decoration") },
            { "Production", new TextBinding("ui.build.tab.production", "Production") },
            { "Produce", new TextBinding("ui.build.tab.production", "Production") },
            { "Resource", new TextBinding("ui.build.tab.resource", "Resource") },
            { "Resources", new TextBinding("ui.build.tab.resource", "Resources") },
            { "Farm", new TextBinding("ui.build.tab.farm", "Farm") },
            { "Farming", new TextBinding("ui.gm.tab.farming", "Farming") },
            { "Decoration", new TextBinding("ui.build.tab.decoration", "Decoration") },
            { "Special", new TextBinding("ui.build.tab.special", "Special") },
            { "Overview", new TextBinding("ui.production.group.overview", "Overview") },
            { "Crops", new TextBinding("ui.production.group.crops", "Crops") },
            { "Ores", new TextBinding("ui.production.group.ores", "Ores") },
            { "Basic Resources", new TextBinding("ui.production.group.basic", "Basic Resources") },
            { "Upgrade", new TextBinding("ui.building_detail.upgrade", "Upgrade") },
            { "Craft", new TextBinding("ui.building_detail.craft", "Craft") },
            { "Remove", new TextBinding("ui.building_detail.remove", "Remove") },
            { "Bag", new TextBinding("ui.bag.title", "Bag") },
            { "Tool Kit", new TextBinding("ui.toolkit.title", "Tool Kit") },
            { "Settings", new TextBinding("ui.td.settings.title", "Settings") },
            { "Main Menu", new TextBinding("ui.td.settings.main_menu", "Main Menu") },
            { "Restart", new TextBinding("ui.td.settings.restart", "Restart") },
            { "End Battle", new TextBinding("ui.td.settings.restart", "End Battle") },
            { "Base Life", new TextBinding("ui.td.label.base_life", "Base Life") },
            { "Auto Next Wave", new TextBinding("ui.td.label.auto_next_wave", "Auto Next Wave") },
            { "Attack", new TextBinding("ui.td.label.attack", "Attack") },
            { "Range", new TextBinding("ui.td.label.range", "Range") },
            { "Level", new TextBinding("ui.td.info.level", "Level") },
            { "Sell", new TextBinding("ui.td.label.sell", "Sell") },
            { "Tower", new TextBinding("ui.td.label.tower", "Tower") },
            { "Normal Tower", new TextBinding("ui.td.label.normal_tower", "Normal Tower") },
            { "Frost Tower", new TextBinding("ui.td.label.frost_tower", "Frost Tower") },
            { "Cancel", new TextBinding("ui.common.cancel", "Cancel") },
            { "设置", new TextBinding("ui.td.settings.title", "设置") },
            { "语言", new TextBinding("ui.menu.language", "语言") },
            { "主菜单", new TextBinding("ui.td.settings.main_menu", "主菜单") },
            { "重新开始", new TextBinding("ui.td.settings.restart", "重新开始") },
            { "基地生命", new TextBinding("ui.td.label.base_life", "基地生命") },
            { "自动下一波", new TextBinding("ui.td.label.auto_next_wave", "自动下一波") },
            { "攻击", new TextBinding("ui.td.label.attack", "攻击") },
            { "范围", new TextBinding("ui.td.label.range", "范围") },
            { "等级", new TextBinding("ui.td.info.level", "等级") },
            { "出售", new TextBinding("ui.td.label.sell", "出售") },
            { "升级", new TextBinding("ui.building_detail.upgrade", "升级") },
            { "制作", new TextBinding("ui.building_detail.craft", "制作") },
            { "拆除", new TextBinding("ui.building_detail.remove", "拆除") },
        };

        [MenuItem("Tools/Game UI/Bind Localization Texts")]
        public static void BindAll()
        {
            int bound = 0;
            bound += BindPrefab("Assets/Arts/UI/Panels/Menu/MenuPanel.prefab", BindMenuPanel);
            bound += BindPrefab("Assets/Arts/UI/Panels/Menu/SoundPanel.prefab", BindSoundPanel);
            bound += BindPrefab("Assets/Arts/UI/Panels/Menu/LanguagePanel.prefab", BindLanguagePanel);
            bound += BindPrefab("Assets/Arts/UI/Panels/Menu/SavePanel.prefab", BindSavePanel);
            bound += BindPrefab("Assets/Arts/UI/Panels/TechTree/TechTreePanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Build/BuildPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Bag/BagPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Farm/FarmPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Build/BuildingDetailPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Production/ProductionPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/ToolKit/ToolKitPanel.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Pages/BuildHudPage.prefab", root => BindByCurrentText(root.transform));
            bound += BindPrefab("Assets/Arts/UI/Panels/Battle/BattleSettingsPopup.prefab", BindSettingsDialog);
            bound += BindPrefab("Assets/Arts/UI/Panels/Battle/BattlePage.prefab", root => BindByCurrentText(root.transform));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldLocalizationPrefabBinder] Bound localization texts: {bound}");
        }

        private static int BindPrefab(string path, Func<GameObject, int> bind)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = bind(root);
                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                return count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int BindMenuPanel(GameObject root)
        {
            int count = 0;
            count += BindTextObject(root.transform, "Title", "ui.menu.title", "Menu");
            count += BindButtonText(root.transform, "Sound", "ui.menu.sound", "Sound");
            count += BindButtonText(root.transform, "Language", "ui.menu.language", "Language");
            count += BindButtonText(root.transform, "Save", "ui.menu.save", "Save");
            count += BindButtonText(root.transform, "GM", "ui.menu.gm", "GM");
            count += BindButtonText(root.transform, "Close", "ui.menu.close", "Close");
            return count;
        }

        private static int BindSoundPanel(GameObject root)
        {
            int count = 0;
            count += BindTextObject(root.transform, "Title", "ui.sound.title", "Sound");
            count += BindButtonText(root.transform, "Decrease", "ui.sound.decrease", "Volume -");
            count += BindButtonText(root.transform, "Increase", "ui.sound.increase", "Volume +");
            count += BindButtonText(root.transform, "Close", "ui.common.close", "Close");
            return count;
        }

        private static int BindLanguagePanel(GameObject root)
        {
            int count = 0;
            count += BindTextObject(root.transform, "Title", "ui.language.title", "Language");
            count += BindButtonText(root.transform, "Chinese", "ui.language.chinese", "Chinese");
            count += BindButtonText(root.transform, "English", "ui.language.english", "English");
            count += BindButtonText(root.transform, "Close", "ui.common.close", "Close");
            return count;
        }

        private static int BindSavePanel(GameObject root)
        {
            int count = 0;
            count += BindTextObject(root.transform, "Title", "ui.save.title", "Save");
            count += BindButtonText(root.transform, "Save", "ui.save.button", "Save");
            count += BindButtonText(root.transform, "Close", "ui.common.close", "Close");
            return count;
        }

        private static int BindSettingsDialog(GameObject root)
        {
            int count = 0;
            count += BindTextObject(root.transform, "Title", "ui.td.settings.title", "Settings");
            count += BindButtonText(root.transform, "LanguageButton", "ui.td.settings.language", "Language");
            count += BindButtonText(root.transform, "MainMenuButton", "ui.td.settings.main_menu", "Main Menu");
            count += BindButtonText(root.transform, "EndBattleButton", "ui.td.settings.restart", "Restart");
            count += BindButtonText(root.transform, "CloseButton", "ui.common.close", "Close");
            return count;
        }

        private static int BindByCurrentText(Transform root)
        {
            int count = 0;
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                if (ShouldSkip(text.transform))
                {
                    continue;
                }

                if (TryGetBinding(text.text, out TextBinding binding))
                {
                    count += BindText(text, binding.Key, binding.Fallback) ? 1 : 0;
                }
            }

            Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text text = legacyTexts[i];
                if (ShouldSkip(text.transform))
                {
                    continue;
                }

                if (TryGetBinding(text.text, out TextBinding binding))
                {
                    count += BindText(text, binding.Key, binding.Fallback) ? 1 : 0;
                }
            }

            return count;
        }

        private static int BindTextObject(Transform root, string objectName, string key, string fallback)
        {
            Transform target = FindByName(root, objectName);
            if (target == null)
            {
                return 0;
            }

            TMP_Text tmpText = target.GetComponent<TMP_Text>();
            if (tmpText != null)
            {
                return BindText(tmpText, key, fallback) ? 1 : 0;
            }

            Text legacyText = target.GetComponent<Text>();
            if (legacyText != null)
            {
                return BindText(legacyText, key, fallback) ? 1 : 0;
            }

            return BindFirstChildText(target, key, fallback);
        }

        private static int BindButtonText(Transform root, string buttonName, string key, string fallback)
        {
            Transform button = FindByName(root, buttonName);
            if (button == null)
            {
                return 0;
            }

            return BindFirstChildText(button, key, fallback);
        }

        private static int BindFirstChildText(Transform target, string key, string fallback)
        {
            TMP_Text tmpText = target.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                return BindText(tmpText, key, fallback) ? 1 : 0;
            }

            Text legacyText = target.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                return BindText(legacyText, key, fallback) ? 1 : 0;
            }

            return 0;
        }

        private static bool BindText(TMP_Text text, string key, string fallback)
        {
            if (text == null)
            {
                return false;
            }

            WorldLocalizedText localized = text.GetComponent<WorldLocalizedText>();
            if (localized == null)
            {
                localized = text.gameObject.AddComponent<WorldLocalizedText>();
            }

            SerializedObject serialized = new SerializedObject(localized);
            serialized.FindProperty("targetText").objectReferenceValue = text;
            serialized.FindProperty("legacyText").objectReferenceValue = null;
            serialized.FindProperty("key").stringValue = key;
            serialized.FindProperty("fallback").stringValue = fallback;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(localized);
            return true;
        }

        private static bool BindText(Text text, string key, string fallback)
        {
            if (text == null)
            {
                return false;
            }

            WorldLocalizedText localized = text.GetComponent<WorldLocalizedText>();
            if (localized == null)
            {
                localized = text.gameObject.AddComponent<WorldLocalizedText>();
            }

            SerializedObject serialized = new SerializedObject(localized);
            serialized.FindProperty("targetText").objectReferenceValue = null;
            serialized.FindProperty("legacyText").objectReferenceValue = text;
            serialized.FindProperty("key").stringValue = key;
            serialized.FindProperty("fallback").stringValue = fallback;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(localized);
            return true;
        }

        private static bool TryGetBinding(string text, out TextBinding binding)
        {
            binding = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            if (normalized.Length <= 1 || normalized.Contains("{", StringComparison.Ordinal))
            {
                return false;
            }

            return CurrentTextBindings.TryGetValue(normalized, out binding);
        }

        private static bool ShouldSkip(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (DynamicObjectNames.Contains(current.name))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindByName(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private readonly struct TextBinding
        {
            public readonly string Key;
            public readonly string Fallback;

            public TextBinding(string key, string fallback)
            {
                Key = key;
                Fallback = fallback;
            }
        }
    }
}
