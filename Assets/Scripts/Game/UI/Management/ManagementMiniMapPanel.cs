using System;
using System.Collections.Generic;
using Game.Framework;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class ManagementMiniMapPanel : MonoBehaviour, IPointerClickHandler
    {
        private const float DynamicRefreshInterval = 0.1f;
        private static readonly Color QuestHighlightColor = new Color(1f, 0.78f, 0.18f, 1f);

        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private RawImage baseMap;
        [SerializeField] private RectTransform iconRoot;
        [SerializeField] private Image iconTemplate;
        [SerializeField] private Image playerMarker;
        [SerializeField] private Image navigationMarker;
        [SerializeField] private RectTransform cameraViewport;

        [Header("Global Icons")]
        [SerializeField] private Sprite playerDirectionIcon;
        [SerializeField] private Sprite navigationIcon;
        [SerializeField] private Sprite defaultDecorationIcon;
        [SerializeField] private Sprite defaultResourceIcon;
        [SerializeField] private Sprite defaultBuildingIcon;
        [SerializeField] private Sprite defaultInteractableIcon;
        [SerializeField] private Vector2 playerIconSize = new Vector2(16f, 16f);
        [SerializeField] private Vector2 navigationIconSize = new Vector2(14f, 14f);
        [SerializeField, Min(0.25f)] private float defaultObjectIconScale = 1f;

        [Header("Responsive Layout")]
        [SerializeField, Min(64f)] private float compactPanelSide = 220f;
        [SerializeField, Min(64f)] private float normalPanelSide = 264f;
        [SerializeField, Min(64f)] private float widePanelSide = 288f;
        [SerializeField, Min(0f)] private float viewportBorder = 8f;
        [SerializeField, Min(1f)] private float compactAspectThreshold = 1.45f;
        [SerializeField, Min(1f)] private float wideAspectThreshold = 1.70f;

        private readonly MiniMapProjection projection = new MiniMapProjection();
        private readonly Dictionary<MapObjectData, Image> objectIcons = new Dictionary<MapObjectData, Image>();
        private readonly Stack<Image> iconPool = new Stack<Image>();
        private readonly HashSet<int> questBuildingIds = new HashSet<int>();
        private readonly HashSet<int> questBuildingTypes = new HashSet<int>();
        private readonly HashSet<int> questInteractableIds = new HashSet<int>();
        private readonly Vector3[] cameraGroundCorners = new Vector3[4];
        private readonly Vector2[] cameraMapCorners = new Vector2[4];

        private MapData boundMap;
        private Texture2D mapTexture;
        private IDisposable questChangedSubscription;
        private float nextDynamicRefreshTime;
        private bool hasNavigationTarget;
        private Vector3Int navigationTarget;
        private UIViewportService subscribedViewport;
        private bool applyingResponsiveLayout;

        private void Awake()
        {
            EnsureRuntimeLayout();
        }

        private void OnEnable()
        {
            EnsureRuntimeLayout();
            EnsureViewportSubscription();
            ApplyResponsiveLayout(GetCurrentViewport());
            MapManager.Instance.MapObjectAdded += OnMapObjectAdded;
            MapManager.Instance.MapObjectRemoved += OnMapObjectRemoved;
            questChangedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, QuestChangedMessage>(
                WorldMessageTopic.QuestChanged,
                _ => RefreshAllObjectIcons());
            TryBindMap();
        }

        public void EnsureRuntimeLayout()
        {
            if (mapViewport != null &&
                baseMap != null &&
                iconRoot != null &&
                iconTemplate != null &&
                playerMarker != null &&
                navigationMarker != null &&
                cameraViewport != null)
            {
                ApplyGlobalVisuals();
                return;
            }

            RectTransform root = GetComponent<RectTransform>();
            if (root == null)
            {
                return;
            }

            Image background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }
            background.color = new Color(0.045f, 0.075f, 0.095f, 0.94f);
            background.raycastTarget = true;

            Text[] legacyLabels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                legacyLabels[i].gameObject.SetActive(false);
            }

            GameObject viewportObject = CreateUiObject("MapViewport", root, typeof(Image), typeof(RectMask2D));
            mapViewport = viewportObject.GetComponent<RectTransform>();
            SetAnchoredRect(mapViewport, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(232f, 232f));
            Image viewportBackground = viewportObject.GetComponent<Image>();
            viewportBackground.color = new Color(0.015f, 0.028f, 0.038f, 1f);
            viewportBackground.raycastTarget = false;

            GameObject baseMapObject = CreateUiObject("BaseMap", mapViewport, typeof(RawImage));
            baseMap = baseMapObject.GetComponent<RawImage>();
            Stretch(baseMap.rectTransform);
            baseMap.raycastTarget = false;

            GameObject iconRootObject = CreateUiObject("IconRoot", mapViewport);
            iconRoot = iconRootObject.GetComponent<RectTransform>();
            Stretch(iconRoot);

            iconTemplate = CreateMarkerImage("IconTemplate", iconRoot, new Vector2(12f, 12f), Color.white, true);
            iconTemplate.gameObject.SetActive(false);
            cameraViewport = CreateCameraViewport(iconRoot);

            navigationMarker = CreateMarkerImage(
                "NavigationMarker",
                iconRoot,
                new Vector2(11f, 11f),
                new Color(1f, 0.66f, 0.16f, 0.95f),
                false);
            navigationMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            navigationMarker.gameObject.SetActive(false);

            playerMarker = CreateMarkerImage(
                "PlayerMarker",
                iconRoot,
                new Vector2(12f, 12f),
                new Color(0.20f, 0.92f, 1f, 1f),
                false);
            Image forward = CreateMarkerImage(
                "Forward",
                playerMarker.rectTransform,
                new Vector2(3f, 9f),
                new Color(0.86f, 1f, 1f, 1f),
                false);
            forward.rectTransform.anchoredPosition = new Vector2(0f, 7f);
            playerMarker.gameObject.SetActive(false);
            ApplyGlobalVisuals();
        }

        private void OnDisable()
        {
            UnsubscribeViewport();
            MapManager.Instance.MapObjectAdded -= OnMapObjectAdded;
            MapManager.Instance.MapObjectRemoved -= OnMapObjectRemoved;
            questChangedSubscription?.Dispose();
            questChangedSubscription = null;
            ClearBoundMap();
        }

        private void Update()
        {
            EnsureViewportSubscription();
            TryBindMap();
            if (boundMap == null || Time.unscaledTime < nextDynamicRefreshTime)
            {
                return;
            }

            nextDynamicRefreshTime = Time.unscaledTime + DynamicRefreshInterval;
            RefreshDynamicMarkers();
        }

        private void EnsureViewportSubscription()
        {
            if (subscribedViewport != null)
            {
                return;
            }

            UIViewportService viewport = UIManager.Instance.Viewport;
            if (viewport == null)
            {
                return;
            }

            subscribedViewport = viewport;
            subscribedViewport.Changed += OnViewportChanged;
        }

        private void UnsubscribeViewport()
        {
            if (subscribedViewport == null)
            {
                return;
            }

            subscribedViewport.Changed -= OnViewportChanged;
            subscribedViewport = null;
        }

        private UIViewportInfo GetCurrentViewport()
        {
            return subscribedViewport != null
                ? subscribedViewport.Current
                : UIViewportInfo.Capture();
        }

        private void OnViewportChanged(UIViewportInfo viewport)
        {
            ApplyResponsiveLayout(viewport);
        }

        private void ApplyResponsiveLayout(UIViewportInfo viewport)
        {
            if (applyingResponsiveLayout || mapViewport == null)
            {
                return;
            }

            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            applyingResponsiveLayout = true;
            try
            {
                float aspect = viewport.IsValid ? viewport.AspectRatio : 16f / 9f;
                float panelSide = viewport.IsPortrait || aspect < compactAspectThreshold
                    ? compactPanelSide
                    : aspect >= wideAspectThreshold
                        ? widePanelSide
                        : normalPanelSide;
                panelSide = Mathf.Max(64f, panelSide);

                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelSide);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelSide);

                float mapSide = Mathf.Max(32f, panelSide - Mathf.Max(0f, viewportBorder) * 2f);
                SetAnchoredRect(
                    mapViewport,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(mapSide, mapSide));

                Canvas.ForceUpdateCanvases();
                ReconfigureProjectionForCurrentLayout();
            }
            finally
            {
                applyingResponsiveLayout = false;
            }
        }

        private void ReconfigureProjectionForCurrentLayout()
        {
            if (boundMap == null ||
                mapViewport == null ||
                !projection.Configure(boundMap, mapViewport))
            {
                return;
            }

            ApplyContentRect();
            RefreshAllObjectIcons();
            RefreshDynamicMarkers();
        }

        private void ApplyGlobalVisuals()
        {
            if (playerMarker != null)
            {
                playerMarker.sprite = playerDirectionIcon;
                playerMarker.preserveAspect = playerDirectionIcon != null;
                playerMarker.color = playerDirectionIcon != null
                    ? Color.white
                    : new Color(0.20f, 0.92f, 1f, 1f);
                playerMarker.rectTransform.sizeDelta = playerIconSize;

                Transform forward = playerMarker.transform.Find("Forward");
                if (forward != null)
                {
                    forward.gameObject.SetActive(playerDirectionIcon == null);
                }
            }

            if (navigationMarker != null)
            {
                navigationMarker.sprite = navigationIcon;
                navigationMarker.preserveAspect = navigationIcon != null;
                navigationMarker.color = navigationIcon != null
                    ? Color.white
                    : new Color(1f, 0.66f, 0.16f, 0.95f);
                navigationMarker.rectTransform.sizeDelta = navigationIconSize;
                navigationMarker.rectTransform.localRotation = navigationIcon != null
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, 45f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (boundMap == null ||
                mapViewport == null ||
                !projection.TryScreenToMap(eventData.position, eventData.pressEventCamera, out Vector2 mapPosition))
            {
                return;
            }

            int x = Mathf.Clamp(Mathf.RoundToInt(mapPosition.x), 0, boundMap.Width - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(mapPosition.y), 0, boundMap.Depth - 1);
            GameplayController.Ensure();
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Vector3 focusPosition = MapManager.Instance.TryGetTopTile(x, z, out TileData focusTile) && focusTile != null
                    ? MapManager.Instance.GetTileWorldPosition(focusTile)
                    : new Vector3(x * MapManager.Instance.TileSize, 0f, z * MapManager.Instance.TileSize);
                GameplayController.Instance.FocusCamera(focusPosition);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) || tileData == null)
            {
                Toast.Warning("该位置不可到达");
                return;
            }

            if (!GameplayController.Instance.TryNavigateTo(tileData.Coord))
            {
                Toast.Warning("无法找到可到达路径");
                return;
            }

            navigationTarget = tileData.Coord;
            hasNavigationTarget = true;
            RefreshNavigationMarker();
        }

        private void TryBindMap()
        {
            MapData current = MapManager.Instance.CurrentMap;
            if (ReferenceEquals(current, boundMap))
            {
                return;
            }

            ClearBoundMap();
            if (current == null || mapViewport == null || baseMap == null || iconRoot == null || iconTemplate == null)
            {
                return;
            }

            boundMap = current;
            boundMap.EnsureRuntimeCollections();
            Canvas.ForceUpdateCanvases();
            if (!projection.Configure(boundMap, mapViewport))
            {
                boundMap = null;
                return;
            }

            ApplyContentRect();
            mapTexture = MiniMapTextureBuilder.Build(boundMap);
            baseMap.texture = mapTexture;
            RefreshAllObjectIcons();
            RefreshDynamicMarkers();
        }

        private void ApplyContentRect()
        {
            RectTransform baseRect = baseMap.rectTransform;
            baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = projection.ContentRect.center;
            baseRect.sizeDelta = projection.ContentRect.size;
        }

        private void ClearBoundMap()
        {
            ReleaseAllObjectIcons();
            boundMap = null;
            hasNavigationTarget = false;
            if (navigationMarker != null)
            {
                navigationMarker.gameObject.SetActive(false);
            }

            if (playerMarker != null)
            {
                playerMarker.gameObject.SetActive(false);
            }

            if (cameraViewport != null)
            {
                cameraViewport.gameObject.SetActive(false);
            }

            if (baseMap != null)
            {
                baseMap.texture = null;
            }

            if (mapTexture != null)
            {
                Destroy(mapTexture);
                mapTexture = null;
            }
        }

        private void RefreshAllObjectIcons()
        {
            if (boundMap == null)
            {
                return;
            }

            RebuildQuestTargets();
            ReleaseAllObjectIcons();
            for (int i = 0; i < boundMap.Objects.Count; i++)
            {
                AddOrRefreshObjectIcon(boundMap.Objects[i]);
            }
        }

        private void OnMapObjectAdded(MapObjectData mapObject)
        {
            if (boundMap != null)
            {
                AddOrRefreshObjectIcon(mapObject);
            }
        }

        private void OnMapObjectRemoved(MapObjectData mapObject)
        {
            if (mapObject != null && objectIcons.TryGetValue(mapObject, out Image icon))
            {
                objectIcons.Remove(mapObject);
                ReleaseIcon(icon);
            }
        }

        private void AddOrRefreshObjectIcon(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return;
            }

            bool questTarget = IsQuestTarget(mapObject);
            MiniMapObjectStyle style = MiniMapObjectResolver.Resolve(mapObject, questTarget);
            if (!style.Visible)
            {
                if (objectIcons.TryGetValue(mapObject, out Image hiddenIcon))
                {
                    objectIcons.Remove(mapObject);
                    ReleaseIcon(hiddenIcon);
                }
                return;
            }

            if (!objectIcons.TryGetValue(mapObject, out Image icon))
            {
                icon = AcquireIcon();
                objectIcons.Add(mapObject, icon);
            }

            Sprite resolvedIcon = style.Icon != null ? style.Icon : GetDefaultObjectIcon(mapObject.ObjectType);
            icon.sprite = resolvedIcon;
            icon.preserveAspect = resolvedIcon != null;
            icon.color = resolvedIcon != null ? Color.white : style.Color;
            Vector2 iconSize = style.Size * Mathf.Max(0.25f, defaultObjectIconScale);
            icon.rectTransform.sizeDelta = questTarget ? iconSize * 1.25f : iconSize;
            Vector3 worldPosition = MapManager.Instance.GetTileWorldPosition(mapObject.Coord) + mapObject.LocalPosition;
            icon.rectTransform.anchoredPosition = projection.WorldToAnchored(worldPosition, MapManager.Instance.TileSize);

            Outline outline = icon.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = questTarget;
                outline.effectColor = QuestHighlightColor;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        private Sprite GetDefaultObjectIcon(MapObjectType objectType)
        {
            switch (objectType)
            {
                case MapObjectType.Decoration:
                    return defaultDecorationIcon;
                case MapObjectType.Resource:
                    return defaultResourceIcon;
                case MapObjectType.Building:
                    return defaultBuildingIcon;
                case MapObjectType.Interactable:
                    return defaultInteractableIcon;
                default:
                    return null;
            }
        }

        private Image AcquireIcon()
        {
            Image icon = iconPool.Count > 0 ? iconPool.Pop() : Instantiate(iconTemplate, iconRoot);
            icon.transform.SetParent(iconRoot, false);
            icon.raycastTarget = false;
            icon.gameObject.SetActive(true);
            return icon;
        }

        private void ReleaseAllObjectIcons()
        {
            foreach (KeyValuePair<MapObjectData, Image> pair in objectIcons)
            {
                ReleaseIcon(pair.Value);
            }
            objectIcons.Clear();
        }

        private void ReleaseIcon(Image icon)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = null;
            icon.gameObject.SetActive(false);
            iconPool.Push(icon);
        }

        private void RefreshDynamicMarkers()
        {
            GameplayController controller = GameplayController.Instance;
            if (controller == null)
            {
                if (playerMarker != null) playerMarker.gameObject.SetActive(false);
                if (cameraViewport != null) cameraViewport.gameObject.SetActive(false);
                return;
            }

            if (playerMarker != null)
            {
                playerMarker.gameObject.SetActive(true);
                playerMarker.rectTransform.anchoredPosition =
                    projection.WorldToAnchored(controller.PlayerPosition, MapManager.Instance.TileSize);
                playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -controller.PlayerRotationY);
                playerMarker.transform.SetAsLastSibling();
            }

            RefreshCameraViewport(controller.WorldCamera);
            RefreshNavigationMarker();
        }

        private void RefreshNavigationMarker()
        {
            if (navigationMarker == null)
            {
                return;
            }

            if (!hasNavigationTarget)
            {
                navigationMarker.gameObject.SetActive(false);
                return;
            }

            Vector3 worldTarget = MapManager.Instance.GetTileWorldPosition(navigationTarget);
            navigationMarker.gameObject.SetActive(true);
            navigationMarker.rectTransform.anchoredPosition =
                projection.WorldToAnchored(worldTarget, MapManager.Instance.TileSize);
            navigationMarker.transform.SetAsLastSibling();

            GameplayController controller = GameplayController.Instance;
            if (controller != null)
            {
                Vector2 player = new Vector2(controller.PlayerPosition.x, controller.PlayerPosition.z);
                Vector2 target = new Vector2(worldTarget.x, worldTarget.z);
                if ((player - target).sqrMagnitude <= MapManager.Instance.TileSize * MapManager.Instance.TileSize * 0.25f)
                {
                    hasNavigationTarget = false;
                    navigationMarker.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshCameraViewport(Camera worldCamera)
        {
            if (cameraViewport == null || worldCamera == null)
            {
                if (cameraViewport != null) cameraViewport.gameObject.SetActive(false);
                return;
            }

            if (!TryIntersectGround(worldCamera, new Vector2(0f, 0f), out cameraGroundCorners[0]) ||
                !TryIntersectGround(worldCamera, new Vector2(1f, 0f), out cameraGroundCorners[1]) ||
                !TryIntersectGround(worldCamera, new Vector2(1f, 1f), out cameraGroundCorners[2]) ||
                !TryIntersectGround(worldCamera, new Vector2(0f, 1f), out cameraGroundCorners[3]))
            {
                cameraViewport.gameObject.SetActive(false);
                return;
            }

            Vector2 center = Vector2.zero;
            for (int i = 0; i < cameraGroundCorners.Length; i++)
            {
                cameraMapCorners[i] = projection.WorldToAnchored(cameraGroundCorners[i], MapManager.Instance.TileSize);
                center += cameraMapCorners[i];
            }
            center *= 0.25f;

            Vector2 bottomEdge = cameraMapCorners[1] - cameraMapCorners[0];
            float width = bottomEdge.magnitude;
            float height = (cameraMapCorners[2] - cameraMapCorners[1]).magnitude;
            cameraViewport.gameObject.SetActive(width > 0.1f && height > 0.1f);
            cameraViewport.anchoredPosition = center;
            cameraViewport.sizeDelta = new Vector2(width, height);
            cameraViewport.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(bottomEdge.y, bottomEdge.x) * Mathf.Rad2Deg);
            cameraViewport.transform.SetAsLastSibling();
        }

        private static bool TryIntersectGround(Camera camera, Vector2 viewportPoint, out Vector3 worldPosition)
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            Ray ray = camera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            if (ground.Raycast(ray, out float distance))
            {
                worldPosition = ray.GetPoint(distance);
                return true;
            }

            worldPosition = default;
            return false;
        }

        private void RebuildQuestTargets()
        {
            questBuildingIds.Clear();
            questBuildingTypes.Clear();
            questInteractableIds.Clear();

            QuestSnapshot quest = QuestManager.Instance.GetTrackedQuest();
            if (quest == null || quest.State != QuestState.Accepted || quest.Objectives == null)
            {
                return;
            }

            for (int i = 0; i < quest.Objectives.Length; i++)
            {
                QuestObjectiveSnapshot objective = quest.Objectives[i];
                if (objective?.Config == null || objective.Completed)
                {
                    continue;
                }

                switch (objective.Config.Type)
                {
                    case QuestObjectiveType.BuildBuilding:
                    case QuestObjectiveType.UpgradeBuilding:
                        questBuildingIds.Add(objective.Config.TargetId);
                        break;
                    case QuestObjectiveType.BuildBuildingType:
                        questBuildingTypes.Add(objective.Config.TargetId);
                        break;
                    case QuestObjectiveType.TalkNpc:
                    case QuestObjectiveType.EnterArea:
                        questInteractableIds.Add(objective.Config.TargetId);
                        break;
                }
            }
        }

        private bool IsQuestTarget(MapObjectData mapObject)
        {
            if (mapObject.ObjectType == MapObjectType.Building)
            {
                if (questBuildingIds.Contains(mapObject.ConfigId))
                {
                    return true;
                }

                return questBuildingTypes.Count > 0 &&
                       DataManager.Instance.WorldBuilding != null &&
                       DataManager.Instance.WorldBuilding.TryGet(mapObject.ConfigId, out WorldBuildingConfig building) &&
                       building != null &&
                       questBuildingTypes.Contains(building.BuildingType);
            }

            return mapObject.ObjectType == MapObjectType.Interactable &&
                   questInteractableIds.Contains(mapObject.ConfigId);
        }

        private static RectTransform CreateCameraViewport(RectTransform parent)
        {
            GameObject viewportObject = CreateUiObject("CameraViewport", parent, typeof(Image), typeof(Outline));
            RectTransform rect = viewportObject.GetComponent<RectTransform>();
            SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 30f));
            Image image = viewportObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.84f, 1f, 0.035f);
            image.raycastTarget = false;
            Outline outline = viewportObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.86f, 1f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            viewportObject.SetActive(false);
            return rect;
        }

        private static Image CreateMarkerImage(
            string name,
            RectTransform parent,
            Vector2 size,
            Color color,
            bool addOutline)
        {
            GameObject marker = addOutline
                ? CreateUiObject(name, parent, typeof(Image), typeof(Outline))
                : CreateUiObject(name, parent, typeof(Image));
            RectTransform rect = marker.GetComponent<RectTransform>();
            SetAnchoredRect(rect, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image image = marker.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (addOutline)
            {
                marker.GetComponent<Outline>().enabled = false;
            }
            return image;
        }

        private static GameObject CreateUiObject(string name, RectTransform parent, params Type[] components)
        {
            Type[] allComponents = new Type[components.Length + 2];
            allComponents[0] = typeof(RectTransform);
            allComponents[1] = typeof(CanvasRenderer);
            for (int i = 0; i < components.Length; i++)
            {
                allComponents[i + 2] = components[i];
            }

            GameObject result = new GameObject(name, allComponents);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                result.layer = uiLayer;
            }
            result.GetComponent<RectTransform>().SetParent(parent, false);
            return result;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
