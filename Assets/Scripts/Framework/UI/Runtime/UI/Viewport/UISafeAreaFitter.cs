using UnityEngine;

namespace UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class UISafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        private UIViewportInfo lastViewport;
        private UIViewportService subscribedViewport;
        private bool hasApplied;

        private void OnEnable()
        {
            EnsureSubscription();
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            EnsureSubscription();

            if (!Application.isPlaying || subscribedViewport == null)
            {
                ApplyIfChanged(UIViewportInfo.Capture());
            }
        }

        public void Apply()
        {
            UIViewportService viewport = UIManager.Current != null
                ? UIManager.Current.Viewport
                : null;
            ApplyIfChanged(viewport != null ? viewport.Current : UIViewportInfo.Capture(), true);
        }

        private void EnsureSubscription()
        {
            if (!Application.isPlaying || subscribedViewport != null || UIManager.Current == null)
            {
                return;
            }

            UIViewportService viewport = UIManager.Current.Viewport;
            if (viewport == null)
            {
                return;
            }

            subscribedViewport = viewport;
            subscribedViewport.Changed += OnViewportChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedViewport == null)
            {
                return;
            }

            subscribedViewport.Changed -= OnViewportChanged;
            subscribedViewport = null;
        }

        private void OnViewportChanged(UIViewportInfo viewport)
        {
            ApplyIfChanged(viewport);
        }

        private void ApplyIfChanged(UIViewportInfo viewport, bool force = false)
        {
            if (!force && hasApplied && viewport == lastViewport)
            {
                return;
            }

            if (target == null)
            {
                target = transform as RectTransform;
            }

            if (target == null || !viewport.IsValid)
            {
                return;
            }

            Rect safeArea = viewport.SafeAreaNormalized;
            target.anchorMin = safeArea.min;
            target.anchorMax = safeArea.max;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            lastViewport = viewport;
            hasApplied = true;
        }
    }
}
