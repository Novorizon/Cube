using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public sealed class UIRoot : MonoBehaviour
    {
        static UIRoot? instance;

        public static UIRoot Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = FindFirstObjectByType<UIRoot>();
                if (instance == null)
                {
                    var go = new GameObject("UIRoot");
                    instance = go.AddComponent<UIRoot>();
                }

                return instance;
            }
        }

        [SerializeField] UISettings? settings;

        public UIMessageBus Bus { get; } = new UIMessageBus();

        public PageNavigator Pages { get; private set; } = null!;
        public PopupManager Popups { get; private set; } = null!;
        public PanelManager Panels { get; private set; } = null!;
        public OverlayManager Overlays { get; private set; } = null!;
        public ToastManager Toasts { get; private set; } = null!;

        IUIAssetLoader loader = new ResourcesAssetLoader();

        readonly Dictionary<UILayer, Transform> layerRoots = new();

        UIInstanceFactory? factory;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureEventSystem();
            EnsureCanvasHierarchy();

            factory = new UIInstanceFactory(loader, layerRoots, startId: 1);

            Pages = new PageNavigator(factory);
            Popups = new PopupManager(factory);
            Panels = new PanelManager(factory);
            Overlays = new OverlayManager(factory);
            Toasts = new ToastManager(factory);
        }

        public void SetSettings(UISettings uiSettings)
        {
            settings = uiSettings;
            EnsureCanvasHierarchy();
        }

        public void SetAssetLoader(IUIAssetLoader assetLoader)
        {
            loader = assetLoader;

            factory = new UIInstanceFactory(loader, layerRoots, startId: factory != null ? factory.NextId : 1);

            Pages = new PageNavigator(factory);
            Popups = new PopupManager(factory);
            Panels = new PanelManager(factory);
            Overlays = new OverlayManager(factory);
            Toasts = new ToastManager(factory);
        }

        public bool HandleBack()
        {
            if (Popups.CloseTop())
            {
                return true;
            }

            if (Panels.HideAnyBackClosablePanel())
            {
                return true;
            }

            if (Pages.Count > 1)
            {
                return Pages.Pop();
            }

            return false;
        }

        void EnsureEventSystem()
        {
            EventSystem? es = FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
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

            Canvas? rootCanvas = GetComponentInChildren<Canvas>(true);
            if (rootCanvas == null)
            {
                var canvasGo = new GameObject("UICanvasRoot");
                canvasGo.transform.SetParent(transform, false);

                rootCanvas = canvasGo.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                rootCanvas.worldCamera = Camera.main;

                if (settings != null)
                {
                    rootCanvas.planeDistance = settings.canvasPlaneDistance;
                }

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

                if (settings != null)
                {
                    scaler.referenceResolution = new Vector2(settings.referenceWidth, settings.referenceHeight);
                    scaler.screenMatchMode = settings.screenMatchMode;
                    scaler.matchWidthOrHeight = settings.matchWidthOrHeight;
                }

                canvasGo.AddComponent<GraphicRaycaster>();
            }

            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Background);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Page);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Popup);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Panel);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Toast);
            CreateOrUpdateLayer(rootCanvas.transform, UILayer.Overlay);
        }

        void CreateOrUpdateLayer(Transform parent, UILayer layer)
        {
            string name = $"Layer_{layer}";
            Transform? layerTransform = parent.Find(name);

            if (layerTransform == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);

                var canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = true;

                int step = settings != null ? settings.sortingOrderStep : 100;
                canvas.sortingOrder = (int)layer * step;

                go.AddComponent<GraphicRaycaster>();

                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                layerTransform = go.transform;
            }

            layerRoots[layer] = layerTransform;
        }
    }
}
