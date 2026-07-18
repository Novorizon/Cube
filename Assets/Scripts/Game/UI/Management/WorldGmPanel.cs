using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldGmPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Gm/GmPanel.prefab";

        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button commonTabButton;
        [SerializeField] private Button resourcesTabButton;
        [SerializeField] private Button farmingTabButton;
        [SerializeField] private GameObject commonRoot;
        [SerializeField] private GameObject resourcesRoot;
        [SerializeField] private GameObject farmingRoot;
        [SerializeField] private Button starterPackButton;
        [SerializeField] private Button addWoodButton;
        [SerializeField] private Button addStoneButton;
        [SerializeField] private Button addGoldButton;
        [SerializeField] private Button addFoodButton;
        [SerializeField] private Button addAllResourcesButton;
        [SerializeField] private Button addAllSeedsButton;
        [SerializeField] private Button addAllCropsButton;
        [SerializeField] private Button timeScalePauseButton;
        [SerializeField] private Button timeScaleNormalButton;
        [SerializeField] private Button timeScaleFastButton;
        [SerializeField] private Button timeScaleVeryFastButton;
        [SerializeField] private WorldGmItemRowView[] resourceRows;
        [SerializeField] private WorldGmItemRowView[] seedRows;
        [SerializeField] private WorldGmItemRowView[] cropRows;
        [SerializeField] private TMP_Text statusText;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            Bind(closeButton, CloseSelf, nameof(closeButton));
            Bind(refreshButton, RefreshPanel, nameof(refreshButton));
            Bind(commonTabButton, () => ShowTab(commonRoot), nameof(commonTabButton));
            Bind(resourcesTabButton, () => ShowTab(resourcesRoot), nameof(resourcesTabButton));
            Bind(farmingTabButton, () => ShowTab(farmingRoot), nameof(farmingTabButton));
            Bind(starterPackButton, AddStarterPack, nameof(starterPackButton));
            Bind(addWoodButton, () => AddItem(ItemIds.Wood, LocalizationManager.Get("item.wood"), 100), nameof(addWoodButton));
            Bind(addStoneButton, () => AddItem(ItemIds.Stone, LocalizationManager.Get("item.stone"), 100), nameof(addStoneButton));
            Bind(addGoldButton, () => AddItem(ItemIds.Gold, LocalizationManager.Get("item.gold"), 100), nameof(addGoldButton));
            Bind(addFoodButton, () => AddItem(ItemIds.Food, LocalizationManager.Get("item.food"), 100), nameof(addFoodButton));
            Bind(addAllResourcesButton, () => AddGroup(ResourceItems, 1000, "ui.gm.group.resources"), nameof(addAllResourcesButton));
            Bind(addAllSeedsButton, () => AddGroup(SeedItems, 100, "ui.gm.group.seeds"), nameof(addAllSeedsButton));
            Bind(addAllCropsButton, () => AddGroup(CropItems, 1000, "ui.gm.group.crops"), nameof(addAllCropsButton));
            Bind(timeScalePauseButton, () => SetTimeScale(0f), nameof(timeScalePauseButton));
            Bind(timeScaleNormalButton, () => SetTimeScale(1f), nameof(timeScaleNormalButton));
            Bind(timeScaleFastButton, () => SetTimeScale(20f), nameof(timeScaleFastButton));
            Bind(timeScaleVeryFastButton, () => SetTimeScale(120f), nameof(timeScaleVeryFastButton));
            BindRows(resourceRows);
            BindRows(seedRows);
            BindRows(cropRows);
        }

        protected override void OnOpen(object args)
        {
            ShowTab(commonRoot);
            RefreshRows();
            SetStatus(LocalizationManager.Get("ui.gm.status.ready"));
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(WorldGmPanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static readonly GmItem[] ResourceItems =
        {
            new GmItem(ItemIds.Wood, "item.wood"),
            new GmItem(ItemIds.Stone, "item.stone"),
            new GmItem(ItemIds.Gold, "item.gold"),
            new GmItem(ItemIds.CopperOre, "item.copper_ore"),
            new GmItem(ItemIds.IronOre, "item.iron_ore"),
            new GmItem(ItemIds.Food, "item.food"),
            new GmItem(ItemIds.Plank, "item.plank"),
            new GmItem(ItemIds.CopperIngot, "item.copper_ingot"),
            new GmItem(ItemIds.IronIngot, "item.iron_ingot"),
        };

        private static readonly GmItem[] SeedItems =
        {
            new GmItem(ItemIds.WheatSeed, "item.wheat_seed"),
            new GmItem(ItemIds.TomatoSeed, "item.tomato_seed"),
            new GmItem(ItemIds.HerbSeed, "item.herb_seed"),
            new GmItem(ItemIds.FlowerSeed, "item.flower_seed"),
        };

        private static readonly GmItem[] CropItems =
        {
            new GmItem(ItemIds.Wheat, "item.wheat"),
            new GmItem(ItemIds.Tomato, "item.tomato"),
            new GmItem(ItemIds.Herb, "item.herb"),
            new GmItem(ItemIds.Flower, "item.flower"),
        };

        private void ShowTab(GameObject targetRoot)
        {
            SetActive(commonRoot, targetRoot == commonRoot);
            SetActive(resourcesRoot, targetRoot == resourcesRoot);
            SetActive(farmingRoot, targetRoot == farmingRoot);
            RefreshRows();
        }

        private void AddStarterPack()
        {
            AddItem(ItemIds.Wood, LocalizationManager.Get("item.wood"), 100, false);
            AddItem(ItemIds.Stone, LocalizationManager.Get("item.stone"), 100, false);
            AddItem(ItemIds.Gold, LocalizationManager.Get("item.gold"), 100, false);
            AddItem(ItemIds.Food, LocalizationManager.Get("item.food"), 100, false);
            AddGroup(SeedItems, 100, "ui.gm.group.seeds", false);
            WorldMainPanel.Instance?.RefreshNow();
            RefreshRows();
            SetStatus(LocalizationManager.Get("ui.gm.status.added_starter_pack"));
            Toast.Info(LocalizationManager.Get("ui.gm.toast.added_starter_pack"));
        }

        private void AddGroup(GmItem[] items, int amount, string groupKey, bool refresh = true)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string groupName = LocalizationManager.Get(groupKey);
            if (items == null || items.Length == 0)
            {
                SetStatus(LocalizationManager.Format("ui.gm.status.no_group", groupName));
                return;
            }

            bool addedAll = true;
            for (int i = 0; i < items.Length; i++)
            {
                GmItem item = items[i];
                addedAll &= AddItem(item.ItemId, item.Name, amount, false, false);
            }

            if (refresh)
            {
                WorldMainPanel.Instance?.RefreshNow();
                RefreshRows();
            }

            SetStatus(LocalizationManager.Format(
                addedAll ? "ui.gm.status.added_group" : "ui.gm.status.add_group_partial_failed",
                groupName,
                amount));
            if (refresh)
            {
                Toast.Info(LocalizationManager.Format(
                    addedAll ? "ui.gm.toast.added_group" : "ui.gm.toast.add_group_partial_failed",
                    groupName,
                    amount));
            }
#else
            SetStatus(LocalizationManager.Get("ui.gm.status.dev_only"));
#endif
        }

        private bool AddItem(int itemId, string name, int amount, bool refresh = true, bool showToast = true)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool added = true;
            if (BagManager.IsBagItem(itemId))
            {
                added = BagManager.Instance.TryAddItem(itemId, amount);
            }
            else
            {
                ItemManager.Instance.AddItem(itemId, amount);
            }

            if (refresh)
            {
                WorldMainPanel.Instance?.RefreshNow();
                RefreshRows();
            }

            SetStatus(LocalizationManager.Format(
                added ? "ui.gm.status.added_item" : "ui.gm.status.add_item_failed",
                name,
                amount));
            if (showToast)
            {
                Toast.Info(LocalizationManager.Format(
                    added ? "ui.gm.toast.added_item" : "ui.gm.toast.add_item_failed",
                    name,
                    amount));
            }

            return added;
#else
            SetStatus(LocalizationManager.Get("ui.gm.status.dev_only"));
            return false;
#endif
        }

        private void SetTimeScale(float scale)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CalendarManager.Instance.SetGameTimeScale(scale);
            WorldMainPanel.Instance?.RefreshNow();
            SetStatus($"Time x{scale:0.###}");
            Toast.Info($"Time x{scale:0.###}");
