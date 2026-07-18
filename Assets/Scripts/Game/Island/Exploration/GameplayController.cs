using Game.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class GameplayController : MonoBehaviour
    {
        public static GameplayController Instance { get; private set; }

        [SerializeField]
        private bool usePathSmoothing = true;

        private NavigationController navigation;
        private CameraController cameraController;
        private ActionController actionController;
        private ResourceInteractionController resourceInteraction;
        private PlacementController placement;

        public int SelectedBuildingId => placement != null ? placement.SelectedBuildingId : 0;
        public bool IsFarmAreaMode => placement != null && placement.IsFarmAreaMode;
        public Farm SelectedFarm => placement?.SelectedFarm;
        public WorldBuilding SelectedBuilding => placement?.SelectedBuilding;
        public CameraFollowMode CurrentCameraFollowMode => cameraController != null
            ? cameraController.FollowMode
            : CameraFollowMode.FollowPlayer;
        public bool UsePathSmoothing => navigation != null ? navigation.UsePathSmoothing : usePathSmoothing;

        public static void Ensure()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject root = new GameObject("GameplayController");
            Instance = root.AddComponent<GameplayController>();
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
            InitializeControllers();
        }

        private void OnEnable()
        {
            InitializeControllers();
            GameInputManager.Instance.WorldSelectPerformed += OnSelectPerformed;
            GameInputManager.Instance.WorldAttackCommandPerformed += OnAttackCommandPerformed;
            GameInputManager.Instance.WorldCancelPerformed += OnCancelPerformed;
        }

        private void OnDisable()
        {
            GameInputManager.Instance.WorldSelectPerformed -= OnSelectPerformed;
            GameInputManager.Instance.WorldAttackCommandPerformed -= OnAttackCommandPerformed;
            GameInputManager.Instance.WorldCancelPerformed -= OnCancelPerformed;
            placement?.Dispose();
            resourceInteraction?.Interrupt();
            actionController?.Stop(ActionStopReason.Disabled, ActionExitMode.ToIdle);
        }

        private void Update()
        {
            if (MapManager.Instance.CurrentMap == null)
            {
                return;
            }

            InitializeControllers();
            cameraController.Ensure();
            if (navigation.Ensure())
            {
                cameraController.SnapToTarget();
            }

            cameraController.Tick();
            navigation.Tick();
            resourceInteraction.Tick();
            actionController.Tick();
            placement.Tick();
        }

        public void InterruptCurrentInteraction()
        {
            resourceInteraction?.Interrupt();
        }

        public void SetCameraFollowMode(CameraFollowMode mode)
        {
            InitializeControllers();
            cameraController.SetFollowMode(mode);
        }

        public void SetPathSmoothingEnabled(bool enabled)
        {
            usePathSmoothing = enabled;
            InitializeControllers();
            navigation.UsePathSmoothing = enabled;
        }

        public void TogglePathSmoothing()
        {
            SetPathSmoothingEnabled(!UsePathSmoothing);
        }

        public void ToggleCameraFollowMode()
        {
            InitializeControllers();
            cameraController.ToggleFollowMode();
        }

        public SavePlayerData CreatePlayerSaveData()
        {
            InitializeControllers();
            return navigation.CreateSaveData();
        }

        public bool TryPlantSelectedFarm(int cropId)
        {
            InitializeControllers();
            return placement.TryPlantSelectedFarm(cropId);
        }

        public void SelectBuilding(int buildingId)
        {
            InitializeControllers();
            placement.SelectBuilding(buildingId);
        }

        public void SelectFarmAreaMode()
        {
            InitializeControllers();
            placement.SelectFarmAreaMode();
        }

        public void ClearSelectedBuilding()
        {
            InitializeControllers();
            placement.ClearSelectedBuilding();
        }

        private void InitializeControllers()
        {
            if (navigation != null)
            {
                return;
            }

            navigation = new NavigationController(() => actionController)
            {
                UsePathSmoothing = usePathSmoothing,
            };
            cameraController = new CameraController(navigation.GetCameraTargetPosition);
            actionController = new ActionController(() => navigation.View);
            resourceInteraction = new ResourceInteractionController(navigation, actionController);
            placement = new PlacementController(cameraController, navigation, resourceInteraction, actionController);
        }

        private void OnSelectPerformed(InputAction.CallbackContext context)
        {
            placement.BeginPointer();
        }

        private void OnAttackCommandPerformed(InputAction.CallbackContext context)
        {
            StartPointerInteractionOrMove();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (IsMouseRightButton(context))
            {
                if (placement.TryCancelCurrentMode())
                {
                    return;
                }

                StartPointerInteractionOrMove();
                return;
            }

            if (resourceInteraction.IsActive)
            {
                resourceInteraction.Interrupt();
                return;
            }

            placement.TryCancelCurrentMode();
        }

        private void StartPointerInteractionOrMove()
        {
            if (WorldPointerPicker.IsPointerOverUi())
            {
                return;
            }

            cameraController.Ensure();
            navigation.Ensure();
            if (resourceInteraction.TryStartAtPointer(cameraController.MainCamera))
            {
                return;
            }

            MoveToPointer();
        }

        private void MoveToPointer()
        {
            if (!TryPickTileDestination(out Vector3Int coord, out Vector3 destination))
            {
                Debug.Log("Move player failed. No tile picked.");
                return;
            }

            if (!navigation.TryMoveTo(coord, destination))
            {
                Debug.Log($"Move player failed. No reachable path. destination: {coord}");
                return;
            }

            resourceInteraction.Cancel();
            StorageManager.Instance.MarkDirty();
        }

        private bool TryPickTileDestination(out Vector3Int coord, out Vector3 destination)
        {
            coord = default;
            destination = default;
            if (!WorldPointerPicker.TryPickTilePosition(
                    GameInputManager.Instance.PointerPosition,
                    cameraController.MainCamera,
                    out TileView tileView,
                    out Vector3 hitPosition,
                    false) ||
                tileView == null)
            {
                return false;
            }

            coord = tileView.Coord;
            destination = hitPosition;
            destination.y = NavigationController.GetStandPosition(coord).y;
            return true;
        }

        private static bool IsMouseRightButton(InputAction.CallbackContext context)
        {
            return context.control != null &&
                   context.control.device is Mouse &&
                   context.control.name == "rightButton";
        }
    }
}
