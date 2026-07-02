using UnityEngine;

namespace Game
{
    [ExecuteAlways]
    public sealed class TileBoundsGizmo : MonoBehaviour
    {
        [SerializeField]
        private bool drawBounds = false;

        [SerializeField]
        private Color boundsColor = Color.yellow;

        [SerializeField]
        private bool drawWhenNotSelected = true;

        [SerializeField]
        private bool includeInactive = true;

        [SerializeField]
        private bool showSizeLabel = true;

        [SerializeField]
        private Vector3 labelOffset = new Vector3(0f, 0.15f, 0f);

        [SerializeField]
        [HideInInspector]
        private Vector3 boundsSize;

        [SerializeField]
        [HideInInspector]
        private Vector3 boundsCenter;

        public Vector3 BoundsSize
        {
            get
            {
                return boundsSize;
            }
        }

        public Vector3 BoundsCenter
        {
            get
            {
                return boundsCenter;
            }
        }

        public bool DrawBoundsEnabled
        {
            get
            {
                return drawBounds;
            }
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void OnDrawGizmos()
        {
            if (!drawBounds)
            {
                return;
            }

            if (!drawWhenNotSelected)
            {
                return;
            }

            DrawBounds();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBounds)
            {
                return;
            }

            DrawBounds();
        }

        private void DrawBounds()
        {
            if (!drawBounds)
            {
                return;
            }

            if (!TryGetRendererBounds(out Bounds bounds))
            {
                return;
            }

            Color oldColor = Gizmos.color;
            Gizmos.color = boundsColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.color = oldColor;

#if UNITY_EDITOR
            if (showSizeLabel)
            {
                UnityEditor.Handles.Label(bounds.center + Vector3.up * bounds.extents.y + labelOffset, FormatBoundsLabel(bounds));
            }
#endif
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        public void RefreshCachedBounds()
        {
            if (!TryGetRendererBounds(out Bounds bounds))
            {
                boundsSize = Vector3.zero;
                boundsCenter = Vector3.zero;
                return;
            }

            boundsSize = bounds.size;
            boundsCenter = bounds.center;
        }

        private string FormatBoundsLabel(Bounds bounds)
        {
            Vector3 size = bounds.size;
            Vector3 center = bounds.center;

            return $"Size: {size.x:F3}, {size.y:F3}, {size.z:F3}\nCenter: {center.x:F3}, {center.y:F3}, {center.z:F3}";
        }
    }
}
