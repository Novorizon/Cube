using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechTreePanel : UIPanel, IScrollHandler
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/TechTree/TechTreePanel.prefab";

        private const string BranchPrefabPath = "Assets/Arts/UI/Panels/TechTree/TechBranch.prefab";
        private const float BranchHeight = 138f;
        private const float BranchTopPadding = 24f;
        private const float BranchBottomPadding = 24f;
        private const float TitleColumnTextPadding = 14f;
        private const float WheelScrollStep = 0.08f;

        [SerializeField] private ScrollRect branchScrollRect;
        [SerializeField] private ScrollRect titleScrollRect;
        [SerializeField] private RectTransform branchContentRoot;
        [SerializeField] private RectTransform titleContentRoot;
        [SerializeField] private Scrollbar horizontalScrollbar;
        [SerializeField] private Scrollbar verticalScrollbar;
        [SerializeField] private Button closeButton;

        private RectTransform rootRect;
        private GameObject branchPrefab;
        private readonly List<TechBranchView> branchViews = new List<TechBranchView>();
        private bool suppressHorizontalScrollbarEvent;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticLayout();
            LocalizationManager.LanguageChanged += Refresh;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, TechChangedMessage>(WorldMessageTopic.TechChanged, _ => Refresh()));
            Refresh();
        }

        private void BindStaticLayout()
        {
            rootRect = GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            if (!ValidateReferences())
            {
                return;
            }

            ConfigureScrollRects();
            ConfigureContentRootRect(branchContentRoot);
            ConfigureContentRootRect(titleContentRoot);
            AlignHorizontalScrollbarToBranchViewport();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() =>
                {
                    if (CanCloseBy(UICloseReason.CloseButton))
                    {
                        UIManager.Instance.Panels.Hide(PrefabPath);
                    }
                });
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            ScrollVerticalByWheel(eventData);
        }

        public void ScrollVerticalByWheel(PointerEventData eventData)
        {
            if (branchScrollRect == null)
            {
                return;
            }

            float delta = eventData != null ? eventData.scrollDelta.y : 0f;
            if (Mathf.Abs(delta) <= 0.01f)
            {
                return;
            }

            branchScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                branchScrollRect.verticalNormalizedPosition + delta * WheelScrollStep);
            SyncTitleScroll();
            eventData?.Use();
        }

        private void Refresh()
        {
            if (branchContentRoot == null || titleContentRoot == null)
            {
                Debug.LogError("[TechTreePanel] Invalid prefab layout. Missing branch/title ScrollView Content.");
                return;
            }

            ClearChildren(branchContentRoot);
            ClearChildren(titleContentRoot);
            branchViews.Clear();

            IReadOnlyDictionary<int, TechNodeConfig> configs = DataManager.Instance.TechNode?.GetAll();
            if (configs == null || configs.Count == 0)
            {
                Debug.LogWarning("[TechTreePanel] Tech config is empty.");
                return;
            }

            Dictionary<TechBranch, List<TechNodeConfig>> branches = CollectBranches(configs);
            float contentWidth = CalculateContentWidth();
            float y = -BranchTopPadding;
            int branchCount = 0;

            foreach (TechBranch branch in GetBranchOrder())
            {
                if (!branches.TryGetValue(branch, out List<TechNodeConfig> nodes) || nodes.Count == 0)
                {
                    continue;
                }

                CreateBranchTitle(branch, y);
                CreateBranch(branch, nodes, y, contentWidth);
                y -= BranchHeight;
                branchCount++;
            }

            float contentHeight = CalculateContentHeight(branchCount);
            branchContentRoot.sizeDelta = new Vector2(contentWidth, contentHeight);
            titleContentRoot.sizeDelta = new Vector2(GetTitleContentWidth(), contentHeight);
            Canvas.ForceUpdateCanvases();
            RefreshHorizontalScrollbar();
            RefreshScrollOverflow();
            ResetScrollPosition();
        }

        private void CreateBranch(TechBranch branch, List<TechNodeConfig> nodes, float y, float width)
        {
            GameObject prefab = GetBranchPrefab();
            if (prefab == null)
            {
                return;
            }

            GameObject row = Instantiate(prefab, branchContentRoot, false);
            row.name = $"Branch_{branch}";

            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(0f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, y);
                rowRect.sizeDelta = new Vector2(width, BranchHeight - 10f);
            }

            TechBranchView branchView = row.GetComponent<TechBranchView>();
            if (branchView == null)
            {
                Debug.LogError($"[TechTreePanel] Missing TechBranchView on prefab: {BranchPrefabPath}");
                return;
            }

            branchView.Bind(branch, nodes, OnTechClicked);
            branchView.SetTitleVisible(false);
            branchViews.Add(branchView);
        }

        private void CreateBranchTitle(TechBranch branch, float y)
        {
            GameObject titleObject = new GameObject($"Title_{branch}");
            titleObject.transform.SetParent(titleContentRoot, false);

            RectTransform titleRect = titleObject.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, y);
            titleRect.sizeDelta = new Vector2(GetTitleContentWidth(), BranchHeight - 10f);

            titleObject.AddComponent<CanvasRenderer>();
            TMP_Text text = titleObject.AddComponent<TextMeshProUGUI>();
            text.text = GetBranchName(branch);
            text.color = GetBranchTextColor(branch);
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
        }

        private void OnTechClicked(int techId)
        {
            ShowTechUnlockPanelAsync(techId).Forget();
        }

        private async System.Threading.Tasks.Task ShowTechUnlockPanelAsync(int techId)
        {
            await UIManager.Instance.Panels.ShowAsync(TechUnlockPanel.PrefabPath, new TechUnlockPanel.Args
            {
                TechId = techId,
            });
        }

        private GameObject GetBranchPrefab()
        {
            if (branchPrefab != null)
            {
                return branchPrefab;
            }

            branchPrefab = ResourceManager.Instance.LoadGameObject(BranchPrefabPath);
            if (branchPrefab == null)
            {
                Debug.LogError($"[TechTreePanel] Missing tech branch prefab: {BranchPrefabPath}");
            }

            return branchPrefab;
        }

        private static Dictionary<TechBranch, List<TechNodeConfig>> CollectBranches(IReadOnlyDictionary<int, TechNodeConfig> configs)
        {
            Dictionary<TechBranch, List<TechNodeConfig>> result = new Dictionary<TechBranch, List<TechNodeConfig>>();
            foreach (KeyValuePair<int, TechNodeConfig> pair in configs)
            {
                TechNodeConfig config = pair.Value;
                if (config == null || !config.Enable)
                {
                    continue;
                }

                TechBranch branch = ToBranch(config.Branch);
                if (!result.TryGetValue(branch, out List<TechNodeConfig> nodes))
                {
                    nodes = new List<TechNodeConfig>();
                    result.Add(branch, nodes);
                }

                nodes.Add(config);
            }

            foreach (List<TechNodeConfig> nodes in result.Values)
            {
                nodes.Sort(CompareTechNodes);
            }

            return result;
        }

        private float CalculateContentWidth()
        {
            float viewportWidth = branchScrollRect != null && branchScrollRect.viewport != null
                ? branchScrollRect.viewport.rect.width
                : 0f;
            if (viewportWidth <= 0f && branchContentRoot != null && branchContentRoot.parent is RectTransform parentRect)
            {
                viewportWidth = parentRect.rect.width;
            }

            return Mathf.Max(1f, viewportWidth);
        }

        private float CalculateContentHeight(int branchCount)
        {
            float usedHeight = BranchTopPadding + branchCount * BranchHeight + BranchBottomPadding;
            float viewportHeight = branchScrollRect != null && branchScrollRect.viewport != null
                ? branchScrollRect.viewport.rect.height
                : 0f;
            return Mathf.Max(usedHeight, viewportHeight);
        }

        private bool ValidateReferences()
        {
            if (branchScrollRect != null &&
                titleScrollRect != null &&
                branchContentRoot != null &&
                titleContentRoot != null &&
                horizontalScrollbar != null &&
                verticalScrollbar != null &&
                closeButton != null)
            {
                return true;
            }

            Debug.LogError("[TechTreePanel] Invalid prefab references. Please wire ScrollRects, content roots, scrollbars, and close button on the prefab.");
            return false;
        }

        private void ConfigureScrollRects()
        {
            if (branchScrollRect == null)
            {
                return;
            }

            branchScrollRect.content = branchContentRoot;
            branchScrollRect.horizontal = false;
            branchScrollRect.vertical = true;
            branchScrollRect.verticalScrollbar = verticalScrollbar;
            branchScrollRect.horizontalScrollbar = null;
            branchScrollRect.movementType = ScrollRect.MovementType.Clamped;
            if (branchScrollRect.scrollSensitivity <= 0f)
            {
                branchScrollRect.scrollSensitivity = 30f;
            }

            if (titleScrollRect != null)
            {
                titleScrollRect.content = titleContentRoot;
                titleScrollRect.horizontal = false;
                titleScrollRect.vertical = false;
                titleScrollRect.horizontalScrollbar = null;
                titleScrollRect.verticalScrollbar = null;
                titleScrollRect.movementType = ScrollRect.MovementType.Clamped;
                titleScrollRect.scrollSensitivity = 0f;
            }

            if (horizontalScrollbar != null)
            {
                horizontalScrollbar.onValueChanged.RemoveListener(OnHorizontalScrollChanged);
                horizontalScrollbar.onValueChanged.AddListener(OnHorizontalScrollChanged);
            }

            branchScrollRect.onValueChanged.RemoveListener(OnBranchScrollChanged);
            branchScrollRect.onValueChanged.AddListener(OnBranchScrollChanged);
        }

        private static void ConfigureContentRootRect(RectTransform content)
        {
            if (content == null)
            {
                return;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
        }

        private void RefreshScrollOverflow()
        {
            if (branchScrollRect == null || branchScrollRect.viewport == null || branchContentRoot == null)
            {
                return;
            }

            bool hasVerticalOverflow = branchContentRoot.rect.height > branchScrollRect.viewport.rect.height + 1f;
            branchScrollRect.vertical = true;
            if (verticalScrollbar != null)
            {
                verticalScrollbar.gameObject.SetActive(true);
                verticalScrollbar.interactable = hasVerticalOverflow;
                if (!hasVerticalOverflow)
                {
                    verticalScrollbar.size = 1f;
                    verticalScrollbar.value = 1f;
                }
            }

            if (!hasVerticalOverflow)
            {
                branchScrollRect.verticalNormalizedPosition = 1f;
                SyncTitleScroll();
            }
        }

        private void RefreshHorizontalScrollbar()
        {
            if (horizontalScrollbar == null)
            {
                return;
            }

            bool hasHorizontalOverflow = false;
            float scrollbarSize = 1f;
            foreach (TechBranchView branchView in branchViews)
            {
                if (branchView == null || !branchView.HasHorizontalOverflow())
                {
                    continue;
                }

                hasHorizontalOverflow = true;
                scrollbarSize = Mathf.Min(scrollbarSize, branchView.GetHorizontalScrollbarSize());
            }

            horizontalScrollbar.gameObject.SetActive(true);
            horizontalScrollbar.interactable = hasHorizontalOverflow;
            suppressHorizontalScrollbarEvent = true;
            horizontalScrollbar.size = hasHorizontalOverflow ? scrollbarSize : 1f;
            horizontalScrollbar.value = 0f;
            suppressHorizontalScrollbarEvent = false;
            OnHorizontalScrollChanged(0f);
        }

        private void OnHorizontalScrollChanged(float value)
        {
            if (suppressHorizontalScrollbarEvent)
            {
                return;
            }

            foreach (TechBranchView branchView in branchViews)
            {
                branchView?.SetHorizontalNormalizedPosition(value);
            }
        }

        private void ResetScrollPosition()
        {
            if (branchScrollRect != null)
            {
                branchScrollRect.verticalNormalizedPosition = 1f;
            }

            SyncTitleScroll();
        }

        private void OnBranchScrollChanged(Vector2 _)
        {
            SyncTitleScroll();
        }

        private void SyncTitleScroll()
        {
            if (branchContentRoot == null || titleContentRoot == null)
            {
                return;
            }

            Vector2 position = titleContentRoot.anchoredPosition;
            position.y = branchContentRoot.anchoredPosition.y;
            titleContentRoot.anchoredPosition = position;
        }

        private void AlignHorizontalScrollbarToBranchViewport()
        {
            if (branchScrollRect == null || horizontalScrollbar == null)
            {
                return;
            }

            RectTransform branchRect = branchScrollRect.GetComponent<RectTransform>();
            RectTransform scrollbarRect = horizontalScrollbar.GetComponent<RectTransform>();
            if (branchRect == null || scrollbarRect == null)
            {
                return;
            }

            scrollbarRect.anchorMin = new Vector2(branchRect.anchorMin.x, scrollbarRect.anchorMin.y);
            scrollbarRect.anchorMax = new Vector2(branchRect.anchorMax.x, scrollbarRect.anchorMax.y);

            Vector2 offsetMin = scrollbarRect.offsetMin;
            Vector2 offsetMax = scrollbarRect.offsetMax;
            offsetMin.x = branchRect.offsetMin.x;
            offsetMax.x = branchRect.offsetMax.x;
            scrollbarRect.offsetMin = offsetMin;
            scrollbarRect.offsetMax = offsetMax;
        }

        private float GetTitleContentWidth()
        {
            if (titleScrollRect != null && titleScrollRect.viewport != null && titleScrollRect.viewport.rect.width > 0f)
            {
                return titleScrollRect.viewport.rect.width;
            }

            if (titleContentRoot != null && titleContentRoot.parent is RectTransform parentRect && parentRect.rect.width > 0f)
            {
                return parentRect.rect.width;
            }

            return 160f;
        }

        internal static string GetBranchName(TechBranch branch)
        {
            switch (branch)
            {
                case TechBranch.Building:
                    return LocalizationManager.Get("ui.tech.branch.building");
                case TechBranch.Farm:
                    return LocalizationManager.Get("ui.tech.branch.farm");
                case TechBranch.Production:
                    return LocalizationManager.Get("ui.tech.branch.production");
                case TechBranch.Resource:
                    return LocalizationManager.Get("ui.tech.branch.resource");
                case TechBranch.Special:
                    return LocalizationManager.Get("ui.tech.branch.special");
                default:
                    return LocalizationManager.Get("ui.tech.branch.other");
            }
        }
        internal static Color GetBranchBackgroundColor(TechBranch branch)
        {
            switch (branch)
            {
                case TechBranch.Building:
                    return new Color(0.86f, 0.92f, 1f, 0.45f);
                case TechBranch.Farm:
                    return new Color(0.86f, 1f, 0.78f, 0.45f);
                case TechBranch.Production:
                    return new Color(1f, 0.88f, 0.72f, 0.45f);
                case TechBranch.Resource:
                    return new Color(0.84f, 0.92f, 0.92f, 0.45f);
                default:
                    return new Color(0.96f, 0.86f, 0.92f, 0.45f);
            }
        }

        internal static Color GetBranchTextColor(TechBranch branch)
        {
            switch (branch)
            {
                case TechBranch.Building:
                    return new Color(0.16f, 0.30f, 0.54f);
                case TechBranch.Farm:
                    return new Color(0.18f, 0.42f, 0.12f);
                case TechBranch.Production:
                    return new Color(0.54f, 0.28f, 0.08f);
                case TechBranch.Resource:
                    return new Color(0.20f, 0.38f, 0.40f);
                default:
                    return new Color(0.48f, 0.22f, 0.34f);
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private static int CompareTechNodes(TechNodeConfig left, TechNodeConfig right)
        {
            int result = left.SortOrder.CompareTo(right.SortOrder);
            return result != 0 ? result : left.Id.CompareTo(right.Id);
        }

        private static TechBranch ToBranch(int value)
        {
            TechBranch branch = (TechBranch)value;
            return Enum.IsDefined(typeof(TechBranch), branch) ? branch : TechBranch.Special;
        }

        private static TechBranch[] GetBranchOrder()
        {
            return new[]
            {
                TechBranch.Building,
                TechBranch.Farm,
                TechBranch.Production,
                TechBranch.Resource,
                TechBranch.Special,
            };
        }
    }
}

