using Game.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class TowerBuildInputController : MonoSingleton<TowerBuildInputController>
    {
        private Camera mainCamera;
        private bool initialized;
        private bool inputRegistered;

        public void Initialize()
        {
            base.Initialize();
            mainCamera = Camera.main;
            initialized = true;
            RegisterInput();
        }

        private void OnEnable()
        {
            RegisterInput();
        }

        private void OnDisable()
        {
            UnregisterInput();
        }

        public bool RefreshPreviewAtCurrentPointer()
        {
            if (!initialized)
            {
                return false;
            }

            if (!TowerBuildManager.Instance.HasSelectedTower)
            {
                return false;
            }

            return RefreshPreview(WorldPointerPicker.CurrentPointerPosition);
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (!TowerBuildManager.Instance.HasSelectedTower)
            {
                return;
            }

            Vector2 screenPosition = WorldPointerPicker.CurrentPointerPosition;
            RefreshPreview(screenPosition);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TowerBuildManager.Instance.CancelSelect();
            }
        }

        private void RegisterInput()
        {
            if (inputRegistered || !GameInputManager.IsCreated)
            {
                return;
            }

            GameInputManager.Instance.BuildPlacePerformed += OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed += OnBuildCancelPerformed;
            inputRegistered = true;
        }

        private void UnregisterInput()
        {
            if (!inputRegistered || !GameInputManager.IsCreated)
            {
                return;
            }

            GameInputManager.Instance.BuildPlacePerformed -= OnBuildPlacePerformed;
            GameInputManager.Instance.BuildCancelPerformed -= OnBuildCancelPerformed;
            inputRegistered = false;
        }

        private void OnBuildPlacePerformed(InputAction.CallbackContext context)
        {
            if (context.canceled || !CanHandleBuildInput())
            {
                return;
            }

            if (IsPointerOverUI())
            {
                return;
            }

            RefreshPreview(WorldPointerPicker.CurrentPointerPosition);
            TowerBuildManager.Instance.TryBuildPreviewTower();
        }

        private void OnBuildCancelPerformed(InputAction.CallbackContext context)
        {
            if (context.canceled || !initialized)
            {
                return;
            }

            if (TowerBuildManager.Instance.HasSelectedTower)
            {
                TowerBuildManager.Instance.CancelSelect();
            }
        }

        private bool CanHandleBuildInput()
        {
            return initialized && TowerBuildManager.Instance.HasSelectedTower;
        }

        private bool RefreshPreview(Vector2 screenPosition)
        {
            TileView tileView = PickTile(screenPosition);
            TowerBuildManager.Instance.UpdatePreview(tileView);
            return tileView != null;
        }

        private TileView PickTile(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                return null;
            }

            bool picked = WorldPointerPicker.TryPickTile(screenPosition, mainCamera, out TileView tileView, false);

            if (!picked)
            {
                return null;
            }

            return tileView;
        }

        private bool IsPointerOverUI()
        {
            return WorldPointerPicker.IsPointerOverUi();
        }
    }
}
