using Game.Framework;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldGameplayController : MonoBehaviour
    {
        public static WorldGameplayController Instance { get; private set; }

        private const float CameraMinHeight = 7f;
        private const float CameraMaxHeight = 24f;
        private const float CameraMoveSpeed = 8f;
        private const float CameraZoomSpeed = 0.025f;
        private const float PlayerMoveSpeed = 4f;
        private const float DragThresholdPixels = 12f;

        private readonly WorldRewardResolver rewardResolver = new WorldRewardResolver(DataManager.Instance.WorldReward);
        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);

        private Camera mainCamera;
        private Transform player;
        private Vector3 playerDestination;
        private bool hasPlayerDestination;
        private Vector3 cameraPivot;
        private float cameraHeight = CameraMinHeight;

        private bool leftPointerActive;
        private bool leftPressOverUi;
        private Vector2 leftPressScreenPosition;
        private Vector3Int leftPressCoord;
        private bool leftPressHasTile;

        private GameObject seedPanel;
        private Farm selectedFarm;
        private GameObject buildPanel;
        private int selectedBuildingId;
        private Text selectedBuildingText;
        private float nextBuildPanelRefreshTime;
        private GameObject worldHud;
        private Text worldHudText;
        private float nextHudRefreshTime;
        private GameObject buildingPreview;
        private int previewBuildingId;
        private Material previewValidMaterial;
        private Material previewInvalidMaterial;
        private bool missingPreviewPrefabLogged;

        public int SelectedBuildingId => selectedBuildingId;

        public static void Ensure()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject root = new GameObject("WorldGameplayController");
            Instance = root.AddComponent<WorldGameplayController>();
        }

        public static void Shutdown()
        {
            if (Instance == null)
            {
                return;
            }

            Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            GameInputManager.Instance.WorldSelectPerformed += OnWorldSelectPerformed;
            GameInputManager.Instance.WorldAttackCommandPerformed += OnWorldAttackCommandPerformed;
            GameInputManager.Instance.WorldCancelPerformed += OnWorldCancelPerformed;
        }

        private void OnDisable()
        {
            GameInputManager.Instance.WorldSelectPerformed -= OnWorldSelectPerformed;
            GameInputManager.Instance.WorldAttackCommandPerformed -= OnWorldAttackCommandPerformed;
            GameInputManager.Instance.WorldCancelPerformed -= OnWorldCancelPerformed;
            HideSeedPanel();
            HideBuildPanel();
            HideWorldHud();
            ClearBuildingPreview();
            WorldMainPanel.Shutdown();
        }

        private void Update()
        {
            if (MapManager.Instance.CurrentMap == null)
            {
                return;
            }

            EnsureCamera();
            EnsurePlayer();
            WorldMainPanel.Ensure();
            UpdateCamera();
            UpdatePlayer();
            UpdateBuildingPreview();
            UpdateLeftPointer();
        }

        private void OnWorldSelectPerformed(InputAction.CallbackContext context)
        {
            BeginLeftPointer();
        }

        private void OnWorldAttackCommandPerformed(InputAction.CallbackContext context)
        {
            MovePlayerToPointer();
        }

        private void OnWorldCancelPerformed(InputAction.CallbackContext context)
        {
            if (IsMouseRightButton(context))
            {
                MovePlayerToPointer();
                return;
            }

            HideSeedPanel();
            ClearSelectedBuilding();
        }

        private void MovePlayerToPointer()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            if (!TryPickTileCoord(out Vector3Int coord))
            {
                return;
            }

            playerDestination = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
            hasPlayerDestination = true;
        }

        private static bool IsMouseRightButton(InputAction.CallbackContext context)
        {
            return context.control != null &&
                   context.control.device is Mouse &&
                   context.control.name == "rightButton";
        }

        private void BeginLeftPointer()
        {
            leftPointerActive = true;
            leftPressScreenPosition = GameInputManager.Instance.PointerPosition;
            leftPressOverUi = IsPointerOverUi();
            leftPressHasTile = TryPickTile(out TileView tileView);
            leftPressCoord = leftPressHasTile && tileView != null ? tileView.Coord : default;
        }

        private void UpdateLeftPointer()
        {
            bool held = GameInputManager.Instance.WorldSelectHeld;
            if (!leftPointerActive || held)
            {
                return;
            }

            Vector2 currentPosition = GameInputManager.Instance.PointerPosition;
            float dragDistance = Vector2.Distance(leftPressScreenPosition, currentPosition);
            bool isDrag = dragDistance >= DragThresholdPixels;

            if (!leftPressOverUi)
            {
                if (isDrag)
                {
                    CompleteFarmDrag();
                }
                else
                {
                    HandleLeftClick();
                }
            }

            leftPointerActive = false;
            leftPressHasTile = false;
        }

        private void HandleLeftClick()
        {
            HideSeedPanel();

            if (TryPickResource(out WorldResourceView resourceView) && HandleResourceClick(resourceView))
            {
                return;
            }

            if (!TryPickTile(out TileView tileView) || tileView == null)
            {
                return;
            }

            if (selectedBuildingId > 0)
            {
                TryBuildSelectedBuilding(tileView.Coord);
                return;
            }

            if (FarmManager.Instance.TryGetFarmAt(tileView.Coord, out Farm farm))
            {
                selectedFarm = farm;
                ShowSeedPanel();
            }
        }

        private void CompleteFarmDrag()
        {
            if (!leftPressHasTile || !TryPickTile(out TileView endTile) || endTile == null)
            {
                return;
            }

            if (!HasMainBase())
            {
                return;
            }

            if (selectedBuildingId > 0)
            {
                return;
            }

            selectedFarm = FarmManager.Instance.CreateFarmArea(leftPressCoord, endTile.Coord);
            if (selectedFarm != null)
            {
                ShowSeedPanel();
            }
        }

        private bool HandleResourceClick(WorldResourceView resourceView)
        {
            if (resourceView == null || resourceView.MapObject == null)
            {
                return false;
            }

            MapObjectData mapObject = resourceView.MapObject;
            if (!DataManager.Instance.WorldResource.TryGet(mapObject.ConfigId, out WorldResourceConfig config) || config == null || !config.Enable)
            {
                return false;
            }

            WorldResourceInteractionType interactionType = (WorldResourceInteractionType)config.InteractionType;
            switch (interactionType)
            {
                case WorldResourceInteractionType.Pickup:
                    return PickupResource(resourceView, config);

                case WorldResourceInteractionType.Gather:
                    if (WorldGatherManager.Instance.TryGather(mapObject, out _))
                    {
                        resourceView.RefreshNow();
                        return true;
                    }

                    return false;

                case WorldResourceInteractionType.MineTarget:
                    return MineManager.Instance.TryBuildMine(resourceView, config);
            }

            return false;
        }

        private bool TryBuildSelectedBuilding(Vector3Int coord)
        {
            if (selectedBuildingId <= 0)
            {
                return false;
            }

            if (WorldBuildingManager.Instance.TryBuild(selectedBuildingId, coord))
            {
                ClearSelectedBuilding();
                RefreshBuildPanel(true);
                WorldMainPanel.Instance?.RefreshNow();
                return true;
            }

            return false;
        }

        private void UpdateBuildingPreview()
        {
            if (selectedBuildingId <= 0 || IsPointerOverUi())
            {
                SetBuildingPreviewVisible(false);
                return;
            }

            if (!TryPickTileCoord(out Vector3Int coord))
            {
                SetBuildingPreviewVisible(false);
                return;
            }

            EnsureBuildingPreview();
            if (buildingPreview == null)
            {
                return;
            }

            bool canPlace = CanPlaceSelectedBuilding(coord);
            buildingPreview.transform.position = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * MapManager.Instance.TileSize;
            buildingPreview.transform.rotation = Quaternion.identity;
            SetBuildingPreviewVisible(true);
            ApplyPreviewMaterial(canPlace);
        }

        private bool CanPlaceSelectedBuilding(Vector3Int coord)
        {
            if (selectedBuildingId <= 0)
            {
                return false;
            }

            if (!DataManager.Instance.WorldBuilding.TryGet(selectedBuildingId, out WorldBuildingConfig config) || config == null || !config.Enable)
            {
                return false;
            }

            if (!WorldBuildingManager.Instance.IsBuildingUnlocked(selectedBuildingId))
            {
                return false;
            }

            if (config.SizeX != 1 || config.SizeZ != 1)
            {
                return false;
            }

            if (!HasBuildCost(selectedBuildingId, out _))
            {
                return false;
            }

            return MapManager.Instance.CanPlaceMapObject(coord);
        }

        private void EnsureBuildingPreview()
        {
            if (previewBuildingId == selectedBuildingId)
            {
                if (buildingPreview != null || missingPreviewPrefabLogged)
                {
                    return;
                }
            }

            ClearBuildingPreview();
            previewBuildingId = selectedBuildingId;
            missingPreviewPrefabLogged = false;

            if (!DataManager.Instance.WorldBuilding.TryGet(selectedBuildingId, out WorldBuildingConfig config) ||
                config == null ||
                string.IsNullOrWhiteSpace(config.PrefabLocation))
            {
                LogMissingPreviewPrefab(config);
                return;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);
            if (prefab == null)
            {
                LogMissingPreviewPrefab(config);
                return;
            }

            buildingPreview = GameObject.Instantiate(prefab);
            buildingPreview.name = $"WorldBuildingPreview_{selectedBuildingId}";
            RemoveColliders(buildingPreview);
            SetBuildingPreviewVisible(false);
        }

        private void LogMissingPreviewPrefab(WorldBuildingConfig config)
        {
            if (missingPreviewPrefabLogged)
            {
                return;
            }

            string location = config != null ? config.PrefabLocation : string.Empty;
            Debug.LogError($"Missing world building preview prefab. buildingId: {selectedBuildingId}, location: {location}");
            missingPreviewPrefabLogged = true;
        }

        private void SetBuildingPreviewVisible(bool visible)
        {
            if (buildingPreview != null && buildingPreview.activeSelf != visible)
            {
                buildingPreview.SetActive(visible);
            }
        }

        private void ApplyPreviewMaterial(bool canPlace)
        {
            if (buildingPreview == null)
            {
                return;
            }

            Material material = canPlace ? GetPreviewValidMaterial() : GetPreviewInvalidMaterial();
            Renderer[] renderers = buildingPreview.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private Material GetPreviewValidMaterial()
        {
            if (previewValidMaterial == null)
            {
                previewValidMaterial = CreatePreviewMaterial(new Color(0.22f, 0.72f, 0.35f, 0.62f));
            }

            return previewValidMaterial;
        }

        private Material GetPreviewInvalidMaterial()
        {
            if (previewInvalidMaterial == null)
            {
                previewInvalidMaterial = CreatePreviewMaterial(new Color(0.9f, 0.18f, 0.18f, 0.62f));
            }

            return previewInvalidMaterial;
        }

        private static Material CreatePreviewMaterial(Color color)
        {
            Material material = new Material(FindRuntimeColorShader());
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }

        private void ClearBuildingPreview()
        {
            if (buildingPreview != null)
            {
                Destroy(buildingPreview);
            }

            buildingPreview = null;
            previewBuildingId = 0;
            missingPreviewPrefabLogged = false;
        }

        private bool PickupResource(WorldResourceView resourceView, WorldResourceConfig config)
        {
            IReadOnlyList<WorldItem> rewards = rewardResolver.GetRewardGroup(config.PickupRewardGroupId);
            if (rewards.Count == 0)
            {
                return false;
            }

            WorldItemManager.Instance.AddItems(rewards);
            RemoveResourceView(resourceView);
            StorageManager.Instance.MarkDirty();
            return true;
        }

        private void RemoveResourceView(WorldResourceView resourceView)
        {
            if (resourceView == null || resourceView.MapObject == null)
            {
                return;
            }

            MapManager.Instance.TryRemoveMapObject(resourceView.MapObject.ObjectId);
            MapManager.Instance.MarkMapObjectRemoved(resourceView.MapObject.ObjectId);
            Destroy(resourceView.gameObject);
        }

        private bool HasMainBase()
        {
            return WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.MainBase);
        }

        private bool TryPickTile(out TileView tileView)
        {
            tileView = null;
            EnsureCamera();
            return MapManager.Instance.TryPickTile(GameInputManager.Instance.PointerPosition, mainCamera, out tileView);
        }

        private bool TryPickTileCoord(out Vector3Int coord)
        {
            coord = default;
            if (TryPickTile(out TileView tileView) && tileView != null)
            {
                coord = tileView.Coord;
                return true;
            }

            EnsureCamera();
            if (mainCamera == null)
            {
                return false;
            }

            Ray ray = mainCamera.ScreenPointToRay(GameInputManager.Instance.PointerPosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 point = ray.GetPoint(enter);
            float tileSize = Mathf.Max(0.01f, MapManager.Instance.TileSize);
            int x = Mathf.RoundToInt(point.x / tileSize);
            int z = Mathf.RoundToInt(point.z / tileSize);
            if (MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) && tileData != null)
            {
                coord = tileData.Coord;
                return true;
            }

            return false;
        }

        private bool TryPickResource(out WorldResourceView resourceView)
        {
            resourceView = null;
            EnsureCamera();
            if (mainCamera == null)
            {
                return false;
            }

            Ray ray = mainCamera.ScreenPointToRay(GameInputManager.Instance.PointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                return false;
            }

            resourceView = hit.collider.GetComponentInParent<WorldResourceView>();
            return resourceView != null;
        }

        private void EnsureCamera()
        {
            if (mainCamera != null)
            {
                return;
            }

            CameraManager.Instance.Initialize();
            mainCamera = CameraManager.Instance.MainCamera != null ? CameraManager.Instance.MainCamera : Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 45f;
            cameraHeight = CameraMinHeight;
            cameraPivot = CalculateMapCenter();
            ApplyCameraTransform();
        }

        private void UpdateCamera()
        {
            if (mainCamera == null)
            {
                return;
            }

            Vector2 move = GameInputManager.Instance.WorldMove;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            cameraPivot += (right * move.x + forward * move.y) * (CameraMoveSpeed * Time.deltaTime);

            float scroll = GameInputManager.Instance.Scroll.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraHeight = Mathf.Clamp(cameraHeight - scroll * CameraZoomSpeed, CameraMinHeight, CameraMaxHeight);
            }

            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            if (mainCamera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(60f, 45f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            float distance = cameraHeight / Mathf.Max(0.1f, -forward.y);
            mainCamera.transform.rotation = rotation;
            mainCamera.transform.position = cameraPivot - forward * distance;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 1000f;
        }

        private Vector3 CalculateMapCenter()
        {
            MapData map = MapManager.Instance.CurrentMap;
            if (map == null)
            {
                return Vector3.zero;
            }

            return new Vector3(
                (map.Width - 1) * MapManager.Instance.TileSize * 0.5f,
                0f,
                (map.Depth - 1) * MapManager.Instance.TileSize * 0.5f);
        }

        private void EnsurePlayer()
        {
            if (player != null)
            {
                return;
            }

            GameObject playerObject = GameObject.Find("WorldPlayer");
            if (playerObject == null)
            {
                playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerObject.name = "WorldPlayer";
                playerObject.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                RemoveCollider(playerObject);
                SetMaterial(playerObject, new Color(0.18f, 0.35f, 0.85f));
            }
            else
            {
                RemoveCollider(playerObject);
            }

            player = playerObject.transform;
            player.position = FindPlayerStartPosition();
            playerDestination = player.position;
        }

        private Vector3 FindPlayerStartPosition()
        {
            if (MapManager.Instance.CurrentMap != null &&
                MapManager.Instance.CurrentMap.SpawnPoints != null &&
                MapManager.Instance.CurrentMap.SpawnPoints.Count > 0)
            {
                return MapManager.Instance.GetTileWorldPosition(MapManager.Instance.CurrentMap.SpawnPoints[0]) + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
            }

            return CalculateMapCenter() + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
        }

        private void UpdatePlayer()
        {
            if (player == null || !hasPlayerDestination)
            {
                return;
            }

            Vector3 current = player.position;
            Vector3 next = Vector3.MoveTowards(current, playerDestination, PlayerMoveSpeed * Time.deltaTime);
            player.position = next;
            if ((next - playerDestination).sqrMagnitude <= 0.0025f)
            {
                hasPlayerDestination = false;
            }
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ShowSeedPanel()
        {
            HideSeedPanel();

            Transform parent = UIManager.Instance.transform.Find("UICanvasRoot/Layer_Popup");
            if (parent == null)
            {
                parent = UIManager.Instance.transform;
            }

            seedPanel = new GameObject("WorldSeedPanel");
            seedPanel.transform.SetParent(parent, false);

            RectTransform rect = seedPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(520f, 96f);

            Image background = seedPanel.AddComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.10f, 0.88f);

            HorizontalLayoutGroup layout = seedPanel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (KeyValuePair<int, WorldCropDefinition> pair in FarmManager.Instance.Crops)
            {
                CreateSeedButton(seedPanel.transform, pair.Value);
            }
        }

        private void CreateSeedButton(Transform parent, WorldCropDefinition crop)
        {
            GameObject buttonObject = new GameObject($"SeedButton_{crop.Name}");
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = crop.CropColor;

            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                PlantSelectedFarmArea(crop.Id);
                HideSeedPanel();
            });

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = crop.Name;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void HideSeedPanel()
        {
            if (seedPanel == null)
            {
                return;
            }

            Destroy(seedPanel);
            seedPanel = null;
        }

        private void PlantSelectedFarmArea(int cropId)
        {
            FarmManager.Instance.TryPlant(selectedFarm, cropId);
        }

        private void EnsureWorldBuildPanel()
        {
            if (!HasMainBase())
            {
                HideBuildPanel();
                selectedBuildingId = 0;
                return;
            }

            if (buildPanel != null)
            {
                return;
            }

            Transform parent = UIManager.Instance.transform.Find("UICanvasRoot/Layer_Panel");
            if (parent == null)
            {
                parent = UIManager.Instance.transform;
            }

            buildPanel = new GameObject("WorldBuildPanel");
            buildPanel.transform.SetParent(parent, false);

            RectTransform rect = buildPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(360f, 324f);

            Image background = buildPanel.AddComponent<Image>();
            background.color = new Color(0.06f, 0.07f, 0.08f, 0.86f);

            VerticalLayoutGroup layout = buildPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateBuildPanelStaticRows();
            RefreshBuildPanel(true);
        }

        private void CreateBuildPanelStaticRows()
        {
            CreateBuildPanelText("Title", "Buildings", 20, TextAnchor.MiddleLeft, 28f);
            selectedBuildingText = CreateBuildPanelText("Selected", string.Empty, 16, TextAnchor.MiddleLeft, 24f);

            GameObject cancelButton = CreateBuildButton("CancelBuild", "Cancel Build", true, () =>
            {
                ClearSelectedBuilding();
            });
            LayoutElement cancelLayout = cancelButton.GetComponent<LayoutElement>();
            if (cancelLayout != null)
            {
                cancelLayout.preferredHeight = 34f;
            }
        }

        private Text CreateBuildPanelText(string name, string content, int fontSize, TextAnchor alignment, float height)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(buildPanel.transform, false);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;

            return text;
        }

        private void UpdateWorldBuildPanel()
        {
            if (buildPanel == null || Time.unscaledTime < nextBuildPanelRefreshTime)
            {
                return;
            }

            RefreshBuildPanel(false);
        }

        private void RefreshBuildPanel(bool force)
        {
            if (buildPanel == null || (!force && Time.unscaledTime < nextBuildPanelRefreshTime))
            {
                return;
            }

            nextBuildPanelRefreshTime = Time.unscaledTime + 0.5f;

            for (int i = buildPanel.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = buildPanel.transform.GetChild(i);
                if (child == null || child.name == "Title" || child.name == "Selected" || child.name == "CancelBuild")
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            if (selectedBuildingText != null)
            {
                selectedBuildingText.text = selectedBuildingId > 0 ? $"Selected: {GetBuildingName(selectedBuildingId)}" : "Selected: None";
            }

            IReadOnlyDictionary<int, WorldBuildingConfig> configs = DataManager.Instance.WorldBuilding?.GetAll();
            if (configs == null)
            {
                return;
            }

            List<WorldBuildingConfig> buildableConfigs = new List<WorldBuildingConfig>();
            foreach (KeyValuePair<int, WorldBuildingConfig> pair in configs)
            {
                WorldBuildingConfig config = pair.Value;
                if (config == null || !config.Enable || ShouldHideFromBuildPanel(config))
                {
                    continue;
                }

                buildableConfigs.Add(config);
            }

            buildableConfigs.Sort((left, right) => left.Id.CompareTo(right.Id));
            for (int i = 0; i < buildableConfigs.Count; i++)
            {
                CreateBuildingConfigButton(buildableConfigs[i]);
            }
        }

        private void CreateBuildingConfigButton(WorldBuildingConfig config)
        {
            bool unlocked = WorldBuildingManager.Instance.IsBuildingUnlocked(config.Id);
            bool hasCost = HasBuildCost(config.Id, out string costText);
            bool interactable = unlocked && hasCost;
            string label = $"{config.Name}  {costText}";
            if (!unlocked)
            {
                label = $"{config.Name}  Locked";
            }
            else if (!hasCost)
            {
                label = $"{config.Name}  Need {costText}";
            }

            GameObject buttonObject = CreateBuildButton($"Build_{config.Id}", label, interactable, () =>
            {
                selectedBuildingId = config.Id;
                HideSeedPanel();
                RefreshBuildPanel(true);
            });

            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                if (selectedBuildingId == config.Id)
                {
                    image.color = new Color(0.18f, 0.38f, 0.72f, 0.95f);
                }
                else if (!unlocked)
                {
                    image.color = new Color(0.18f, 0.18f, 0.18f, 0.82f);
                }
                else if (!hasCost)
                {
                    image.color = new Color(0.30f, 0.22f, 0.18f, 0.88f);
                }
                else
                {
                    image.color = new Color(0.18f, 0.24f, 0.28f, 0.9f);
                }
            }
        }

        private GameObject CreateBuildButton(string name, string label, bool interactable, UnityEngine.Events.UnityAction clicked)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(buildPanel.transform, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.28f, 0.9f);

            Button button = buttonObject.AddComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(clicked);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 38f;

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = interactable ? Color.white : new Color(0.62f, 0.62f, 0.62f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return buttonObject;
        }

        private bool HasBuildCost(int buildingId, out string costText)
        {
            costText = "Free";
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                costText = "Config";
                return false;
            }

            IReadOnlyList<WorldItem> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            if (levelConfig.BuildCostGroupId <= 0 || costs.Count == 0)
            {
                return true;
            }

            costText = FormatCosts(costs);
            return WorldItemManager.Instance.HasItems(costs);
        }

        private string FormatCosts(IReadOnlyList<WorldItem> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return "Free";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {cost.Count}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "Free";
        }

        private string GetItemName(int itemId)
        {
            if (DataManager.Instance.Item != null && DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) && config != null && !string.IsNullOrWhiteSpace(config.Name))
            {
                return config.Name;
            }

            return itemId.ToString();
        }

        private string GetBuildingName(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding != null && DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config) && config != null)
            {
                return config.Name;
            }

            return buildingId.ToString();
        }

        private static bool ShouldHideFromBuildPanel(WorldBuildingConfig config)
        {
            WorldBuildingType buildingType = (WorldBuildingType)config.BuildingType;
            return buildingType == WorldBuildingType.MainBase ||
                   buildingType == WorldBuildingType.FarmPlot ||
                   buildingType == WorldBuildingType.Mine;
        }

        public void SelectBuilding(int buildingId)
        {
            selectedBuildingId = buildingId;
            ClearBuildingPreview();
            HideSeedPanel();
            RefreshBuildPanel(true);
            WorldMainPanel.Instance?.RefreshNow();
        }

        public void ClearSelectedBuilding()
        {
            selectedBuildingId = 0;
            ClearBuildingPreview();
            RefreshBuildPanel(true);
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void HideBuildPanel()
        {
            if (buildPanel == null)
            {
                return;
            }

            Destroy(buildPanel);
            buildPanel = null;
            selectedBuildingText = null;
        }

        private void EnsureWorldHud()
        {
            if (worldHud != null)
            {
                return;
            }

            Transform parent = UIManager.Instance.transform.Find("UICanvasRoot/Layer_Panel");
            if (parent == null)
            {
                parent = UIManager.Instance.transform;
            }

            worldHud = new GameObject("WorldHud");
            worldHud.transform.SetParent(parent, false);

            RectTransform rect = worldHud.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(430f, 174f);

            Image background = worldHud.AddComponent<Image>();
            background.color = new Color(0.06f, 0.07f, 0.08f, 0.82f);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(worldHud.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);

            worldHudText = textObject.AddComponent<Text>();
            worldHudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            worldHudText.fontSize = 18;
            worldHudText.alignment = TextAnchor.UpperLeft;
            worldHudText.color = Color.white;
            worldHudText.horizontalOverflow = HorizontalWrapMode.Wrap;
            worldHudText.verticalOverflow = VerticalWrapMode.Overflow;
            UpdateWorldHud(true);
        }

        private void UpdateWorldHud(bool force = false)
        {
            if (worldHudText == null)
            {
                return;
            }

            if (!force && Time.unscaledTime < nextHudRefreshTime)
            {
                return;
            }

            nextHudRefreshTime = Time.unscaledTime + 0.25f;
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            worldHudText.text =
                $"World Map {mapId}\n" +
                $"Base: {(HasMainBase() ? "Built" : "Left click a tile to build")}\n" +
                $"Wood {WorldItemManager.Instance.GetCount(ItemIds.Wood)}   Stone {WorldItemManager.Instance.GetCount(ItemIds.Stone)}   Food {WorldItemManager.Instance.GetCount(ItemIds.Food)}\n" +
                $"Copper {WorldItemManager.Instance.GetCount(ItemIds.CopperOre)}   Iron {WorldItemManager.Instance.GetCount(ItemIds.IronOre)}\n" +
                $"Wheat {WorldItemManager.Instance.GetCount(ItemIds.Wheat)}   Tomato {WorldItemManager.Instance.GetCount(ItemIds.Tomato)}   Herb {WorldItemManager.Instance.GetCount(ItemIds.Herb)}   Flower {WorldItemManager.Instance.GetCount(ItemIds.Flower)}\n" +
                $"Build: {(selectedBuildingId > 0 ? GetBuildingName(selectedBuildingId) : "None")}\n" +
                "LMB select/build/farm   RMB move   WASD camera   Wheel height";
        }

        private void HideWorldHud()
        {
            if (worldHud == null)
            {
                return;
            }

            Destroy(worldHud);
            worldHud = null;
            worldHudText = null;
        }

        private static void SetMaterial(GameObject instance, Color color)
        {
            Renderer renderer = instance != null ? instance.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }

            Material material = new Material(FindRuntimeColorShader());
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            renderer.sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject instance)
        {
            Collider collider = instance != null ? instance.GetComponent<Collider>() : null;
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private static void RemoveColliders(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null)
                {
                    Destroy(colliders[i]);
                }
            }
        }

        private static Shader FindRuntimeColorShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Sprites/Default");
        }
    }
}
