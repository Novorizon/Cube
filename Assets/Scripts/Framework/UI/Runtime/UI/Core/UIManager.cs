using System.Collections.Generic;
using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public sealed class UIManager : MonoBehaviour
    {
        static UIManager instance;

        public static UIManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = FindFirstObjectByType<UIManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("UIManager");
                    instance = go.AddComponent<UIManager>();
                }

                return instance;
            }
        }

        public static UIManager Current => instance;

        [SerializeField] UISettings settings;

        public UIMessageBus Bus { get; } = new UIMessageBus();

        public PageNavigator Pages { get; private set; }
        public PopupManager Popups { get; private set; }
        public PanelManager Panels { get; private set; }
        public OverlayManager Overlays { get; private set; }
        public ToastManager Toasts { get; private set; }
        public TooltipManager Tooltips { get; private set; }
        public UIViewportService Viewport { get; private set; }

        IUIAssetLoader loader = new ResourcesUIAssetLoader();
        readonly Dictionary<UILayer, Transform> layerRoots = new Dictionary<UILayer, Transform>();

        UIInstanceFactory factory;
        UIOutsideClickDetector outsideClickDetector;
        GameObject panelOutsideBlocker;
        Canvas panelOutsideBlockerCanvas;
        int pointerConsumedFrame = -1;

        public bool IsPointerConsumedThisFrame => pointerConsumedFrame == Time.frameCount;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Viewport = new UIViewportService(DeviceManager.Instance);
            EnsureEventSystem();
            EnsureCanvasHierarchy();
            RebuildManagers(factory != null ? factory.NextId : 1, factory != null ? factory.NextVersion : 1);
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            Toasts?.Shutdown();
            Tooltips?.Shutdown();
            Overlays?.ForceHideBlocking(true);
            Popups?.CloseAll(true);
            Panels?.HideAll(true);
            Pages?.Clear(true);
            Bus.Clear();

            factory?.DestroyAll();
            Viewport?.Shutdown();
            Viewport = null;
        }
        public void SetSettings(UISettings uiSettings)
        {
            settings = uiSettings;
            EnsureCanvasHierarchy();
            Tooltips?.ApplySettings(settings);
        }

        public void UseResourceManagerLoader()
        {
            SetAssetLoader(new ResourceManagerUIAssetLoader());
        }

        public void SetAssetLoader(IUIAssetLoader assetLoader)
        {
            if (assetLoader == null)
            {
                Debug.LogError("[UI] assetLoader is null.");
                return;
            }

            int startId = factory != null ? factory.NextId : 1;
            int startVersion = factory != null ? factory.NextVersion : 1;
            Tooltips?.Shutdown();
            factory?.DestroyAll();
            loader = assetLoader;
            RebuildManagers(startId, startVersion);
        }

        public bool HandleBack()
        {
            Tooltips?.HideAll();

            if (Popups != null && Popups.CloseTop(UICloseReason.Back))
            {
                return true;
            }

            if (Panels != null && Panels.HideAnyBackClosablePanel())
            {
                return true;
            }

            if (Pages != null && Pages.Count > 1)
            {
                return Pages.Pop();
            }

            return false;
        }

        public void ClearAll(bool destroy = false)
        {
            Tooltips?.HideAll();
            Toasts?.Clear(true);
            Overlays?.ForceHideBlocking(destroy);
            Popups?.CloseAll(destroy);
            Panels?.HideAll(destroy);
            Pages?.Clear(destroy);
            Bus.Clear();
        }

        internal void MarkPointerConsumedForCurrentFrame()
        {
            pointerConsumedFrame = Time.frameCount;
        }

        void LateUpdate()
        {
            RefreshPanelOutsideBlocker();
        }

        void RebuildManagers(int startId, int startVersion)
        {
            factory = new UIInstanceFactory(loader, layerRoots, startId, startVersion);
            Pages = new PageNavigator(factory);
            Popups = new PopupManager(factory);
            Panels = new PanelManager(factory);
            Overlays = new OverlayManager(factory);
            Toasts = new ToastManager(factory);
            Tooltips = new TooltipManager(factory, settings);
            EnsureOutsideClickDetector();
        }

        void EnsureOutsideClickDetector()
        {
            if (outsideClickDetector == null)
            {
                outsideClickDetector = GetComponent<UIOutsideClickDetector>();
                if (outsideClickDetector == null)
                {
                    outsideClickDetector = gameObject.AddComponent<UIOutsideClickDetector>();
                }
            }

            outsideClickDetector.Initialize(this);
        }

        void RefreshPanelOutsideBlocker()
        {
            if (panelOutsideBlocker == null)
            {
                return;
            }

            bool shouldShow = Panels != null && Panels.HasOutsideClickTarget();
            if (panelOutsideBlocker.activeSelf != shouldShow)
            {
                panelOutsideBlocker.SetActive(shouldShow);
            }
        }

        void EnsureEventSystem()
        {
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(go);
        }

        void EnsureCanvasHierarchy()
        {
            if (settings == null)
            {
                settings = Resources.Load<UISettings>("UISettings");
            }

            Canvas rootCanvas = GetComponentInChildren<Canvas>(true);
            if (rootCanvas == null)
            {
                GameObject canvasGo = new GameObject("UICanvasRoot");
                canvasGo.transform.SetParent(transform, false);

                rootCanvas = canvasGo.AddComponent<Canvas>();
            }

            rootCanvas.renderMode = settings != null ? settings.renderMode : RenderMode.ScreenSpaceOverlay;
            rootCanvas.worldCamera = ResolveCamera();

            if (settings != null)
            {
                rootCanvas.planeDistance = settings.canvasPlaneDistance;
            }

            CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (settings != null)
            {
                scaler.referenceResolution = new Vector2(settings.referenceWidth, settings.referenceHeight);
                scaler.screenMatchMode = settings.screenMatchMode;
                scaler.matchWidthOrHeight = settings.matchWidthOrHeight;
            }

            if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            CreateOrUpdatePanelOutsideBlocker(rootCanvas.transform);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Background);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Page);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Popup);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Panel);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Toast);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Tooltip);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Overlay);
        }

        Camera ResolveCamera()
        {
            if (settings != null && settings.explicitWorldCamera != null)
            {
                return settings.explicitWorldCamera;
            }

            return Camera.main;
        }

        void CreateOrUpdateLayer(Transform parent, UILayer layer)
        {
            string name = $"Layer_{layer}";
            Transform layerTransform = parent.Find(name);

            if (layerTransform == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);

                RectTransform rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Canvas canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = true;

                int step = settings != null ? settings.sortingOrderStep : 100;
                canvas.sortingOrder = (int)layer * step;

                go.AddComponent<GraphicRaycaster>();
                layerTransform = go.transform;
            }

            layerRoots[layer] = layerTransform;
        }

        void CreateOrUpdatePanelOutsideBlocker(Transform parent)
        {
            const string blockerName = "PanelOutsideBlocker";
            Transform blockerTransform = parent.Find(blockerName);

            if (blockerTransform == null)
            {
                panelOutsideBlocker = new GameObject(blockerName);
                panelOutsideBlocker.transform.SetParent(parent, false);

                RectTransform rt = panelOutsideBlocker.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                panelOutsideBlockerCanvas = panelOutsideBlocker.AddComponent<Canvas>();
                panelOutsideBlockerCanvas.overrideSorting = true;

                panelOutsideBlocker.AddComponent<GraphicRaycaster>();

                Image image = panelOutsideBlocker.AddComponent<Image>();
                image.raycastTarget = true;
                image.color = Color.clear;
            }
            else
            {
                panelOutsideBlocker = blockerTransform.gameObject;
                panelOutsideBlockerCanvas = panelOutsideBlocker.GetComponent<Canvas>();
                if (panelOutsideBlockerCanvas == null)
                {
                    panelOutsideBlockerCanvas = panelOutsideBlocker.AddComponent<Canvas>();
                }

                if (panelOutsideBlocker.GetComponent<GraphicRaycaster>() == null)
                {
                    panelOutsideBlocker.AddComponent<GraphicRaycaster>();
                }

                Image image = panelOutsideBlocker.GetComponent<Image>();
                if (image == null)
                {
                    image = panelOutsideBlocker.AddComponent<Image>();
                }

                image.raycastTarget = true;
                image.color = Color.clear;
            }

            panelOutsideBlockerCanvas.overrideSorting = true;
            int step = settings != null ? settings.sortingOrderStep : 100;
            panelOutsideBlockerCanvas.sortingOrder = ((int)UILayer.Background * step) - 1;
            panelOutsideBlocker.transform.SetAsFirstSibling();
            panelOutsideBlocker.SetActive(false);
        }
    }
}
