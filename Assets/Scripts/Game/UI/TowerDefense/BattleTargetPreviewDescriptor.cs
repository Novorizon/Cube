using UnityEngine;

namespace Game
{
    public sealed class BattleTargetPreviewDescriptor : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Animator[] animators;
        [SerializeField] private Behaviour[] behavioursToDisable;
        [SerializeField] private Collider[] colliders;
        [SerializeField] private Rigidbody[] rigidbodies;
        [SerializeField] private Vector3 centerOffset;
        [SerializeField, Min(0.1f)] private float cameraScale = 0.72f;

        public float CameraScale => cameraScale;

        public bool Prepare(int previewLayer, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.gameObject.layer = previewLayer;
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (animators != null)
            {
                for (int i = 0; i < animators.Length; i++)
                {
                    if (animators[i] != null)
                    {
                        animators[i].enabled = true;
                    }
                }
            }

            if (behavioursToDisable != null)
            {
                for (int i = 0; i < behavioursToDisable.Length; i++)
                {
                    if (behavioursToDisable[i] != null)
                    {
                        behavioursToDisable[i].enabled = false;
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                    }
                }
            }

            if (rigidbodies != null)
            {
                for (int i = 0; i < rigidbodies.Length; i++)
                {
                    if (rigidbodies[i] != null)
                    {
                        rigidbodies[i].isKinematic = true;
                        rigidbodies[i].detectCollisions = false;
                    }
                }
            }

            if (hasBounds)
            {
                bounds.center += centerOffset;
            }

            return hasBounds;
        }
    }
}
