using UnityEngine;

namespace Game
{
    internal enum WorldFloatingPanelHorizontalAnchor
    {
        Left,
        Center,
        Right,
    }

    internal static class WorldFloatingPanelLayout
    {
        public static void AlignBottomToBottomBar(RectTransform panel, float offset = 0f)
        {
            if (panel == null)
            {
                return;
            }

            RectTransform parent = panel.parent as RectTransform;
            if (parent == null ||
                WorldMainPanel.Instance == null ||
                !WorldMainPanel.Instance.TryGetBottomBarTopInParent(parent, out float bottomBarTopY))
            {
                return;
            }

            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);

            Vector2 position = panel.anchoredPosition;
            position.x = 0f;
            position.y = bottomBarTopY - parent.rect.yMin + offset;
            panel.anchoredPosition = position;
        }

        public static void AlignBottomToHotBarGrid(
            RectTransform panel,
            WorldFloatingPanelHorizontalAnchor horizontalAnchor = WorldFloatingPanelHorizontalAnchor.Center,
            float xOffset = 0f,
            float yOffset = 0f)
        {
            if (!TryGetHotBarGridRect(panel, out RectTransform parent, out Rect targetRect))
            {
                AlignBottomToBottomBar(panel, yOffset);
                return;
            }

            AlignBottomToRect(panel, parent, targetRect, horizontalAnchor, xOffset, yOffset);
        }

        public static void AlignBottomToHotSlot(
            RectTransform panel,
            int slotNumber,
            WorldFloatingPanelHorizontalAnchor horizontalAnchor = WorldFloatingPanelHorizontalAnchor.Center,
            float xOffset = 0f,
            float yOffset = 0f)
        {
            if (!TryGetHotSlotRect(panel, slotNumber, out RectTransform parent, out Rect targetRect))
            {
                AlignBottomToHotBarGrid(panel, horizontalAnchor, xOffset, yOffset);
                return;
            }

            AlignBottomToRect(panel, parent, targetRect, horizontalAnchor, xOffset, yOffset);
        }

        private static bool TryGetHotBarGridRect(RectTransform panel, out RectTransform parent, out Rect targetRect)
        {
            parent = panel != null ? panel.parent as RectTransform : null;
            targetRect = default;
            return parent != null &&
                   WorldMainPanel.Instance != null &&
                   WorldMainPanel.Instance.TryGetHotBarGridRectInParent(parent, out targetRect);
        }

        private static bool TryGetHotSlotRect(RectTransform panel, int slotNumber, out RectTransform parent, out Rect targetRect)
        {
            parent = panel != null ? panel.parent as RectTransform : null;
            targetRect = default;
            return parent != null &&
                   WorldMainPanel.Instance != null &&
                   WorldMainPanel.Instance.TryGetHotSlotRectInParent(slotNumber, parent, out targetRect);
        }

        private static void AlignBottomToRect(
            RectTransform panel,
            RectTransform parent,
            Rect targetRect,
            WorldFloatingPanelHorizontalAnchor horizontalAnchor,
            float xOffset,
            float yOffset)
        {
            if (panel == null || parent == null)
            {
                return;
            }

            float pivotX = GetPivotX(horizontalAnchor);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(pivotX, 0f);

            Vector2 position = panel.anchoredPosition;
            position.x = GetAnchorX(targetRect, horizontalAnchor) + xOffset;
            position.y = targetRect.yMax - parent.rect.yMin + yOffset;
            panel.anchoredPosition = position;
        }

        private static float GetAnchorX(Rect rect, WorldFloatingPanelHorizontalAnchor horizontalAnchor)
        {
            switch (horizontalAnchor)
            {
                case WorldFloatingPanelHorizontalAnchor.Left:
                    return rect.xMin;
                case WorldFloatingPanelHorizontalAnchor.Right:
                    return rect.xMax;
                default:
                    return rect.center.x;
            }
        }

        private static float GetPivotX(WorldFloatingPanelHorizontalAnchor horizontalAnchor)
        {
            switch (horizontalAnchor)
            {
                case WorldFloatingPanelHorizontalAnchor.Left:
                    return 0f;
                case WorldFloatingPanelHorizontalAnchor.Right:
                    return 1f;
                default:
                    return 0.5f;
            }
        }

        public static void StretchToParent(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
