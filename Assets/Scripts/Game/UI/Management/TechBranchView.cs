using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechBranchView : MonoBehaviour
    {
        private const string NodeSlotPrefabPath = "Assets/Arts/UI/Panels/TechSlot.prefab";
        private const float RightPadding = 60f;
        private const float SlotWidth = 138f;
        private const float SlotHeight = 116f;
        private const float SlotSpacing = 44f;
        private const float ConnectorWidthOffset = 10f;

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image background;
        [SerializeField] private RectTransform contentRoot;

        private RectTransform rectTransform;
        private GameObject nodeSlotPrefab;

        public static float CalculateRowWidth(int nodeCount)
        {
            if (nodeCount <= 0)
            {
                return SlotWidth + RightPadding;
            }

            return CalculateNodeContentWidth(nodeCount);
        }

        public void Bind(TechBranch branch, IReadOnlyList<TechNodeConfig> nodes, Action<int> nodeClicked)
        {
            BindStaticLayout();
            if (contentRoot == null)
            {
                Debug.LogError($"[TechBranchView] Invalid prefab layout. Missing Scroll View/Viewport/Content: {name}");
                return;
            }

            RefreshBranchVisual(branch);
            ClearContent();

            int count = nodes != null ? nodes.Count : 0;
            contentRoot.sizeDelta = new Vector2(CalculateNodeContentWidth(count), SlotHeight);

            for (int i = 0; i < count; i++)
            {
                float x = SlotWidth * 0.5f + i * (SlotWidth + SlotSpacing);
                if (i > 0)
                {
                    CreateConnector(x - SlotSpacing, 0f, SlotSpacing - ConnectorWidthOffset);
                }

                CreateNode(nodes[i], new Vector2(x, -4f), nodeClicked);
            }
            SetHorizontalNormalizedPosition(0f);
        }

        private void BindStaticLayout()
        {
            rectTransform = GetComponent<RectTransform>();
            if (contentRoot != null)
            {
                contentRoot.anchorMin = new Vector2(0f, 0.5f);
                contentRoot.anchorMax = new Vector2(0f, 0.5f);
                contentRoot.pivot = new Vector2(0f, 0.5f);
                contentRoot.anchoredPosition = Vector2.zero;
                return;
            }

            Debug.LogError($"[TechBranchView] Missing contentRoot reference on prefab: {name}");
        }

        public void SetTitleVisible(bool visible)
        {
            if (titleText != null)
            {
                titleText.gameObject.SetActive(visible);
            }
        }

        public void SetHorizontalNormalizedPosition(float value)
        {
            if (contentRoot == null)
            {
                return;
            }

            float overflow = Mathf.Max(0f, GetContentWidth() - GetViewportWidth());
            contentRoot.anchoredPosition = new Vector2(-overflow * Mathf.Clamp01(value), contentRoot.anchoredPosition.y);
        }

        public bool HasHorizontalOverflow()
        {
            return GetContentWidth() > GetViewportWidth() + 1f;
        }

        public float GetHorizontalScrollbarSize()
        {
            float contentWidth = GetContentWidth();
            if (contentWidth <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(GetViewportWidth() / contentWidth);
        }

        private void RefreshBranchVisual(TechBranch branch)
        {
            if (titleText != null)
            {
                titleText.text = TechTreePanel.GetBranchName(branch);
                titleText.color = TechTreePanel.GetBranchTextColor(branch);
            }

            if (background != null)
            {
                background.color = TechTreePanel.GetBranchBackgroundColor(branch);
                background.raycastTarget = false;
            }
        }

        private void CreateNode(TechNodeConfig config, Vector2 anchoredPosition, Action<int> nodeClicked)
        {
            GameObject prefab = GetNodeSlotPrefab();
            if (prefab == null || contentRoot == null || config == null)
            {
                return;
            }

            GameObject node = Instantiate(prefab, contentRoot, false);
            node.name = $"Tech_{config.Id}";

            RectTransform nodeRect = node.GetComponent<RectTransform>();
            if (nodeRect != null)
            {
                nodeRect.anchorMin = new Vector2(0f, 0.5f);
                nodeRect.anchorMax = new Vector2(0f, 0.5f);
                nodeRect.pivot = new Vector2(0.5f, 0.5f);
                nodeRect.anchoredPosition = anchoredPosition;
                nodeRect.sizeDelta = new Vector2(SlotWidth, SlotHeight);
            }

            TechSlotView slotView = node.GetComponent<TechSlotView>();
            if (slotView == null)
            {
                Debug.LogError($"[TechBranchView] Missing TechSlotView on prefab: {NodeSlotPrefabPath}");
                return;
            }

            slotView.Bind(config, nodeClicked);
        }

        private void CreateConnector(float x, float y, float width)
        {
            GameObject connector = new GameObject("Connector");
            connector.transform.SetParent(contentRoot, false);
            RectTransform connectorRect = connector.AddComponent<RectTransform>();
            connectorRect.anchorMin = new Vector2(0f, 0.5f);
            connectorRect.anchorMax = new Vector2(0f, 0.5f);
            connectorRect.pivot = new Vector2(0f, 0.5f);
            connectorRect.anchoredPosition = new Vector2(x, y);
            connectorRect.sizeDelta = new Vector2(width, 4f);
            connector.AddComponent<CanvasRenderer>();
            Image image = connector.AddComponent<Image>();
            image.color = new Color(0.65f, 0.48f, 0.26f, 0.72f);
            image.raycastTarget = false;
        }

        private GameObject GetNodeSlotPrefab()
        {
            if (nodeSlotPrefab != null)
            {
                return nodeSlotPrefab;
            }

            nodeSlotPrefab = ResourceManager.Instance.LoadGameObject(NodeSlotPrefabPath);
            if (nodeSlotPrefab == null)
            {
                Debug.LogError($"[TechBranchView] Missing tech node slot prefab: {NodeSlotPrefabPath}");
            }

            return nodeSlotPrefab;
        }

        private void ClearContent()
        {
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }
        }

        public static float CalculateNodeContentWidth(int nodeCount)
        {
            if (nodeCount <= 0)
            {
                return SlotWidth + RightPadding;
            }

            return nodeCount * SlotWidth + Mathf.Max(0, nodeCount - 1) * SlotSpacing + RightPadding;
        }

        private float GetViewportWidth()
        {
            if (rectTransform != null && rectTransform.rect.width > 0f)
            {
                return rectTransform.rect.width;
            }

            return contentRoot != null && contentRoot.parent is RectTransform parentRect
                ? parentRect.rect.width
                : 0f;
        }

        private float GetContentWidth()
        {
            return contentRoot != null ? contentRoot.rect.width : 0f;
        }
    }
}
