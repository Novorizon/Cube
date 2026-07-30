using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class StoryMotionPlayer : MonoBehaviour
    {
        [SerializeField] private RawImage view;

        private Coroutine motionCoroutine;

        public void Stop()
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
                motionCoroutine = null;
            }
        }

        public void Play(StoryMotionPreset preset, float duration, Action completed)
        {
            Stop();

            if (view == null || preset == StoryMotionPreset.None || duration <= 0f)
            {
                ResetView();
                completed?.Invoke();
                return;
            }

            GetMotion(preset, out Rect fromUvRect, out Rect toUvRect);
            view.uvRect = fromUvRect;
            motionCoroutine = StartCoroutine(PlayRoutine(fromUvRect, toUvRect, duration, completed));
        }

        public void ResetView()
        {
            if (view == null)
            {
                return;
            }

            view.uvRect = FullView();
        }

        private IEnumerator PlayRoutine(
            Rect fromUvRect,
            Rect toUvRect,
            float duration,
            Action completed)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                view.uvRect = LerpRect(fromUvRect, toUvRect, t);
                yield return null;
            }

            view.uvRect = toUvRect;
            motionCoroutine = null;
            completed?.Invoke();
        }

        private static void GetMotion(
            StoryMotionPreset preset,
            out Rect fromUvRect,
            out Rect toUvRect)
        {
            fromUvRect = FullView();
            toUvRect = FullView();

            switch (preset)
            {
                case StoryMotionPreset.ZoomOut:
                    fromUvRect = CenteredView(1f / GameConfig.Story.ZoomOutStartScale);
                    break;
                case StoryMotionPreset.ZoomIn:
                    toUvRect = CenteredView(1f / GameConfig.Story.ZoomInEndScale);
                    break;
                case StoryMotionPreset.PanLeftToRight:
                    fromUvRect = CreatePanView(0f);
                    toUvRect = CreatePanView(1f - GameConfig.Story.PanViewScale);
                    break;
                case StoryMotionPreset.PanRightToLeft:
                    fromUvRect = CreatePanView(1f - GameConfig.Story.PanViewScale);
                    toUvRect = CreatePanView(0f);
                    break;
            }
        }

        private static Rect FullView()
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        private static Rect CenteredView(float visibleFraction)
        {
            float size = Mathf.Clamp01(visibleFraction);
            float offset = (1f - size) * 0.5f;
            return new Rect(offset, offset, size, size);
        }

        private static Rect CreatePanView(float x)
        {
            float size = Mathf.Clamp01(GameConfig.Story.PanViewScale);
            return new Rect(x, (1f - size) * 0.5f, size, size);
        }

        private static Rect LerpRect(Rect from, Rect to, float t)
        {
            return new Rect(
                Mathf.LerpUnclamped(from.x, to.x, t),
                Mathf.LerpUnclamped(from.y, to.y, t),
                Mathf.LerpUnclamped(from.width, to.width, t),
                Mathf.LerpUnclamped(from.height, to.height, t));
        }
    }
}
