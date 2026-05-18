using TMPro;
using UnityEngine;
using UI;

namespace Game
{
    public sealed class BattleToast : UIToast
    {
        [SerializeField]
        private TMP_Text messageText;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private float duration = 1.5f;

        [SerializeField]
        private float moveUpDistance = 30f;

        [SerializeField]
        private float fadeInTime = 0.12f;

        [SerializeField]
        private float fadeOutTime = 0.35f;

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

            string message = args as string;

            if (string.IsNullOrEmpty(message))
            {
                message = string.Empty;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            if (contentRoot != null)
            {
                startPosition = contentRoot.anchoredPosition;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            openTime = Time.unscaledTime;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - openTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            if (contentRoot != null)
            {
                float moveProgress = EaseOutQuad(progress);
                contentRoot.anchoredPosition = startPosition + Vector2.up * moveUpDistance * moveProgress;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = GetAlpha(elapsed);
            }
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

        private float EaseOutQuad(float value)
        {
            return 1f - (1f - value) * (1f - value);
        }
    }
}