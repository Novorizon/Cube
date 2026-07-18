#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game;
using TMPro;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class BattleUiPrefabBinder
    {
        private const string BattlePagePath = "Assets/Arts/UI/Panels/Battle/BattlePage.prefab";
        private const string SettingsPopupPath = "Assets/Arts/UI/Panels/Battle/BattleSettingsPopup.prefab";
        private const string ResultPopupPath = "Assets/Arts/UI/Panels/Battle/BattleResultPopup.prefab";
        private const string CommonSlotPath = "Assets/Arts/UI/Panels/Battle/CommonSlot.prefab";
        private const string ItemContentPath = "Assets/Arts/UI/Panels/Battle/Item.prefab";
        private const string SkillContentPath = "Assets/Arts/UI/Panels/Battle/Skill.prefab";
        private const string TowerSkillContentPath = "Assets/Arts/UI/Panels/Battle/TowerCardSkill.prefab";
        private const string TowerBuildCardPath = "Assets/Arts/UI/Panels/Battle/TowerBuildCard.prefab";
        private const string TowerCardPath = "Assets/Arts/UI/Panels/Battle/TowerCard.prefab";
        private const string InfoSlotPath = "Assets/Arts/UI/Panels/Battle/InfoSlot.prefab";
        private const string WorldHpBarPath = "Assets/Arts/UI/Panels/Battle/WorldHpBar.prefab";
        private const string FontPath = "Assets/Arts/Font/NotoSansSC-Regular SDF.asset";
        private static readonly string[] TargetPreviewPrefabPaths =
        {
            "Assets/Arts/Tower/NormalTower.prefab",
            "Assets/Arts/Tower/IceTower.prefab",
            "Assets/Arts/Character/Pirate/Pirate_Male.prefab",
            "Assets/Arts/Character/Elf.prefab",
            "Assets/Arts/Tool/Base.prefab"
        };

        [MenuItem("Tools/Battle UI/Apply Explicit Bindings")]
        public static void Apply()
        {
            EnsureFontAtlasReadable();
            BindSlotContent(ItemContentPath);
            BindSlotContent(SkillContentPath);
            BindSlotContent(TowerSkillContentPath);
            BindCommonSlot();
            BindTowerCard(TowerBuildCardPath);
            BindTowerCard(TowerCardPath);
            BindInfoSlot();
            BindSettingsPopup();
            BuildResultPopup();
            BindTargetPreviews();
            BindBattlePage();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("Battle UI prefab binding completed.");
        }

        private static void EnsureFontAtlasReadable()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(FontPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Texture2D atlasTexture))
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(atlasTexture);
                SerializedProperty readable = serialized.FindProperty("m_IsReadable");
                if (readable != null && !readable.boolValue)
                {
                    readable.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(atlasTexture);
                }
            }
        }

        [MenuItem("Tools/Battle UI/Validate Explicit Bindings")]
        public static void Validate()
        {
            List<string> errors = new List<string>();
            ValidateComponent<BattlePage>(BattlePagePath, errors,
                "topPanel", "buildTowerPanel", "itemPanel", "targetInfoPanel", "skillPanel", "battleControlPanel");
            ValidateComponent<BattleSafeAreaFitter>(BattlePagePath, errors, "target");
            ValidateChildComponent<TopPanel>(BattlePagePath, errors,
                "baseHpBar", "coinText", "baseHpValueText", "waveText", "enemyText");
            ValidateChildComponent<BuildTowerPanel>(BattlePagePath, errors,
                "contentRoot", "cardPrefab", "towerCardSkillPrefab");
            ValidateChildComponent<ItemPanel>(BattlePagePath, errors,
                "contentRoot", "slotPrefab", "itemContentPrefab");
            ValidateChildComponent<SkillPanel>(BattlePagePath, errors,
                "contentRoot", "slotPrefab", "skillContentPrefab");
            ValidateChildComponent<InfoPanel>(BattlePagePath, errors,
                "canvasGroup", "targetIconImage", "targetNameText", "descriptionText", "contentRoot",
                "infoSlotPrefab", "upgradeButton", "sellButton");
            ValidateChildComponent<BattleControlPanel>(BattlePagePath, errors,
                "speed1Button", "speed2Button", "speed3Button", "autoNextWaveToggle", "pauseButton", "playButton",
                "soundButton", "settingButton");
            ValidateComponent<BattleSettingsPopup>(SettingsPopupPath, errors,
                "languageButton", "soundButton", "restartButton", "mainMenuButton", "closeButton", "languageText", "soundText");
            ValidateComponent<BattleResultPopup>(ResultPopupPath, errors,
                "titleText", "mapText", "reasonText", "rewardText", "nextButton", "nextButtonText",
                "restartButton", "restartButtonText", "mainMenuButton", "mainMenuButtonText");
            ValidateComponent<CommonSlotView>(CommonSlotPath, errors, "contentRoot", "button");
            ValidateComponent<InfoSlotView>(InfoSlotPath, errors, "nameText", "valueText", "addValueText");
            ValidateComponent<TowerBuildCardView>(TowerBuildCardPath, errors,
                "button", "iconImage", "selectedFrame", "nameText", "costText", "skillContentRoot");
            ValidateComponent<TowerBuildCardView>(TowerCardPath, errors,
                "button", "iconImage", "selectedFrame", "nameText", "costText", "skillContentRoot");
            ValidateComponent<BattleSlotContentView>(ItemContentPath, errors, "iconImage");
            ValidateComponent<BattleSlotContentView>(SkillContentPath, errors, "iconImage");
            ValidateComponent<BattleSlotContentView>(TowerSkillContentPath, errors, "iconImage");
            ValidateComponent<WorldHpBarView>(WorldHpBarPath, errors, "hpBar", "nameText", "rectTransform");
            ValidateNoMissingScripts(BattlePagePath, errors);
            ValidateNoMissingScripts(SettingsPopupPath, errors);
            ValidateNoMissingScripts(ResultPopupPath, errors);
            ValidateNoMissingScripts(CommonSlotPath, errors);
            ValidateNoMissingScripts(ItemContentPath, errors);
            ValidateNoMissingScripts(SkillContentPath, errors);
            ValidateNoMissingScripts(TowerSkillContentPath, errors);
            ValidateNoMissingScripts(TowerBuildCardPath, errors);
            ValidateNoMissingScripts(TowerCardPath, errors);
            ValidateNoMissingScripts(InfoSlotPath, errors);
            ValidateNoMissingScripts(WorldHpBarPath, errors);
            ValidateLoadedBattlePagesNoMissingScripts(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Battle UI prefab validation failed:\n" + string.Join("\n", errors));
            }
        }

        private static void BindBattlePage()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BattlePagePath);
            try
            {
                root.name = "BattlePage";
                Transform embeddedSettings = Find(root.transform, "SettingsDialog");
                if (embeddedSettings != null)
                {
                    UnityEngine.Object.DestroyImmediate(embeddedSettings.gameObject);
                }

                BattlePage page = Require(root.GetComponent<BattlePage>(), BattlePagePath, nameof(BattlePage));
                BattleSafeAreaFitter safeArea = root.GetComponent<BattleSafeAreaFitter>();
                if (safeArea == null)
                {
                    safeArea = root.AddComponent<BattleSafeAreaFitter>();
                }
                SetReferences(safeArea, ("target", root.transform as RectTransform));
                TopPanel top = root.GetComponentInChildren<TopPanel>(true);
                BuildTowerPanel build = root.GetComponentInChildren<BuildTowerPanel>(true);
                ItemPanel item = root.GetComponentInChildren<ItemPanel>(true);
                InfoPanel info = root.GetComponentInChildren<InfoPanel>(true);
                SkillPanel skill = root.GetComponentInChildren<SkillPanel>(true);
                BattleControlPanel control = root.GetComponentInChildren<BattleControlPanel>(true);
                MiniMapPanel miniMap = root.GetComponentInChildren<MiniMapPanel>(true);

                SetReferences(page,
                    ("topPanel", top),
                    ("buildTowerPanel", build),
                    ("itemPanel", item),
                    ("targetInfoPanel", info),
                    ("skillPanel", skill),
                    ("battleControlPanel", control),
                    ("miniMapPanel", miniMap));

                GameObject towerSkillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerSkillContentPath);
                SetReferences(build, ("towerCardSkillPrefab", towerSkillPrefab));
                BindTopPanel(top);
                BindSlotPanel(item, ItemContentPath);
                BindSlotPanel(skill, SkillContentPath);
                BindInfoPanel(info);
                BindBattleControl(control);

                PrefabUtility.SaveAsPrefabAsset(root, BattlePagePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BindTopPanel(TopPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            Transform baseRoot = panel.transform.Find("Base");
            Transform coinText = panel.transform.Find("Coin/GoldText");
            Transform baseHpValueText = panel.transform.Find("Base/Hp/ValueText");
            Transform waveText = panel.transform.Find("Wave/WaveText");
            Transform enemyText = panel.transform.Find("Enemy/EnemyText");

            SetReferences(panel,
                ("baseHpBar", baseRoot != null ? baseRoot.GetComponent<UIProgressBar>() : null),
                ("coinText", coinText != null ? coinText.GetComponent<TMP_Text>() : null),
                ("baseHpValueText", baseHpValueText != null ? baseHpValueText.GetComponent<TMP_Text>() : null),
                ("waveText", waveText != null ? waveText.GetComponent<TMP_Text>() : null),
                ("enemyText", enemyText != null ? enemyText.GetComponent<TMP_Text>() : null));
        }

        private static void BindSlotPanel(Component panel, string contentPrefabPath)
        {
            SerializedObject serialized = new SerializedObject(panel);
            RectTransform contentRoot = serialized.FindProperty("contentRoot").objectReferenceValue as RectTransform;
            CommonSlotView[] slots = contentRoot != null ? contentRoot.GetComponentsInChildren<CommonSlotView>(true) : Array.Empty<CommonSlotView>();
            Array.Sort(slots, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            SetArray(serialized.FindProperty("initialSlots"), slots);
            serialized.FindProperty(panel is ItemPanel ? "itemContentPrefab" : "skillContentPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(contentPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindBattleControl(BattleControlPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(panel);
            SerializedProperty toggleProperty = serialized.FindProperty("autoNextWaveToggle");
            if (toggleProperty.objectReferenceValue == null)
            {
                Toggle toggle = CreateAutoWaveToggle(panel.transform);
                toggleProperty.objectReferenceValue = toggle;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindInfoPanel(InfoPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(panel);
            SerializedProperty descriptionProperty = serialized.FindProperty("descriptionText");
            if (descriptionProperty.objectReferenceValue == null)
            {
                TMP_Text description = CreateText("DescriptionText", panel.transform, 15f, TextAlignmentOptions.Center, string.Empty);
                RectTransform rect = description.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -40f);
                rect.sizeDelta = new Vector2(250f, 34f);
                descriptionProperty.objectReferenceValue = description;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Toggle CreateAutoWaveToggle(Transform parent)
        {
            GameObject root = new GameObject("AutoNextWaveToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(88f, 34f);
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 88f;
            layout.preferredHeight = 34f;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.86f, 0.74f, 0.51f, 1f);

            GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkObject.transform.SetParent(root.transform, false);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = new Vector2(14f, 0f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            Image checkmark = checkObject.GetComponent<Image>();
            checkmark.color = new Color(0.2f, 0.65f, 0.22f, 1f);

            TMP_Text label = CreateText("Label", root.transform, 14f, TextAlignmentOptions.Center, "Auto");
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(26f, 0f);
            labelRect.offsetMax = new Vector2(-4f, 0f);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            return toggle;
        }

        private static void BindCommonSlot()
        {
            EditPrefab(CommonSlotPath, root =>
            {
                CommonSlotView slot = Require(root.GetComponent<CommonSlotView>(), CommonSlotPath, nameof(CommonSlotView));
                Transform cooldownTransform = Find(root.transform, "CooldownMask");
                SetReferences(slot,
                    ("contentRoot", root.transform),
                    ("cooldownMask", cooldownTransform != null ? cooldownTransform.GetComponent<Image>() : null));
            });
        }

        private static void BindSlotContent(string path)
        {
            EditPrefab(path, root =>
            {
                BattleSlotContentView view = root.GetComponent<BattleSlotContentView>();
                if (view == null)
                {
                    view = root.AddComponent<BattleSlotContentView>();
                }

                Transform iconTransform = Find(root.transform, "Icon");
                SetReferences(view, ("iconImage", iconTransform != null ? iconTransform.GetComponent<Image>() : null));
            });
        }

        private static void BindTowerCard(string path)
        {
            EditPrefab(path, root =>
            {
                TowerBuildCardView card = Require(root.GetComponent<TowerBuildCardView>(), path, nameof(TowerBuildCardView));
                Transform selected = Find(root.transform, "SelectedFrame");
                if (selected == null)
                {
                    selected = Find(root.transform, "Selected");
                }

                Transform skillRoot = Find(root.transform, "SkillContent");
                if (skillRoot == null)
                {
                    skillRoot = Find(root.transform, "Skills");
                }

                if (skillRoot == null)
                {
                    skillRoot = Find(root.transform, "Skill");
                }

                if (skillRoot == null)
                {
                    skillRoot = root.transform;
                }

                SetReferences(card,
                    ("selectedFrame", selected != null ? selected.GetComponent<Image>() : null),
                    ("skillContentRoot", skillRoot as RectTransform));
            });
        }

        private static void BindInfoSlot()
        {
            EditPrefab(InfoSlotPath, root =>
            {
                InfoSlotView slot = Require(root.GetComponent<InfoSlotView>(), InfoSlotPath, nameof(InfoSlotView));
                Transform addValue = Find(root.transform, "AddValueText");
                if (addValue == null)
                {
                    TMP_Text text = CreateText("AddValueText", root.transform, 16f, TextAlignmentOptions.MidlineRight, string.Empty);
                    text.color = new Color(0.18f, 0.65f, 0.2f, 1f);
                    RectTransform rect = text.rectTransform;
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = new Vector2(-6f, 0f);
                    rect.sizeDelta = new Vector2(70f, 0f);
                    addValue = text.transform;
                }

                SetReferences(slot, ("addValueText", addValue.GetComponent<TMP_Text>()));
            });
        }

        private static void BindSettingsPopup()
        {
            EditPrefab(SettingsPopupPath, root =>
            {
                root.SetActive(true);
                BattleSettingsPopup popup = root.GetComponent<BattleSettingsPopup>();
                if (popup == null)
                {
                    popup = root.AddComponent<BattleSettingsPopup>();
                }

                Button language = GetButton(root.transform, "LanguageButton");
                Button sound = GetButton(root.transform, "SoundButton");
                Button restart = GetButton(root.transform, "RestartButton") ?? GetButton(root.transform, "EndBattleButton");
                Button menu = GetButton(root.transform, "MainMenuButton");
                Button close = GetButton(root.transform, "CloseButton");
                SetReferences(popup,
                    ("languageButton", language),
                    ("soundButton", sound),
                    ("restartButton", restart),
                    ("mainMenuButton", menu),
                    ("closeButton", close),
                    ("languageText", GetButtonText(language)),
                    ("soundText", GetButtonText(sound)));
            });
        }

        private static void BindTargetPreviews()
        {
            for (int i = 0; i < TargetPreviewPrefabPaths.Length; i++)
            {
                string path = TargetPreviewPrefabPaths[i];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"Battle target preview prefab does not exist: {path}");
                    continue;
                }

                EditPrefab(path, root =>
                {
                    BattleTargetPreviewDescriptor descriptor = root.GetComponent<BattleTargetPreviewDescriptor>();
                    if (descriptor == null)
                    {
                        descriptor = root.AddComponent<BattleTargetPreviewDescriptor>();
                    }

                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                    Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                    Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
                    Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
                    List<Behaviour> disabledBehaviours = new List<Behaviour>();
                    for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                    {
                        Behaviour behaviour = behaviours[behaviourIndex];
                        if (behaviour != null && behaviour != descriptor && !(behaviour is Animator))
                        {
                            disabledBehaviours.Add(behaviour);
                        }
                    }

                    SerializedObject serialized = new SerializedObject(descriptor);
                    SetArray(serialized.FindProperty("renderers"), renderers);
                    SetArray(serialized.FindProperty("animators"), animators);
                    SetArray(serialized.FindProperty("behavioursToDisable"), disabledBehaviours.ToArray());
                    SetArray(serialized.FindProperty("colliders"), colliders);
                    SetArray(serialized.FindProperty("rigidbodies"), rigidbodies);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                });
            }
        }

        private static void BuildResultPopup()
        {
            GameObject root = new GameObject("BattleResultPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BattleResultPopup));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(520f, 340f);

            Image background = root.GetComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/Panels/Battle/Common/Panels/ui_td_panel_bg_horizontal.png");
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 0.96f, 0.84f, 1f);

            TMP_Text title = CreateAnchoredText("Title", root.transform, 34f, TextAlignmentOptions.Center, new Vector2(0f, 132f), new Vector2(440f, 48f));
            TMP_Text map = CreateAnchoredText("Map", root.transform, 22f, TextAlignmentOptions.Center, new Vector2(0f, 88f), new Vector2(440f, 34f));
            TMP_Text reason = CreateAnchoredText("Reason", root.transform, 20f, TextAlignmentOptions.Center, new Vector2(0f, 30f), new Vector2(440f, 62f));
            TMP_Text reward = CreateAnchoredText("Reward", root.transform, 20f, TextAlignmentOptions.Center, new Vector2(0f, -34f), new Vector2(440f, 44f));

            Button next = CreateButton("NextButton", root.transform, new Vector2(-156f, -126f), out TMP_Text nextText);
            Button restart = CreateButton("RestartButton", root.transform, new Vector2(0f, -126f), out TMP_Text restartText);
            Button menu = CreateButton("MainMenuButton", root.transform, new Vector2(156f, -126f), out TMP_Text menuText);

            BattleResultPopup popup = root.GetComponent<BattleResultPopup>();
            SetReferences(popup,
                ("titleText", title), ("mapText", map), ("reasonText", reason), ("rewardText", reward),
                ("nextButton", next), ("nextButtonText", nextText),
                ("restartButton", restart), ("restartButtonText", restartText),
                ("mainMenuButton", menu), ("mainMenuButtonText", menuText));

            PrefabUtility.SaveAsPrefabAsset(root, ResultPopupPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Button CreateButton(string name, Transform parent, Vector2 position, out TMP_Text text)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(140f, 48f);

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/Panels/Battle/Common/Buttons/ui_td_button_green_normal.png");
            image.type = Image.Type.Sliced;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            text = CreateText("Text", buttonObject.transform, 20f, TextAlignmentOptions.Center, string.Empty);
            text.color = Color.white;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static TMP_Text CreateAnchoredText(string name, Transform parent, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 rectSize)
        {
            TMP_Text text = CreateText(name, parent, size, alignment, string.Empty);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            return text;
        }

        private static TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment, string value)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.13f, 0.05f, 1f);
            text.text = value;
            text.enableWordWrapping = true;
            return text;
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateComponent<T>(string path, List<string> errors, params string[] properties) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            T component = prefab != null ? prefab.GetComponent<T>() : null;
            if (component == null)
            {
                errors.Add($"{path}: missing {typeof(T).Name}");
                return;
            }

            ValidateSerializedReferences(path, component, errors, properties);
        }

        private static void ValidateSerializedReferences(string path, Component component, List<string> errors, params string[] properties)
        {
            SerializedObject serialized = new SerializedObject(component);
            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(properties[i]);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add($"{path}: {component.GetType().Name}.{properties[i]} is not assigned");
                }
            }
        }

        private static void ValidateChildComponent<T>(string path, List<string> errors, params string[] properties) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            T component = prefab != null ? prefab.GetComponentInChildren<T>(true) : null;
            if (component == null)
            {
                errors.Add($"{path}: missing child {typeof(T).Name}");
                return;
            }

            ValidateSerializedReferences(path, component, errors, properties);
        }

        private static void ValidateNoMissingScripts(string path, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"{path}: prefab does not exist");
                return;
            }

            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject target = transforms[i].gameObject;
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
                if (missingCount > 0)
                {
                    errors.Add($"{path}: {GetHierarchyPath(target.transform)} has {missingCount} missing script(s)");
                }
            }
        }

        private static void ValidateLoadedBattlePagesNoMissingScripts(List<string> errors)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    BattlePage[] pages = roots[rootIndex].GetComponentsInChildren<BattlePage>(true);
                    for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
                    {
                        Transform[] transforms = pages[pageIndex].GetComponentsInChildren<Transform>(true);
                        for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                        {
                            GameObject target = transforms[transformIndex].gameObject;
                            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
                            if (missingCount > 0)
                            {
                                errors.Add($"Loaded scene {scene.name}: {GetHierarchyPath(target.transform)} has {missingCount} missing script(s)");
                            }
                        }
                    }
                }
            }
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }

        private static void SetReferences(Component component, params (string name, UnityEngine.Object value)[] references)
        {
            if (component == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(component);
            for (int i = 0; i < references.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(references[i].name);
                if (property != null)
                {
                    property.objectReferenceValue = references[i].value;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = Find(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Button GetButton(Transform root, string name)
        {
            Transform transform = Find(root, name);
            return transform != null ? transform.GetComponent<Button>() : null;
        }

        private static TMP_Text GetButtonText(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static T Require<T>(T value, string path, string label) where T : UnityEngine.Object
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{path}: missing {label}");
            }

            return value;
        }
    }
}
#endif
