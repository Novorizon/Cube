using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WorldResourceView : MonoBehaviour
    {
        private const float RefreshInterval = 1f;

        private static readonly Dictionary<Vector3Int, List<WorldResourceView>> viewsByCoord = new Dictionary<Vector3Int, List<WorldResourceView>>();

        private MapObjectData mapObject;
        private Renderer[] renderers;
        private Vector3 originalLocalScale;
        private float nextRefreshTime;
        private bool initialized;

        public Vector3Int Coord
        {
            get
            {
                return mapObject != null ? mapObject.Coord : default;
            }
        }

        public MapObjectData MapObject
        {
            get
            {
                return mapObject;
            }
        }

        public void Initialize(MapObjectData resourceObject)
        {
            Unregister();

            mapObject = resourceObject;
            renderers = GetComponentsInChildren<Renderer>(true);
            originalLocalScale = transform.localScale;
            initialized = mapObject != null && mapObject.ObjectType == MapObjectType.Resource;

            Register();
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (!initialized || mapObject == null)
            {
                SetVisible(true);
                return;
            }

            if (!WorldGatherManager.Instance.TryGetStatus(mapObject, out WorldGatherStatus status))
            {
                SetVisible(true);
                return;
            }

            SetVisible(status.CanGather || status.RemainingTimes > 0);
        }

        public static void RefreshAtCoord(Vector3Int coord)
        {
            if (!viewsByCoord.TryGetValue(coord, out List<WorldResourceView> views) || views == null)
            {
                return;
            }

            for (int i = 0; i < views.Count; i++)
            {
                WorldResourceView view = views[i];
                if (view != null)
                {
                    view.RefreshNow();
                }
            }
        }

        private void Update()
        {
            if (!initialized || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            RefreshNow();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Register()
        {
            if (!initialized || mapObject == null)
            {
                return;
            }

            Vector3Int coord = mapObject.Coord;
            if (!viewsByCoord.TryGetValue(coord, out List<WorldResourceView> views))
            {
                views = new List<WorldResourceView>();
                viewsByCoord.Add(coord, views);
            }

            if (!views.Contains(this))
            {
                views.Add(this);
            }
        }

        private void Unregister()
        {
            if (mapObject == null)
            {
                return;
            }

            Vector3Int coord = mapObject.Coord;
            if (!viewsByCoord.TryGetValue(coord, out List<WorldResourceView> views) || views == null)
            {
                return;
            }

            views.Remove(this);
            if (views.Count == 0)
            {
                viewsByCoord.Remove(coord);
            }
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }

            transform.localScale = visible ? originalLocalScale : originalLocalScale * 0.35f;
        }
    }
}
