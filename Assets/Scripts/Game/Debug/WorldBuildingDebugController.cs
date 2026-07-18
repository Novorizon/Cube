using UnityEngine;

namespace Game
{
    public sealed class WorldBuildingDebugController : MonoBehaviour
    {
        [SerializeField]
        private int farmBuildingId = 30001001;

        [SerializeField]
        private int quarryBuildingId = 30002001;

        [SerializeField]
        private int storageBuildingId = 30000002;

        [SerializeField]
        private KeyCode buildFarmKey = KeyCode.F7;

        [SerializeField]
        private KeyCode buildQuarryKey = KeyCode.F8;

        [SerializeField]
        private KeyCode buildStorageKey = KeyCode.F9;

        [SerializeField]
        private KeyCode addResourceKey = KeyCode.F10;

        [SerializeField]
        private KeyCode removeBuildingKey = KeyCode.Delete;

        [SerializeField]
        private int testResourceCount = 100;

        private void Update()
        {
            if (Input.GetKeyDown(addResourceKey))
            {
                AddTestResources();
                return;
            }

            if (Input.GetKeyDown(buildFarmKey))
            {
                TryBuildAtPointer(farmBuildingId);
                return;
            }

            if (Input.GetKeyDown(buildQuarryKey))
            {
                TryBuildAtPointer(quarryBuildingId);
                return;
            }

            if (Input.GetKeyDown(buildStorageKey))
            {
                TryBuildAtPointer(storageBuildingId);
                return;
            }

            if (Input.GetKeyDown(removeBuildingKey))
            {
                TryRemoveAtPointer();
            }
        }

        private void AddTestResources()
        {
            ItemManager.Instance.AddItem(ItemIds.Wood, testResourceCount);
            ItemManager.Instance.AddItem(ItemIds.Stone, testResourceCount);
            Debug.Log($"WorldBuildingDebugController add resources. Wood +{testResourceCount}, Stone +{testResourceCount}");
        }

        private void TryBuildAtPointer(int buildingId)
        {
            if (!TryPickPointerTile(out TileView tileView))
            {
                return;
            }

            bool success = WorldBuildingManager.Instance.TryBuild(buildingId, tileView.Coord, out WorldBuilding building);
            if (!success)
            {
                Debug.LogWarning($"WorldBuildingDebugController build failed. buildingId: {buildingId}, coord: {tileView.Coord}");
                return;
            }

            Debug.Log($"WorldBuildingDebugController build success. instanceId: {building.InstanceId}, buildingId: {buildingId}, coord: {tileView.Coord}");
        }

        private void TryRemoveAtPointer()
        {
            if (!TryPickPointerTile(out TileView tileView))
            {
                return;
            }

            bool success = WorldBuildingManager.Instance.TryRemoveAt(tileView.Coord);
            if (!success)
            {
                Debug.LogWarning($"WorldBuildingDebugController remove failed. coord: {tileView.Coord}");
                return;
            }

            Debug.Log($"WorldBuildingDebugController remove success. coord: {tileView.Coord}");
        }

        private bool TryPickPointerTile(out TileView tileView)
        {
            tileView = null;

            if (WorldPointerPicker.IsPointerOverUi())
            {
                return false;
            }

            Camera camera = CameraManager.Instance.MainCamera != null ? CameraManager.Instance.MainCamera : Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("WorldBuildingDebugController pick failed. Main camera is null.");
                return false;
            }

            bool picked = WorldPointerPicker.TryPickTile(WorldPointerPicker.CurrentPointerPosition, camera, out tileView, false);
            if (!picked)
            {
                Debug.LogWarning("WorldBuildingDebugController pick failed. Tile not found.");
                return false;
            }

            return true;
        }
    }
}
