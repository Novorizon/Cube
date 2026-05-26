using Game.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TargetModelPreview
    {
        private const int TextureSize = 256;
        private const float CameraPitchDegrees = 45f;
        private const float CameraYawDegrees = -35f;
        private static readonly Vector3 PreviewWorldOrigin = new Vector3(50000f, 50000f, 50000f);
        private static int nextPreviewIndex;

        private Image fallbackImage;
        private RawImage previewImage;
        private RenderTexture renderTexture;
        private Transform previewRoot;
        private Camera previewCamera;
        private Light keyLight;
        private Light fillLight;
        private GameObject previewObject;
        private Vector3 previewOrigin;

        public void Initialize(Image fallbackImage)
        {
            this.fallbackImage = fallbackImage;
            previewOrigin = PreviewWorldOrigin + new Vector3(nextPreviewIndex++ * 20f, 0f, 0f);

            EnsurePreviewImage();
            EnsureRenderObjects();
            Clear();
        }

        public bool Show(string prefabLocation)
        {
            if (string.IsNullOrEmpty(prefabLocation))
            {
                Clear();
                return false;
            }

            GameObject prefab = LoadPreviewPrefab(prefabLocation);
            if (prefab == null)
            {
                Clear();
                return false;
            }

            EnsurePreviewImage();
            EnsureRenderObjects();
            ClearPreviewObject();

            previewObject = Object.Instantiate(prefab, previewRoot);
            previewObject.name = $"{prefab.name}_Preview";
            previewObject.transform.localPosition = Vector3.zero;
            previewObject.transform.localRotation = Quaternion.identity;
            previewObject.transform.localScale = Vector3.one;

            DisableRuntimeComponents(previewObject);

            if (!TryFramePreviewObject())
            {
                Clear();
                return false;
            }

            previewCamera.Render();

            if (previewImage != null)
            {
                previewImage.texture = renderTexture;
                previewImage.enabled = true;
            }

            if (previewCamera != null)
            {
                // Keep the preview camera rendering so default Animator/Animation clips can play in the UI.
                previewCamera.enabled = true;
            }

            return true;
        }

        public void Clear()
        {
            ClearPreviewObject();

            if (previewImage != null)
            {
                previewImage.enabled = false;
                previewImage.texture = null;
            }

            if (previewCamera != null)
            {
                previewCamera.enabled = false;
            }
        }

        public void Dispose()
        {
            ClearPreviewObject();

            if (previewImage != null)
            {
                previewImage.texture = null;
                Object.Destroy(previewImage.gameObject);
                previewImage = null;
            }

            if (previewRoot != null)
            {
                Object.Destroy(previewRoot.gameObject);
                previewRoot = null;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.Destroy(renderTexture);
                renderTexture = null;
            }

            fallbackImage = null;
            previewCamera = null;
            keyLight = null;
            fillLight = null;
        }

        private GameObject LoadPreviewPrefab(string prefabLocation)
        {
            try
            {
                return ResourceManager.Instance.LoadGameObject(prefabLocation);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Load target preview prefab failed. Location: {prefabLocation}, Error: {exception.Message}");
                return null;
            }
        }

        private void EnsurePreviewImage()
        {
            if (fallbackImage == null || previewImage != null)
            {
                return;
            }

            GameObject previewImageObject = new GameObject("ModelPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform previewTransform = previewImageObject.GetComponent<RectTransform>();
            previewTransform.SetParent(fallbackImage.rectTransform, false);
            previewTransform.anchorMin = Vector2.zero;
            previewTransform.anchorMax = Vector2.one;
            previewTransform.offsetMin = Vector2.zero;
            previewTransform.offsetMax = Vector2.zero;
            previewTransform.localScale = Vector3.one;

            previewImage = previewImageObject.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.enabled = false;
        }

        private void EnsureRenderObjects()
        {
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32);
                renderTexture.name = "InfoPanelTargetPreviewRT";
                renderTexture.Create();
            }

            if (previewRoot == null)
            {
                GameObject rootObject = new GameObject("InfoPanelTargetPreviewRoot");
                rootObject.hideFlags = HideFlags.HideAndDontSave;
                previewRoot = rootObject.transform;
                previewRoot.position = previewOrigin;
            }

            if (previewCamera == null)
            {
                GameObject cameraObject = new GameObject("InfoPanelTargetPreviewCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                cameraObject.transform.SetParent(previewRoot, false);

                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.enabled = false;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                previewCamera.orthographic = true;
                previewCamera.allowHDR = false;
                previewCamera.allowMSAA = true;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 100f;
                previewCamera.targetTexture = renderTexture;
            }

            if (keyLight == null)
            {
                GameObject lightObject = new GameObject("InfoPanelTargetPreviewKeyLight");
                lightObject.hideFlags = HideFlags.HideAndDontSave;
                lightObject.transform.SetParent(previewRoot, false);
                lightObject.transform.localRotation = Quaternion.Euler(45f, -35f, 0f);

                keyLight = lightObject.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.25f;
                keyLight.shadows = LightShadows.None;
            }

            if (fillLight == null)
            {
                GameObject lightObject = new GameObject("InfoPanelTargetPreviewFillLight");
                lightObject.hideFlags = HideFlags.HideAndDontSave;
                lightObject.transform.SetParent(previewRoot, false);
                lightObject.transform.localPosition = new Vector3(-2.5f, 2.2f, -2f);

                fillLight = lightObject.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 8f;
                fillLight.intensity = 0.55f;
                fillLight.shadows = LightShadows.None;
            }
        }

        private void ClearPreviewObject()
        {
            if (previewObject == null)
            {
                return;
            }

            Object.Destroy(previewObject);
            previewObject = null;
        }

        private void DisableRuntimeComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].enabled = false;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }

            Animation[] animations = root.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                Animation animation = animations[i];
                if (animation == null)
                {
                    continue;
                }

                animation.enabled = true;
                if (animation.clip != null)
                {
                    animation.Play();
                }
            }
        }

        private bool TryFramePreviewObject()
        {
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
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
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            Vector3 center = bounds.center;
            float radius = Mathf.Max(0.35f, bounds.extents.magnitude);
            Vector3 cameraForward = Quaternion.Euler(CameraPitchDegrees, CameraYawDegrees, 0f) * Vector3.forward;
            Vector3 lookAt = center + Vector3.up * bounds.extents.y * 0.12f;
            float distance = radius * 3.2f;

            previewCamera.transform.position = lookAt - cameraForward * distance;
            previewCamera.transform.rotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            previewCamera.orthographicSize = Mathf.Max(0.45f, radius * 0.72f);
            previewCamera.farClipPlane = Mathf.Max(100f, radius * 8f);

            if (fillLight != null)
            {
                fillLight.transform.position = lookAt + new Vector3(-radius * 1.8f, radius * 1.5f, -radius * 1.5f);
            }

            return true;
        }
    }
}
