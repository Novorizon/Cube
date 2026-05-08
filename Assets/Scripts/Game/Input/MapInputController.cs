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
        private float rotateSpeed = 0.2f;

        [SerializeField]
        private float minDragDelta = 0.01f;

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
            // 防止这个物体比 GameEntry 更早 Enable，导致 OnEnable 时 GameInputManager 还没初始化。
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

                //判断是否可建造
                //显示选中框
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
    }
}