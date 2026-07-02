using Game.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    public enum CameraFollowMode
    {
        FollowPlayer,
        Free,
    }

    public sealed class WorldGameplayController : MonoBehaviour
    {
        private enum PendingInteractionType
        {
            None,
            Resource,
        }

        public static WorldGameplayController Instance { get; private set; }

        private const float CameraMinHeight = 7f;
        private const float CameraMaxHeight = 24f;
        private const float CameraMoveSpeed = 8f;
        private const float CameraFollowSpeed = 8f;
        private const float CameraZoomSpeed = 0.025f;
        private const float PlayerMoveSpeed = 2f;
        private const float PlayerTurnSpeed = 540f;
        private const float InteractionDistance = 1.35f;
        private const float ResourceGatherActionSeconds = 0.95f;
        private const float ResourcePickupActionSeconds = 0.45f;
        private const float ResourceMineActionSeconds = 0.95f;
        private const float DragThresholdPixels = 12f;
        private const int MoveTargetSearchRadius = 6;
        private const string PlacementValidMaterialPath = "Assets/Arts/Map/Buildings/Materials/Placement_Valid.mat";
        private const string PlacementInvalidMaterialPath = "Assets/Arts/Map/Buildings/Materials/Placement_Invalid.mat";

        private readonly WorldRewardResolver rewardResolver = new WorldRewardResolver(DataManager.Instance.WorldReward);
        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);
        private readonly MapPathFinder playerPathFinder = new MapPathFinder();
        private readonly List<Vector3Int> playerPath = new List<Vector3Int>();
        private readonly List<Vector3Int> smoothedPlayerPath = new List<Vector3Int>();

        [SerializeField]
        private bool usePathSmoothing = true;

        private Camera mainCamera;
        private Transform player;
        private WorldPlayerView playerView;
        private Vector3 playerDestination;
        private bool hasPlayerDestination;
        private int playerPathIndex;
        private PendingInteractionType pendingInteractionType;
        private WorldResourceView pendingResourceView;
        private Vector3 pendingInteractionPosition;
        private float pendingInteractionDistance;
        private bool activeResourceInteraction;
        private bool waitingForResourceActionEnd;
        private WorldResourceView activeResourceView;
        private WorldResourceConfig activeResourceConfig;
        private WorldResourceInteractionType activeResourceInteractionType;
        private Vector3 activeResourcePosition;
        private float activeResourceDistance;
        private float resourceActionEndTime;
        private Vector3 cameraPivot;
        private float cameraHeight = CameraMinHeight;
        private CameraFollowMode cameraFollowMode = CameraFollowMode.FollowPlayer;

        private bool leftPointerActive;
        private bool leftPressOverUi;
        private Vector2 leftPressScreenPosition;
        private Vector3Int leftPressCoord;
        private bool leftPressHasTile;

        private Farm selectedFarm;
        private WorldBuilding selectedWorldBuilding;
        private int selectedBuildingId;
        private GameObject buildingPreview;
        private int previewBuildingId;
        private Material previewValidMaterial;
        private Material previewInvalidMaterial;
        private bool missingPreviewPrefabLogged;
        private bool missingPreviewValidMaterialLogged;
        private bool missingPreviewInvalidMaterialLogged;
        private bool farmAreaMode;
        private GameObject farmAreaPreviewRoot;
        private GameObject farmAreaPreviewPrefab;
        private readonly List<GameObject> farmAreaPreviewViews = new List<GameObject>();
        private bool missingFarmAreaPreviewPrefabLogged;

        public int SelectedBuildingId => selectedBuildingId;
        public bool IsFarmAreaMode => farmAreaMode;
        public Farm SelectedFarm => selectedFarm;
        public WorldBuilding SelectedWorldBuilding => selectedWorldBuilding;
        public CameraFollowMode CurrentCameraFollowMode => cameraFollowMode;
        public bool UsePathSmoothing => usePathSmoothing;

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
            ClearBuildingPreview();
            ClearFarmAreaPreview();
            StopActiveResourceInteraction();
        }

        private void Update()
        {
            if (MapManager.Instance.CurrentMap == null)
            {
                return;
            }

            EnsureCamera();
            EnsurePlayer();
            UpdateCamera();
            UpdatePlayer();
            UpdateActiveResourceInteraction();
            UpdateBuildingPreview();
            UpdateFarmAreaPreview();
            UpdateLeftPointer();
        }

        private void OnWorldSelectPerformed(InputAction.CallbackContext context)
        {
            BeginLeftPointer();
        }

        private void OnWorldAttackCommandPerformed(InputAction.CallbackContext context)
        {
            StartPointerInteractionOrMove();
        }

        private void OnWorldCancelPerformed(InputAction.CallbackContext context)
        {
            if (IsMouseRightButton(context))
            {
                if (TryCancelCurrentMode())
                {
                    return;
                }

                StartPointerInteractionOrMove();
                return;
            }

            if (pendingInteractionType != PendingInteractionType.None)
            {
                ClearPendingInteraction();
                StopPlayerMovement();
                playerView?.SetMoveSpeed(0f);
                return;
            }

            if (activeResourceInteraction)
            {
                StopActiveResourceInteraction();
                StopPlayerMovement();
                playerView?.SetMoveSpeed(0f);
                return;
            }

            TryCancelCurrentMode();
        }

        private bool TryCancelCurrentMode()
        {
            bool hasModeToCancel = selectedBuildingId > 0 ||
                                   farmAreaMode ||
                                   selectedFarm != null ||
                                   selectedWorldBuilding != null;
            if (!hasModeToCancel)
            {
                return false;
            }

            HideSeedPanel();
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            selectedBuildingId = 0;
            ClearSelectedWorldObject();
            SetFarmAreaMode(false);
            ClearBuildingPreview();
            WorldMainPanel.Instance?.RefreshNow();
            return true;
        }

        private void StartPointerInteractionOrMove()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            if (TryStartPointerInteraction())
            {
                return;
            }

            MovePlayerToPointer();
        }

        private void MovePlayerToPointer()
        {
            if (IsPointerOverUi())
            {
                return;
            }

            if (!TryPickTileCoord(out Vector3Int coord))
            {
                Debug.Log("Move player failed. No tile picked.");
                return;
            }

            if (!TryMovePlayerToTile(coord))
            {
                Debug.Log($"Move player failed. No reachable path. destination: {coord}");
                return;
            }

            ClearPendingInteraction();
            StopActiveResourceInteraction();
            StorageManager.Instance.MarkDirty();
        }

        private bool TryMovePlayerToTile(Vector3Int destinationCoord)
        {
            if (!TryGetPlayerPathStartCoord(out Vector3Int startCoord))
            {
                return false;
            }

            if (!TryResolveReachablePath(startCoord, destinationCoord, out List<Vector3Int> path) ||
                path == null ||
                path.Count == 0)
            {
                return false;
            }

            SetPlayerPath(path);
            return true;
        }

        private bool TryGetPlayerPathStartCoord(out Vector3Int coord)
        {
            coord = default;
            if (!TryGetPlayerTileCoord(out Vector3Int currentCoord))
            {
                return false;
            }

            if (MapManager.Instance.IsWalkable(currentCoord))
            {
                coord = currentCoord;
                return true;
            }

            return TryFindNearestWalkableCoord(currentCoord, MoveTargetSearchRadius, out coord);
        }

        private bool TryResolveReachablePath(Vector3Int startCoord, Vector3Int targetCoord, out List<Vector3Int> path)
        {
            path = null;
            if (MapManager.Instance.IsWalkable(targetCoord) &&
                playerPathFinder.TryFindPath(startCoord, targetCoord, out path) &&
                path != null &&
                path.Count > 0)
            {
                return true;
            }

            return TryFindNearestReachablePath(startCoord, targetCoord, MoveTargetSearchRadius, out path);
        }

        private bool TryFindNearestReachablePath(Vector3Int startCoord, Vector3Int origin, int maxRadius, out List<Vector3Int> path)
        {
            path = null;
            maxRadius = Mathf.Max(0, maxRadius);

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                bool found = false;
                List<Vector3Int> bestPath = null;
                int bestDistance = int.MaxValue;

                if (radius == 0)
                {
                    TryConsiderReachablePath(startCoord, origin, 0, 0, ref found, ref bestPath, ref bestDistance);
                }
                else
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        TryConsiderReachablePath(startCoord, origin, dx, -radius, ref found, ref bestPath, ref bestDistance);
                        TryConsiderReachablePath(startCoord, origin, dx, radius, ref found, ref bestPath, ref bestDistance);
                    }

                    for (int dz = -radius + 1; dz <= radius - 1; dz++)
                    {
                        TryConsiderReachablePath(startCoord, origin, -radius, dz, ref found, ref bestPath, ref bestDistance);
                        TryConsiderReachablePath(startCoord, origin, radius, dz, ref found, ref bestPath, ref bestDistance);
                    }
                }

                if (found)
                {
                    path = bestPath;
                    return true;
                }
            }

            return false;
        }

        private void TryConsiderReachablePath(Vector3Int startCoord, Vector3Int origin, int offsetX, int offsetZ, ref bool found, ref List<Vector3Int> bestPath, ref int bestDistance)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(origin.x + offsetX, origin.z + offsetZ, out TileData tileData) ||
                tileData == null ||
                !MapManager.Instance.IsWalkable(tileData.Coord))
            {
                return;
            }

            int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetZ);
            if (found && distance >= bestDistance)
            {
                return;
            }

            if (!playerPathFinder.TryFindPath(startCoord, tileData.Coord, out List<Vector3Int> candidatePath) ||
                candidatePath == null ||
                candidatePath.Count == 0)
            {
                return;
            }

            found = true;
            bestPath = candidatePath;
            bestDistance = distance;
        }

        private bool TryFindNearestWalkableCoord(Vector3Int origin, int maxRadius, out Vector3Int result)
        {
            result = default;
            maxRadius = Mathf.Max(0, maxRadius);

            if (MapManager.Instance.IsWalkable(origin))
            {
                result = origin;
                return true;
            }

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool found = false;
                Vector3Int best = default;
                int bestDistance = int.MaxValue;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    TryConsiderWalkableCoord(origin, dx, -radius, ref found, ref best, ref bestDistance);
                    TryConsiderWalkableCoord(origin, dx, radius, ref found, ref best, ref bestDistance);
                }

                for (int dz = -radius + 1; dz <= radius - 1; dz++)
                {
                    TryConsiderWalkableCoord(origin, -radius, dz, ref found, ref best, ref bestDistance);
                    TryConsiderWalkableCoord(origin, radius, dz, ref found, ref best, ref bestDistance);
                }

                if (found)
                {
                    result = best;
                    return true;
                }
            }

            return false;
        }

        private static void TryConsiderWalkableCoord(Vector3Int origin, int offsetX, int offsetZ, ref bool found, ref Vector3Int best, ref int bestDistance)
        {
            if (!MapManager.Instance.TryGetTopLogicTile(origin.x + offsetX, origin.z + offsetZ, out TileData tileData) ||
                tileData == null)
            {
                return;
            }

            Vector3Int coord = tileData.Coord;
            if (!MapManager.Instance.IsWalkable(coord))
            {
                return;
            }

            int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetZ);
            if (found && distance >= bestDistance)
            {
                return;
            }

            found = true;
            best = coord;
            bestDistance = distance;
        }

        private bool TryStartPointerInteraction()
        {
            if (TryPickResource(out WorldResourceView resourceView) && resourceView != null)
            {
                return StartResourceInteraction(resourceView);
            }

            return false;
        }

        private bool StartResourceInteraction(WorldResourceView resourceView)
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

            StartPendingInteraction(
                PendingInteractionType.Resource,
                resourceView,
                GetResourceInteractionPosition(resourceView, mapObject, player != null ? player.position : resourceView.transform.position),
                InteractionDistance);
            return true;
        }

        private void StartPendingInteraction(
            PendingInteractionType interactionType,
            WorldResourceView resourceView,
            Vector3 targetPosition,
            float interactionDistance)
        {
            EnsurePlayer();
            pendingInteractionType = interactionType;
            pendingResourceView = resourceView;
            pendingInteractionPosition = targetPosition;
            pendingInteractionDistance = Mathf.Max(0.1f, interactionDistance);
            StopActiveResourceInteraction();

            if (IsPlayerInInteractionRange())
            {
                hasPlayerDestination = false;
                playerView?.SetMoveSpeed(0f);
                TryExecutePendingInteraction();
                return;
            }

            if (!MovePlayerNearInteractionTarget())
            {
                Debug.Log($"World interaction move failed. No reachable path. target: {GetCurrentPendingInteractionPosition()}");
            }
        }

        private bool MovePlayerNearInteractionTarget()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 target = GetCurrentPendingInteractionPosition();
            if (TryGetTopLogicCoordFromWorld(target, out Vector3Int targetCoord) &&
                TryMovePlayerToTile(targetCoord))
            {
                return true;
            }

            if (pendingResourceView != null &&
                pendingResourceView.MapObject != null &&
                TryMovePlayerToTile(pendingResourceView.MapObject.Coord))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetTopLogicCoordFromWorld(Vector3 worldPosition, out Vector3Int coord)
        {
            coord = default;
            float tileSize = Mathf.Max(0.01f, MapManager.Instance.TileSize);
            int x = Mathf.FloorToInt(worldPosition.x / tileSize + 0.5f);
            int z = Mathf.FloorToInt(worldPosition.z / tileSize + 0.5f);
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) || tileData == null)
            {
                return false;
            }

            coord = tileData.Coord;
            return true;
        }

        private bool IsPlayerInInteractionRange()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 playerPosition = player.position;
            Vector3 target = GetCurrentPendingInteractionPosition();
            Vector2 playerFlat = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 targetFlat = new Vector2(target.x, target.z);
            return Vector2.Distance(playerFlat, targetFlat) <= pendingInteractionDistance;
        }

        private void TryExecutePendingInteraction()
        {
            if (pendingInteractionType == PendingInteractionType.None || !IsPlayerInInteractionRange())
            {
                return;
            }

            FaceInteractionTarget();

            PendingInteractionType interactionType = pendingInteractionType;
            WorldResourceView resourceView = pendingResourceView;
            ClearPendingInteraction();

            switch (interactionType)
            {
                case PendingInteractionType.Resource:
                    BeginResourceInteraction(resourceView);
                    break;
            }
        }

        private bool BeginResourceInteraction(WorldResourceView resourceView)
        {
            StopActiveResourceInteraction(false);
            if (resourceView == null || resourceView.MapObject == null)
            {
                return false;
            }

            MapObjectData mapObject = resourceView.MapObject;
            if (!DataManager.Instance.WorldResource.TryGet(mapObject.ConfigId, out WorldResourceConfig config) || config == null || !config.Enable)
            {
                return false;
            }

            activeResourceInteraction = true;
            waitingForResourceActionEnd = false;
            activeResourceView = resourceView;
            activeResourceConfig = config;
            activeResourceInteractionType = (WorldResourceInteractionType)config.InteractionType;
            activeResourcePosition = GetResourceInteractionPosition(resourceView, mapObject, player != null ? player.position : resourceView.transform.position);
            activeResourceDistance = InteractionDistance;

            return StartNextResourceAction();
        }

        private void UpdateActiveResourceInteraction()
        {
            if (!activeResourceInteraction)
            {
                return;
            }

            if (!IsActiveResourceTargetValid() || !IsPlayerInActiveResourceRange())
            {
                StopActiveResourceInteraction();
                return;
            }

            FaceActiveResourceTarget();
            if (!waitingForResourceActionEnd)
            {
                StartNextResourceAction();
                return;
            }

            if (Time.time >= resourceActionEndTime)
            {
                CompleteResourceAction();
            }
        }

        private bool StartNextResourceAction()
        {
            if (!activeResourceInteraction || !IsActiveResourceTargetValid() || !IsPlayerInActiveResourceRange())
            {
                StopActiveResourceInteraction();
                return false;
            }

            FaceActiveResourceTarget();
            switch (activeResourceInteractionType)
            {
                case WorldResourceInteractionType.Pickup:
                    playerView?.PlayPickUp();
                    resourceActionEndTime = Time.time + ResourcePickupActionSeconds;
                    break;

                case WorldResourceInteractionType.Gather:
                    if (!CanContinueGather(activeResourceView))
                    {
                        StopActiveResourceInteraction();
                        return false;
                    }

                    if (!TryPlayGatherAction(activeResourceConfig))
                    {
                        StopActiveResourceInteraction();
                        return false;
                    }

                    resourceActionEndTime = Time.time + ResourceGatherActionSeconds;
                    break;

                case WorldResourceInteractionType.MineTarget:
                    if (!ToolKitManager.Instance.TryUseToolForAction(ToolKitActionType.BuildMine, out _))
                    {
                        StopActiveResourceInteraction();
                        return false;
                    }

                    playerView?.PlayToolAction(ToolKitActionType.BuildMine);
                    resourceActionEndTime = Time.time + ResourceMineActionSeconds;
                    break;

                default:
                    StopActiveResourceInteraction();
                    return false;
            }

            waitingForResourceActionEnd = true;
            return true;
        }

        private void CompleteResourceAction()
        {
            if (!activeResourceInteraction || !IsActiveResourceTargetValid() || !IsPlayerInActiveResourceRange())
            {
                StopActiveResourceInteraction();
                return;
            }

            WorldResourceView resourceView = activeResourceView;
            WorldResourceConfig config = activeResourceConfig;
            WorldResourceInteractionType interactionType = activeResourceInteractionType;
            bool success = CompleteResourceActionOnce(resourceView, config, interactionType);
            if (!success)
            {
                StopActiveResourceInteraction();
                return;
            }

            WorldMainPanel.Instance?.RefreshNow();
            if (interactionType != WorldResourceInteractionType.Gather || !CanContinueGather(resourceView))
            {
                StopActiveResourceInteraction(interactionType != WorldResourceInteractionType.Gather);
                return;
            }

            waitingForResourceActionEnd = false;
        }

        private bool CompleteResourceActionOnce(
            WorldResourceView resourceView,
            WorldResourceConfig config,
            WorldResourceInteractionType interactionType)
        {
            if (resourceView == null || resourceView.MapObject == null || config == null)
            {
                return false;
            }

            MapObjectData mapObject = resourceView.MapObject;
            switch (interactionType)
            {
                case WorldResourceInteractionType.Pickup:
                    return PickupResource(resourceView, config);

                case WorldResourceInteractionType.Gather:
                    if (!CanUseToolForGather(config))
                    {
                        return false;
                    }

                    if (!WorldGatherManager.Instance.TryGather(mapObject, out _))
                    {
                        return false;
                    }

                    resourceView.RefreshNow();
                    return true;

                case WorldResourceInteractionType.MineTarget:
                    if (!ToolKitManager.Instance.TryUseToolForAction(ToolKitActionType.BuildMine, out _))
                    {
                        return false;
                    }

                    return MineManager.Instance.TryBuildMine(resourceView, config);

                default:
                    return false;
            }
        }

        private bool CanContinueGather(WorldResourceView resourceView)
        {
            if (resourceView == null || resourceView.MapObject == null)
            {
                return false;
            }

            if (!WorldGatherManager.Instance.TryGetStatus(resourceView.MapObject, out WorldGatherStatus status))
            {
                return false;
            }

            return status.CanGather;
        }

        private bool TryPlayGatherAction(WorldResourceConfig config)
        {
            ToolKitActionType actionType = GetGatherToolAction(config);
            if (!ToolKitManager.Instance.TryUseToolForAction(actionType, out _))
            {
                return false;
            }

            playerView?.PlayToolAction(actionType);
            return true;
        }

        private bool CanUseToolForGather(WorldResourceConfig config)
        {
            ToolKitActionType actionType = GetGatherToolAction(config);
            return ToolKitManager.Instance.TryUseToolForAction(actionType, out _);
        }

        private static ToolKitActionType GetGatherToolAction(WorldResourceConfig config)
        {
            if (config == null)
            {
                return ToolKitActionType.None;
            }

            WorldResourceCategory category = (WorldResourceCategory)config.ResourceType;
            return ActionToolResolver.GetGatherAction(category);
        }

        private bool IsActiveResourceTargetValid()
        {
            return activeResourceView != null &&
                   activeResourceView.MapObject != null &&
                   activeResourceConfig != null;
        }

        private bool IsPlayerInActiveResourceRange()
        {
            if (player == null)
            {
                return false;
            }

            Vector2 playerFlat = new Vector2(player.position.x, player.position.z);
            Vector3 targetPosition = GetCurrentActiveResourcePosition();
            Vector2 targetFlat = new Vector2(targetPosition.x, targetPosition.z);
            return Vector2.Distance(playerFlat, targetFlat) <= activeResourceDistance;
        }

        private void FaceActiveResourceTarget()
        {
            RotatePlayerToward(GetCurrentActiveResourcePosition() - player.position);
        }

        public void InterruptCurrentInteraction()
        {
            ClearPendingInteraction();
            StopActiveResourceInteraction();
            StopPlayerMovement();
            playerView?.SetMoveSpeed(0f);
        }

        private void StopActiveResourceInteraction(bool hideTool = true)
        {
            activeResourceInteraction = false;
            waitingForResourceActionEnd = false;
            bool wasPlayingResourceAction = activeResourceView != null;
            activeResourceView = null;
            activeResourceConfig = null;
            activeResourceInteractionType = default;
            activeResourcePosition = Vector3.zero;
            activeResourceDistance = 0f;
            resourceActionEndTime = 0f;
            if (wasPlayingResourceAction)
            {
                playerView?.CancelActionPlayback(hasPlayerDestination);
                return;
            }

            if (hideTool)
            {
                playerView?.HideTool();
            }
        }

        private void FaceInteractionTarget()
        {
            RotatePlayerToward(GetCurrentPendingInteractionPosition() - player.position);
        }

        private void RotatePlayerToward(Vector3 direction)
        {
            if (player == null)
            {
                return;
            }

            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            player.rotation = Quaternion.RotateTowards(
                player.rotation,
                targetRotation,
                PlayerTurnSpeed * Time.deltaTime);
        }

        private void ClearPendingInteraction()
        {
            pendingInteractionType = PendingInteractionType.None;
            pendingResourceView = null;
            pendingInteractionPosition = Vector3.zero;
            pendingInteractionDistance = 0f;
        }

        private Vector3 GetCurrentPendingInteractionPosition()
        {
            if (pendingInteractionType == PendingInteractionType.Resource &&
                pendingResourceView != null &&
                pendingResourceView.MapObject != null &&
                player != null)
            {
                return GetResourceInteractionPosition(pendingResourceView, pendingResourceView.MapObject, player.position);
            }

            return pendingInteractionPosition;
        }

        private Vector3 GetCurrentActiveResourcePosition()
        {
            if (activeResourceView != null &&
                activeResourceView.MapObject != null &&
                player != null)
            {
                return GetResourceInteractionPosition(activeResourceView, activeResourceView.MapObject, player.position);
            }

            return activeResourcePosition;
        }

        private static Vector3 GetResourceInteractionPosition(WorldResourceView resourceView, MapObjectData mapObject, Vector3 fromPosition)
        {
            if (resourceView != null && resourceView.TryGetClosestPoint(fromPosition, out Vector3 closestPoint))
            {
                return closestPoint;
            }

            return GetMapObjectWorldPosition(mapObject, resourceView != null ? resourceView.transform.position : fromPosition);
        }

        private static Vector3 GetMapObjectWorldPosition(MapObjectData mapObject, Vector3 fallback)
        {
            if (mapObject == null)
            {
                return fallback;
            }

            Vector3 tilePosition = MapManager.Instance.GetTileWorldPosition(mapObject.Coord);
            Vector3 localPosition = mapObject.LocalPosition;
            return tilePosition + new Vector3(localPosition.x, 0f, localPosition.z);
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
            leftPressHasTile = TryPickTileCoord(out leftPressCoord);
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
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            ClearSelectedWorldObject();
            ClearPendingInteraction();
            StopActiveResourceInteraction();

            if (farmAreaMode)
            {
                return;
            }

            if (selectedBuildingId > 0)
            {
                if (TryPickTileCoord(out Vector3Int buildCoord))
                {
                    TryBuildSelectedBuilding(buildCoord);
                }

                return;
            }

            if (!TryPickTileCoord(out Vector3Int coord))
            {
                return;
            }

            if (FarmManager.Instance.TryGetFarmAt(coord, out Farm farm))
            {
                SelectFarmForInteraction(farm);
                return;
            }

            if (TrySelectBuildingAt(coord))
            {
                WorldMainPanel.Instance?.ShowBuildingDetailPanel(selectedWorldBuilding);
                WorldMainPanel.Instance?.RefreshNow();
            }
        }

        private void SelectFarmForInteraction(Farm farm)
        {
            if (farm == null)
            {
                return;
            }

            HideSeedPanel();
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            ClearSelectedWorldObject();
            selectedFarm = farm;
            ShowSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void SelectBuildingForInteraction(WorldBuilding building)
        {
            if (building == null)
            {
                return;
            }

            HideSeedPanel();
            WorldMainPanel.Instance?.HideBuildingDetailPanel();
            ClearSelectedWorldObject();
            selectedWorldBuilding = building;
            WorldMainPanel.Instance?.ShowBuildingDetailPanel(selectedWorldBuilding);
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void CompleteFarmDrag()
        {
            if (!farmAreaMode)
            {
                return;
            }

            if (!leftPressHasTile || !TryPickTileCoord(out Vector3Int endCoord))
            {
                return;
            }

            if (!HasHouse())
            {
                return;
            }

            if (selectedBuildingId > 0)
            {
                return;
            }

            if (!ToolKitManager.Instance.TryUseToolForAction(ToolKitActionType.CultivateFarm, out _))
            {
                return;
            }

            playerView?.PlayToolAction(ToolKitActionType.CultivateFarm);
            selectedFarm = FarmManager.Instance.CreateFarmArea(leftPressCoord, endCoord);
            if (selectedFarm != null)
            {
                SetFarmAreaMode(false);
                ShowSeedPanel();
            }
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

            bool canPlace = CanPreviewPlaceSelectedBuilding(coord);
            if (!DataManager.Instance.WorldBuilding.TryGet(selectedBuildingId, out WorldBuildingConfig config) || config == null)
            {
                SetBuildingPreviewVisible(false);
                return;
            }

            int sizeX = WorldBuildingFootprint.GetSizeX(config);
            int sizeZ = WorldBuildingFootprint.GetSizeZ(config);
            buildingPreview.transform.position = WorldBuildingFootprint.GetCenterWorldPosition(coord, sizeX, sizeZ, MapManager.Instance.TileSize) + Vector3.up * MapManager.Instance.TileSize;
            SetBuildingPreviewVisible(true);
            ApplyPreviewMaterial(canPlace);
        }

        private bool CanPreviewPlaceSelectedBuilding(Vector3Int coord)
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

            return MapManager.Instance.CanPlaceMapObject(
                coord,
                WorldBuildingFootprint.GetSizeX(config),
                WorldBuildingFootprint.GetSizeZ(config));
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
                config == null)
            {
                LogMissingPreviewPrefab(config);
                return;
            }

            string prefabLocation = WorldBuildingManager.GetPrefabLocation(config);
            if (string.IsNullOrWhiteSpace(prefabLocation))
            {
                LogMissingPreviewPrefab(config);
                return;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(prefabLocation);
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

            string location = config != null ? WorldBuildingManager.GetPrefabLocation(config) : string.Empty;
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
            if (material == null)
            {
                SetBuildingPreviewVisible(false);
                return;
            }

            ApplyMaterial(buildingPreview, material);
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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
                previewValidMaterial = LoadPreviewMaterial(PlacementValidMaterialPath, "valid", ref missingPreviewValidMaterialLogged);
            }

            return previewValidMaterial;
        }

        private Material GetPreviewInvalidMaterial()
        {
            if (previewInvalidMaterial == null)
            {
                previewInvalidMaterial = LoadPreviewMaterial(PlacementInvalidMaterialPath, "invalid", ref missingPreviewInvalidMaterialLogged);
            }

            return previewInvalidMaterial;
        }

        private static Material LoadPreviewMaterial(string path, string label, ref bool missingLogged)
        {
            Material material = ResourceManager.Instance.LoadAsset<Material>(path);
            if (material == null && !missingLogged)
            {
                Debug.LogError($"Missing world placement {label} material: {path}");
                missingLogged = true;
            }
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

        private void UpdateFarmAreaPreview()
        {
            if (!farmAreaMode || selectedBuildingId > 0 || IsPointerOverUi() || !HasHouse())
            {
                HideFarmAreaPreview();
                return;
            }

            if (!TryPickTileCoord(out Vector3Int currentCoord))
            {
                HideFarmAreaPreview();
                return;
            }

            Vector3Int startCoord = currentCoord;
            if (leftPointerActive && GameInputManager.Instance.WorldSelectHeld && !leftPressOverUi && leftPressHasTile)
            {
                startCoord = leftPressCoord;
            }

            ShowFarmAreaPreview(startCoord, currentCoord);
        }

        private void ShowFarmAreaPreview(Vector3Int a, Vector3Int b)
        {
            EnsureFarmAreaPreviewRoot();
            EnsureFarmAreaPreviewPrefab();
            Material validMaterial = GetPreviewValidMaterial();
            Material invalidMaterial = GetPreviewInvalidMaterial();
            if (farmAreaPreviewRoot == null || farmAreaPreviewPrefab == null || validMaterial == null || invalidMaterial == null)
            {
                HideFarmAreaPreview();
                return;
            }

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.z, b.z);
            int maxZ = Mathf.Max(a.z, b.z);
            int y = a.y;
            int neededCount = Mathf.Max(1, (maxX - minX + 1) * (maxZ - minZ + 1));
            EnsureFarmAreaPreviewViewCount(neededCount);

            int index = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    GameObject view = farmAreaPreviewViews[index++];
                    view.SetActive(true);
                    view.transform.position = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.03f);
                    view.transform.rotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one * MapManager.Instance.TileSize;
                    ApplyMaterial(view, MapManager.Instance.CanPlaceMapObject(coord) ? validMaterial : invalidMaterial);
                }
            }

            for (int i = index; i < farmAreaPreviewViews.Count; i++)
            {
                if (farmAreaPreviewViews[i] != null)
                {
                    farmAreaPreviewViews[i].SetActive(false);
                }
            }
        }

        private void EnsureFarmAreaPreviewRoot()
        {
            if (farmAreaPreviewRoot != null)
            {
                return;
            }

            farmAreaPreviewRoot = new GameObject("WorldFarmAreaPreview");
        }

        private void EnsureFarmAreaPreviewPrefab()
        {
            if (farmAreaPreviewPrefab != null || missingFarmAreaPreviewPrefabLogged)
            {
                return;
            }

            farmAreaPreviewPrefab = ResourceManager.Instance.LoadGameObject(FarmManager.FarmPlotPrefabPath);
            if (farmAreaPreviewPrefab == null)
            {
                Debug.LogError($"Missing farm area preview prefab: {FarmManager.FarmPlotPrefabPath}");
                missingFarmAreaPreviewPrefabLogged = true;
            }
        }

        private void EnsureFarmAreaPreviewViewCount(int count)
        {
            while (farmAreaPreviewViews.Count < count)
            {
                GameObject view = GameObject.Instantiate(farmAreaPreviewPrefab, farmAreaPreviewRoot.transform);
                view.name = $"FarmAreaPreview_{farmAreaPreviewViews.Count}";
                RemoveColliders(view);
                farmAreaPreviewViews.Add(view);
            }
        }

        private void HideFarmAreaPreview()
        {
            for (int i = 0; i < farmAreaPreviewViews.Count; i++)
            {
                if (farmAreaPreviewViews[i] != null)
                {
                    farmAreaPreviewViews[i].SetActive(false);
                }
            }
        }

        private void ClearFarmAreaPreview()
        {
            if (farmAreaPreviewRoot != null)
            {
                Destroy(farmAreaPreviewRoot);
            }

            farmAreaPreviewRoot = null;
            farmAreaPreviewPrefab = null;
            farmAreaPreviewViews.Clear();
            missingFarmAreaPreviewPrefabLogged = false;
        }

        private bool PickupResource(WorldResourceView resourceView, WorldResourceConfig config)
        {
            IReadOnlyList<WorldItem> rewards = rewardResolver.GetRewardGroup(config.PickupRewardGroupId);
            if (rewards.Count == 0)
            {
                return false;
            }

            if (!BagManager.Instance.TryAddItems(rewards))
            {
                return false;
            }

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

        private bool HasHouse()
        {
            return WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House);
        }

        private bool TryPickTile(out TileView tileView)
        {
            tileView = null;
            EnsureCamera();
            return WorldPointerPicker.TryPickTile(GameInputManager.Instance.PointerPosition, mainCamera, out tileView, false);
        }

        private bool TryPickTileCoord(out Vector3Int coord)
        {
            coord = default;
            EnsureCamera();
            return WorldPointerPicker.TryPickTileCoord(GameInputManager.Instance.PointerPosition, mainCamera, out coord, false);
        }

        private bool TryPickResource(out WorldResourceView resourceView)
        {
            resourceView = null;
            EnsureCamera();
            if (mainCamera == null)
            {
                return false;
            }

            return WorldPointerPicker.TryPickComponent(GameInputManager.Instance.PointerPosition, mainCamera, out resourceView, false);
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

            UpdateCameraHeight();
            if (cameraFollowMode == CameraFollowMode.FollowPlayer && TryGetCameraTargetPosition(out Vector3 targetPosition))
            {
                Vector3 targetPivot = new Vector3(targetPosition.x, 0f, targetPosition.z);
                cameraPivot = Vector3.Lerp(cameraPivot, targetPivot, Mathf.Clamp01(CameraFollowSpeed * Time.deltaTime));
            }
            else
            {
                UpdateFreeCameraMove();
            }

            ApplyCameraTransform();
        }

        private void UpdateFreeCameraMove()
        {
            Vector2 move = GameInputManager.Instance.WorldMove;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            cameraPivot += (right * move.x + forward * move.y) * (CameraMoveSpeed * Time.deltaTime);
        }

        private void UpdateCameraHeight()
        {
            float scroll = GameInputManager.Instance.Scroll.y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
            {
                cameraHeight = Mathf.Clamp(cameraHeight - scroll * CameraZoomSpeed, CameraMinHeight, CameraMaxHeight);
            }
        }

        private bool TryGetCameraTargetPosition(out Vector3 position)
        {
            if (playerView != null)
            {
                position = playerView.CameraTargetPosition;
                return true;
            }

            if (player != null)
            {
                position = player.position;
                return true;
            }

            position = default;
            return false;
        }

        public void SetCameraFollowMode(CameraFollowMode mode)
        {
            cameraFollowMode = mode;
        }

        public void SetPathSmoothingEnabled(bool enabled)
        {
            usePathSmoothing = enabled;
        }

        public void TogglePathSmoothing()
        {
            usePathSmoothing = !usePathSmoothing;
        }

        public void ToggleCameraFollowMode()
        {
            cameraFollowMode = cameraFollowMode == CameraFollowMode.FollowPlayer
                ? CameraFollowMode.Free
                : CameraFollowMode.FollowPlayer;
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
                GameObject prefab = ResourceManager.Instance.LoadGameObject(WorldPlayerView.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Missing world player prefab: {WorldPlayerView.PrefabPath}");
                    return;
                }

                playerObject = GameObject.Instantiate(prefab);
                playerObject.name = "WorldPlayer";
            }

            player = playerObject.transform;
            playerView = playerObject.GetComponent<WorldPlayerView>();
            if (playerView == null)
            {
                playerView = playerObject.AddComponent<WorldPlayerView>();
            }

            ApplyPlayerStartTransform();
            ClearPlayerPath();
            playerDestination = player.position;
            playerView.SetMoveSpeed(0f);
            if (cameraFollowMode == CameraFollowMode.FollowPlayer && TryGetCameraTargetPosition(out Vector3 cameraTargetPosition))
            {
                cameraPivot = new Vector3(cameraTargetPosition.x, 0f, cameraTargetPosition.z);
                ApplyCameraTransform();
            }
        }

        public SavePlayerData CreatePlayerSaveData()
        {
            if (player == null || MapManager.Instance.CurrentMap == null)
            {
                return null;
            }

            Vector3 position = player.position;
            return new SavePlayerData
            {
                MapId = MapManager.Instance.CurrentMap.Id,
                X = position.x,
                Y = position.y,
                Z = position.z,
                RotationY = player.eulerAngles.y,
            };
        }

        private void ApplyPlayerStartTransform()
        {
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (StorageManager.Instance.TryGetPlayerSaveData(mapId, out SavePlayerData savedPlayer) && savedPlayer != null)
            {
                player.position = new Vector3(savedPlayer.X, savedPlayer.Y, savedPlayer.Z);
                player.rotation = Quaternion.Euler(0f, savedPlayer.RotationY, 0f);
                return;
            }

            player.position = FindPlayerStartPosition();
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
            if (player == null)
            {
                return;
            }

            if (!hasPlayerDestination)
            {
                playerView?.SetMoveSpeed(0f);
                return;
            }

            Vector3 current = player.position;
            Vector3 next = Vector3.MoveTowards(current, playerDestination, PlayerMoveSpeed * Time.deltaTime);
            Vector3 direction = playerDestination - current;
            RotatePlayerToward(direction);

            player.position = next;
            playerView?.SetMoveSpeed(PlayerMoveSpeed);
            if ((next - playerDestination).sqrMagnitude <= 0.0025f)
            {
                player.position = playerDestination;
                if (TryAdvancePlayerPath())
                {
                    return;
                }

                hasPlayerDestination = false;
                playerView?.SetMoveSpeed(0f);
                StorageManager.Instance.MarkDirty();
                TryExecutePendingInteraction();
            }
        }

        private bool TryGetPlayerTileCoord(out Vector3Int coord)
        {
            coord = default;
            if (player == null)
            {
                return false;
            }

            float tileSize = Mathf.Max(0.01f, MapManager.Instance.TileSize);
            int x = Mathf.FloorToInt(player.position.x / tileSize + 0.5f);
            int z = Mathf.FloorToInt(player.position.z / tileSize + 0.5f);
            if (!MapManager.Instance.TryGetTopLogicTile(x, z, out TileData tileData) || tileData == null)
            {
                return false;
            }

            coord = tileData.Coord;
            return true;
        }

        private void SetPlayerPath(IReadOnlyList<Vector3Int> path)
        {
            playerPath.Clear();
            if (path == null || path.Count == 0)
            {
                StopPlayerMovement();
                return;
            }

            IReadOnlyList<Vector3Int> movementPath = path;
            if (usePathSmoothing && path.Count > 2)
            {
                MapPathSmoother.SmoothBySupercoverLineOfSight(path, smoothedPlayerPath);
                if (smoothedPlayerPath.Count > 0)
                {
                    movementPath = smoothedPlayerPath;
                }
            }

            for (int i = 0; i < movementPath.Count; i++)
            {
                playerPath.Add(movementPath[i]);
            }

            playerPathIndex = playerPath.Count > 1 ? 1 : 0;
            SetPlayerDestination(playerPath[playerPathIndex]);
        }

        private bool TryAdvancePlayerPath()
        {
            if (playerPath.Count == 0 || playerPathIndex >= playerPath.Count - 1)
            {
                ClearPlayerPath();
                return false;
            }

            playerPathIndex++;
            SetPlayerDestination(playerPath[playerPathIndex]);
            return true;
        }

        private void SetPlayerDestination(Vector3Int coord)
        {
            playerDestination = MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * (MapManager.Instance.TileSize * 1.12f);
            hasPlayerDestination = true;
        }

        private void StopPlayerMovement()
        {
            hasPlayerDestination = false;
            ClearPlayerPath();
        }

        private void ClearPlayerPath()
        {
            playerPath.Clear();
            smoothedPlayerPath.Clear();
            playerPathIndex = 0;
        }

        private bool IsPointerOverUi()
        {
            return WorldPointerPicker.IsPointerOverUi();
        }

        private void ShowSeedPanel()
        {
            WorldMainPanel.Instance?.ShowFarmPanel(selectedFarm);
        }

        private void HideSeedPanel()
        {
            WorldMainPanel.Instance?.HideFarmPanel();
        }

        public bool TryPlantSelectedFarm(int cropId)
        {
            bool success = FarmManager.Instance.TryPlant(selectedFarm, cropId);
            if (success)
            {
                WorldMainPanel.Instance?.RefreshNow();
            }

            return success;
        }

        private bool TrySelectBuildingAt(Vector3Int coord)
        {
            if (!TryGetBuildingAt(coord, out WorldBuilding building))
            {
                return false;
            }

            selectedWorldBuilding = building;
            return true;
        }

        private bool TryGetBuildingAt(Vector3Int coord, out WorldBuilding building)
        {
            building = null;
            if (!MapManager.Instance.TryGetMapObjectsAt(coord, out IReadOnlyList<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject == null || mapObject.ObjectType != MapObjectType.Building)
                {
                    continue;
                }

                if (WorldBuildingManager.Instance.TryGetBuilding(mapObject.ObjectId, out WorldBuilding foundBuilding) && foundBuilding != null)
                {
                    building = foundBuilding;
                    return true;
                }
            }

            return false;
        }

        private bool HasBuildCost(int buildingId, out string costText)
        {
            costText = LocalizationManager.Get("ui.common.free");
            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                costText = LocalizationManager.Get("ui.common.config");
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
                return LocalizationManager.Get("ui.common.free");
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

            return parts.Count > 0 ? string.Join(", ", parts) : LocalizationManager.Get("ui.common.free");
        }

        private string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private string GetBuildingName(int buildingId)
        {
            return LocalizedConfigText.BuildingName(buildingId);
        }

        public void SelectBuilding(int buildingId)
        {
            selectedBuildingId = buildingId;
            ClearSelectedWorldObject();
            SetFarmAreaMode(false);
            ClearBuildingPreview();
            HideSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
        }

        public void SelectFarmAreaMode()
        {
            selectedBuildingId = 0;
            ClearSelectedWorldObject();
            ClearBuildingPreview();
            SetFarmAreaMode(true);
            HideSeedPanel();
            WorldMainPanel.Instance?.RefreshNow();
        }

        public void ClearSelectedBuilding()
        {
            selectedBuildingId = 0;
            ClearSelectedWorldObject();
            SetFarmAreaMode(false);
            ClearBuildingPreview();
            WorldMainPanel.Instance?.RefreshNow();
        }

        private void ClearSelectedWorldObject()
        {
            selectedFarm = null;
            selectedWorldBuilding = null;
        }

        private void SetFarmAreaMode(bool enabled)
        {
            if (farmAreaMode == enabled)
            {
                return;
            }

            farmAreaMode = enabled;
            if (!farmAreaMode)
            {
                HideFarmAreaPreview();
            }
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
