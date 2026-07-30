using System;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldMainPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/World/WorldMainPanel.prefab";
        private const string ManagementFloatingGroup = "ManagementFloating";
        private const string BuildEntryId = "build";
        private const string ToolKitEntryId = "toolkit";
        private const string ProductionEntryId = "production";
        private const string QuestEntryId = "quest";
        private const string TechTreeEntryId = "techTree";
        private const string MenuEntryId = "menu";
        private const int DefaultBattleMapConfigId = 30950001;

        public static WorldMainPanel Instance { get; private set; }

        [SerializeField] private WorldTopBarPanel topBarPanel;
        [SerializeField] private WorldBottomBarPanel bottomBarPanel;
        [SerializeField] private WorldEntryBarPanel entryBarPanel;
        [SerializeField] private WorldRightBarPanel rightBarPanel;
        [SerializeField] private WorldBuildingDetailPanel buildingDetailPanel;
        [SerializeField] private Button settingButton;

        private RectTransform rootRect;
        private RectTransform bottomBarRect;
        private TMP_Text currentModeText;
        private TMP_Text selectedSummaryText;
        private readonly WorldPanelEntryController panelEntries = new WorldPanelEntryController();
        private float nextRefreshTime;

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
                    RefreshNow();
                }));
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, QuestChangedMessage>(
                WorldMessageTopic.QuestChanged,
                _ =>
                {
                    RefreshNow();
                }));
            RefreshNow();
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
            bottomBarPanel?.Dispose();

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
            topBarPanel?.RefreshCalendarMotion();

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
            EnsureManagementMiniMap();

            if (TryBindExistingLayout())
            {
                return;
            }

            Debug.LogError($"[{nameof(WorldMainPanel)}] Invalid prefab layout. Please rebuild or fix prefab: {PrefabPath}");
        }

        private void EnsureManagementMiniMap()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child.name != "MiniMapPanel")
                {
                    continue;
                }

                ManagementMiniMapPanel miniMap = child.GetComponent<ManagementMiniMapPanel>();
                if (miniMap == null)
                {
                    miniMap = child.gameObject.AddComponent<ManagementMiniMapPanel>();
                }
                miniMap.EnsureRuntimeLayout();
                return;
            }

            Debug.LogWarning($"[{nameof(WorldMainPanel)}] MiniMapPanel node is missing from prefab: {PrefabPath}");
        }

        private bool TryBindExistingLayout()
        {
            if (topBarPanel == null || bottomBarPanel == null || settingButton == null)
            {
                return false;
            }

            topBarPanel.Initialize();
            settingButton.onClick.RemoveListener(ShowWorldMenuPanel);
            settingButton.onClick.AddListener(ShowWorldMenuPanel);
            RegisterPanelEntries();
            bottomBarPanel.Initialize(ToggleBagPanel, ToggleBuildPanel, ToggleToolKitPanel, ShowTechTreePanel);
            entryBarPanel?.Initialize(ToggleQuestPanel);
            rightBarPanel?.Initialize(
                ToggleProductionPanel,
                ToggleToolKitPanel,
                EnterFarmAreaMode,
                ShowTechTreePanel,
                EnterBattleMap);
            buildingDetailPanel?.Initialize(HideBuildingDetailPanel, RefreshNow);

            bottomBarRect = bottomBarPanel.RootRect;
            currentModeText = bottomBarPanel.CurrentModeText;
            selectedSummaryText = bottomBarPanel.SelectedSummaryText;

            BindPopupCloseButtons();
            RefreshPanelEntryStates();
            return true;
        }

        private void RegisterPanelEntries()
        {
            panelEntries.Clear();
            panelEntries.Register(new WorldPanelEntry(
                BuildEntryId,
                ManagementFloatingGroup,
                WorldBuildPanel.PrefabPath));
            panelEntries.Register(new WorldPanelEntry(
                ToolKitEntryId,
                ManagementFloatingGroup,
                WorldToolKitPanel.PrefabPath));
            panelEntries.Register(new WorldPanelEntry(
                ProductionEntryId,
                ManagementFloatingGroup,
                WorldProductionPanel.PrefabPath));
            panelEntries.Register(new WorldPanelEntry(
                QuestEntryId,
                ManagementFloatingGroup,
                QuestPanel.PrefabPath));
            panelEntries.Register(new WorldPanelEntry(
                TechTreeEntryId,
                ManagementFloatingGroup,
                TechTreePanel.PrefabPath,
                bottomBarPanel.SetTechOpen));
            panelEntries.Register(new WorldPanelEntry(
                MenuEntryId,
                ManagementFloatingGroup,
                WorldMenuPanel.PrefabPath));
        }

        private void BindPopupCloseButtons()
        {
        }

        private void ToggleQuestPanel()
        {
            TogglePanelEntry(QuestEntryId);
        }

        private void ToggleBuildPanel()
        {
            TogglePanelEntry(BuildEntryId);
        }

        private void ShowTechTreePanel()
        {
            TogglePanelEntry(TechTreeEntryId);
        }

        private void ShowWorldMenuPanel()
        {
            TogglePanelEntry(MenuEntryId);
        }

        private void OnLanguageChanged()
        {
            topBarPanel?.RefreshCalendarWidget();
            topBarPanel?.RefreshWeather();
            bottomBarPanel?.RefreshSlots();
            RefreshNow();
        }

        private void ToggleBagPanel()
        {
            if (bottomBarPanel == null)
            {
                return;
            }

            bool shouldOpen = !bottomBarPanel.IsBagOpen;
            if (shouldOpen)
            {
                HideFloatingPanels(true, null, false);
            }

            bottomBarPanel.SetBagOpen(shouldOpen);
            RefreshNow();
        }

        private void ToggleProductionPanel()
        {
            TogglePanelEntry(ProductionEntryId);
        }

        private void ToggleToolKitPanel()
        {
            TogglePanelEntry(ToolKitEntryId);
        }

        private void EnterFarmAreaMode()
        {
            GameplayController gameplay = GameplayController.Instance;
            RequirementResult requirement = gameplay != null
                ? gameplay.SelectFarmAreaMode()
                : FarmRequirementChecker.GameplayUnavailable();
            RequirementToast.TryPass(requirement);
        }

        private void EnterBattleMap()
        {
            if (!MapManager.Instance.LoadBattleMap(DefaultBattleMapConfigId))
            {
                Debug.LogError($"Enter battle map failed. Map config id: {DefaultBattleMapConfigId}");
            }
        }

        private void TogglePanelEntry(string entryId)
        {
            TogglePanelEntryAsync(entryId).Forget();
        }

        private async System.Threading.Tasks.Task TogglePanelEntryAsync(string entryId)
        {
            if (!panelEntries.TryGet(entryId, out WorldPanelEntry entry))
            {
                return;
            }

            if (panelEntries.IsShown(entry))
            {
                UIManager.Instance.Panels.Hide(entry.PrefabPath);
                RefreshNow();
                return;
            }

            HideFloatingPanels(true, entry.PrefabPath);
            await UIManager.Instance.Panels.ShowExclusiveAsync(entry.GroupId, entry.PrefabPath);
            RefreshNow();
        }

        public void ShowFarmPanel(Farm farm)
        {
            if (farm == null)
            {
                Debug.LogWarning("Show farm panel failed. Farm is null.");
                return;
            }

            WorldFarmPanel.Instance?.SetSelectedFarm(farm);
            ShowFarmPanelAsync(farm).Forget();
        }

        private async System.Threading.Tasks.Task ShowFarmPanelAsync(Farm farm)
        {
            HideFloatingPanels(false);
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(WorldFarmPanel.PrefabPath, farm);
            if (handle.View is WorldFarmPanel farmPanel)
            {
                farmPanel.SetSelectedFarm(farm);
            }

            RefreshNow();
        }

        public void ShowBuildingDetailPanel(WorldBuilding building)
        {
            if (buildingDetailPanel == null)
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
            buildingDetailPanel?.Show(building);
            RefreshNow();
        }

        public void HideFarmPanel()
        {
            UIManager.Instance.Panels.Hide(WorldFarmPanel.PrefabPath);
        }

        public void HideBuildingDetailPanel()
        {
            buildingDetailPanel?.Hide();
        }

        private void HideFloatingPanels(
            bool hideFarmPanel = true,
            string exceptPrefabPath = null,
            bool hideBagPanel = true)
        {
            if (hideBagPanel)
            {
                bottomBarPanel?.SetBagOpen(false);
            }

            buildingDetailPanel?.Hide();
            UIManager.Instance.Panels.HideExclusiveGroup(ManagementFloatingGroup, exceptPrefabPath);
            UIManager.Instance.Panels.HideStack(WorldMenuPanel.SettingsStackGroupId);
            if (hideFarmPanel)
            {
                UIManager.Instance.Panels.Hide(WorldFarmPanel.PrefabPath);
            }
        }

        private void RefreshPanelEntryStates()
        {
            panelEntries.RefreshStates();
        }

        private void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + 0.25f;

            if (!EnsureLayoutReferences())
            {
                return;
            }

            int selectedBuildingId = GameplayController.Instance != null ? GameplayController.Instance.SelectedBuildingId : 0;

            RefreshPanelEntryStates();
            RefreshCalendarWidget();
            RefreshQuestPanel();
            buildingDetailPanel?.Refresh();

            bool farmAreaMode = GameplayController.Instance != null && GameplayController.Instance.IsFarmAreaMode;
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

            if (bottomBarRect != null)
            {
                return true;
            }

            bottomBarRect = null;
            currentModeText = null;
            selectedSummaryText = null;

            return TryBindExistingLayout();
        }

        public bool TryGetBottomBarTopInParent(RectTransform targetParent, out float topY)
        {
            topY = 0f;
            if (bottomBarRect == null || targetParent == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            bottomBarRect.GetWorldCorners(corners);
            topY = targetParent.InverseTransformPoint(corners[1]).y;
            return true;
        }

        public bool TryGetHotBarGridRectInParent(RectTransform targetParent, out Rect rect)
        {
            return TryGetRectInParent(bottomBarPanel != null ? bottomBarPanel.HotBarGridRect : null, targetParent, out rect);
        }

        public bool TryGetHotSlotRectInParent(int slotNumber, RectTransform targetParent, out Rect rect)
        {
            rect = default;
            if (bottomBarPanel == null || !bottomBarPanel.TryGetHotSlotRect(slotNumber, out RectTransform slotRect))
            {
                return false;
            }

            return TryGetRectInParent(slotRect, targetParent, out rect);
        }

        private static bool TryGetRectInParent(RectTransform source, RectTransform targetParent, out Rect rect)
        {
            rect = default;
            if (source == null || targetParent == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);

            Vector2 min = targetParent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = targetParent.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private string FormatSelectedObjectSummary(int selectedBuildingId)
        {
            GameplayController gameplay = GameplayController.Instance;
            if (gameplay != null)
            {
                if (gameplay.SelectedFarm != null)
                {
                    return FormatFarmSummary(gameplay.SelectedFarm);
                }

                if (gameplay.SelectedBuilding != null)
                {
                    return FormatBuildingSummary(gameplay.SelectedBuilding);
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

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        private void RefreshCalendarWidget()
        {
            topBarPanel?.RefreshCalendarWidget();
        }

        private void RefreshQuestPanel()
        {
        }
    }
}
