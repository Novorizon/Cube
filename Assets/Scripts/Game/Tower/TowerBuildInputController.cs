using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class TowerBuildInputController : MonoSingleton<TowerBuildInputController>
    {
        private Camera mainCamera;
        private bool initialized;

        public void Initialize()
        {
            base.Initialize();
            mainCamera = Camera.main;
            initialized = true;
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

            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            return RefreshPreview(screenPosition);
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

            if (Mouse.current == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            bool pointerOverUi = IsPointerOverUI();

            RefreshPreview(screenPosition);

            if (!pointerOverUi && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TowerBuildManager.Instance.TryBuildPreviewTower();
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TowerBuildManager.Instance.CancelSelect();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                TowerBuildManager.Instance.CancelSelect();
            }
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

            bool picked = MapManager.Instance.TryPickTile(screenPosition, mainCamera, out TileView tileView);

            if (!picked)
            {
                return null;
            }

            return tileView;
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
