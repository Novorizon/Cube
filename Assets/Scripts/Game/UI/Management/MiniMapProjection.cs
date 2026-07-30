using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shared world/map/UI coordinate conversion for the management mini map.
    /// It deliberately depends only on MapData so battle maps can reuse it later.
    /// </summary>
    public sealed class MiniMapProjection
    {
        private MapData map;
        private RectTransform viewport;
        private Rect contentRect;

        public Rect ContentRect => contentRect;

        public bool Configure(MapData mapData, RectTransform viewportRect)
        {
            map = mapData;
            viewport = viewportRect;
            if (map == null || viewport == null || map.Width <= 0 || map.Depth <= 0)
            {
                contentRect = default;
                return false;
            }

            Rect available = viewport.rect;
            float mapAspect = map.Width / (float)map.Depth;
            float availableAspect = available.width / Mathf.Max(1f, available.height);
            float width = available.width;
            float height = available.height;
            if (availableAspect > mapAspect)
            {
                width = height * mapAspect;
            }
            else
            {
                height = width / mapAspect;
            }

            contentRect = new Rect(-width * 0.5f, -height * 0.5f, width, height);
            return true;
        }

        public Vector2 MapToAnchored(float mapX, float mapZ)
        {
            if (map == null)
            {
                return Vector2.zero;
            }

            float normalizedX = Mathf.Clamp01((mapX + 0.5f) / map.Width);
            float normalizedY = Mathf.Clamp01((mapZ + 0.5f) / map.Depth);
            return new Vector2(
                Mathf.Lerp(contentRect.xMin, contentRect.xMax, normalizedX),
                Mathf.Lerp(contentRect.yMin, contentRect.yMax, normalizedY));
        }

        public Vector2 WorldToAnchored(Vector3 worldPosition, float tileSize)
        {
            float safeTileSize = Mathf.Max(0.01f, tileSize);
            return MapToAnchored(worldPosition.x / safeTileSize, worldPosition.z / safeTileSize);
        }

        public bool TryScreenToMap(Vector2 screenPosition, Camera eventCamera, out Vector2 mapPosition)
        {
            mapPosition = default;
            if (map == null ||
                viewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPosition, eventCamera, out Vector2 local) ||
                !contentRect.Contains(local))
            {
                return false;
            }

            float normalizedX = Mathf.InverseLerp(contentRect.xMin, contentRect.xMax, local.x);
            float normalizedY = Mathf.InverseLerp(contentRect.yMin, contentRect.yMax, local.y);
            mapPosition = new Vector2(
                normalizedX * map.Width - 0.5f,
                normalizedY * map.Depth - 0.5f);
            return true;
        }
    }
}
