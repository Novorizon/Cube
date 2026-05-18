using TMPro;
using UnityEngine;

namespace UI.Sample
{
    public sealed class SimpleToast : UIToast
    {
        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private RectTransform contentRoot;

        [Header("Time")]
        [SerializeField]
        private float duration = 1.5f;

        [SerializeField]
        private float fadeInTime = 0.12f;

        [SerializeField]
        private float fadeOutTime = 0.35f;

        [Header("Move")]
        [SerializeField]
        private float moveUpDistance = 30f;

        [Header("Text Colors")]
        [SerializeField]
        private Color infoColor = Color.white;

        [SerializeField]
        private Color warningColor = new Color(1f, 0.75f, 0.2f);

        [SerializeField]
        private Color errorColor = new Color(1f, 0.25f, 0.25f);

        private Vector2 startPosition;
        private float openTime;

        public override float Duration
        {
            get
            {
                return duration;
            }
        }

        protected override void OnOpen(object args)
        {
            base.OnOpen(args);

            ApplyArgs(args);

            if (contentRoot != null)
            {
                startPosition = contentRoot.anchoredPosition;
                contentRoot.anchoredPosition = startPosition;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            openTime = Time.unscaledTime;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - openTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            UpdateMove(progress);
            //UpdateAlpha(elapsed);
        }

        private void ApplyArgs(object args)
        {
            if (messageText == null)
            {
                return;
            }

            if (args is ToastArgs toastArgs)
            {
                messageText.text = toastArgs.Message;
                messageText.color = GetColor(toastArgs.Level);
                return;
            }

            string message = args as string;

            if (string.IsNullOrEmpty(message))
            {
                message = string.Empty;
            }

            messageText.text = message;
            messageText.color = infoColor;
        }

        private void UpdateMove(float progress)
        {
            if (contentRoot == null)
            {
                return;
            }

            float moveProgress = EaseOutQuad(progress);
            contentRoot.anchoredPosition = startPosition + Vector2.up * moveUpDistance * moveProgress;
        }

        private void UpdateAlpha(float elapsed)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = GetAlpha(elapsed);
        }

        private float GetAlpha(float elapsed)
        {
            if (elapsed < fadeInTime)
            {
                return Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInTime));
            }

            if (elapsed > duration - fadeOutTime)
            {
                return Mathf.Clamp01((duration - elapsed) / Mathf.Max(0.01f, fadeOutTime));
            }

            return 1f;
        }

        private Color GetColor(ToastLevel level)
        {
            switch (level)
            {
                case ToastLevel.Warning:
                    return warningColor;

                case ToastLevel.Error:
                    return errorColor;

                case ToastLevel.Info:
                default:
                    return infoColor;
            }
        }

        private float EaseOutQuad(float value)
        {
            return 1f - (1f - value) * (1f - value);
        }
    }
}