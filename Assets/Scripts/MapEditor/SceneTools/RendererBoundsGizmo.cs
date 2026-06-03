using UnityEngine;

namespace Game
{
    [ExecuteAlways]
    public sealed class RendererBoundsGizmo : MonoBehaviour
    {
        [SerializeField]
        private Color color = Color.yellow;

        [SerializeField]
        private bool includeInactive = true;

        [SerializeField]
        private bool drawOnlyWhenSelected = false;

        [SerializeField]
        private bool drawCenter = true;

        [SerializeField]
        private float centerSize = 0.05f;

        private void OnDrawGizmos()
        {
            if (drawOnlyWhenSelected)
            {
                return;
            }

            DrawBounds();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawOnlyWhenSelected)
            {
                return;
            }

            DrawBounds();
        }

        private void DrawBounds()
        {
            if (!TryGetRendererBounds(out Bounds bounds))
            {
                return;
            }

            Color oldColor = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (drawCenter)
            {
                Gizmos.DrawSphere(bounds.center, centerSize);
            }

            Gizmos.color = oldColor;
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
    }
}
