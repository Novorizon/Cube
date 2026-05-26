using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class MapInputController : MonoSingleton<MapInputController>
    {
        [SerializeField]
        private bool enableInput = true;

        [SerializeField]
        private float minDragDelta = 0.01f;

        [SerializeField]
        private float keyboardPanSpeed = 8f;

        private TileView selectedTile;

        private void OnEnable()
        {
            if (!GameInputManager.IsCreated)
            {
                return;
            }

            GameInputManager.Instance.BuildPlacePerformed += OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed += OnBuildCancelPerformed;
        }

        private void Start()
        {
            // This object can wake before GameEntry finishes input initialization.
            if (!GameInputManager.IsCreated)
            {
                return;
            }

            GameInputManager.Instance.BuildPlacePerformed -= OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed -= OnBuildCancelPerformed;

            GameInputManager.Instance.BuildPlacePerformed += OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed += OnBuildCancelPerformed;
        }

        private void OnDisable()
        {
            if (!GameInputManager.IsCreated)
            {
                return;
            }

            GameInputManager.Instance.BuildPlacePerformed -= OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed -= OnBuildCancelPerformed;
        }

        private void Update()
        {
            if (!enableInput)
            {
                return;
            }

            if (!GameInputManager.IsCreated)
            {
                return;
            }

            if (!IsCameraInputMode())
            {
                return;
            }

            HandleKeyboardCameraMove();

            if (IsPointerOverUI())
            {
                return;
            }

            HandleCameraDrag();
            HandleCameraZoom();
        }

        private void HandleKeyboardCameraMove()
        {
            Vector2 move = GetCameraMoveInput();

            if (move.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            float distance = keyboardPanSpeed * Time.unscaledDeltaTime;
            CameraManager.Instance.PanByWorldDirection(move, distance);
        }

        private Vector2 GetCameraMoveInput()
        {
            InputMode mode = GameInputManager.Instance.CurrentMode;

            if (mode == InputMode.Build)
            {
                return GameInputManager.Instance.BuildMove;
            }

            if (mode == InputMode.Battle)
            {
                return GameInputManager.Instance.BattleMove;
            }

            return Vector2.zero;
        }

        private void HandleCameraDrag()
        {
            if (!GameInputManager.Instance.BuildPlaceHeld && !GameInputManager.Instance.BuildRemoveHeld)
            {
                return;
            }

            Vector2 delta = GameInputManager.Instance.PointerDelta;

            if (delta.sqrMagnitude < minDragDelta)
            {
                return;
            }

            CameraManager.Instance.PanByScreenDelta(delta);
        }

        private void HandleCameraZoom()
        {
            Vector2 scroll = GameInputManager.Instance.Scroll;

            if (Mathf.Abs(scroll.y) < 0.01f)
            {
                return;
            }

            CameraManager.Instance.Zoom(scroll.y);
        }

        private void OnBuildPlacePerformed(InputAction.CallbackContext context)
        {
            if (!enableInput)
            {
                return;
            }

            if (!GameInputManager.IsCreated)
            {
                return;
            }

            if (GameInputManager.Instance.CurrentMode != InputMode.Build)
            {
                return;
            }

            if (IsPointerOverUI())
            {
                return;
            }

            Vector2 pointerPosition = GameInputManager.Instance.PointerPosition;
            Camera camera = CameraManager.Instance.MainCamera;

            if (!MapManager.Instance.TryPickTile(pointerPosition, camera, out TileView tileView))
            {
                ClearSelection();
                return;
            }

            SelectTile(tileView);
        }

        private void OnBuildCancelPerformed(InputAction.CallbackContext context)
        {
            ClearSelection();
        }

        private void SelectTile(TileView tileView)
        {
            if (selectedTile == tileView)
            {
                return;
            }

            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
            }

            selectedTile = tileView;

            if (selectedTile != null)
            {
                selectedTile.SetSelected(true);
                Debug.Log($"Select tile: {selectedTile.Coord}, Type: {selectedTile.Type}");
            }
        }

        private void ClearSelection()
        {
            if (selectedTile == null)
            {
                return;
            }

            selectedTile.SetSelected(false);
            Debug.Log($"Clear tile selection: {selectedTile.Coord}");
            selectedTile = null;
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsCameraInputMode()
        {
            InputMode mode = GameInputManager.Instance.CurrentMode;
            return mode == InputMode.Build || mode == InputMode.Battle;
        }
    }
}
