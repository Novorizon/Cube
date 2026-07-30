using System;
using System.Threading.Tasks;
using UnityEngine;

namespace UI
{
    public sealed class TooltipManager
    {
        private const float DefaultInitialDelay = 0.55f;
        private const float DefaultReshowDelay = 0.08f;
        private const float DefaultWarmDuration = 0.75f;
        private static readonly Vector2 DefaultOffset = new Vector2(16f, 8f);
        private const float DefaultScreenPadding = 12f;

        private readonly UIInstanceFactory factory;

        private UISettings settings;
        private TooltipView view;
        private UnityEngine.Object pendingOwner;
        private Func<TooltipData> pendingDataProvider;
        private RectTransform pendingAnchor;
        private TooltipOptions pendingOptions;
        private UnityEngine.Object visibleOwner;
        private Func<TooltipData> visibleDataProvider;
        private RectTransform visibleAnchor;
        private TooltipOptions visibleOptions;
        private int requestVersion;
        private float lastHideTime = float.NegativeInfinity;

        public TooltipManager(UIInstanceFactory factory, UISettings settings)
        {
            this.factory = factory;
            this.settings = settings;
        }

        public void ApplySettings(UISettings uiSettings)
        {
            settings = uiSettings;
            if (visibleOwner != null)
            {
                Refresh(visibleOwner);
            }
        }

        public void Show(
            UnityEngine.Object owner,
            RectTransform anchor,
            Func<TooltipData> dataProvider,
            TooltipOptions options = null)
        {
            if (owner == null || anchor == null || dataProvider == null)
            {
                return;
            }

            if (ReferenceEquals(visibleOwner, owner))
            {
                visibleDataProvider = dataProvider;
                visibleAnchor = anchor;
                visibleOptions = options;
                Refresh(owner);
                return;
            }

            if (ReferenceEquals(pendingOwner, owner))
            {
                pendingDataProvider = dataProvider;
                pendingAnchor = anchor;
                pendingOptions = options;
                return;
            }

            if (visibleOwner != null)
            {
                view?.Hide();
                visibleOwner = null;
                visibleDataProvider = null;
                visibleAnchor = null;
                visibleOptions = null;
                lastHideTime = Time.realtimeSinceStartup;
            }

            int version = ++requestVersion;
            pendingOwner = owner;
            pendingDataProvider = dataProvider;
            pendingAnchor = anchor;
            pendingOptions = options;
            float delaySeconds = ResolveDelay(options);
            _ = ShowDelayedAsync(version, owner, delaySeconds);
        }

        public void Hide(UnityEngine.Object owner)
        {
            if (owner == null ||
                !ReferenceEquals(pendingOwner, owner) &&
                !ReferenceEquals(visibleOwner, owner))
            {
                return;
            }

            ++requestVersion;
            if (ReferenceEquals(pendingOwner, owner))
            {
                pendingOwner = null;
                pendingDataProvider = null;
                pendingAnchor = null;
                pendingOptions = null;
            }

            if (ReferenceEquals(visibleOwner, owner))
            {
                view?.Hide();
                visibleOwner = null;
                visibleDataProvider = null;
                visibleAnchor = null;
                visibleOptions = null;
                lastHideTime = Time.realtimeSinceStartup;
            }
        }

        public void Refresh(UnityEngine.Object owner)
        {
            if (owner == null ||
                !ReferenceEquals(visibleOwner, owner) ||
                visibleDataProvider == null ||
                visibleAnchor == null ||
                view == null)
            {
                return;
            }

            TooltipData data;
            try
            {
                data = visibleDataProvider();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Hide(owner);
                return;
            }

            if (data == null || data.IsEmpty)
            {
                Hide(owner);
                return;
            }

            ShowView(data, visibleAnchor, visibleOptions);
        }

        public void HideAll()
        {
            ++requestVersion;
            pendingOwner = null;
            pendingDataProvider = null;
            pendingAnchor = null;
            pendingOptions = null;
            visibleOwner = null;
            visibleDataProvider = null;
            visibleAnchor = null;
            visibleOptions = null;
            lastHideTime = float.NegativeInfinity;
            view?.Hide();
        }

        public void Shutdown()
        {
            HideAll();
            view = null;
            settings = null;
        }