#else
            SetStatus(LocalizationManager.Get("ui.gm.status.dev_only"));
#endif
        }

        private void AddRowItem(WorldGmItemRowView row, int amount)
        {
            if (row == null)
            {
                return;
            }

            AddItem(row.ItemId, row.DisplayName, amount);
        }

        private void BindRows(WorldGmItemRowView[] rows)
        {
            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i]?.Initialize(AddRowItem);
            }
        }

        private void RefreshPanel()
        {
            WorldMainPanel.Instance?.RefreshNow();
            RefreshRows();
            SetStatus(LocalizationManager.Get("ui.gm.status.refreshed"));
        }

        private void RefreshRows()
        {
            RefreshRows(resourceRows);
            RefreshRows(seedRows);
            RefreshRows(cropRows);
        }

        private static void RefreshRows(WorldGmItemRowView[] rows)
        {
            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i]?.Refresh();
            }
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void CloseSelf()
        {
            if (!UIManager.Instance.Panels.PopStack(WorldMenuPanel.SettingsStackGroupId))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private readonly struct GmItem
        {
            public readonly int ItemId;
            public readonly string NameKey;
            public string Name => LocalizationManager.Get(NameKey);

            public GmItem(int itemId, string nameKey)
            {
                ItemId = itemId;
                NameKey = nameKey;
            }
        }
    }
}
