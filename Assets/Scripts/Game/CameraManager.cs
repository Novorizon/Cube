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
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            mainCamera.transform.rotation = rotation;

            float mapWidth = Mathf.Max(1, mapData.Width) * tileSize;
            float mapDepth = Mathf.Max(1, mapData.Depth) * tileSize;
            float mapHeight = Mathf.Max(1, mapData.Height) * tileSize;

            float mapDiagonal = Mathf.Sqrt(mapWidth * mapWidth + mapDepth * mapDepth);
            float orthographicSize = mapDiagonal * 0.55f + padding;
            orthographicSize = Mathf.Max(minOrthographicSize, orthographicSize);

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthographicSize;

            float distance = mapDiagonal + mapHeight + 10f;
            Vector3 position = center - mainCamera.transform.forward * distance;

            mainCamera.transform.position = position;
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

            FocusCurrentMap();
        }

        public void SetPadding(float newPadding)
        {
            padding = Mathf.Max(0f, newPadding);

            FocusCurrentMap();
        }
    }
}