using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class WorldFarmPanel
    {
        private const string SeedPrefabPath = "Assets/Arts/UI/Panels/Seed.prefab";

        private readonly List<GameObject> seedEntries = new List<GameObject>();
        private readonly Dictionary<int, GameObject> seedEntriesByCropId = new Dictionary<int, GameObject>();
        private static readonly HashSet<string> MissingCropIconWarnings = new HashSet<string>();
        private GameObject root;
        private TMP_Text infoText;
        private Transform seedContent;
        private GameObject seedPrefab;
        private Farm selectedFarm;
        private Func<int, bool> seedClicked;

        public GameObject Root => root;

        public bool Bind(Transform rootTransform, Action closeClicked, Func<int, bool> onSeedClicked)
        {
            ClearSeedEntries();
            root = rootTransform != null ? rootTransform.gameObject : null;
            selectedFarm = null;
            seedClicked = onSeedClicked;

            if (rootTransform == null)
            {
                infoText = null;
                seedContent = null;
                seedPrefab = null;
                return false;
            }

            infoText = FindText(rootTransform, "Info");
            seedContent = FindChild(rootTransform, "Content");
            seedPrefab = GetSeedPrefab();
            if (seedContent == null)
            {
                Debug.LogError("[WorldFarmPanel] Missing static Content node. Expected FarmPanel/Scroll View/Viewport/Content.");
            }

            WorldPanelBindingUtility.BindButton(rootTransform.Find("Close"), () => closeClicked?.Invoke(), "Farm close");
            return infoText != null;
        }

        public void Show(Farm farm)
        {
            selectedFarm = farm;
            if (root != null)
            {
                root.SetActive(true);
            }

            RebuildSeeds();
            Refresh();
        }

        public void Hide()
        {
            selectedFarm = null;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Refresh()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            RefreshInfo();
            RefreshSeeds();
        }

        private void RefreshInfo()
        {
            if (infoText == null)
            {
                return;
            }

            if (selectedFarm == null)
            {
                infoText.text = LocalizationManager.Get("ui.main.selected_farm_none");
                return;
            }

            string cropName = LocalizationManager.Get("ui.common.empty");
            string maturity = "-";
            string output = "-";
            string operation = LocalizationManager.Get("ui.farm.operation.choose_seed");
            if (selectedFarm.HasCrop &&
                FarmManager.Instance.Crops.TryGetValue(selectedFarm.CropId, out WorldCropDefinition crop) &&
                crop != null)
            {
                cropName = LocalizedConfigText.CropName(crop.Id);
                maturity = FormatMaturity(selectedFarm.MatureAtUnixTime);
                output = $"{crop.OutputCountPerSecond * selectedFarm.CellCount * 60}/min";
                operation = IsMature(selectedFarm.MatureAtUnixTime)
                    ? LocalizationManager.Get("ui.farm.operation.producing")
                    : LocalizationManager.Get("ui.farm.operation.growing");
            }

            infoText.text = LocalizationManager.Format(
                "ui.farm.info",
                selectedFarm.FarmId,
                selectedFarm.CellCount,
                cropName,
                maturity,
                output,
                operation);
        }

        private void RefreshSeeds()
        {
            if (seedContent == null || GetSeedPrefab() == null)
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
                if (seedEntriesByCropId.TryGetValue(pair.Key, out GameObject entry))
                {
                    RefreshSeedEntry(pair.Value, entry);
                }
            }
        }

        private void RebuildSeeds()
        {
            ClearSeedEntries();
            if (seedContent == null || GetSeedPrefab() == null)
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

            GameObject entry = UnityEngine.Object.Instantiate(GetSeedPrefab(), seedContent, false);
            entry.name = $"Seed_{crop.Id}";
            entry.SetActive(true);
            seedEntries.Add(entry);
            seedEntriesByCropId[crop.Id] = entry;

            RefreshSeedEntry(crop, entry);
        }

        private void RefreshSeedEntry(WorldCropDefinition crop, GameObject entry)
        {
            if (crop == null || entry == null)
            {
                return;
            }

            Image background = entry.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.98f, 0.91f, 0.78f, 0.96f);
            }

            Image icon = FindImage(entry.transform, "Icon");
            TMP_Text nameText = FindText(entry.transform, "Name");
            TMP_Text seedInfoText = FindText(entry.transform, "Info");
            int cellCount = selectedFarm != null ? selectedFarm.CellCount : 0;
            int need = Mathf.Max(0, crop.SeedCost) * cellCount;
            int have = crop.SeedItemId > 0 ? WorldItemManager.Instance.GetCount(crop.SeedItemId) : 0;
            bool enoughSeed = crop.SeedItemId <= 0 || need <= 0 || have >= need;
            bool canPlant = selectedFarm != null && !selectedFarm.HasCrop && enoughSeed;

            if (icon != null)
            {
                Sprite sprite = LoadCropIcon(crop);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.gameObject.SetActive(sprite != null);
                icon.color = canPlant ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (nameText != null)
            {
                nameText.text = LocalizedConfigText.CropName(crop.Id);
                nameText.color = canPlant ? new Color(0.18f, 0.13f, 0.07f, 1f) : new Color(0.42f, 0.36f, 0.28f, 1f);
            }

            if (seedInfoText != null)
            {
                string seedCost = crop.SeedItemId > 0 && need > 0
                    ? LocalizationManager.Format("ui.farm.seed_cost", have, need)
                    : LocalizationManager.Get("ui.farm.no_seed_cost");
                string output = crop.OutputCountPerSecond > 0 && cellCount > 0 ? $"\n{crop.OutputCountPerSecond * cellCount * 60}/min" : string.Empty;
                string state = selectedFarm != null && selectedFarm.HasCrop
                    ? "\n" + LocalizationManager.Get("ui.farm.state.planted")
                    : enoughSeed
                        ? string.Empty
                        : "\n" + LocalizationManager.Get("ui.farm.state.not_enough");
                seedInfoText.text = $"{seedCost}{output}{state}";
                seedInfoText.color = canPlant ? new Color(0.18f, 0.13f, 0.07f, 1f) : new Color(0.42f, 0.36f, 0.28f, 1f);
            }

            Button button = entry.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = canPlant;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (seedClicked != null && seedClicked(crop.Id))
                    {
                        Refresh();
                    }
                });
            }

            if (background != null)
            {
                Color color = background.color;
                color.a = canPlant ? 0.96f : 0.58f;
                background.color = color;
            }
        }

        private GameObject GetSeedPrefab()
        {
            if (seedPrefab != null)
            {
                return seedPrefab;
            }

            seedPrefab = ResourceManager.Instance.LoadGameObject(SeedPrefabPath);
            if (seedPrefab == null)
            {
                Debug.LogError($"[WorldFarmPanel] Missing seed prefab: {SeedPrefabPath}");
            }

            return seedPrefab;
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
                    UnityEngine.Object.Destroy(seedEntries[i]);
                }
            }

            seedEntries.Clear();
            seedEntriesByCropId.Clear();
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Image FindImage(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChild(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
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
    }
}
