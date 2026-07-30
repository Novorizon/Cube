using System;
using System.Collections.Generic;
using Game.Framework;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldToolKitPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/ToolKit/ToolKitPanel.prefab";
        private const string ToolSlotPrefabPath = "Assets/Arts/UI/Panels/ToolKit/ToolSlot.prefab";

        private readonly List<WorldToolSlotView> slotViews = new List<WorldToolSlotView>();
        private Transform root;
        private Transform content;
        private GameObject toolSlotPrefab;
        private Image dragIcon;
        private Canvas dragCanvas;
        private RectTransform dragCanvasRect;
        private int draggingSlotIndex = -1;
        private float nextRefreshTime;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticLayout();
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            WorldFloatingPanelLayout.AlignBottomToHotBarGrid(GetComponent<RectTransform>());
            RefreshNow();
        }

        protected override void OnClose()
        {
            ClearDropHighlights();
            SetTooltipsSuppressed(false);
            DestroyDragIcon();
            draggingSlotIndex = -1;
        }

        protected override void OnDestroyed()
        {
            ClearSlots();
            DestroyDragIcon();
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            Refresh();
        }

        private void BindStaticLayout()
        {
            ClearSlots();
            DestroyDragIcon();

            root = transform;
            content = FindChild(root, "Content");

            WorldPanelBindingUtility.BindButton(root != null ? root.Find("Close") : null, CloseSelf, "ToolKit close");
            WorldPanelBindingUtility.BindButton(root != null ? root.Find("Upgrade") : null, UpgradeFromButton, "ToolKit upgrade");

            if (content == null && root != null)
            {
                Debug.LogError($"[WorldToolKitPanel] Missing Content node on {WorldPanelBindingUtility.GetTransformPath(root)}.");
            }
        }

        private void RefreshNow()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (root == null || !root.gameObject.activeSelf)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;
            RebuildSlotsIfNeeded();
            IReadOnlyList<int> slots = ToolKitManager.Instance.Slots;
            for (int i = 0; i < slotViews.Count; i++)
            {
                int itemId = i < slots.Count ? slots[i] : 0;
                bool selected = itemId > 0 && itemId == ToolKitManager.Instance.CurrentToolItemId;
                slotViews[i].Bind(
                    i,
                    itemId,
                    selected,
                    SelectSlot,
                    BeginDrag,
                    Drag,
                    EndDrag,
                    DropOnSlot,
                    HoverSlot);
            }
        }

        private void RebuildSlotsIfNeeded()
        {
            int capacity = ToolKitManager.Instance.Capacity;
            if (slotViews.Count == capacity)
            {
                return;
            }

            ClearSlots();
            if (content == null)
            {
                return;
            }

            GameObject prefab = GetToolSlotPrefab();
            if (prefab == null)
            {
                return;
            }

            for (int i = 0; i < capacity; i++)
            {
                GameObject slotObject = UnityEngine.Object.Instantiate(prefab, content, false);
                slotObject.name = $"ToolSlot_{i + 1:00}";
                slotObject.SetActive(true);

                WorldToolSlotView slotView = slotObject.GetComponent<WorldToolSlotView>();
                if (slotView == null)
                {
                    slotView = slotObject.AddComponent<WorldToolSlotView>();
                }

                slotViews.Add(slotView);
            }
        }

        private void SelectSlot(int slotIndex)
        {
            IReadOnlyList<int> slots = ToolKitManager.Instance.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return;
            }

            int itemId = slots[slotIndex];
            if (itemId <= 0 || !ToolKitManager.Instance.TrySelectToolItem(itemId))
            {
                return;
            }

            Refresh();
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void BeginDrag(int slotIndex, PointerEventData eventData)
        {
            IReadOnlyList<int> slots = ToolKitManager.Instance.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] <= 0)
            {
                return;
            }

            draggingSlotIndex = slotIndex;
            SetTooltipsSuppressed(true);
            CreateDragIcon(slotViews[slotIndex]);
            UpdateDragIcon(eventData);
        }

        private void Drag(PointerEventData eventData)
        {
            UpdateDragIcon(eventData);
        }

        private void EndDrag(PointerEventData eventData)
        {
            ClearDropHighlights();
            DestroyDragIcon();
            draggingSlotIndex = -1;
            SetTooltipsSuppressed(false);
        }

        private void DropOnSlot(int targetSlotIndex, PointerEventData eventData)
        {
            if (draggingSlotIndex < 0)
            {
                return;
            }

            if (ToolKitManager.Instance.TryMoveOrSwapSlot(draggingSlotIndex, targetSlotIndex))
            {
                Refresh();
                WorldMainPanel.Instance?.RefreshNow();
            }
        }

        private void HoverSlot(int slotIndex, bool hovering)
        {
            if (draggingSlotIndex < 0 || slotIndex < 0 || slotIndex >= slotViews.Count)
            {
                return;
            }

            slotViews[slotIndex].SetDropHighlighted(hovering && slotIndex != draggingSlotIndex);
        }

        private void UpgradeFromButton()
        {
            if (!ToolKitManager.Instance.Upgrade())
            {
                return;
            }

            Refresh();
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void CloseSelf()
        {
            if (CanCloseBy(UICloseReason.CloseButton))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private void ClearSlots()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                if (slotViews[i] != null)
                {
                    UnityEngine.Object.Destroy(slotViews[i].gameObject);
                }
            }

            slotViews.Clear();
        }

        private void ClearDropHighlights()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                slotViews[i]?.SetDropHighlighted(false);
            }
        }

        private void SetTooltipsSuppressed(bool value)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                slotViews[i]?.SetTooltipSuppressed(value);
            }
        }

        private GameObject GetToolSlotPrefab()
        {
            if (toolSlotPrefab != null)
            {
                return toolSlotPrefab;
            }

            toolSlotPrefab = ResourceManager.Instance.LoadGameObject(ToolSlotPrefabPath);
            if (toolSlotPrefab == null)
            {
                Debug.LogError($"[WorldToolKitPanel] Missing tool slot prefab: {ToolSlotPrefabPath}");
            }

            return toolSlotPrefab;
        }

        private void CreateDragIcon(WorldToolSlotView sourceSlot)
        {
            DestroyDragIcon();
            if (sourceSlot == null || !sourceSlot.HasTool)
            {
                return;
            }

            EnsureDragCanvas();
            if (dragCanvasRect == null)
            {
                return;
            }

            GameObject dragObject = new GameObject("ToolKitDragIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = dragObject.GetComponent<RectTransform>();
            rect.SetParent(dragCanvasRect, false);
            rect.sizeDelta = new Vector2(96f, 96f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            dragIcon = dragObject.GetComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.preserveAspect = true;

            if (sourceSlot.IconSprite != null)
            {
                dragIcon.sprite = sourceSlot.IconSprite;
                dragIcon.color = new Color(1f, 1f, 1f, 0.88f);
            }
        }

        private void UpdateDragIcon(PointerEventData eventData)
        {
            if (dragIcon == null || dragCanvasRect == null || eventData == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragCanvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition);
            ((RectTransform)dragIcon.transform).anchoredPosition = localPosition;
        }

        private void DestroyDragIcon()
        {
            if (dragIcon == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(dragIcon.gameObject);
            dragIcon = null;
        }

        private void EnsureDragCanvas()
        {
            if (dragCanvasRect != null)
            {
                return;
            }

            dragCanvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            dragCanvasRect = dragCanvas != null ? dragCanvas.transform as RectTransform : null;
        }

        private static Transform FindChild(Transform root, string childName)
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
                Transform found = FindChild(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
