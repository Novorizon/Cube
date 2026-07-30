using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class TooltipView : UIView
    {
        public const string DefaultPrefabPath = "Assets/Arts/UI/Panels/Common/Tooltip.prefab";

        [SerializeField]
        private RectTransform root;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private GameObject iconContainer;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private TMP_Text valuesText;

        [SerializeField]
        private TMP_Text footerText;

        private RectTransform anchor;
        private TooltipPlacement placement;
        private Vector2 offset;
        private float screenPadding;
        private bool visible;

        protected override void OnCreate()
        {
            root = root != null ? root : transform as RectTransform;
            Hide();
        }

        protected override void OnOpen(object args)
        {
            Hide();
        }

        protected override void OnClose()
        {
            Hide();
        }

        public void Show(
            TooltipData data,
            RectTransform sourceAnchor,
            TooltipPlacement sourcePlacement,
            Vector2 sourceOffset,
            float sourceScreenPadding)
        {
            if (data == null || data.IsEmpty || sourceAnchor == null)
            {
                Hide();
                return;
            }

            anchor = sourceAnchor;
            placement = sourcePlacement;
            offset = sourceOffset;
            screenPadding = Mathf.Max(0f, sourceScreenPadding);

            ApplyData(data);
            visible = true;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            Canvas.ForceUpdateCanvases();
            if (root != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            }

            Reposition();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        public void Hide()
        {
            visible = false;
            anchor = null;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            if (anchor == null)
            {
                Hide();
                return;
            }

            Reposition();
        }

        private void ApplyData(TooltipData data)
        {
            SetText(titleText, data.Title);
            SetText(descriptionText, data.Description);
            SetText(valuesText, FormatValues(data.Values));
            SetText(footerText, data.Footer);

            if (icon != null)
            {
                icon.sprite = data.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            if (iconContainer != null)
            {
                iconContainer.SetActive(data.Icon != null);
            }
        }

        private void Reposition()
        {
            if (root == null || anchor == null || !(root.parent is RectTransform parent))
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);

            Vector2 anchorMin = parent.InverseTransformPoint(corners[0]);
            Vector2 anchorMax = anchorMin;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = parent.InverseTransformPoint(corners[i]);
                anchorMin = Vector2.Min(anchorMin, point);
                anchorMax = Vector2.Max(anchorMax, point);
            }

            float width = Mathf.Max(0f, root.rect.width);
            float height = Mathf.Max(0f, root.rect.height);
            Rect bounds = parent.rect;
            TooltipPlacement resolvedPlacement = ResolvePlacement(
                placement,
                anchorMin,
                anchorMax,
                width,
                height,
                bounds);

            Vector2 topLeft = CalculateTopLeft(
                resolvedPlacement,
                anchorMin,
                anchorMax,
                width,
                height);

            float minX = bounds.xMin + screenPadding;
            float maxX = bounds.xMax - screenPadding - width;
            float minTop = bounds.yMin + screenPadding + height;
            float maxTop = bounds.yMax - screenPadding;

            topLeft.x = maxX >= minX ? Mathf.Clamp(topLeft.x, minX, maxX) : minX;
            topLeft.y = maxTop >= minTop ? Mathf.Clamp(topLeft.y, minTop, maxTop) : maxTop;

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(
                topLeft.x - bounds.xMin,
                topLeft.y - bounds.yMax);
        }

        private TooltipPlacement ResolvePlacement(
            TooltipPlacement requested,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float width,
            float height,
            Rect bounds)
        {
            if (requested != TooltipPlacement.Auto)
            {
                return requested;
            }

            if (anchorMax.x + offset.x + width <= bounds.xMax - screenPadding)
            {
                return TooltipPlacement.Right;
            }

            if (anchorMin.x - offset.x - width >= bounds.xMin + screenPadding)
            {
                return TooltipPlacement.Left;
            }

            if (anchorMax.y + offset.y + height <= bounds.yMax - screenPadding)
            {
                return TooltipPlacement.Above;
            }

            return TooltipPlacement.Below;
        }

        private Vector2 CalculateTopLeft(
            TooltipPlacement resolvedPlacement,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float width,
            float height)
        {
            float centerX = (anchorMin.x + anchorMax.x) * 0.5f;
            switch (resolvedPlacement)
            {
                case TooltipPlacement.Left:
                    return new Vector2(anchorMin.x - offset.x - width, anchorMax.y);

                case TooltipPlacement.Above:
                    return new Vector2(centerX - width * 0.5f, anchorMax.y + offset.y + height);

                case TooltipPlacement.Below:
                    return new Vector2(centerX - width * 0.5f, anchorMin.y - offset.y);

                default:
                    return new Vector2(anchorMax.x + offset.x, anchorMax.y);
            }
        }

        private static string FormatValues(IReadOnlyList<TooltipValue> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                TooltipValue value = values[i];
                if (i > 0)
                {
                    builder.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(value.Label))
                {
                    builder.Append(value.Label);
                    builder.Append(": ");
                }

                builder.Append("<color=#");
                builder.Append(ColorUtility.ToHtmlStringRGBA(value.Color));
                builder.Append('>');
                builder.Append(value.Text);
                builder.Append("</color>");
            }

            return builder.ToString();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            bool hasText = !string.IsNullOrWhiteSpace(value);
            target.text = hasText ? value : string.Empty;
            target.raycastTarget = false;
            target.gameObject.SetActive(hasText);
        }
    }
}
