using TMPro;
using UnityEngine;

namespace Game
{
    public sealed class GuideOverlay : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform topBlocker;
        [SerializeField] private RectTransform bottomBlocker;
        [SerializeField] private RectTransform leftBlocker;
        [SerializeField] private RectTransform rightBlocker;
        [SerializeField] private RectTransform centerBlocker;
        [SerializeField] private RectTransform focusTop;
        [SerializeField] private RectTransform focusBottom;
        [SerializeField] private RectTransform focusLeft;
        [SerializeField] private RectTransform focusRight;
        [SerializeField] private RectTransform hintPanel;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private float focusPadding = 12f;

        private GuideTarget target;
        private bool allowInteraction;

        public void Show(GuideTarget guideTarget, bool canInteract, string text)
        {
            target = guideTarget;
            allowInteraction = canInteract;
            if (hintText != null)
            {
                hintText.text = text ?? string.Empty;
            }

            gameObject.SetActive(true);
            RefreshLayout();
        }

        public void Hide()
        {
            target = null;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (!gameObject.activeInHierarchy || root == null)
            {
                return;
            }

            Rect bounds = root.rect;
            if (!TryGetFocusRect(bounds, out Rect focus))
            {
                SetLocalRect(topBlocker, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
                SetLocalRect(bottomBlocker, 0f, 0f, 0f, 0f);
                SetLocalRect(leftBlocker, 0f, 0f, 0f, 0f);
                SetLocalRect(rightBlocker, 0f, 0f, 0f, 0f);
                SetLocalRect(centerBlocker, 0f, 0f, 0f, 0f);
                SetFocusVisible(false);
                PositionHint(bounds.center.x, bounds.center.y, bounds);
                return;
            }

            SetLocalRect(topBlocker, bounds.xMin, focus.yMax, bounds.xMax, bounds.yMax);
            SetLocalRect(bottomBlocker, bounds.xMin, bounds.yMin, bounds.xMax, focus.yMin);
            SetLocalRect(leftBlocker, bounds.xMin, focus.yMin, focus.xMin, focus.yMax);
            SetLocalRect(rightBlocker, focus.xMax, focus.yMin, bounds.xMax, focus.yMax);
            SetLocalRect(centerBlocker, focus.xMin, focus.yMin, focus.xMax, focus.yMax);
            if (centerBlocker != null)
            {
                centerBlocker.gameObject.SetActive(!allowInteraction);
            }

            const float border = 4f;
            SetLocalRect(focusTop, focus.xMin, focus.yMax - border, focus.xMax, focus.yMax);
            SetLocalRect(focusBottom, focus.xMin, focus.yMin, focus.xMax, focus.yMin + border);
            SetLocalRect(focusLeft, focus.xMin, focus.yMin, focus.xMin + border, focus.yMax);
            SetLocalRect(focusRight, focus.xMax - border, focus.yMin, focus.xMax, focus.yMax);
            SetFocusVisible(true);

            float hintY = focus.yMax + 74f;
            if (hintPanel != null && hintY + hintPanel.rect.height * 0.5f > bounds.yMax)
            {
                hintY = focus.yMin - 74f;
            }

            PositionHint(focus.center.x, hintY, bounds);
        }

        private bool TryGetFocusRect(Rect bounds, out Rect focus)
        {
            focus = default;
            RectTransform targetRect = target != null ? target.RectTransform : null;
            if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
            {
                return false;
            }

            Canvas targetCanvas = targetRect.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;
            Canvas rootCanvas = root.GetComponentInParent<Canvas>();
            Camera rootCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, rootCamera, out Vector2 localPoint))
                {
                    return false;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            float left = Mathf.Clamp(min.x - focusPadding, bounds.xMin, bounds.xMax);
            float right = Mathf.Clamp(max.x + focusPadding, bounds.xMin, bounds.xMax);
            float bottom = Mathf.Clamp(min.y - focusPadding, bounds.yMin, bounds.yMax);
            float top = Mathf.Clamp(max.y + focusPadding, bounds.yMin, bounds.yMax);
            if (right <= left || top <= bottom)
            {
                return false;
            }

            focus = Rect.MinMaxRect(left, bottom, right, top);
            return true;
        }

        private void PositionHint(float x, float y, Rect bounds)
        {
            if (hintPanel == null)
            {
                return;
            }

            Vector2 size = hintPanel.rect.size;
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            hintPanel.anchoredPosition = new Vector2(
                Mathf.Clamp(x, bounds.xMin + halfWidth + 16f, bounds.xMax - halfWidth - 16f),
                Mathf.Clamp(y, bounds.yMin + halfHeight + 16f, bounds.yMax - halfHeight - 16f));
        }

        private void SetFocusVisible(bool visible)
        {
            SetActive(focusTop, visible);
            SetActive(focusBottom, visible);
            SetActive(focusLeft, visible);
            SetActive(focusRight, visible);
            if (centerBlocker != null && !visible)
            {
                centerBlocker.gameObject.SetActive(false);
            }
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null && rect.gameObject.activeSelf != active)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private static void SetLocalRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
        }
    }
}
