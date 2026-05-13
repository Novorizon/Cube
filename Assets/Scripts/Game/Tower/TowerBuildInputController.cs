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
            mainCamera = Camera.main;
            initialized = true;
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

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (IsPointerOverUI())
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            TryBuildAtScreenPosition(screenPosition);
        }

        private void TryBuildAtScreenPosition(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Debug.LogWarning("Build failed. Main camera is null.");
                return;
            }

            bool picked = MapManager.Instance.TryPickTile(screenPosition, mainCamera, out TileView tileView);

            if (!picked)
            {
                return;
            }

            bool success = TowerBuildManager.Instance.TryBuildSelectedTower(tileView);

            if (!success)
            {
                return;
            }

            // Current design: after selecting one tower type, user can keep building that type.
            // If you want to build only once, uncomment:
            // TowerBuildManager.Instance.CancelSelect();
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