        private async Task ShowDelayedAsync(
            int version,
            UnityEngine.Object owner,
            float delaySeconds)
        {
            try
            {
                int delayMilliseconds = Mathf.CeilToInt(Mathf.Max(0f, delaySeconds) * 1000f);
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds);
                }

                if (!IsCurrentRequest(version, owner))
                {
                    ClearPendingIfCurrent(version, owner);
                    return;
                }

                if (IsBlockedByOverlay())
                {
                    ClearPending();
                    return;
                }

                TooltipData data = pendingDataProvider();
                if (data == null || data.IsEmpty)
                {
                    pendingOwner = null;
                    pendingDataProvider = null;
                    pendingAnchor = null;
                    pendingOptions = null;
                    return;
                }

                TooltipView loadedView = await EnsureViewAsync();
                if (loadedView == null || !IsCurrentRequest(version, owner))
                {
                    ClearPendingIfCurrent(version, owner);
                    return;
                }

                if (IsBlockedByOverlay())
                {
                    ClearPending();
                    return;
                }

                Func<TooltipData> dataProvider = pendingDataProvider;
                RectTransform anchor = pendingAnchor;
                TooltipOptions options = pendingOptions;
                data = dataProvider();
                if (data == null || data.IsEmpty)
                {
                    pendingOwner = null;
                    pendingDataProvider = null;
                    pendingAnchor = null;
                    pendingOptions = null;
                    return;
                }

                pendingOwner = null;
                pendingDataProvider = null;
                pendingAnchor = null;
                pendingOptions = null;
                visibleOwner = owner;
                visibleDataProvider = dataProvider;
                visibleAnchor = anchor;
                visibleOptions = options;
                ShowView(data, anchor, options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ClearPendingIfCurrent(version, owner);
            }
        }

        private async Task<TooltipView> EnsureViewAsync()
        {
            if (view != null)
            {
                return view;
            }

            string prefabPath = settings != null && !string.IsNullOrWhiteSpace(settings.tooltipPrefabPath)
                ? settings.tooltipPrefabPath
                : TooltipView.DefaultPrefabPath;

            UIHandle handle = await factory.OpenAsync(
                UIKind.Tooltip,
                UILayer.Tooltip,
                prefabPath,
                null,
                false,
                true,
                null);

            if (!handle.IsValid || !(handle.View is TooltipView tooltipView))
            {
                Debug.LogError($"[UI] Tooltip prefab must contain {nameof(TooltipView)}. path={prefabPath}");
                return null;
            }

            view = tooltipView;
            view.Hide();
            return view;
        }

        private bool IsCurrentRequest(int version, UnityEngine.Object owner)
        {
            return version == requestVersion &&
                   owner != null &&
                   pendingAnchor != null &&
                   pendingDataProvider != null &&
                   ReferenceEquals(pendingOwner, owner);
        }

        private void ClearPendingIfCurrent(int version, UnityEngine.Object owner)
        {
            if (version == requestVersion && ReferenceEquals(pendingOwner, owner))
            {
                ClearPending();
            }
        }

        private void ClearPending()
        {
            pendingOwner = null;
            pendingDataProvider = null;
            pendingAnchor = null;
            pendingOptions = null;
        }

        private float ResolveDelay(TooltipOptions options)
        {
            if (options != null && options.DelaySeconds >= 0f)
            {
                return options.DelaySeconds;
            }

            float warmDuration = settings != null ? settings.tooltipWarmDuration : DefaultWarmDuration;
            bool warm = Time.realtimeSinceStartup - lastHideTime <= Mathf.Max(0f, warmDuration);
            return warm
                ? settings != null ? settings.tooltipReshowDelay : DefaultReshowDelay
                : settings != null ? settings.tooltipInitialDelay : DefaultInitialDelay;
        }

        private void ShowView(TooltipData data, RectTransform anchor, TooltipOptions options)
        {
            TooltipPlacement placement = options != null ? options.Placement : TooltipPlacement.Auto;
            Vector2 sourceOffset = settings != null ? settings.tooltipOffset : DefaultOffset;
            float padding = settings != null ? settings.tooltipScreenPadding : DefaultScreenPadding;
            view.Show(data, anchor, placement, sourceOffset, padding);
        }

        private static bool IsBlockedByOverlay()
        {
            UIManager manager = UIManager.Current;
            return manager != null &&
                   manager.Overlays != null &&
                   manager.Overlays.HasBlockingOverlay;
        }
    }
}
