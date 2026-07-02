using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldMainPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/WorldMainPanel.prefab";
        private const string BuildSlotPrefabPath = "Assets/Arts/UI/Panels/BuildSlot.prefab";
        private static readonly HashSet<string> MissingBuildIconWarnings = new HashSet<string>();
        private static readonly Color BuildCostEnoughColor = new Color(0.23f, 0.18f, 0.12f, 1f);
        private static readonly Color BuildCostMissingColor = new Color(0.82f, 0.12f, 0.08f, 1f);

        public static WorldMainPanel Instance { get; private set; }

        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);
        private readonly WorldTopBarPanel topBarPanel = new WorldTopBarPanel();
        private readonly WorldBuildPanel buildPanel = new WorldBuildPanel();
        private readonly WorldBottomBarPanel bottomBarPanel = new WorldBottomBarPanel();
        private readonly WorldEntryBarPanel entryBarPanel = new WorldEntryBarPanel();
        private readonly WorldRightBarPanel rightBarPanel = new WorldRightBarPanel();
        private readonly WorldBagPanel bagPanel = new WorldBagPanel();
        private readonly WorldProductionPanel productionPanel = new WorldProductionPanel();
        private readonly WorldFarmPanel farmPanel = new WorldFarmPanel();
        private readonly WorldBuildingDetailPanel buildingDetailPanel = new WorldBuildingDetailPanel();

        private RectTransform rootRect;
        private TMP_Text toolKitText;
        private TMP_Text currentModeText;
        private TMP_Text selectedSummaryText;
        private GameObject buildPanelRoot;
        private Transform buildButtonContainer;
        private GameObject bagPanelRoot;
        private GameObject productionPanelRoot;
        private GameObject farmPanelRoot;
        private GameObject buildingDetailPanelRoot;
        private GameObject toolKitPanelRoot;
        private GameObject questPanelRoot;
        private GameObject buildSlotPrefab;
        private float nextRefreshTime;
        private int lastBuildListStateHash = int.MinValue;

        public void RefreshNow()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        protected override void OnCreate()
        {
            BuildIfNeeded();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        protected override void OnOpen(object args)
        {
            BuildIfNeeded();
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, TechChangedMessage>(
                WorldMessageTopic.TechChanged,
                _ =>
                {
                    lastBuildListStateHash = int.MinValue;
                    RefreshNow();
                }));
            RefreshNow();
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            bottomBarPanel.Dispose();
            bagPanel.Dispose();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildIfNeeded();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        public void BuildIfNeeded()
        {
            if (rootRect != null)
            {
                return;
            }

            rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            if (TryBindExistingLayout())
            {
                return;
            }

            Debug.LogError($"[{nameof(WorldMainPanel)}] Invalid prefab layout. Please rebuild or fix prefab: {PrefabPath}");
        }

        private bool TryBindExistingLayout()
        {
            bottomBarPanel.Dispose();
            bagPanel.Dispose();

            Transform buildPanelTransform = rootRect.Find("BuildPanel");
            Transform topBar = rootRect.Find("TopBar");
            Transform resources = rootRect.Find("Resources");
            Transform bottomBar = rootRect.Find("BottomBar");
            Transform leftBar = rootRect.Find("LeftBar");
            Transform rightBar = rootRect.Find("RightBar");
            Transform bagPanelTransform = rootRect.Find("BottomBar/BagPanel") ?? rootRect.Find("BagPanel");
            Transform productionPanelTransform = rootRect.Find("ProductionPanel") ?? rootRect.Find("DebugPanel");
            Transform buildingDetailPanelTransform = rootRect.Find("BuildingDetailPanel");
            Transform farmPanelTransform = rootRect.Find("FarmPanel");
            if (buildPanelTransform == null)
            {
                return false;
            }

            topBarPanel.Bind(topBar, resources, ShowWorldMenuPanel);
            buildPanel.Bind(buildPanelTransform, CloseBuildPanelFromButton, OnBuildCategoryChanged);
            bottomBarPanel.Bind(bottomBar, ToggleBagPanel, ToggleBuildPanel, ShowTechTreePanel);
            entryBarPanel.Bind(leftBar, () => ToggleExclusivePanel(questPanelRoot));
            rightBarPanel.Bind(rightBar, ToggleProductionPanel, () => ToggleExclusivePanel(toolKitPanelRoot), () => WorldGameplayController.Instance?.SelectFarmAreaMode(), ShowTechTreePanel);
            bagPanel.Bind(bagPanelTransform, () => SetPanelVisible(bagPanelRoot, false));
            productionPanel.Bind(productionPanelTransform);
            buildingDetailPanel.Bind(buildingDetailPanelTransform, HideBuildingDetailPanel, RefreshNow);
            farmPanel.Bind(farmPanelTransform, HideFarmPanel, cropId => WorldGameplayController.Instance != null && WorldGameplayController.Instance.TryPlantSelectedFarm(cropId));

            currentModeText = bottomBarPanel.CurrentModeText;
            selectedSummaryText = bottomBarPanel.SelectedSummaryText;
            buildPanelRoot = buildPanel.Root;
            buildButtonContainer = buildPanel.ButtonContainer;
            bagPanelRoot = bagPanel.Root;
            productionPanelRoot = productionPanel.Root;
            farmPanelRoot = farmPanel.Root;
            buildingDetailPanelRoot = buildingDetailPanel.Root;
            toolKitPanelRoot = FindGameObject("ToolKitPanel");
            questPanelRoot = FindGameObject("QuestPanel");
            toolKitText = FindText(toolKitPanelRoot != null ? toolKitPanelRoot.transform : null, "Content");

            if (buildButtonContainer == null)
            {
                toolKitText = null;
                currentModeText = null;
                selectedSummaryText = null;
                buildPanelRoot = null;
                buildButtonContainer = null;
                bagPanelRoot = null;
                productionPanelRoot = null;
                farmPanelRoot = null;
                buildingDetailPanelRoot = null;
                toolKitPanelRoot = null;
                questPanelRoot = null;
                return false;
            }

            BindPopupCloseButtons();
            RefreshBagToggle();
            return true;
        }

        private void BindPopupCloseButtons()
        {
            WorldPanelBindingUtility.BindButton(rootRect.Find("ToolKitPanel/Close"), () => SetPanelVisible(toolKitPanelRoot, false), "ToolKit close");
            WorldPanelBindingUtility.BindButton(rootRect.Find("ToolKitPanel/Upgrade"), UpgradeToolKitFromButton, "ToolKit upgrade");
            WorldPanelBindingUtility.BindButton(rootRect.Find("QuestPanel/Close"), () => SetPanelVisible(questPanelRoot, false), "Quest close");
        }

        private void ToggleBuildPanel()
        {
            if (buildPanelRoot == null)
            {
                return;
            }

            bool nextVisible = !buildPanelRoot.activeSelf;
            HideFloatingPanels();
            SetPanelVisible(buildPanelRoot, nextVisible);
            RefreshNow();
        }

        private void CloseBuildPanelFromButton()
        {
            SetPanelVisible(buildPanelRoot, false);
            RefreshNow();
        }

        private void ShowTechTreePanel()
        {
            ShowTechTreePanelAsync().Forget();
        }

        private void ShowWorldMenuPanel()
        {
            ShowWorldMenuPanelAsync().Forget();
        }

        private async System.Threading.Tasks.Task ShowTechTreePanelAsync()
        {
            await UIManager.Instance.Panels.ShowAsync(TechTreePanel.PrefabPath);
        }

        private async System.Threading.Tasks.Task ShowWorldMenuPanelAsync()
        {
            HideFloatingPanels();
            await UIManager.Instance.Panels.ShowAsync(WorldMenuPanel.PrefabPath);
        }

        private void OnBuildCategoryChanged()
        {
            lastBuildListStateHash = int.MinValue;
            RefreshNow();
        }

        private void OnLanguageChanged()
        {
            lastBuildListStateHash = int.MinValue;
            bottomBarPanel.RefreshSlots();
            bagPanel.RefreshSlots();
            RefreshNow();
        }

        private void ToggleBagPanel()
        {
            ToggleExclusivePanel(bagPanelRoot);
        }

        private void ToggleProductionPanel()
        {
            ToggleExclusivePanel(productionPanelRoot);
        }

        public void ShowFarmPanel(Farm farm)
        {
            if (farmPanelRoot == null)
            {
                Debug.LogWarning("Show farm panel failed. FarmPanel is missing from WorldMainPanel prefab.");
                return;
            }

            if (farm == null)
            {
                Debug.LogWarning("Show farm panel failed. Farm is null.");
                return;
            }

            HideFloatingPanels();
            farmPanel.Show(farm);
            RefreshNow();
        }

        public void ShowBuildingDetailPanel(WorldBuilding building)
        {
            if (buildingDetailPanelRoot == null)
            {
                Debug.LogWarning("Show building detail panel failed. BuildingDetailPanel is missing from WorldMainPanel prefab.");
                return;
            }

            if (building == null)
            {
                Debug.LogWarning("Show building detail panel failed. Building is null.");
                return;
            }

            HideFloatingPanels();
            buildingDetailPanel.Show(building);
            RefreshNow();
        }

        public void HideFarmPanel()
        {
            farmPanel.Hide();
        }

        public void HideBuildingDetailPanel()
        {
            buildingDetailPanel.Hide();
        }

        private void ToggleExclusivePanel(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            bool nextVisible = !panel.activeSelf;
            HideFloatingPanels();
            SetPanelVisible(panel, nextVisible);
            RefreshNow();
        }

        private void HideFloatingPanels()
        {
            SetPanelVisible(buildPanelRoot, false);
            SetPanelVisible(bagPanelRoot, false);
            SetPanelVisible(productionPanelRoot, false);
            SetPanelVisible(buildingDetailPanelRoot, false);
            SetPanelVisible(farmPanelRoot, false);
            SetPanelVisible(toolKitPanelRoot, false);
            SetPanelVisible(questPanelRoot, false);
        }

        private void SetPanelVisible(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }

            if (panel == bagPanelRoot)
            {
                RefreshBagToggle();
            }

        }

        private void RefreshBagToggle()
        {
            bool isOpen = bagPanelRoot != null && bagPanelRoot.activeSelf;
            bottomBarPanel.SetBagOpen(isOpen);
        }

        private void UpgradeToolKitFromButton()
        {
            ToolKitManager.Instance.Upgrade();
            RefreshNow();
        }

        private void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + 0.25f;

            if (!EnsureLayoutReferences())
            {
                return;
            }

            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            bool hasHouse = WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House);
            int selectedBuildingId = WorldGameplayController.Instance != null ? WorldGameplayController.Instance.SelectedBuildingId : 0;

            RefreshTopBarCells();
            RefreshCalendarWidget();
            buildPanel.RefreshTabs(hasHouse);
            productionPanel.Refresh();
            farmPanel.Refresh();
            buildingDetailPanel.Refresh();

            if (toolKitText != null)
            {
                toolKitText.text = ToolKitManager.Instance.GetDisplayText();
            }

            bool farmAreaMode = WorldGameplayController.Instance != null && WorldGameplayController.Instance.IsFarmAreaMode;
            if (currentModeText != null)
            {
                currentModeText.text = farmAreaMode
                    ? LocalizationManager.Get("ui.main.mode.farm")
                    : selectedBuildingId > 0
                        ? LocalizationManager.Format("ui.main.mode.build", GetBuildingName(selectedBuildingId))
                        : LocalizationManager.Get("ui.main.mode.select");
            }

            if (selectedSummaryText != null)
            {
                selectedSummaryText.text = FormatSelectedObjectSummary(selectedBuildingId);
            }

            int buildListStateHash = CalculateBuildListStateHash(selectedBuildingId, hasHouse, farmAreaMode);
            if (buildListStateHash != lastBuildListStateHash)
            {
                lastBuildListStateHash = buildListStateHash;
                RebuildBuildingButtons(selectedBuildingId, hasHouse);
            }
        }

        private void RebuildBuildingButtons(int selectedBuildingId, bool hasHouse)
        {
            if (buildButtonContainer == null)
            {
                return;
            }

            List<GameObject> oldButtons = new List<GameObject>();
            for (int i = buildButtonContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = buildButtonContainer.GetChild(i);
                if (child == null || child.gameObject == null)
                {
                    continue;
                }

                oldButtons.Add(child.gameObject);
            }

            for (int i = 0; i < oldButtons.Count; i++)
            {
                DestroyUnityObject(oldButtons[i]);
            }

            IReadOnlyDictionary<int, WorldBuildingConfig> configs = DataManager.Instance.WorldBuilding?.GetAll();
            if (configs == null)
            {
                return;
            }

            List<WorldBuildingConfig> buildableConfigs = new List<WorldBuildingConfig>();
            foreach (KeyValuePair<int, WorldBuildingConfig> pair in configs)
            {
                WorldBuildingConfig config = pair.Value;
                bool isHouse = config != null && (WorldBuildingType)config.BuildingType == WorldBuildingType.House;
                if (config == null ||
                    !config.Enable ||
                    !config.ShowInBuildPanel ||
                    ShouldHideFromBuildPanel(config, hasHouse) ||
                    (!isHouse || hasHouse) && !IsInBuildCategory(config, buildPanel.CurrentCategory))
                {
                    continue;
                }

                buildableConfigs.Add(config);
            }

            buildableConfigs.Sort(CompareBuildConfigs);
            for (int i = 0; i < buildableConfigs.Count; i++)
            {
                CreateBuildingButton(buildableConfigs[i], selectedBuildingId);
            }
        }

        private bool EnsureLayoutReferences()
        {
            if (rootRect == null)
            {
                rootRect = GetComponent<RectTransform>();
            }

            if (rootRect == null)
            {
                return false;
            }

            if (buildPanelRoot != null &&
                buildButtonContainer != null)
            {
                return true;
            }

            toolKitText = null;
            currentModeText = null;
            selectedSummaryText = null;
            buildPanelRoot = null;
            buildButtonContainer = null;
            bagPanelRoot = null;
            toolKitPanelRoot = null;
            questPanelRoot = null;
            lastBuildListStateHash = int.MinValue;

            return TryBindExistingLayout();
        }

        private int CalculateBuildListStateHash(int selectedBuildingId, bool hasHouse, bool farmAreaMode)
        {
            unchecked
            {
                int hash = selectedBuildingId;
                hash = hash * 397 ^ (hasHouse ? 1 : 0);
                hash = hash * 397 ^ (farmAreaMode ? 1 : 0);
                hash = hash * 397 ^ (int)buildPanel.CurrentCategory;
                hash = hash * 397 ^ GetItemCount(ItemIds.Gold);
                hash = hash * 397 ^ GetItemCount(ItemIds.Wood);
                hash = hash * 397 ^ GetItemCount(ItemIds.Stone);
                hash = hash * 397 ^ GetItemCount(ItemIds.Food);
                hash = hash * 397 ^ GetItemCount(ItemIds.CopperOre);
                hash = hash * 397 ^ GetItemCount(ItemIds.IronOre);
                hash = hash * 397 ^ TechManager.Instance.Revision;
                foreach (KeyValuePair<int, WorldBuilding> pair in WorldBuildingManager.Instance.GetAllBuildings())
                {
                    WorldBuilding building = pair.Value;
                    if (building == null)
                    {
                        continue;
                    }

                    hash = hash * 397 ^ building.ConfigId;
                    hash = hash * 397 ^ building.Level;
                    hash = hash * 397 ^ (int)building.Status;
                }

                return hash;
            }
        }

        private void CreateBuildingButton(WorldBuildingConfig config, int selectedBuildingId)
        {
            bool unlocked = WorldBuildingManager.Instance.IsBuildingUnlocked(config.Id);
            bool hasCost = HasBuildCost(config.Id, out string costText);
            string requirementText = string.Empty;

            if (!unlocked)
            {
                requirementText = WorldBuildingManager.Instance.GetUnlockRequirementText(config);
            }

            GameObject prefab = GetBuildSlotPrefab();
            if (prefab == null || buildButtonContainer == null)
            {
                return;
            }

            GameObject slotObject = Instantiate(prefab, buildButtonContainer, false);
            slotObject.name = $"Build_{config.Id}";

            Image background = slotObject.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.98f, 0.91f, 0.78f, 0.98f);
            }

            SetChildActive(slotObject.transform, "Selected", selectedBuildingId == config.Id);
            SetChildText(slotObject.transform, "NameText", GetBuildingName(config.Id));
            SetChildText(slotObject.transform, "CostText", !unlocked ? LocalizationManager.Get("ui.common.locked") : hasCost ? costText : LocalizationManager.Format("ui.build.cost.need", GetBuildCostDisplayText(config.Id)));
            SetChildTextColor(slotObject.transform, "CostText", BuildCostEnoughColor);
            SetChildText(slotObject.transform, "RequirementText", unlocked ? string.Empty : string.IsNullOrWhiteSpace(requirementText) ? LocalizationManager.Get("ui.common.locked") : requirementText);
            SetChildActive(slotObject.transform, "LockOverlay", !unlocked);
            RefreshBuildSlotIcon(slotObject.transform, config);

            Button button = slotObject.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"[WorldMainPanel] Missing static Button on build slot prefab: {BuildSlotPrefabPath}");
                return;
            }

            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (!WorldBuildingManager.Instance.IsBuildingUnlocked(config.Id))
                {
                    string currentRequirement = WorldBuildingManager.Instance.GetUnlockRequirementText(config);
                    Toast.Warning(string.IsNullOrWhiteSpace(currentRequirement) ? LocalizationManager.Get("ui.build.locked") : currentRequirement);
                    RefreshNow();
                    return;
                }

                if (!HasBuildCost(config.Id, out string currentCostText))
                {
                    Toast.Warning(LocalizationManager.Format("ui.build.missing_materials", GetMissingBuildCostText(config.Id, currentCostText)));
                    RefreshNow();
                    return;
                }

                WorldGameplayController.Instance?.SelectBuilding(config.Id);
                SetPanelVisible(buildPanelRoot, false);
                RefreshNow();
            });
        }

        private GameObject GetBuildSlotPrefab()
        {
            if (buildSlotPrefab != null)
            {
                return buildSlotPrefab;
            }

            buildSlotPrefab = ResourceManager.Instance.LoadGameObject(BuildSlotPrefabPath);
            if (buildSlotPrefab == null)
            {
                Debug.LogError($"[WorldMainPanel] Missing build slot prefab: {BuildSlotPrefabPath}");
            }

            return buildSlotPrefab;
        }

        private static void RefreshBuildSlotIcon(Transform slot, WorldBuildingConfig config)
        {
            Image icon = FindImage(slot, "Icon");
            TMP_Text iconLabel = FindText(slot, "IconLabel");
            Sprite sprite = LoadBuildIcon(config);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(0.86f, 0.68f, 0.38f, 0.9f);
                icon.preserveAspect = true;
            }

            if (iconLabel != null)
            {
                iconLabel.gameObject.SetActive(sprite == null);
                iconLabel.text = GetBuildIconLabel(GetBuildingName(config.Id));
            }
        }

        private static Sprite LoadBuildIcon(WorldBuildingConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.IconLocation))
            {
                return null;
            }

            if (!config.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingBuildIconWarnings.Add(config.IconLocation))
                {
                    Debug.LogWarning($"[WorldMainPanel] Building icon location must be a full asset path. location: {config.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
            if (sprite == null && MissingBuildIconWarnings.Add(config.IconLocation))
            {
                Debug.LogWarning($"[WorldMainPanel] Building icon load failed. location: {config.IconLocation}");
            }

            return sprite;
        }

        private static string GetBuildIconLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return LocalizationManager.Get("ui.build.icon_fallback");
            }

            return name.Length <= 2 ? name : name.Substring(0, 2);
        }

        private string FormatSelectedObjectSummary(int selectedBuildingId)
        {
            WorldGameplayController gameplay = WorldGameplayController.Instance;
            if (gameplay != null)
            {
                if (gameplay.SelectedFarm != null)
                {
                    return FormatFarmSummary(gameplay.SelectedFarm);
                }

                if (gameplay.SelectedWorldBuilding != null)
                {
                    return FormatBuildingSummary(gameplay.SelectedWorldBuilding);
                }
            }

            if (selectedBuildingId > 0)
            {
                return LocalizationManager.Format("ui.main.selected_build", GetBuildingName(selectedBuildingId));
            }

            return LocalizationManager.Format("ui.main.current_tool", ToolKitDefinitions.GetToolName(ToolKitManager.Instance.CurrentToolItemId));
        }

        private string FormatFarmSummary(Farm farm)
        {
            if (farm == null)
            {
                return LocalizationManager.Get("ui.main.selected_farm_none");
            }

            string cropName = LocalizationManager.Get("ui.common.empty");
            string production = "0/min";
            if (farm.HasCrop &&
                FarmManager.Instance.Crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) &&
                crop != null)
            {
                cropName = LocalizedConfigText.CropName(crop.Id);
                production = $"{crop.OutputCountPerSecond * farm.CellCount * 60}/min";
            }

            return LocalizationManager.Format("ui.main.farm_summary", farm.FarmId, farm.CellCount, cropName, production);
        }

        private string FormatBuildingSummary(WorldBuilding building)
        {
            if (building == null)
            {
                return LocalizationManager.Get("ui.main.selected_building_none");
            }

            string status = building.Status == WorldBuildingStatus.Constructing
                ? LocalizationManager.Get("ui.build.status.constructing")
                : LocalizationManager.Get("ui.build.status.active");
            return LocalizationManager.Format("ui.main.building_summary", GetBuildingName(building.ConfigId), building.Level, status);
        }

        private bool HasBuildCost(int buildingId, out string costText)
        {
            costText = LocalizationManager.Get("ui.common.free");
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                costText = LocalizationManager.Get("ui.common.config");
                return false;
            }

            IReadOnlyList<WorldItem> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId <= 0 || costs.Count == 0)
            {
                return true;
            }

            costText = FormatCosts(costs);
            return WorldItemManager.Instance.HasItems(costs);
        }

        private string GetBuildCostDisplayText(int buildingId)
        {
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                return LocalizationManager.Get("ui.common.config");
            }

            IReadOnlyList<WorldItem> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId <= 0 || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.free");
            }

            return FormatCosts(costs, true);
        }

        private string GetMissingBuildCostText(int buildingId, string fallbackCostText)
        {
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                return fallbackCostText;
            }

            IReadOnlyList<WorldItem> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (costs == null || costs.Count == 0)
            {
                return fallbackCostText;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                int current = WorldItemManager.Instance.GetCount(cost.ItemId);
                if (current >= cost.Count)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {current}/{cost.Count}");
            }

            return parts.Count > 0 ? string.Join("、", parts) : fallbackCostText;
        }

        private string FormatCosts(IReadOnlyList<WorldItem> costs)
        {
            return FormatCosts(costs, false);
        }

        private string FormatCosts(IReadOnlyList<WorldItem> costs, bool colorMissingCount)
        {
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.free");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                string countText = cost.Count.ToString();
                if (colorMissingCount && !WorldItemManager.Instance.HasItem(cost.ItemId, cost.Count))
                {
                    countText = $"<color=#{ColorUtility.ToHtmlStringRGB(BuildCostMissingColor)}>{countText}</color>";
                }

                parts.Add($"{GetItemName(cost.ItemId)} {countText}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : LocalizationManager.Get("ui.common.free");
        }

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private int GetItemCount(int itemId)
        {
            return WorldItemManager.Instance.GetCount(itemId);
        }

        private void RefreshTopBarCells()
        {
            topBarPanel.RefreshCells(GetItemCount);
        }

        private void RefreshCalendarWidget()
        {
            topBarPanel.RefreshCalendarWidget();
        }

        private static bool ShouldHideFromBuildPanel(WorldBuildingConfig config, bool hasHouse)
        {
            WorldBuildingType buildingType = (WorldBuildingType)config.BuildingType;
            if (buildingType == WorldBuildingType.House)
            {
                return hasHouse;
            }

            if (!hasHouse)
            {
                return true;
            }

            return false;
        }

        private static bool IsInBuildCategory(WorldBuildingConfig config, WorldBuildCategory category)
        {
            if (config == null || category == WorldBuildCategory.All)
            {
                return true;
            }

            return GetBuildCategory(config) == category;
        }

        private static WorldBuildCategory GetBuildCategory(WorldBuildingConfig config)
        {
            if (config == null)
            {
                return WorldBuildCategory.Special;
            }

            int category = config.BuildCategory;
            if (category < (int)WorldBuildCategory.Building || category > (int)WorldBuildCategory.Special)
            {
                return WorldBuildCategory.Special;
            }

            return (WorldBuildCategory)category;
        }

        private static int CompareBuildConfigs(WorldBuildingConfig left, WorldBuildingConfig right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int sort = left.SortOrder.CompareTo(right.SortOrder);
            return sort != 0 ? sort : left.Id.CompareTo(right.Id);
        }

        private static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Image FindImage(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void SetChildText(Transform parent, string childName, string content)
        {
            TMP_Text text = FindText(parent, childName);
            if (text != null)
            {
                text.richText = true;
                text.text = content;
            }
        }

        private static void SetChildTextColor(Transform parent, string childName, Color color)
        {
            TMP_Text text = FindText(parent, childName);
            if (text != null)
            {
                text.color = color;
            }
        }

        private GameObject FindGameObject(string childName)
        {
            Transform child = rootRect != null ? rootRect.Find(childName) : null;
            return child != null ? child.gameObject : null;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

#if UNITY_EDITOR
            DestroyImmediate(target);
#endif
        }
    }
}
