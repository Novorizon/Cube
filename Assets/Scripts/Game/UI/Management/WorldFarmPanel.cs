using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldFarmPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Farm/FarmPanel.prefab";

        public static WorldFarmPanel Instance { get; private set; }

        private readonly List<WorldFarmSeedView> seedEntries = new List<WorldFarmSeedView>();
        private readonly Dictionary<int, WorldFarmSeedView> seedEntriesByCropId = new Dictionary<int, WorldFarmSeedView>();
        private static readonly HashSet<string> MissingCropIconWarnings = new HashSet<string>();

        [SerializeField] private Button closeButton;
        [SerializeField] private WorldFarmSeedView infoView;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Transform seedContent;
        [SerializeField] private WorldFarmSeedView seedPrefab;

        private GameObject root;
        private Farm selectedFarm;
        private Func<int, bool> seedClicked;
        private float nextRefreshTime;

        public GameObject Root => root;
        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        private void Awake()
        {
            Instance = this;
        }

        protected override void OnCreate()
        {
            BindStaticLayout();
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            WorldFloatingPanelLayout.AlignBottomToHotBarGrid(GetComponent<RectTransform>());
            selectedFarm = args as Farm ?? selectedFarm ?? GameplayController.Instance?.SelectedFarm;
            RebuildSeeds();
            RefreshNow();
        }

        protected override void OnClose()
        {
            selectedFarm = null;
        }

        protected override void OnDestroyed()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ClearSeedEntries();
        }

        public void SetSelectedFarm(Farm farm)
        {
            selectedFarm = farm;
            if (IsOpen)
            {
                RefreshNow();
            }
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
            ClearSeedEntries();
            root = gameObject;
            seedClicked = cropId => GameplayController.Instance != null && GameplayController.Instance.TryPlantSelectedFarm(cropId);

            if (root == null)
            {
                return false;
            }

            if (closeButton == null)
            {
                closeButton = FindChildByName(transform, "Close")?.GetComponent<Button>();
            }

            Transform infoRoot = FindDirectChildByName(transform, "Info") ?? FindChildByName(transform, "Info");
            if (infoView == null)
            {
                infoView = infoRoot != null ? infoRoot.GetComponent<WorldFarmSeedView>() : null;
            }

            if (infoText == null)
            {
                infoText = FindTextByName(infoRoot, "Info") ?? FindTextByName(transform, "Info");
            }

            if (seedContent == null)
            {
                seedContent = FindChildByName(transform, "Content");
            }

            if (infoView == null && infoText == null)
            {
                Debug.LogError("[WorldFarmPanel] info view is not assigned on FarmPanel prefab.");
            }

            if (seedContent == null)
            {
                Debug.LogError("[WorldFarmPanel] seedContent is not assigned on FarmPanel prefab.");
            }

            if (seedPrefab == null)
            {
                Debug.LogError("[WorldFarmPanel] seedPrefab is not assigned on FarmPanel prefab.");
            }

            WorldPanelBindingUtility.BindButton(closeButton != null ? closeButton.transform : null, CloseSelf, "Farm close");
            return (infoView != null || infoText != null) && seedContent != null && seedPrefab != null;
        }

        private void RefreshNow()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;
            RefreshInfo();
            RefreshSeeds();
        }

        private void RefreshInfo()
        {
            if (infoView == null && infoText == null)
            {
                return;
            }

            Farm farm = GetActiveFarm();
            if (farm == null)
            {
                SetCropInfo(null, LocalizationManager.Get("ui.main.selected_farm_none"), string.Empty);
                return;
            }

            string cropName = LocalizationManager.Get("ui.common.empty");
            string maturity = "-";
            string output = "-";
            string operation = LocalizationManager.Get("ui.farm.operation.choose_seed");
            Sprite cropIcon = null;
            if (farm.HasCrop &&
                FarmManager.Instance.Crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) &&
                crop != null)
            {
                cropName = LocalizedConfigText.CropName(crop.Id);
                maturity = FormatMaturity(farm.MatureAtUnixTime);
                output = $"{crop.OutputCountPerSecond * farm.CellCount * 60}/min";
                cropIcon = LoadCropIcon(crop);
                operation = IsMature(farm.MatureAtUnixTime)
                    ? LocalizationManager.Get("ui.farm.operation.producing")
                    : LocalizationManager.Get("ui.farm.operation.growing");
            }

            if (infoView != null)
            {
                SetCropInfo(cropIcon, cropName, FormatOutputInfo(output));
                return;
            }

            if (infoText != null)
            {
                infoText.text = LocalizationManager.Format(
                    "ui.farm.info",
                    farm.FarmId,
                    farm.CellCount,
                    cropName,
                    maturity,
                    output,
                    operation);
            }
        }

        private void SetCropInfo(Sprite icon, string cropName, string outputInfo)
        {
            Color textColor = new Color(0.18f, 0.13f, 0.07f, 1f);
            if (infoView != null)
            {
                infoView.SetIcon(icon, Color.white);
                infoView.SetName(cropName, textColor);
                infoView.SetInfo(outputInfo, textColor);
                infoView.SetClick(null, false);
                infoView.SetBackgroundAlpha(0.96f);
                return;
            }

            if (infoText != null)
            {
                infoText.text = string.IsNullOrEmpty(outputInfo) ? cropName : $"{cropName}\n{outputInfo}";
            }
        }

        private static string FormatOutputInfo(string output)
        {
            string label = LocalizationManager.CurrentLanguage == LocalizationManager.English ? "Output" : "产量";
            return $"{label}: {output}";
        }

        private void RefreshSeeds()
        {
            if (seedContent == null || seedPrefab == null)
            {
                return;
            }

            if (seedEntriesByCropId.Count != FarmManager.Instance.Crops.Count)
            {
                RebuildSeeds();
                return;
            }

            foreach (KeyValuePair<int, WorldCropDefinition> pair in FarmManager.Instance.Crops)
            {
                if (seedEntriesByCropId.TryGetValue(pair.Key, out WorldFarmSeedView entry))
                {
                    RefreshSeedEntry(pair.Value, entry);
                }
            }
        }

        private void RebuildSeeds()
        {
            ClearSeedEntries();
            if (seedContent == null || seedPrefab == null)
            {
                return;
            }

            foreach (KeyValuePair<int, WorldCropDefinition> pair in FarmManager.Instance.Crops)
            {
                CreateSeedEntry(pair.Value);
            }
        }

        private void CreateSeedEntry(WorldCropDefinition crop)
        {
            if (crop == null)
            {
                return;
            }

            WorldFarmSeedView entry = UnityEngine.Object.Instantiate(seedPrefab, seedContent, false);
            entry.name = $"Seed_{crop.Id}";
            entry.gameObject.SetActive(true);
            seedEntries.Add(entry);
            seedEntriesByCropId[crop.Id] = entry;

            RefreshSeedEntry(crop, entry);
        }

        private void RefreshSeedEntry(WorldCropDefinition crop, WorldFarmSeedView entry)
        {
            if (crop == null || entry == null)
            {
                return;
            }

            entry.SetBackground(new Color(0.98f, 0.91f, 0.78f, 0.96f));

            Farm farm = GetActiveFarm();
            int cellCount = farm != null ? farm.CellCount : 0;
            int need = crop.SeedItemId > 0 ? GetSeedCostPerCell(crop) * cellCount : 0;
            int have = crop.SeedItemId > 0 ? ItemManager.Instance.GetCount(crop.SeedItemId) : 0;
            bool enoughSeed = crop.SeedItemId <= 0 || need <= 0 || have >= need;
            bool canPlant = farm != null && !farm.HasCrop && enoughSeed;

            Color textColor = canPlant ? new Color(0.18f, 0.13f, 0.07f, 1f) : new Color(0.42f, 0.36f, 0.28f, 1f);
            string seedCost = FormatSeedCost(crop.SeedItemId, have, need);
            string output = crop.OutputCountPerSecond > 0 && cellCount > 0 ? $"\n{crop.OutputCountPerSecond * cellCount * 60}/min" : string.Empty;
            string state = farm != null && farm.HasCrop
                ? "\n" + LocalizationManager.Get("ui.farm.state.planted")
                : enoughSeed
                    ? string.Empty
                    : "\n" + LocalizationManager.Get("ui.farm.state.not_enough");

            entry.SetIcon(LoadCropIcon(crop), canPlant ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            entry.SetName(LocalizedConfigText.CropName(crop.Id), textColor);
            entry.SetInfo($"{seedCost}{output}{state}", textColor);
            entry.SetClick(() =>
            {
                if (seedClicked != null && seedClicked(crop.Id))
                {
                    Refresh();
                    WorldMainPanel.Instance?.RefreshNow();
                }
            }, canPlant);
            entry.SetBackgroundAlpha(canPlant ? 0.96f : 0.58f);
        }

        private static string FormatSeedCost(int seedItemId, int have, int need)
        {
            if (seedItemId <= 0)
            {
                return LocalizationManager.Get("ui.farm.no_seed_cost");
            }

            if (need > 0)
            {
                return LocalizationManager.Format("ui.farm.seed_cost", have, need);
            }

            string label = LocalizationManager.CurrentLanguage == LocalizationManager.English ? "Seed" : "种子";
            return $"{label} {have}";
        }

        private static int GetSeedCostPerCell(WorldCropDefinition crop)
        {
            if (crop == null || crop.SeedItemId <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, crop.SeedCost);
        }

        private Farm GetActiveFarm()
        {
            return selectedFarm ?? GameplayController.Instance?.SelectedFarm;
        }

        private void CloseSelf()
        {
            if (CanCloseBy(UICloseReason.CloseButton))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private static Sprite LoadCropIcon(WorldCropDefinition crop)
        {
            if (crop == null || crop.OutputItemId <= 0)
            {
                return null;
            }

            if (DataManager.Instance.Item == null ||
                !DataManager.Instance.Item.TryGet(crop.OutputItemId, out ItemConfig itemConfig) ||
                itemConfig == null ||
                string.IsNullOrWhiteSpace(itemConfig.IconLocation))
            {
                return null;
            }

            string location = itemConfig.IconLocation;
            if (!location.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingCropIconWarnings.Add(location))
                {
                    Debug.LogWarning($"Farm crop icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite == null && MissingCropIconWarnings.Add(location))
            {
                Debug.LogWarning($"Farm crop icon load failed. location: {location}");
            }

            return sprite;
        }

        private void ClearSeedEntries()
        {
            for (int i = 0; i < seedEntries.Count; i++)
            {
                if (seedEntries[i] != null)
                {
                    UnityEngine.Object.Destroy(seedEntries[i].gameObject);
                }
            }

            seedEntries.Clear();
            seedEntriesByCropId.Clear();
        }

        private static string FormatMaturity(long matureAtUnixTime)
        {
            if (matureAtUnixTime <= 0)
            {
                return "-";
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remaining = matureAtUnixTime - now;
            if (remaining <= 0)
            {
                return LocalizationManager.Get("ui.common.ready");
            }

            return LocalizationManager.Format("ui.time.remain", FormatDuration(remaining));
        }

        private static bool IsMature(long matureAtUnixTime)
        {
            return matureAtUnixTime > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= matureAtUnixTime;
        }

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}s";
            }

            long minutes = seconds / 60;
            long remainSeconds = seconds % 60;
            if (minutes < 60)
            {
                return remainSeconds > 0 ? $"{minutes}m {remainSeconds}s" : $"{minutes}m";
            }

            long hours = minutes / 60;
            long remainMinutes = minutes % 60;
            return remainMinutes > 0 ? $"{hours}h {remainMinutes}m" : $"{hours}h";
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            Transform direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDirectChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static TMP_Text FindTextByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName && root.TryGetComponent(out TMP_Text rootText))
            {
                return rootText;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                TMP_Text childText = FindTextByName(root.GetChild(i), childName);
                if (childText != null)
                {
                    return childText;
                }
            }

            return null;
        }
    }
}
