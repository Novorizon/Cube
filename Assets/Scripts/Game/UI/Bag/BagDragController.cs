using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Coordinates one drag operation shared by the BottomBar hot slots and bag slots.
    /// The controller is owned by WorldBottomBarPanel, so a drag cannot leak into other panels.
    /// </summary>
    internal sealed class BagDragController
    {
        private BagSlotView source;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform dragRect;

        public bool IsDragging => source != null;

        public void Begin(BagSlotView slot, PointerEventData eventData)
        {
            if (slot == null || !slot.CanDrag)
            {
                Cancel();
                return;
            }

            Cancel();
            source = slot;
            source.SetDragging(true);
            CreateDragVisual(slot);
            Move(eventData);
        }

        public void Move(PointerEventData eventData)
        {
            if (source == null || dragRect == null || canvasRect == null || eventData == null)
            {
                return;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? eventData.pressEventCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    eventData.position,
                    eventCamera,
                    out Vector2 localPosition))
            {
                dragRect.anchoredPosition = localPosition;
            }
        }

        public bool Drop(BagSlotView target, Action<int, int> dropped)
        {
            if (source == null || target == null)
            {
                Cancel();
                return false;
            }

            int fromSlotIndex = source.SlotIndex;
            int toSlotIndex = target.SlotIndex;
            bool moved = fromSlotIndex != toSlotIndex;
            if (moved)
            {
                dropped?.Invoke(fromSlotIndex, toSlotIndex);
            }

            Cancel();
            return moved;
        }

        public void End(BagSlotView slot)
        {
            if (slot == null || source == slot)
            {
                Cancel();
            }
        }

        public void Cancel(BagSlotView slot = null)
        {
            if (slot != null && source != slot)
            {
                return;
            }

            source?.SetDragging(false);
            source = null;
            if (dragRect != null)
            {
                dragRect.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(dragRect.gameObject);
            }

            dragRect = null;
            canvasRect = null;
            canvas = null;
        }

        private void CreateDragVisual(BagSlotView slot)
        {
            RectTransform sourceRect = slot.RootRect;
            if (sourceRect == null)
            {
                return;
            }

            canvas = sourceRect.GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                return;
            }

            GameObject dragObject = new GameObject(
                "BagDragIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            dragRect = dragObject.GetComponent<RectTransform>();
            dragRect.SetParent(canvasRect, false);
            dragRect.SetAsLastSibling();
            dragRect.anchorMin = new Vector2(0.5f, 0.5f);
            dragRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragRect.pivot = new Vector2(0.5f, 0.5f);

            Vector2 sourceSize = sourceRect.rect.size;
            float width = sourceSize.x > 0f ? sourceSize.x : 64f;
            float height = sourceSize.y > 0f ? sourceSize.y : 64f;
            dragRect.sizeDelta = new Vector2(
                Mathf.Clamp(width, 48f, 96f),
                Mathf.Clamp(height, 48f, 96f));

            Image image = dragObject.GetComponent<Image>();
            image.sprite = slot.IconSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0.9f);

            CanvasGroup canvasGroup = dragObject.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (slot.ItemCount > 1)
            {
                CreateCountText(slot.ItemCount);
            }
        }

        private void CreateCountText(int count)
        {
            GameObject countObject = new GameObject(
                "Count",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform countRect = countObject.GetComponent<RectTransform>();
            countRect.SetParent(dragRect, false);
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-4f, 4f);
            countRect.sizeDelta = new Vector2(48f, 24f);

            TextMeshProUGUI countText = countObject.GetComponent<TextMeshProUGUI>();
            countText.text = count.ToString();
            countText.fontSize = 18f;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.color = Color.white;
            countText.outlineColor = new Color32(0, 0, 0, 220);
            countText.outlineWidth = 0.18f;
            countText.raycastTarget = false;
        }
    }
}
