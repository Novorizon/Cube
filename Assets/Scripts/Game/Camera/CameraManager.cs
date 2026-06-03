using Game.Framework;
using UnityEngine;

namespace Game
{
    public class CameraManager : Singleton<CameraManager>
    {
        private Camera mainCamera;

        private bool initialized;

        private float tileSize = 1f;

        private float pitch = 55f;
        private float yaw = 45f;

        private float padding = 2f;
        private float minOrthographicSize = 5f;
        private float maxOrthographicSize = 60f;

        private Vector3 currentFocus;
        private bool hasFocus;

        public bool Initialized
        {
            get
            {
                return initialized;
            }
        }

        public Camera MainCamera
        {
            get
            {
                return mainCamera;
            }
        }

        public bool Initialize()
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("CameraManager initialize failed. Main Camera not found.");
                initialized = false;
                return false;
            }

            mainCamera.orthographic = true;
            initialized = true;

            return true;
        }

        public void FocusCurrentMap()
        {
            MapData mapData = MapManager.Instance.CurrentMap;

            if (mapData == null)
            {
                Debug.LogWarning("FocusCurrentMap failed. CurrentMap is null.");
                return;
            }

            FocusMap(mapData, MapManager.Instance.TileSize);
        }

        public void FocusMap(MapData mapData, float mapTileSize)
        {
            if (!initialized)
            {
                bool success = Initialize();

                if (!success)
                {
                    return;
                }
            }

            if (mapData == null)
            {
                Debug.LogWarning("FocusMap failed. MapData is null.");
                return;
            }

            tileSize = Mathf.Max(0.01f, mapTileSize);

            Vector3 center = CalculateMapCenter(mapData);
            currentFocus = center;
            hasFocus = true;

            ApplyRotation();

            float mapWidth = Mathf.Max(1, mapData.Width) * tileSize;
            float mapDepth = Mathf.Max(1, mapData.Depth) * tileSize;
            float mapHeight = Mathf.Max(1, mapData.Height) * tileSize;

            float mapDiagonal = Mathf.Sqrt(mapWidth * mapWidth + mapDepth * mapDepth);
            float orthographicSize = mapDiagonal * 0.55f + padding;
            orthographicSize = Mathf.Max(minOrthographicSize, orthographicSize);

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = Mathf.Clamp(orthographicSize, minOrthographicSize, maxOrthographicSize);

            float distance = mapDiagonal + mapHeight + 10f;
            mainCamera.transform.position = currentFocus - mainCamera.transform.forward * distance;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = distance + mapDiagonal + mapHeight + 50f;

            Debug.Log($"Camera focus map success. Center: {center}, OrthoSize: {mainCamera.orthographicSize}");
        }

        private Vector3 CalculateMapCenter(MapData mapData)
        {
            float centerX = (mapData.Width - 1) * tileSize * 0.5f;
            float centerY = (mapData.Height - 1) * tileSize * 0.5f;
            float centerZ = (mapData.Depth - 1) * tileSize * 0.5f;

            return new Vector3(centerX, centerY, centerZ);
        }

        public void SetViewAngle(float newPitch, float newYaw)
        {
            pitch = newPitch;
            yaw = newYaw;

            if (initialized && mainCamera != null)
            {
                ApplyRotation();

                if (hasFocus)
                {
                    RotateAroundFocus(0f);
                }
            }
        }

        public void SetPadding(float newPadding)
        {
            padding = Mathf.Max(0f, newPadding);
        }

        public void PanByScreenDelta(Vector2 screenDelta)
        {
            if (!EnsureCamera())
            {
                return;
            }

            if (!mainCamera.orthographic)
            {
                return;
            }

            float pixelsToWorld = mainCamera.orthographicSize * 2f / Mathf.Max(1, Screen.height);

            Vector3 right = mainCamera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = mainCamera.transform.up;
            }

            forward.Normalize();

            Vector3 move = -right * screenDelta.x * pixelsToWorld - forward * screenDelta.y * pixelsToWorld;

            mainCamera.transform.position += move;

            if (hasFocus)
            {
                currentFocus += move;
            }
        }

        public void PanByWorldDirection(Vector2 direction, float distance)
        {
            if (!EnsureCamera())
            {
                return;
            }

            if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
            {
                return;
            }

            Vector3 right = mainCamera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(mainCamera.transform.up, Vector3.up);
            }

            right.y = 0f;
            right.Normalize();
            forward.Normalize();

            Vector3 move = (right * direction.x + forward * direction.y) * distance;
            mainCamera.transform.position += move;

            if (hasFocus)
            {
                currentFocus += move;
            }
        }

        public void RotateAroundFocus(float deltaYaw)
        {
            if (!EnsureCamera())
            {
                return;
            }

            yaw += deltaYaw;
            ApplyRotation();

            if (!hasFocus)
            {
                currentFocus = mainCamera.transform.position + mainCamera.transform.forward * 10f;
                hasFocus = true;
            }

            float distance = Vector3.Distance(mainCamera.transform.position, currentFocus);

            if (distance < 0.01f)
            {
                distance = 10f;
            }

            mainCamera.transform.position = currentFocus - mainCamera.transform.forward * distance;
        }

        public void Zoom(float scrollDelta)
        {
            if (!EnsureCamera())
            {
                return;
            }

            if (!mainCamera.orthographic)
            {
                return;
            }

            float zoomSpeed = 0.02f;
            float size = mainCamera.orthographicSize - scrollDelta * zoomSpeed;

            mainCamera.orthographicSize = Mathf.Clamp(size, minOrthographicSize, maxOrthographicSize);
        }

        private void ApplyRotation()
        {
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private bool EnsureCamera()
        {
            if (initialized && mainCamera != null)
            {
                return true;
            }

            return Initialize();
        }
    }
}
