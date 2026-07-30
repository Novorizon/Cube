using Game.Framework;
using System;
using UnityEngine;

namespace Game
{
    public enum CameraFollowMode
    {
        FollowPlayer,
        Free,
    }

    public sealed class CameraController
    {
        private const float MinHeight = 7f;
        private const float MaxHeight = 24f;
        private const float MoveSpeed = 8f;
        private const float FollowSpeed = 8f;
        private const float ZoomSpeed = 0.025f;

        private readonly Func<Vector3?> targetPositionProvider;
        private Camera mainCamera;
        private Vector3 pivot;
        private float height = MinHeight;
        private CameraFollowMode followMode = CameraFollowMode.FollowPlayer;

        public CameraController(Func<Vector3?> targetPositionProvider)
        {
            this.targetPositionProvider = targetPositionProvider;
        }

        public Camera MainCamera => mainCamera;
        public CameraFollowMode FollowMode => followMode;
        public Vector3 Pivot => pivot;

        public void Ensure()
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
            height = MinHeight;
            pivot = CalculateMapCenter();
            ApplyTransform();
        }

        public void Tick()
        {
            Ensure();
            if (mainCamera == null)
            {
                return;
            }

            UpdateHeight();
            Vector3? targetPosition = targetPositionProvider?.Invoke();
            if (followMode == CameraFollowMode.FollowPlayer && targetPosition.HasValue)
            {
                Vector3 targetPivot = new Vector3(targetPosition.Value.x, 0f, targetPosition.Value.z);
                pivot = Vector3.Lerp(pivot, targetPivot, Mathf.Clamp01(FollowSpeed * Time.deltaTime));
            }
            else
            {
                UpdateFreeMove();
            }

            ApplyTransform();
        }

        public void SetFollowMode(CameraFollowMode mode)
        {
            followMode = mode;
        }

        public void ToggleFollowMode()
        {
            followMode = followMode == CameraFollowMode.FollowPlayer
                ? CameraFollowMode.Free
                : CameraFollowMode.FollowPlayer;
        }

        public void SnapToTarget()
        {
            Ensure();
            Vector3? targetPosition = targetPositionProvider?.Invoke();
            if (mainCamera == null || followMode != CameraFollowMode.FollowPlayer || !targetPosition.HasValue)
            {
                return;
            }

            pivot = new Vector3(targetPosition.Value.x, 0f, targetPosition.Value.z);
            ApplyTransform();
        }

        public void FocusWorldPosition(Vector3 worldPosition)
        {
            Ensure();
            if (mainCamera == null)
            {
                return;
            }

            followMode = CameraFollowMode.Free;
            pivot = new Vector3(worldPosition.x, 0f, worldPosition.z);
            ApplyTransform();
        }

        private void UpdateFreeMove()
        {
            Vector2 move = GameInputManager.Instance.WorldMove;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            pivot += (right * move.x + forward * move.y) * (MoveSpeed * Time.deltaTime);
        }

        private void UpdateHeight()
        {
            float scroll = GameInputManager.Instance.Scroll.y;
            if (Mathf.Abs(scroll) > 0.01f && !WorldPointerPicker.IsPointerOverUi())
            {
                height = Mathf.Clamp(height - scroll * ZoomSpeed, MinHeight, MaxHeight);
            }
        }

        private void ApplyTransform()
        {
            if (mainCamera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(60f, 45f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            float distance = height / Mathf.Max(0.1f, -forward.y);
            mainCamera.transform.rotation = rotation;
            mainCamera.transform.position = pivot - forward * distance;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 1000f;
        }

        private static Vector3 CalculateMapCenter()
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
    }
}
