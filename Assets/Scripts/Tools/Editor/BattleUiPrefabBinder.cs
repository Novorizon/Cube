#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
        private const string SkillPanelPrefabPath = "Assets/Arts/UI/Panels/Battle/Skill/SkillPanel.prefab";
        private const string SkillSlotPath = "Assets/Arts/UI/Panels/Battle/Skill/SkillSlot.prefab";
        private const string SkillSlotSmallPath = "Assets/Arts/UI/Panels/Battle/Skill/SkillSlotSmall.prefab";
        private const string ItemContentPath = "Assets/Arts/UI/Panels/Battle/Item.prefab";
        private const string SkillContentPath = "Assets/Arts/UI/Panels/Battle/Skill/Skill.prefab";
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
            BindCommonSlot();
            BindSkillSlot(SkillSlotPath);
            BindSkillSlot(SkillSlotSmallPath);
            BindSkillPanelPrefab();
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

        [MenuItem("Tools/Battle UI/Apply Radial Skill Panel")]
        public static void ApplyRadialSkillPanel()
        {
            EnsureFontAtlasReadable();
            BindSlotContent(SkillContentPath);
            BindSkillSlot(SkillSlotPath);
            BindSkillSlot(SkillSlotSmallPath);
            BindSkillPanelPrefab();
            BindRadialSkillPanelOnBattlePage();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateRadialSkillPanel();
            RenderRadialSkillPreview();
            Debug.Log("Battle radial skill panel completed.");
        }

        [MenuItem("Tools/Battle UI/Render Radial Skill Preview")]
        public static void RenderRadialSkillPreview()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string previewPath = Path.Combine(projectRoot, "Logs", "RadialSkillPreview.png");
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture renderTexture = null;
            Texture2D previewTexture = null;

            try
            {
                cameraObject = new GameObject("RadialSkillPreviewCamera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.065f, 0.095f, 1f);
                int previewLayer = LayerMask.NameToLayer("UI");
                if (previewLayer < 0)
                {
                    previewLayer = 5;
                }

                camera.cullingMask = 1 << previewLayer;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;

                renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RadialSkillPreviewRT"
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;

                canvasObject = new GameObject("RadialSkillPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvasObject.layer = previewLayer;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject panelPrefab = Require(
                    AssetDatabase.LoadAssetAtPath<GameObject>(SkillPanelPrefabPath),
                    SkillPanelPrefabPath,
                    nameof(SkillPanel));
                GameObject panelInstance = PrefabUtility.InstantiatePrefab(panelPrefab, canvasObject.transform) as GameObject;
                if (panelInstance == null)
                {
                    throw new InvalidOperationException($"Could not instantiate {SkillPanelPrefabPath} for preview.");
                }

                panelInstance.name = "SkillPanelPreview";
                SkillPanel skillPanel = Require(
                    panelInstance.GetComponent<SkillPanel>(),
                    SkillPanelPrefabPath,
                    nameof(SkillPanel));

                RectTransform panelRect = panelInstance.transform as RectTransform;
                panelRect.anchorMin = new Vector2(1f, 0f);
                panelRect.anchorMax = new Vector2(1f, 0f);
                panelRect.pivot = new Vector2(1f, 0f);
                panelRect.anchoredPosition = new Vector2(-18f, 14f);
                panelRect.sizeDelta = new Vector2(538.5847f, 445.848f);
                panelRect.localRotation = Quaternion.identity;
                panelRect.localScale = Vector3.one;

                CommonSlotView[] slots = skillPanel.GetComponentsInChildren<CommonSlotView>(true);
                string[] iconPaths =
                {
                    "Assets/Arts/UI/Icons/Skills/ui_td_skill_fireball_icon.png",
                    "Assets/Arts/UI/Icons/Skills/ice_snowflake_transparent.png",
                    "Assets/Arts/UI/Icons/Skills/ui_td_skill_bomb_icon.png",
                    "Assets/Arts/UI/Icons/Skills/ui_td_skill_bomb_icon.png",
                    "Assets/Arts/UI/Icons/Skills/lightning.png"
                };
                int[] counts = { 1, 8, 6, 5, 12 };
                for (int i = 0; i < slots.Length && i < iconPaths.Length; i++)
                {
                    slots[i].gameObject.SetActive(true);
                    Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPaths[i]);
                    slots[i].Init(i + 1, string.Empty, counts[i], icon, null);
                }

                if (slots.Length > 2)
                {
                    slots[2].SetCooldown(5f, 8f);
                }

                SetLayerRecursively(panelInstance, previewLayer);

                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                previewTexture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
                previewTexture.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
                previewTexture.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetDirectoryName(previewPath));
                File.WriteAllBytes(previewPath, previewTexture.EncodeToPNG());
                Debug.Log($"Battle radial skill preview rendered: {previewPath}");
            }
            finally
            {
                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                }

                if (renderTexture != null)
                {
                    if (cameraObject != null)
                    {
                        Camera previewCamera = cameraObject.GetComponent<Camera>();
                        if (previewCamera != null)
                        {
                            previewCamera.targetTexture = null;
                        }
                    }

                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (canvasObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                }

                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
            }
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
            ValidateComponent<UISafeAreaFitter>(BattlePagePath, errors, "target");
            ValidateChildComponent<TopPanel>(BattlePagePath, errors,
                "baseHpBar", "coinText", "baseHpValueText", "waveText", "enemyText");
            ValidateChildComponent<BuildTowerPanel>(BattlePagePath, errors,
                "contentRoot", "cardPrefab");
            ValidateChildComponent<ItemPanel>(BattlePagePath, errors,
                "contentRoot", "slotPrefab", "itemContentPrefab");
            ValidateChildComponent<SkillPanel>(BattlePagePath, errors,
                "contentRoot", "slotPrefab");
            ValidateChildComponent<InfoPanel>(BattlePagePath, errors,
                "canvasGroup", "targetIconImage", "targetNameText", "levelText", "descriptionText", "statusRoot", "contentRoot",
                "infoSlotPrefab", "actionRoot");
            ValidateInfoPanelActions(BattlePagePath, errors);
            ValidateChildComponent<BattleControlPanel>(BattlePagePath, errors,
                "speed1Button", "speed2Button", "speed3Button", "autoNextWaveToggle", "pauseButton", "playButton",
                "soundButton", "settingButton");
            ValidateComponent<BattleSettingsPopup>(SettingsPopupPath, errors,
                "languageButton", "soundButton", "restartButton", "mainMenuButton", "closeButton", "languageText", "soundText");
            ValidateComponent<BattleResultPopup>(ResultPopupPath, errors,
                "titleText", "mapText", "reasonText", "rewardText", "nextButton", "nextButtonText",
                "restartButton", "restartButtonText", "mainMenuButton", "mainMenuButtonText");
            ValidateComponent<CommonSlotView>(CommonSlotPath, errors, "contentRoot", "button");
            ValidateComponent<CommonSlotView>(SkillSlotPath, errors,
                "contentRoot", "contentView", "button", "countText", "countBadge", "disabledMask", "cooldownMask", "cooldownText");
            ValidateComponent<CommonSlotView>(SkillSlotSmallPath, errors,
                "contentRoot", "contentView", "button", "countText", "countBadge", "disabledMask", "cooldownMask", "cooldownText");
            ValidateComponent<SkillPanel>(SkillPanelPrefabPath, errors,
                "contentRoot", "slotPrefab");
            ValidateComponent<InfoSlotView>(InfoSlotPath, errors, "nameText", "valueText", "addValueText");
            ValidateComponent<TowerBuildCardView>(TowerCardPath, errors,
                "button", "iconImage", "normalFrame", "selectedFrame", "nameText", "descriptionText",
                "costText", "damageValueText");
            ValidateTowerCardSlots(TowerCardPath, errors);
            ValidateComponent<BattleSlotContentView>(ItemContentPath, errors, "iconImage");
            ValidateComponent<BattleSlotContentView>(SkillContentPath, errors, "iconImage");
            ValidateComponent<WorldHpBarView>(WorldHpBarPath, errors, "hpBar", "nameText", "rectTransform");
            ValidateNoMissingScripts(BattlePagePath, errors);
            ValidateNoMissingScripts(SettingsPopupPath, errors);
            ValidateNoMissingScripts(ResultPopupPath, errors);
            ValidateNoMissingScripts(CommonSlotPath, errors);
            ValidateNoMissingScripts(SkillSlotPath, errors);
            ValidateNoMissingScripts(SkillSlotSmallPath, errors);
            ValidateNoMissingScripts(SkillPanelPrefabPath, errors);
            ValidateNoMissingScripts(ItemContentPath, errors);
            ValidateNoMissingScripts(SkillContentPath, errors);
            ValidateNoMissingScripts(TowerCardPath, errors);
            ValidateNoMissingScripts(InfoSlotPath, errors);
            ValidateNoMissingScripts(WorldHpBarPath, errors);
            ValidateLoadedBattlePagesNoMissingScripts(errors);

            GameObject skillPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPanelPrefabPath);
            SkillPanel authoredSkillPanel = skillPanelPrefab != null ? skillPanelPrefab.GetComponent<SkillPanel>() : null;
            if (authoredSkillPanel != null)
            {
                ValidateSkillPanelSlots(SkillPanelPrefabPath, authoredSkillPanel, errors);
            }

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
                UISafeAreaFitter safeArea = root.GetComponent<UISafeAreaFitter>();
                if (safeArea == null)
                {
                    safeArea = root.AddComponent<UISafeAreaFitter>();
                }
                SetReferences(safeArea, ("target", root.transform as RectTransform));
                TopPanel top = root.GetComponentInChildren<TopPanel>(true);
                BuildTowerPanel build = root.GetComponentInChildren<BuildTowerPanel>(true);
                ItemPanel item = root.GetComponentInChildren<ItemPanel>(true);
                InfoPanel info = root.GetComponentInChildren<InfoPanel>(true);
                SkillPanel skill = GetOrCreateSkillPanel(root);
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

                BindTopPanel(top);
                BindSlotPanel(item, ItemContentPath);
                BindInfoPanel(info);
                BindBattleControl(control);

                PrefabUtility.SaveAsPrefabAsset(root, BattlePagePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BindRadialSkillPanelOnBattlePage()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BattlePagePath);
            try
            {
                BattlePage page = Require(root.GetComponent<BattlePage>(), BattlePagePath, nameof(BattlePage));
                SkillPanel skill = GetOrCreateSkillPanel(root);
                SetReferences(page, ("skillPanel", skill));
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
            SerializedProperty contentRootProperty = serialized.FindProperty("contentRoot");
            RectTransform contentRoot = contentRootProperty.objectReferenceValue as RectTransform;
            if (contentRoot == null)
            {
                contentRoot = panel.transform as RectTransform;
                contentRootProperty.objectReferenceValue = contentRoot;
            }

            CommonSlotView[] slots = contentRoot != null ? contentRoot.GetComponentsInChildren<CommonSlotView>(true) : Array.Empty<CommonSlotView>();
            Array.Sort(slots, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            SetArray(serialized.FindProperty("initialSlots"), slots);
            serialized.FindProperty("itemContentPrefab").objectReferenceValue =
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
            Transform level = Find(panel.transform, "Level");
            serialized.FindProperty("levelText").objectReferenceValue =
                level != null ? level.GetComponent<TMP_Text>() : null;
            Transform status = Find(panel.transform, "Status");
            serialized.FindProperty("statusRoot").objectReferenceValue =
                status != null ? status.gameObject : null;
            status?.gameObject.SetActive(false);
            Transform actionRoot = panel.transform.Find("Action");
            if (actionRoot != null)
            {
                ItemPanel copiedItemPanel = actionRoot.GetComponent<ItemPanel>();
                if (copiedItemPanel != null)
                {
                    UnityEngine.Object.DestroyImmediate(copiedItemPanel);
                }

                CommonSlotView[] copiedSlotViews = actionRoot.GetComponentsInChildren<CommonSlotView>(true);
                for (int i = 0; i < copiedSlotViews.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(copiedSlotViews[i]);
                }

                Button[] actionButtons = actionRoot.GetComponentsInChildren<Button>(true);
                Array.Sort(actionButtons, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    if (actionButtons[i] != null && actionButtons[i].targetGraphic != null)
                    {
                        actionButtons[i].targetGraphic.raycastTarget = true;
                    }
                }

                serialized.FindProperty("actionRoot").objectReferenceValue = actionRoot.gameObject;
                SetArray(serialized.FindProperty("actionButtons"), actionButtons);
            }

            RectTransform contentRoot = serialized.FindProperty("contentRoot").objectReferenceValue as RectTransform;
            InfoSlotView[] infoSlots = contentRoot != null
                ? contentRoot.GetComponentsInChildren<InfoSlotView>(true)
                : Array.Empty<InfoSlotView>();
            Array.Sort(infoSlots, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            SetArray(serialized.FindProperty("initialSlots"), infoSlots);

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

        private static void BindSkillSlot(string path)
        {
            EditPrefab(path, root =>
            {
                CommonSlotView slot = Require(root.GetComponent<CommonSlotView>(), path, nameof(CommonSlotView));
                Button button = root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true);
                Transform frameTransform = Find(root.transform, "Frame");
                Image frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
                Transform contentRoot = Find(root.transform, "ContentRoot");
                Transform skillContent = contentRoot != null ? Find(contentRoot, "Skill") : null;
                BattleSlotContentView contentView = skillContent != null ? skillContent.GetComponent<BattleSlotContentView>() : null;
                Transform countText = Find(root.transform, "CountText");
                Transform countBadge = Find(root.transform, "CountBadge");
                Transform disabledMask = Find(root.transform, "DisabledMask");
                Transform cooldownMask = Find(root.transform, "CooldownMask");
                Transform cooldownText = Find(root.transform, "CooldownText");

                Require(button, path, nameof(Button));
                Require(frame, path, "Frame Image");
                Require(contentRoot, path, "ContentRoot");
                Require(contentView, path, "ContentRoot/Skill");
                Require(countText, path, "CountText");
                Require(countBadge, path, "CountBadge");
                Require(disabledMask, path, "DisabledMask");
                Require(cooldownMask, path, "CooldownMask");
                Require(cooldownText, path, "CooldownText");

                button.targetGraphic = frame;
                SetReferences(slot,
                    ("contentRoot", contentRoot),
                    ("contentView", contentView),
                    ("button", button),
                    ("countText", countText.GetComponent<TMP_Text>()),
                    ("countBadge", countBadge.gameObject),
                    ("disabledMask", disabledMask.gameObject),
                    ("cooldownMask", cooldownMask.GetComponent<Image>()),
                    ("cooldownText", cooldownText.GetComponent<TMP_Text>()));
            });
        }

        private static void BindSkillPanelPrefab()
        {
            EditPrefab(SkillPanelPrefabPath, root =>
            {
                SkillPanel panel = Require(root.GetComponent<SkillPanel>(), SkillPanelPrefabPath, nameof(SkillPanel));
                CommonSlotView featuredSlot = null;
                List<CommonSlotView> smallSlots = new List<CommonSlotView>();
                CommonSlotView[] allSlots = root.GetComponentsInChildren<CommonSlotView>(true);

                for (int i = 0; i < allSlots.Length; i++)
                {
                    CommonSlotView slot = allSlots[i];
                    string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(slot.gameObject);
                    bool isFeatured = sourcePath == SkillSlotPath || string.Equals(slot.name, "SkillSlot", StringComparison.Ordinal);
                    bool isSmall = sourcePath == SkillSlotSmallPath || slot.name.StartsWith("SkillSlotSmall", StringComparison.Ordinal);

                    if (isFeatured && !isSmall)
                    {
                        if (featuredSlot != null)
                        {
                            throw new InvalidOperationException($"{SkillPanelPrefabPath}: expected exactly one {SkillSlotPath} instance.");
                        }

                        featuredSlot = slot;
                    }
                    else if (isSmall)
                    {
                        smallSlots.Add(slot);
                    }
                    else
                    {
                        throw new InvalidOperationException($"{SkillPanelPrefabPath}: unsupported slot instance {slot.name}.");
                    }
                }

                if (featuredSlot == null || smallSlots.Count != 4)
                {
                    throw new InvalidOperationException(
                        $"{SkillPanelPrefabPath}: expected one {SkillSlotPath} and four {SkillSlotSmallPath} instances, " +
                        $"found featured={featuredSlot != null}, small={smallSlots.Count}.");
                }

                smallSlots.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
                RepairSmallSkillSlotLayout(smallSlots);
                CommonSlotView[] orderedSlots = new CommonSlotView[5];
                orderedSlots[0] = featuredSlot;
                for (int i = 0; i < smallSlots.Count; i++)
                {
                    orderedSlots[i + 1] = smallSlots[i];
                }

                GameObject smallSlotPrefabAsset = Require(
                    AssetDatabase.LoadAssetAtPath<GameObject>(SkillSlotSmallPath),
                    SkillSlotSmallPath,
                    nameof(SkillSlotSmallPath));
                CommonSlotView smallSlotPrefab = Require(
                    smallSlotPrefabAsset.GetComponent<CommonSlotView>(),
                    SkillSlotSmallPath,
                    nameof(CommonSlotView));
                root.SetActive(true);
                SerializedObject serialized = new SerializedObject(panel);
                serialized.FindProperty("contentRoot").objectReferenceValue = root.transform;
                serialized.FindProperty("slotPrefab").objectReferenceValue = smallSlotPrefab;
                SetArray(serialized.FindProperty("initialSlots"), orderedSlots);
                serialized.FindProperty("featuredSkillId").intValue = 50000001;
                serialized.FindProperty("maxVisibleSlots").intValue = orderedSlots.Length;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void RepairSmallSkillSlotLayout(IReadOnlyList<CommonSlotView> smallSlots)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<Vector2> positions = new HashSet<Vector2>();
            for (int i = 0; i < smallSlots.Count; i++)
            {
                names.Add(smallSlots[i].name);
                positions.Add(((RectTransform)smallSlots[i].transform).anchoredPosition);
            }

            if (names.Count == smallSlots.Count && positions.Count == smallSlots.Count)
            {
                return;
            }

            Vector2[] authoredPositions =
            {
                new Vector2(-345f, 61f),
                new Vector2(-248f, 319f),
                new Vector2(-326f, 204f),
                new Vector2(-95f, 342f)
            };

            for (int i = 0; i < smallSlots.Count; i++)
            {
                CommonSlotView slot = smallSlots[i];
                slot.gameObject.name = $"SkillSlotSmall{i + 1}";
                RectTransform rect = (RectTransform)slot.transform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(64f, 64f);
                rect.anchoredPosition = authoredPositions[i];
                rect.localRotation = Quaternion.identity;
            }

            Debug.LogWarning(
                $"{SkillPanelPrefabPath}: repaired invalid nested-prefab transform overrides for the four small skill slots.");
        }

        private static SkillPanel GetOrCreateSkillPanel(GameObject battlePageRoot)
        {
            GameObject panelPrefab = Require(
                AssetDatabase.LoadAssetAtPath<GameObject>(SkillPanelPrefabPath),
                SkillPanelPrefabPath,
                nameof(SkillPanel));
            SkillPanel panel = null;
            SkillPanel[] existingPanels = battlePageRoot.GetComponentsInChildren<SkillPanel>(true);
            for (int i = 0; i < existingPanels.Length; i++)
            {
                SkillPanel candidate = existingPanels[i];
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate.gameObject);
                if (panel == null && sourcePath == SkillPanelPrefabPath)
                {
                    panel = candidate;
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(candidate.gameObject);
            }

            if (panel == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(panelPrefab, battlePageRoot.transform) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException($"Could not instantiate {SkillPanelPrefabPath} in {BattlePagePath}.");
                }

                panel = Require(instance.GetComponent<SkillPanel>(), SkillPanelPrefabPath, nameof(SkillPanel));
            }

            panel.gameObject.name = "SkillPanel";
            panel.gameObject.SetActive(true);
            BattleControlPanel controlPanel = battlePageRoot.GetComponentInChildren<BattleControlPanel>(true);
            if (controlPanel != null)
            {
                panel.transform.SetSiblingIndex(controlPanel.transform.GetSiblingIndex());
            }

            return panel;
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
                Transform normal = Find(root.transform, "normal");
                Transform selected = Find(root.transform, "selected");
                Transform towerIcon = Find(root.transform, "TowerIcon");
                Transform towerName = Find(root.transform, "TowerName");
                Transform description = Find(root.transform, "DescriptionText");
                Transform cost = Find(root.transform, "CostText");
                Transform skill1 = Find(root.transform, "Skill1");
                Transform skill2 = Find(root.transform, "Skill2");
                Transform skill3 = Find(root.transform, "Skill3");
                TMP_Text damageValue = skill1 != null
                    ? skill1.GetComponentInChildren<TMP_Text>(true)
                    : null;

                SetReferences(card,
                    ("button", root.GetComponent<Button>()),
                    ("iconImage", towerIcon != null ? towerIcon.GetComponent<Image>() : null),
                    ("normalFrame", normal != null ? normal.GetComponent<Image>() : null),
                    ("selectedFrame", selected != null ? selected.GetComponent<Image>() : null),
                    ("nameText", towerName != null ? towerName.GetComponent<TMP_Text>() : null),
                    ("descriptionText", description != null ? description.GetComponent<TMP_Text>() : null),
                    ("costText", cost != null ? cost.GetComponent<TMP_Text>() : null),
                    ("damageValueText", damageValue));

                GameObject[] skillSlots =
                {
                    skill2 != null ? skill2.gameObject : null,
                    skill3 != null ? skill3.gameObject : null,
                };
                BattleSlotContentView[] skillViews =
                {
                    skill2 != null ? skill2.GetComponent<BattleSlotContentView>() : null,
                    skill3 != null ? skill3.GetComponent<BattleSlotContentView>() : null,
                };
                SerializedObject serialized = new SerializedObject(card);
                SetArray(serialized.FindProperty("skillSlots"), skillSlots);
                SetArray(serialized.FindProperty("skillViews"), skillViews);
                serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
            }
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

        private static void ValidateRadialSkillPanel()
        {
            List<string> errors = new List<string>();
            ValidateComponent<CommonSlotView>(SkillSlotPath, errors,
                "contentRoot", "contentView", "button", "countText", "countBadge", "disabledMask", "cooldownMask", "cooldownText");
            ValidateComponent<CommonSlotView>(SkillSlotSmallPath, errors,
                "contentRoot", "contentView", "button", "countText", "countBadge", "disabledMask", "cooldownMask", "cooldownText");
            ValidateComponent<SkillPanel>(SkillPanelPrefabPath, errors,
                "contentRoot", "slotPrefab");
            ValidateNoMissingScripts(SkillSlotPath, errors);
            ValidateNoMissingScripts(SkillSlotSmallPath, errors);
            ValidateNoMissingScripts(SkillPanelPrefabPath, errors);
            ValidateNoMissingScripts(BattlePagePath, errors);

            GameObject authoredPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPanelPrefabPath);
            SkillPanel authoredPanel = authoredPanelPrefab != null ? authoredPanelPrefab.GetComponent<SkillPanel>() : null;
            if (authoredPanel != null)
            {
                ValidateSkillPanelSlots(SkillPanelPrefabPath, authoredPanel, errors);
            }

            GameObject pagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattlePagePath);
            SkillPanel[] panels = pagePrefab != null ? pagePrefab.GetComponentsInChildren<SkillPanel>(true) : Array.Empty<SkillPanel>();
            if (panels.Length != 1)
            {
                errors.Add($"{BattlePagePath}: expected exactly one {nameof(SkillPanel)}, found {panels.Length}");
            }
            else
            {
                SkillPanel panel = panels[0];
                ValidateSerializedReferences(BattlePagePath, panel, errors,
                    "contentRoot", "slotPrefab");
                ValidateSkillPanelSlots(BattlePagePath, panel, errors);

                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
                if (sourcePath != SkillPanelPrefabPath)
                {
                    errors.Add($"{BattlePagePath}: {nameof(SkillPanel)} must be an instance of {SkillPanelPrefabPath}");
                }

                if (!panel.gameObject.activeSelf)
                {
                    errors.Add($"{BattlePagePath}: radial {nameof(SkillPanel)} must be active");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Battle radial skill panel validation failed:\n" + string.Join("\n", errors));
            }
        }

        private static void ValidateSkillPanelSlots(string path, SkillPanel panel, List<string> errors)
        {
            SerializedObject serialized = new SerializedObject(panel);
            SerializedProperty slotPrefab = serialized.FindProperty("slotPrefab");
            string slotPrefabPath = slotPrefab != null
                ? AssetDatabase.GetAssetPath(slotPrefab.objectReferenceValue)
                : string.Empty;
            if (slotPrefabPath != SkillSlotSmallPath)
            {
                errors.Add($"{path}: {nameof(SkillPanel)}.slotPrefab must use {SkillSlotSmallPath}");
            }

            SerializedProperty initialSlots = serialized.FindProperty("initialSlots");
            if (initialSlots == null || initialSlots.arraySize != 5)
            {
                errors.Add($"{path}: {nameof(SkillPanel)} must contain one featured and four auxiliary slots");
                return;
            }

            for (int i = 0; i < initialSlots.arraySize; i++)
            {
                CommonSlotView slot = initialSlots.GetArrayElementAtIndex(i).objectReferenceValue as CommonSlotView;
                if (slot == null)
                {
                    errors.Add($"{path}: {nameof(SkillPanel)}.initialSlots[{i}] is not assigned");
                    continue;
                }

                bool expectsSmall = i > 0;
                bool isSmall = slot.name.StartsWith("SkillSlotSmall", StringComparison.Ordinal);
                if (expectsSmall != isSmall)
                {
                    string expectedPath = expectsSmall ? SkillSlotSmallPath : SkillSlotPath;
                    errors.Add($"{path}: {nameof(SkillPanel)}.initialSlots[{i}] must use {expectedPath}");
                }
            }
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

        private static void ValidateTowerCardSlots(string path, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            TowerBuildCardView card = prefab != null ? prefab.GetComponent<TowerBuildCardView>() : null;
            if (card == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(card);
            ValidateReferenceArray(serialized.FindProperty("skillSlots"), path, nameof(TowerBuildCardView), "skillSlots", 2, errors);
            ValidateReferenceArray(serialized.FindProperty("skillViews"), path, nameof(TowerBuildCardView), "skillViews", 2, errors);
        }

        private static void ValidateInfoPanelActions(string path, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            InfoPanel panel = prefab != null ? prefab.GetComponentInChildren<InfoPanel>(true) : null;
            if (panel == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(panel);
            ValidateReferenceArray(serialized.FindProperty("actionButtons"), path, nameof(InfoPanel), "actionButtons", 3, errors);
        }

        private static void ValidateReferenceArray(
            SerializedProperty property,
            string path,
            string componentName,
            string propertyName,
            int expectedSize,
            List<string> errors)
        {
            if (property == null || !property.isArray || property.arraySize != expectedSize)
            {
                errors.Add($"{path}: {componentName}.{propertyName} must contain {expectedSize} references");
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    errors.Add($"{path}: {componentName}.{propertyName}[{i}] is not assigned");
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
