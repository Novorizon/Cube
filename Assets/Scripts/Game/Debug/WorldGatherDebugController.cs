using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WorldGatherDebugController : MonoBehaviour
    {
        [SerializeField]
        private KeyCode gatherKey = KeyCode.G;

        private void Update()
        {
            if (Input.GetKeyDown(gatherKey))
            {
                TryGatherAtPointer();
            }
        }

        private void TryGatherAtPointer()
        {
            if (!TryPickPointerTile(out TileView tileView))
            {
                return;
            }

            if (!TryGetFirstResourceObject(tileView.Coord, out MapObjectData resourceObject))
            {
                Debug.LogWarning($"WorldGatherDebugController gather failed. No resource at coord: {tileView.Coord}");
                return;
            }

            if (!WorldGatherManager.Instance.TryGather(resourceObject, out IReadOnlyList<WorldItem> rewards))
            {
                if (WorldGatherManager.Instance.TryGetStatus(resourceObject, out WorldGatherStatus status))
                {
                    Debug.LogWarning($"WorldGatherDebugController gather failed. objectId: {status.ObjectId}, remaining: {status.RemainingTimes}, availableAt: {status.AvailableAtUnixTime}");
                }
                else
                {
                    Debug.LogWarning($"WorldGatherDebugController gather failed. objectId: {resourceObject.ObjectId}");
                }

                return;
            }

            Debug.Log($"WorldGatherDebugController gather success. coord: {tileView.Coord}, rewards: {FormatRewards(rewards)}");
            WorldResourceView.RefreshAtCoord(tileView.Coord);
        }

        private static bool TryGetFirstResourceObject(Vector3Int coord, out MapObjectData resourceObject)
        {
            resourceObject = null;

            if (!MapManager.Instance.TryGetMapObjectsAt(coord, out IReadOnlyList<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject != null && mapObject.ObjectType == MapObjectType.Resource)
                {
                    resourceObject = mapObject;
                    return true;
                }
            }

            return false;
        }

        private static string FormatRewards(IReadOnlyList<WorldItem> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return "None";
            }

            List<string> parts = new List<string>(rewards.Count);
            for (int i = 0; i < rewards.Count; i++)
            {
                WorldItem item = rewards[i];
                parts.Add($"{item.ItemId} x{item.Count}");
            }

            return string.Join(", ", parts);
        }

        private static bool TryPickPointerTile(out TileView tileView)
        {
            tileView = null;

            if (WorldPointerPicker.IsPointerOverUi())
            {
                return false;
            }

            Camera camera = CameraManager.Instance.MainCamera != null ? CameraManager.Instance.MainCamera : Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("WorldGatherDebugController pick failed. Main camera is null.");
                return false;
            }

            bool picked = WorldPointerPicker.TryPickTile(WorldPointerPicker.CurrentPointerPosition, camera, out tileView, false);
            if (!picked)
            {
                Debug.LogWarning("WorldGatherDebugController pick failed. Tile not found.");
                return false;
            }

            return true;
        }
    }
}
