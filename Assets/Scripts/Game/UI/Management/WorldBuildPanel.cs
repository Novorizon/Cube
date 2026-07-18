using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldBuildPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Build/BuildPanel.prefab";

        private const string BuildSlotPrefabPath = "Assets/Arts/UI/Panels/Build/BuildSlot.prefab";
        private static readonly HashSet<string> MissingBuildIconWarnings = new HashSet<string>();
        private static readonly Color BuildCostEnoughColor = new Color(0.23f, 0.18f, 0.12f, 1f);
        private static readonly Color BuildCostMissingColor = new Color(0.82f, 0.12f, 0.08f, 1f);

        private readonly Dictionary<WorldBuildCategory, Transform> tabRoots = new Dictionary<WorldBuildCategory, Transform>();
        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);

        private GameObject root;
        private Transform buttonContainer;
        private GameObject buildSlotPrefab;
        private WorldBuildCategory currentCategory = WorldBuildCategory.All;
        private bool hasHouse = true;
        private float nextRefreshTime;
        private int lastBuildListStateHash = int.MinValue;

        internal WorldBuildCategory CurrentCategory => currentCategory;
        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticLayout();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            WorldFloatingPanelLayout.AlignBottomToHotBarGrid(GetComponent<RectTransform>());
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, TechChangedMessage>(
                WorldMessageTopic.TechChanged,
                _ => RefreshNow()));
            RefreshNow();
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        private bool BindStaticLayout()
        {
            root = gameObject;
            buttonContainer = FindBuildButtonContainer(transform);

            BindTabs(transform);
            WorldPanelBindingUtility.BindButton(transform.Find("Close"), CloseSelf, "Build panel close");

            return buttonContainer != null;
        }

        private void RefreshNow()
        {
            lastBuildListStateHash = int.MinValue;
            nextRefreshTime = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;

            hasHouse = WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House);
            if (!hasHouse && IsLockedBeforeHouse(currentCategory))
            {
                currentCategory = WorldBuildCategory.Building;
            }

            RefreshTabs();

            int selectedBuildingId = GameplayController.Instance != null ? GameplayController.Instance.SelectedBuildingId : 0;
            bool farmAreaMode = GameplayController.Instance != null && GameplayController.Instance.IsFarmAreaMode;
            int buildListStateHash = CalculateBuildListStateHash(selectedBuildingId, farmAreaMode);
            if (buildListStateHash == lastBuildListStateHash)
            {
                return;
            }

            lastBuildListStateHash = buildListStateHash;
            RebuildBuildingButtons(selectedBuildingId);
        }

        private void BindTabs(Transform rootTransform)
        {
            tabRoots.Clear();
            Transform tabBar = rootTransform != null ? rootTransform.Find("TabBar") : null;
            if (tabBar == null)
            {
                currentCategory = WorldBuildCategory.All;
                return;
            }

            TryBindTab(tabBar, WorldBuildCategory.Building, "Tab_Building", "Building");
            TryBindTab(tabBar, WorldBuildCategory.Production, "Tab_Production", "Production");
            TryBindTab(tabBar, WorldBuildCategory.Resource, "Tab_Resource", "Resource");
            TryBindTab(tabBar, WorldBuildCategory.Farm, "Tab_Farm", "Farm");
            TryBindTab(tabBar, WorldBuildCategory.Decoration, "Tab_Decoration", "Decoration");
            TryBindTab(tabBar, WorldBuildCategory.Special, "Tab_Special", "Special");

            if (tabRoots.Count == 0)
            {
                currentCategory = WorldBuildCategory.All;
            }
            else if (currentCategory == WorldBuildCategory.All || !tabRoots.ContainsKey(currentCategory))
            {
                currentCategory = GetFirstTabCategory();
            }

            RefreshTabs();
        }

        private void TryBindTab(Transform tabBar, WorldBuildCategory category, params string[] names)
        {
            Transform tab = WorldPanelBindingUtility.FindFirst(tabBar, names);
            if (tab == null)
            {
                return;
            }

            tabRoots[category] = tab;
            WorldBuildCategory capturedCategory = category;
            WorldPanelBindingUtility.BindButton(tab, () =>
            {
                if (IsTabLocked(capturedCategory))
                {
                    Toast.Warning(LocalizationManager.Get("ui.build.require_house"));
                    return;
                }

                if (currentCategory == capturedCategory)
                {
                    return;
                }

                currentCategory = capturedCategory;
                RefreshTabs();
                RefreshNow();
            }, $"{category} build tab");
        }

        private void RefreshTabs()
        {
            foreach (KeyValuePair<WorldBuildCategory, Transform> pair in tabRoots)
            {
                bool selected = pair.Key == currentCategory;
                bool locked = IsTabLocked(pair.Key);
                Transform tab = pair.Value;
                if (tab == null)
                {
                    continue;
                }

                Transform selectedTransform = tab.Find("Selected");
                if (selectedTransform != null)
                {
                    selectedTransform.gameObject.SetActive(selected);
                }

                Image image = tab.GetComponent<Image>();
                if (image != null)
                {
                    image.color = selected
                        ? new Color(0.96f, 0.74f, 0.25f, 0.96f)
                        : locked
                            ? new Color(0.70f, 0.66f, 0.58f, 0.82f)
                            : new Color(0.98f, 0.91f, 0.78f, 0.92f);
                }

                Button button = tab.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !locked;
                }

                SetLockMarker(tab, locked);
            }
        }

        private void RebuildBuildingButtons(int selectedBuildingId)
        {
            if (buttonContainer == null)
            {
                return;
            }

            List<GameObject> oldButtons = new List<GameObject>();
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = buttonContainer.GetChild(i);
                if (child != null && child.gameObject != null)
                {
                    oldButtons.Add(child.gameObject);
                }
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
                    (!isHouse || hasHouse) && !IsInBuildCategory(config, currentCategory))
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

        private void CreateBuildingButton(WorldBuildingConfig config, int selectedBuildingId)
        {
            bool unlocked = WorldBuildingManager.Instance.IsBuildingUnlocked(config.Id);
            bool hasCost = HasBuildCost(config.Id, out string costText);
            string requirementText = !unlocked ? WorldBuildingManager.Instance.GetUnlockRequirementText(config) : string.Empty;

            GameObject prefab = GetBuildSlotPrefab();
            if (prefab == null || buttonContainer == null)
            {
                return;
            }

            GameObject slotObject = Instantiate(prefab, buttonContainer, false);
            slotObject.name = $"Build_{config.Id}";

            Image background = slotObject.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.98f, 0.91f, 0.78f, 0.98f);
            }

            SetChildActive(slotObject.transform, "Selected", selectedBuildingId == config.Id);
            SetChildText(slotObject.transform, "Name", GetBuildingName(config.Id));
            SetChildText(slotObject.transform, "CostText", !unlocked ? LocalizationManager.Get("ui.common.locked") : hasCost ? costText : LocalizationManager.Format("ui.build.cost.need", GetBuildCostDisplayText(config.Id)));
            SetChildTextColor(slotObject.transform, "CostText", BuildCostEnoughColor);
            SetChildText(slotObject.transform, "LockOverlay/LockText", unlocked ? string.Empty : string.IsNullOrWhiteSpace(requirementText) ? LocalizationManager.Get("ui.common.locked") : requirementText);
            SetChildActive(slotObject.transform, "LockOverlay", !unlocked);
            RefreshBuildSlotIcon(slotObject.transform, config);

            Button button = slotObject.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"[WorldBuildPanel] Missing static Button on build slot prefab: {BuildSlotPrefabPath}");
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

                GameplayController.Instance?.SelectBuilding(config.Id);
                UIManager.Instance.Panels.Hide(PrefabPath);
                WorldMainPanel.Instance?.RefreshNow();
            });
        }

        private int CalculateBuildListStateHash(int selectedBuildingId, bool farmAreaMode)
        {
            unchecked
            {
                int hash = selectedBuildingId;
                hash = hash * 397 ^ (hasHouse ? 1 : 0);
                hash = hash * 397 ^ (farmAreaMode ? 1 : 0);
                hash = hash * 397 ^ (int)currentCategory;
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

        private GameObject GetBuildSlotPrefab()
        {
            if (buildSlotPrefab != null)
            {
                return buildSlotPrefab;
            }

            buildSlotPrefab = ResourceManager.Instance.LoadGameObject(BuildSlotPrefabPath);
            if (buildSlotPrefab == null)
            {
                Debug.LogError($"[WorldBuildPanel] Missing build slot prefab: {BuildSlotPrefabPath}");
            }

            return buildSlotPrefab;
        }

        private bool HasBuildCost(int buildingId, out string costText)
        {
            costText = LocalizationManager.Get("ui.common.free");
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                costText = LocalizationManager.Get("ui.common.config");
                return false;
            }

            IReadOnlyList<ItemStack> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId <= 0 || costs.Count == 0)
            {
                return true;
            }

            costText = FormatCosts(costs);
            return ItemManager.Instance.HasItems(costs);
        }

        private string GetBuildCostDisplayText(int buildingId)
        {
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                return LocalizationManager.Get("ui.common.config");
            }

            IReadOnlyList<ItemStack> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
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

            IReadOnlyList<ItemStack> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (costs == null || costs.Count == 0)
            {
                return fallbackCostText;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                ItemStack cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                int current = ItemManager.Instance.GetCount(cost.ItemId);
                if (current < cost.Count)
                {
                    parts.Add($"{GetItemName(cost.ItemId)} {current}/{cost.Count}");
                }
            }

            return parts.Count > 0 ? string.Join("、", parts) : fallbackCostText;
        }

        private string FormatCosts(IReadOnlyList<ItemStack> costs)
        {
            return FormatCosts(costs, false);
        }

        private string FormatCosts(IReadOnlyList<ItemStack> costs, bool colorMissingCount)
        {
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.free");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                ItemStack cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                string countText = cost.Count.ToString();
                if (colorMissingCount && !ItemManager.Instance.HasItem(cost.ItemId, cost.Count))
                {
                    countText = $"<color=#{ColorUtility.ToHtmlStringRGB(BuildCostMissingColor)}>{countText}</color>";
                }

                parts.Add($"{GetItemName(cost.ItemId)} {countText}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : LocalizationManager.Get("ui.common.free");
        }

        private bool IsTabLocked(WorldBuildCategory category)
        {
            return !hasHouse && IsLockedBeforeHouse(category);
        }

        private WorldBuildCategory GetFirstTabCategory()
        {
            if (tabRoots.ContainsKey(WorldBuildCategory.Building))
            {
                return WorldBuildCategory.Building;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Production))
            {
                return WorldBuildCategory.Production;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Resource))
            {
                return WorldBuildCategory.Resource;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Farm))
            {
                return WorldBuildCategory.Farm;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Decoration))
            {
                return WorldBuildCategory.Decoration;
            }

            if (tabRoots.ContainsKey(WorldBuildCategory.Special))
            {
                return WorldBuildCategory.Special;
            }

            return WorldBuildCategory.All;
        }

        private void OnLanguageChanged()
        {
            RefreshNow();
        }

        private void CloseSelf()
        {
            if (CanCloseBy(UICloseReason.CloseButton))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private static Transform FindBuildButtonContainer(Transform root)
        {
            Transform scrollView = root != null ? root.Find("ScrollView") : null;
            Transform viewport = scrollView != null ? scrollView.Find("Viewport") : null;
            return viewport != null ? viewport.Find("Content") : null;
        }

        private static void RefreshBuildSlotIcon(Transform slot, WorldBuildingConfig config)
        {
            Image icon = FindImage(slot, "Icon");
            Sprite sprite = LoadBuildIcon(config);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(0.86f, 0.68f, 0.38f, 0.9f);
                icon.preserveAspect = true;
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
                    Debug.LogWarning($"[WorldBuildPanel] Building icon location must be a full asset path. location: {config.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
            if (sprite == null && MissingBuildIconWarnings.Add(config.IconLocation))
            {
                Debug.LogWarning($"[WorldBuildPanel] Building icon load failed. location: {config.IconLocation}");
            }

            return sprite;
        }

        private static bool ShouldHideFromBuildPanel(WorldBuildingConfig config, bool hasHouseBuilt)
        {
            WorldBuildingType buildingType = (WorldBuildingType)config.BuildingType;
            if (buildingType == WorldBuildingType.House)
            {
                return hasHouseBuilt;
            }

            return !hasHouseBuilt;
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

        private static bool IsLockedBeforeHouse(WorldBuildCategory category)
        {
            return category != WorldBuildCategory.All && category != WorldBuildCategory.Building;
        }

        private static void SetLockMarker(Transform tab, bool locked)
        {
            Transform marker =
                tab != null
                    ? tab.Find("Lock") ?? tab.Find("LockIcon") ?? tab.Find("Locked") ?? tab.Find("LockOverlay")
                    : null;
            if (marker != null)
            {
                marker.gameObject.SetActive(locked);
            }
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

        private static string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private static int GetItemCount(int itemId)
        {
            return ItemManager.Instance.GetCount(itemId);
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
