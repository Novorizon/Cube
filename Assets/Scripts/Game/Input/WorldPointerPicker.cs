using Game.Framework;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace Game
{
    public enum WorldPointerHitType
    {
        None = 0,
        UI = 1,
        Object = 2,
        Tile = 3,
    }

    public struct WorldPointerHit
    {
        public WorldPointerHitType Type;
        public Component Object;
        public Collider Collider;
        public TileView Tile;
        public Vector3Int TileCoord;

        public bool IsValid
        {
            get
            {
                return Type != WorldPointerHitType.None;
            }
        }
    }

    public static class WorldPointerPicker
    {
        private const float DefaultRayDistance = 1000f;

        public static Vector2 CurrentPointerPosition
        {
            get
            {
                if (GameInputManager.IsCreated)
                {
                    return GameInputManager.Instance.PointerPosition;
                }

                if (Pointer.current != null)
                {
                    return Pointer.current.position.ReadValue();
                }

                return Input.mousePosition;
            }
        }

        public static bool IsPointerOverUi()
        {
            if (IsPointerConsumedByUiFramework())
            {
                return true;
            }

            if (EventSystem.current == null)
            {
                return false;
            }

            if (Touchscreen.current != null)
            {
                ReadOnlyArray<TouchControl> touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    TouchControl touch = touches[i];
                    if (touch == null || !touch.press.isPressed)
                    {
                        continue;
                    }

                    if (EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue()))
                    {
                        return true;
                    }
                }
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsPointerConsumedByUiFramework()
        {
            UIManager uiManager = UIManager.Current;
            return uiManager != null && uiManager.IsPointerConsumedThisFrame;
        }

        public static bool TryPick(Vector2 screenPosition, Camera camera, out WorldPointerHit hit, bool includeUi = true)
        {
            return TryPick(screenPosition, camera, out hit, includeUi, DefaultRayDistance, Physics.DefaultRaycastLayers);
        }

        public static bool TryPick(Vector2 screenPosition, Camera camera, out WorldPointerHit hit, bool includeUi, float rayDistance, int objectLayerMask)
        {
            hit = default;

            if (includeUi && IsPointerOverUi())
            {
                hit.Type = WorldPointerHitType.UI;
                return true;
            }

            if (TryPickWorldObject(screenPosition, camera, out Component worldObject, out Collider collider, rayDistance, objectLayerMask))
            {
                hit.Type = WorldPointerHitType.Object;
                hit.Object = worldObject;
                hit.Collider = collider;
                return true;
            }

            if (TryPickTile(screenPosition, camera, out TileView tileView, false))
            {
                hit.Type = WorldPointerHitType.Tile;
                hit.Tile = tileView;
                hit.TileCoord = tileView.Coord;
                return true;
            }

            return false;
        }

        public static bool TryPickTile(Camera camera, out TileView tileView, bool includeUi = true)
        {
            return TryPickTile(CurrentPointerPosition, camera, out tileView, includeUi);
        }

        public static bool TryPickTile(Vector2 screenPosition, Camera camera, out TileView tileView, bool includeUi = true)
        {
            tileView = null;

            if (includeUi && IsPointerOverUi())
            {
                return false;
            }

            if (camera == null)
            {
                return false;
            }

            return MapManager.Instance.TryPickTile(screenPosition, camera, out tileView);
        }

        public static bool TryPickTilePosition(Vector2 screenPosition, Camera camera, out TileView tileView, out Vector3 worldPosition, bool includeUi = true)
        {
            tileView = null;
            worldPosition = Vector3.zero;

            if (includeUi && IsPointerOverUi())
            {
                return false;
            }

            if (camera == null)
            {
                return false;
            }

            return MapManager.Instance.TryPickTile(screenPosition, camera, out tileView, out worldPosition);
        }

        public static bool TryPickTileCoord(Camera camera, out Vector3Int coord, bool includeUi = true)
        {
            return TryPickTileCoord(CurrentPointerPosition, camera, out coord, includeUi);
        }

        public static bool TryPickTileCoord(Vector2 screenPosition, Camera camera, out Vector3Int coord, bool includeUi = true)
        {
            coord = default;

            if (!TryPickTile(screenPosition, camera, out TileView tileView, includeUi) || tileView == null)
            {
                return false;
            }

            coord = tileView.Coord;
            return true;
        }

        public static bool TryPickComponent<T>(Camera camera, out T component, bool includeUi = true) where T : Component
        {
            return TryPickComponent(CurrentPointerPosition, camera, out component, includeUi);
        }

        public static bool TryPickComponent<T>(Vector2 screenPosition, Camera camera, out T component, bool includeUi = true) where T : Component
        {
            return TryPickComponent(screenPosition, camera, out component, includeUi, DefaultRayDistance, Physics.DefaultRaycastLayers);
        }

        public static bool TryPickComponent<T>(Vector2 screenPosition, Camera camera, out T component, bool includeUi, float rayDistance, int layerMask) where T : Component
        {
            component = null;

            if (includeUi && IsPointerOverUi())
            {
                return false;
            }

            if (!TryRaycastSorted(screenPosition, camera, rayDistance, layerMask, out RaycastHit[] hits))
            {
                return false;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                component = hitCollider.GetComponentInParent<T>();
                if (component != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryPickWorldObject(Vector2 screenPosition, Camera camera, out Component worldObject, out Collider collider)
        {
            return TryPickWorldObject(screenPosition, camera, out worldObject, out collider, DefaultRayDistance, Physics.DefaultRaycastLayers);
        }

        public static bool TryPickWorldObject(Vector2 screenPosition, Camera camera, out Component worldObject, out Collider collider, float rayDistance, int layerMask)
        {
            worldObject = null;
            collider = null;

            if (!TryRaycastSorted(screenPosition, camera, rayDistance, layerMask, out RaycastHit[] hits))
            {
                return false;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                Component target = GetKnownWorldObject(hitCollider);
                if (target != null)
                {
                    worldObject = target;
                    collider = hitCollider;
                    return true;
                }

                if (!TileView.TryGetValidFrom(hitCollider.transform, out _))
                {
                    worldObject = hitCollider;
                    collider = hitCollider;
                    return true;
                }
            }

            return false;
        }

        private static bool TryRaycastSorted(Vector2 screenPosition, Camera camera, float rayDistance, int layerMask, out RaycastHit[] hits)
        {
            hits = null;

            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            hits = Physics.RaycastAll(ray, Mathf.Max(1f, rayDistance), layerMask);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, CompareRaycastHitDistance);
            return true;
        }

        private static int CompareRaycastHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private static Component GetKnownWorldObject(Collider collider)
        {
            WorldResourceView resourceView = collider.GetComponentInParent<WorldResourceView>();
            if (resourceView != null)
            {
                return resourceView;
            }

            Tower tower = collider.GetComponentInParent<Tower>();
            if (tower != null)
            {
                return tower;
            }

            Npc npc = collider.GetComponentInParent<Npc>();
            if (npc != null)
            {
                return npc;
            }

            BaseView baseView = collider.GetComponentInParent<BaseView>();
            if (baseView != null)
            {
                return baseView;
            }

            return null;
        }
    }
}
