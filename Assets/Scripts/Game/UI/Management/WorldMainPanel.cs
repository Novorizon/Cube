using System.Collections.Generic;
using Game.Framework;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldMainPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Management/Prefabs/WorldMainPanel.prefab";

        public static WorldMainPanel Instance { get; private set; }

        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);

        private RectTransform rootRect;
        private Text resourceText;
        private Text statusText;
        private Text selectedBuildingText;
        private GameObject buildPanelRoot;
        private Transform buildButtonContainer;
        private float nextRefreshTime;
        private int lastBuildListStateHash;
        private static bool openingPrefab;

        public static void Ensure()
        {
            if (Instance != null || openingPrefab)
            {
                return;
            }

            if (TryCreateEditorPrefabInstance())
            {
                return;
            }

            if (ShouldTryOpenPrefab())
            {
                OpenPrefabAsync();
                return;
            }

            CreateRuntimeInstance();
        }

        private static void CreateRuntimeInstance()
        {
            Transform parent = UIManager.Instance.transform.Find("UICanvasRoot/Layer_Panel");
            if (parent == null)
            {
                parent = UIManager.Instance.transform;
            }

            GameObject panelObject = new GameObject("WorldMainPanel");
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Instance = panelObject.AddComponent<WorldMainPanel>();
            Instance.BuildIfNeeded();
            Instance.RefreshNow();
        }

        private static async void OpenPrefabAsync()
        {
            openingPrefab = true;
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(PrefabPath);
            openingPrefab = false;

            if (Instance != null)
            {
                Instance.RefreshNow();
                return;
            }

            if (handle.IsValid && handle.View is WorldMainPanel panel)
            {
                Instance = panel;
                Instance.BuildIfNeeded();
                Instance.RefreshNow();
                return;
            }

            CreateRuntimeInstance();
        }

        private static bool ShouldTryOpenPrefab()
        {
            return ResourceManager.Instance.Initialized && ResourceManager.Instance.IsValid(PrefabPath);
        }

        private static bool TryCreateEditorPrefabInstance()
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                return false;
            }

            Transform parent = UIManager.Instance.transform.Find("UICanvasRoot/Layer_Panel");
            if (parent == null)
            {
                parent = UIManager.Instance.transform;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;

            Instance = instance.GetComponent<WorldMainPanel>();
            if (Instance == null)
            {
                Instance = instance.AddComponent<WorldMainPanel>();
            }

            Instance.BuildIfNeeded();
            Instance.RefreshNow();
            return true;
#else
            return false;
#endif
        }

        public static void Shutdown()
        {
            if (Instance == null)
            {
                return;
            }

            Destroy(Instance.gameObject);
            Instance = null;
        }

        public void RefreshNow()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        public void RebuildDefaultLayout(bool refreshAfterBuild)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            rootRect = null;
            resourceText = null;
            statusText = null;
            selectedBuildingText = null;
            buildPanelRoot = null;
            buildButtonContainer = null;
            lastBuildListStateHash = 0;

            ClearChildren(rectTransform);

            BuildIfNeeded();

            if (refreshAfterBuild)
            {
                RefreshNow();
            }
        }

        protected override void OnCreate()
        {
            BuildIfNeeded();
        }

        protected override void OnDestroyed()
        {
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

        private void BuildIfNeeded()
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

            CreateHud();
            CreateBuildPanel();
        }

        private bool TryBindExistingLayout()
        {
            Transform hud = rootRect.Find("Hud");
            Transform buildPanel = rootRect.Find("BuildPanel");
            if (hud == null || buildPanel == null)
            {
                return false;
            }

            statusText = FindText(hud, "Status");
            resourceText = FindText(hud, "Resources");
            selectedBuildingText = FindText(buildPanel, "Selected");
            buildPanelRoot = buildPanel.gameObject;
            buildButtonContainer = EnsureBuildScrollLayout(buildPanel);

            if (statusText == null || resourceText == null || selectedBuildingText == null || buildButtonContainer == null)
            {
                statusText = null;
                resourceText = null;
                selectedBuildingText = null;
                buildPanelRoot = null;
                buildButtonContainer = null;
                return false;
            }

            ApplyDefaultVisualSettings(hud, buildPanel);
            BindCancelButton(buildPanel);
            return true;
        }

        private void CreateHud()
        {
            GameObject hud = CreatePanelObject("Hud", rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(620f, 220f));

            VerticalLayoutGroup layout = hud.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateText("Title", hud.transform, "World", 24, TextAnchor.MiddleLeft, Color.white);
            AddLayout(title.gameObject, 34f);

            statusText = CreateText("Status", hud.transform, "Map 0   Base Not Built\nLMB select/build/farm   RMB move   WASD camera   Wheel height", 18, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.92f));
            AddLayout(statusText.gameObject, 50f);

            resourceText = CreateText("Resources", hud.transform, "Gold 0   Wood 0   Stone 0   Food 0\nCopper 0   Iron 0\nWheat 0   Tomato 0   Herb 0   Flower 0", 18, TextAnchor.UpperLeft, Color.white);
            resourceText.verticalOverflow = VerticalWrapMode.Overflow;
            AddLayout(resourceText.gameObject, 92f);
        }

        private void CreateBuildPanel()
        {
            GameObject panel = CreatePanelObject("BuildPanel", rootRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(440f, -40f));
            buildPanelRoot = panel;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateText("Title", panel.transform, "Buildings", 24, TextAnchor.MiddleLeft, Color.white);
            AddLayout(title.gameObject, 34f);

            selectedBuildingText = CreateText("Selected", panel.transform, "Selected: None", 18, TextAnchor.MiddleLeft, new Color(0.86f, 0.9f, 0.92f));
            AddLayout(selectedBuildingText.gameObject, 28f);

            CreateButton(panel.transform, "CancelBuild", "Cancel Build", true, new Color(0.24f, 0.24f, 0.25f, 0.94f), ClearSelectedBuildingFromButton);
            buildButtonContainer = EnsureBuildScrollLayout(panel.transform);
        }

        private void BindCancelButton(Transform buildPanel)
        {
            Transform cancelTransform = buildPanel.Find("CancelBuild");
            Button cancelButton = cancelTransform != null ? cancelTransform.GetComponent<Button>() : null;
            if (cancelButton == null)
            {
                return;
            }

            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(ClearSelectedBuildingFromButton);
        }

        private void ClearSelectedBuildingFromButton()
        {
            WorldGameplayController.Instance?.ClearSelectedBuilding();
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
            bool hasBase = WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.MainBase);
            int selectedBuildingId = WorldGameplayController.Instance != null ? WorldGameplayController.Instance.SelectedBuildingId : 0;

            statusText.text =
                $"Map {mapId}   Base {(hasBase ? "Built" : "Select Main Base")}\n" +
                $"LMB select/build/farm   RMB move   WASD camera   Wheel height";

            resourceText.text =
                $"Gold {GetItemCount(ItemIds.Gold)}   Wood {GetItemCount(ItemIds.Wood)}   Stone {GetItemCount(ItemIds.Stone)}   Food {GetItemCount(ItemIds.Food)}\n" +
                $"Copper {GetItemCount(ItemIds.CopperOre)}   Iron {GetItemCount(ItemIds.IronOre)}\n" +
                $"Wheat {GetItemCount(ItemIds.Wheat)}   Tomato {GetItemCount(ItemIds.Tomato)}   Herb {GetItemCount(ItemIds.Herb)}   Flower {GetItemCount(ItemIds.Flower)}";

            buildPanelRoot.SetActive(true);

            selectedBuildingText.text = selectedBuildingId > 0 ? $"Selected: {GetBuildingName(selectedBuildingId)}" : "Selected: None";
            int buildListStateHash = CalculateBuildListStateHash(selectedBuildingId, hasBase);
            if (buildListStateHash != lastBuildListStateHash)
            {
                lastBuildListStateHash = buildListStateHash;
                RebuildBuildingButtons(selectedBuildingId, hasBase);
            }
        }

        private void RebuildBuildingButtons(int selectedBuildingId, bool hasBase)
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
                if (config == null || !config.Enable || ShouldHideFromBuildPanel(config, hasBase))
                {
                    continue;
                }

                buildableConfigs.Add(config);
            }

            buildableConfigs.Sort((left, right) => left.Id.CompareTo(right.Id));
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

            if (resourceText != null &&
                statusText != null &&
                selectedBuildingText != null &&
                buildPanelRoot != null &&
                buildButtonContainer != null)
            {
                return true;
            }

            resourceText = null;
            statusText = null;
            selectedBuildingText = null;
            buildPanelRoot = null;
            buildButtonContainer = null;
            lastBuildListStateHash = 0;

            return TryBindExistingLayout();
        }

        private int CalculateBuildListStateHash(int selectedBuildingId, bool hasBase)
        {
            unchecked
            {
                int hash = selectedBuildingId;
                hash = hash * 397 ^ (hasBase ? 1 : 0);
                hash = hash * 397 ^ GetItemCount(ItemIds.Gold);
                hash = hash * 397 ^ GetItemCount(ItemIds.Wood);
                hash = hash * 397 ^ GetItemCount(ItemIds.Stone);
                hash = hash * 397 ^ GetItemCount(ItemIds.Food);
                hash = hash * 397 ^ GetItemCount(ItemIds.CopperOre);
                hash = hash * 397 ^ GetItemCount(ItemIds.IronOre);
                return hash;
            }
        }

        private void CreateBuildingButton(WorldBuildingConfig config, int selectedBuildingId)
        {
            bool unlocked = WorldBuildingManager.Instance.IsBuildingUnlocked(config.Id);
            bool hasCost = HasBuildCost(config.Id, out string costText);
            bool interactable = unlocked && hasCost;
            string label = $"{config.Name}  {costText}";

            if (!unlocked)
            {
                label = $"{config.Name}  Locked";
            }
            else if (!hasCost)
            {
                label = $"{config.Name}  Need {costText}";
            }

            Color color = new Color(0.18f, 0.24f, 0.28f, 0.94f);
            if (selectedBuildingId == config.Id)
            {
                color = new Color(0.16f, 0.42f, 0.76f, 0.96f);
            }
            else if (!unlocked)
            {
                color = new Color(0.16f, 0.16f, 0.17f, 0.88f);
            }
            else if (!hasCost)
            {
                color = new Color(0.32f, 0.22f, 0.18f, 0.9f);
            }

            CreateButton(buildButtonContainer, $"Build_{config.Id}", label, interactable, color, () =>
            {
                WorldGameplayController.Instance?.SelectBuilding(config.Id);
                RefreshNow();
            });
        }

        private bool HasBuildCost(int buildingId, out string costText)
        {
            costText = "Free";
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                costText = "Config";
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

        private string FormatCosts(IReadOnlyList<WorldItem> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return "Free";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {cost.Count}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "Free";
        }

        private string GetBuildingName(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding != null &&
                DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                return config.Name;
            }

            return buildingId.ToString();
        }

        private string GetItemName(int itemId)
        {
            if (DataManager.Instance.Item != null &&
                DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                return config.Name;
            }

            return itemId.ToString();
        }

        private int GetItemCount(int itemId)
        {
            return WorldItemManager.Instance.GetCount(itemId);
        }

        private static bool ShouldHideFromBuildPanel(WorldBuildingConfig config, bool hasBase)
        {
            WorldBuildingType buildingType = (WorldBuildingType)config.BuildingType;
            if (buildingType == WorldBuildingType.MainBase)
            {
                return hasBase;
            }

            if (!hasBase)
            {
                return true;
            }

            return buildingType == WorldBuildingType.FarmPlot ||
                   buildingType == WorldBuildingType.Mine;
        }

        private GameObject CreatePanelObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.055f, 0.065f, 0.075f, 0.88f);

            return panel;
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return text;
        }

        private static Text FindText(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Text>() : null;
        }

        private Transform EnsureBuildScrollLayout(Transform buildPanel)
        {
            if (buildPanel == null)
            {
                return null;
            }

            Transform scrollView = buildPanel.Find("ScrollView");
            if (scrollView == null)
            {
                GameObject scrollObject = new GameObject("ScrollView");
                scrollObject.transform.SetParent(buildPanel, false);
                scrollView = scrollObject.transform;

                RectTransform scrollRectTransform = scrollObject.AddComponent<RectTransform>();
                scrollRectTransform.anchorMin = Vector2.zero;
                scrollRectTransform.anchorMax = Vector2.one;
                scrollRectTransform.offsetMin = Vector2.zero;
                scrollRectTransform.offsetMax = Vector2.zero;

                Image scrollBackground = scrollObject.AddComponent<Image>();
                scrollBackground.color = new Color(0.035f, 0.043f, 0.052f, 0.74f);

                LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
                scrollLayout.minHeight = 220f;
                scrollLayout.flexibleHeight = 1f;

                ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.scrollSensitivity = 26f;

                GameObject viewportObject = new GameObject("Viewport");
                viewportObject.transform.SetParent(scrollView, false);
                RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(6f, 6f);
                viewportRect.offsetMax = new Vector2(-6f, -6f);

                Image viewportImage = viewportObject.AddComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

                Mask mask = viewportObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                GameObject contentObject = new GameObject("Content");
                contentObject.transform.SetParent(viewportObject.transform, false);
                RectTransform contentRect = contentObject.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = Vector2.zero;

                VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
                contentLayout.spacing = 8f;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;

                ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.viewport = viewportRect;
                scrollRect.content = contentRect;
            }

            Transform viewport = scrollView.Find("Viewport");
            Transform content = viewport != null ? viewport.Find("Content") : null;
            if (content == null)
            {
                return null;
            }

            MoveLegacyBuildButtonsToContent(buildPanel, content);
            return content;
        }

        private void ApplyDefaultVisualSettings(Transform hud, Transform buildPanel)
        {
            SetRect(hud as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(620f, 220f));
            SetRect(buildPanel as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(440f, -40f));

            SetTextSize(hud.Find("Title"), 24);
            SetTextSize(hud.Find("Status"), 18);
            SetTextSize(hud.Find("Resources"), 18);
            SetTextSize(buildPanel.Find("Title"), 24);
            SetTextSize(buildPanel.Find("Selected"), 18);

            SetLayoutHeight(hud.Find("Title"), 34f);
            SetLayoutHeight(hud.Find("Status"), 50f);
            SetLayoutHeight(hud.Find("Resources"), 92f);
            SetLayoutHeight(buildPanel.Find("Title"), 34f);
            SetLayoutHeight(buildPanel.Find("Selected"), 28f);
            SetLayoutHeight(buildPanel.Find("CancelBuild"), 48f);
        }

        private void MoveLegacyBuildButtonsToContent(Transform buildPanel, Transform content)
        {
            if (buildPanel == null || content == null)
            {
                return;
            }

            List<Transform> legacyChildren = new List<Transform>();
            for (int i = 0; i < buildPanel.childCount; i++)
            {
                Transform child = buildPanel.GetChild(i);
                if (child == null ||
                    child.name == "Title" ||
                    child.name == "Selected" ||
                    child.name == "CancelBuild" ||
                    child.name == "ScrollView")
                {
                    continue;
                }

                legacyChildren.Add(child);
            }

            for (int i = 0; i < legacyChildren.Count; i++)
            {
                if (legacyChildren[i] != null && content != null)
                {
                    legacyChildren[i].SetParent(content, false);
                }
            }
        }

        private GameObject CreateButton(Transform parent, string name, string label, bool interactable, Color color, UnityEngine.Events.UnityAction clicked)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;

            Button button = buttonObject.AddComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(clicked);

            AddLayout(buttonObject, 48f);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = interactable ? Color.white : new Color(0.62f, 0.62f, 0.62f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return buttonObject;
        }

        private static void AddLayout(GameObject gameObject, float preferredHeight)
        {
            LayoutElement layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetTextSize(Transform transform, int fontSize)
        {
            Text text = transform != null ? transform.GetComponent<Text>() : null;
            if (text != null)
            {
                text.fontSize = fontSize;
            }
        }

        private static void SetLayoutHeight(Transform transform, float height)
        {
            if (transform == null)
            {
                return;
            }

            LayoutElement layout = transform.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = transform.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            List<GameObject> children = new List<GameObject>();
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.gameObject != null)
                {
                    children.Add(child.gameObject);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                DestroyUnityObject(children[i]);
            }
        }

        private static void DestroyUnityObject(Object target)
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
