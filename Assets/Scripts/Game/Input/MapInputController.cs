using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class MapInputController : MonoBehaviour
    {
        [SerializeField]
        private bool enableInput = true;

        [SerializeField]
        private float rotateSpeed = 0.2f;

        [SerializeField]
        private float minDragDelta = 0.01f;

        private TileView selectedTile;

        private void OnEnable()
        {
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

            if (GameInputManager.Instance.CurrentMode != InputMode.Build)
            {
                return;
            }

            if (IsPointerOverUI())
            {
                return;
            }

            HandleCameraDrag();
            HandleCameraRotate();
            HandleCameraZoom();
        }

        private void HandleCameraDrag()
        {
            // 临时用 Remove 按住拖动地图。
            // 如果你后面新增 PanCamera action，把这里换成 BuildPanHeld。
            if (!GameInputManager.Instance.BuildRemoveHeld)
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

        private void HandleCameraRotate()
        {
            if (!GameInputManager.Instance.BuildRotateHeld)
            {
                return;
            }

            Vector2 delta = GameInputManager.Instance.PointerDelta;

            if (Mathf.Abs(delta.x) < minDragDelta)
            {
                return;
            }

            CameraManager.Instance.RotateAroundFocus(delta.x * rotateSpeed);
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
            selectedTile = tileView;

            Debug.Log($"Select tile: {selectedTile.Coord}, Type: {selectedTile.Data.Type}");

            // 下一步：
            // 1. 显示选中框
            // 2. 如果当前选择了塔，则尝试放塔
            // 3. 如果没选择塔，则显示地块信息
        }

        private void ClearSelection()
        {
            if (selectedTile == null)
            {
                return;
            }

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
    }
}