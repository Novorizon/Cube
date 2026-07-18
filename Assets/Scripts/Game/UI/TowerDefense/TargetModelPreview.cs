using System.Collections.Generic;
using Game.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TargetModelPreview
    {
        private const int TextureSize = 256;
        private const int PreviewLayer = 31;
        private const float CameraPitchDegrees = 45f;
        private const float CameraYawDegrees = -35f;
        private static readonly Vector3 PreviewWorldOrigin = new Vector3(50000f, 50000f, 50000f);
        private static readonly HashSet<string> MissingDescriptorWarnings = new HashSet<string>();
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

        public void Initialize(Image image)
        {
            fallbackImage = image;
            previewOrigin = PreviewWorldOrigin + new Vector3(nextPreviewIndex++ * 20f, 0f, 0f);
            EnsurePreviewImage();
            EnsureRenderObjects();
            Clear();
        }

        public bool Show(string prefabLocation)
        {
            if (string.IsNullOrWhiteSpace(prefabLocation))
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

            previewRoot.gameObject.SetActive(false);
            previewObject = Object.Instantiate(prefab, previewRoot);
            previewObject.name = $"{prefab.name}_Preview";
            previewObject.transform.localPosition = Vector3.zero;
            previewObject.transform.localRotation = Quaternion.identity;
            previewObject.transform.localScale = Vector3.one;

            BattleTargetPreviewDescriptor descriptor = previewObject.GetComponent<BattleTargetPreviewDescriptor>();
            if (descriptor == null || !descriptor.Prepare(PreviewLayer, out Bounds bounds))
            {
                if (MissingDescriptorWarnings.Add(prefabLocation))
                {
                    Debug.LogWarning($"Target preview requires {nameof(BattleTargetPreviewDescriptor)} on the prefab root. Location: {prefabLocation}");
                }

                Clear();
                return false;
            }

            previewRoot.gameObject.SetActive(true);
            Frame(bounds, descriptor.CameraScale);
            previewCamera.Render();

            previewImage.texture = renderTexture;
            previewImage.enabled = true;
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

        private static GameObject LoadPreviewPrefab(string prefabLocation)
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

            GameObject imageObject = new GameObject("ModelPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(fallbackImage.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            previewImage = imageObject.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.enabled = false;
        }

        private void EnsureRenderObjects()
        {
            int cullingMask = 1 << PreviewLayer;

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "InfoPanelTargetPreviewRT"
                };
                renderTexture.Create();
            }

            if (previewRoot == null)
            {
                GameObject rootObject = new GameObject("InfoPanelTargetPreviewRoot") { hideFlags = HideFlags.HideAndDontSave };
                previewRoot = rootObject.transform;
                previewRoot.position = previewOrigin;
            }

            if (previewCamera == null)
            {
                GameObject cameraObject = new GameObject("InfoPanelTargetPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
                cameraObject.transform.SetParent(previewRoot, false);
                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.enabled = false;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = Color.clear;
                previewCamera.orthographic = true;
                previewCamera.allowHDR = false;
                previewCamera.allowMSAA = true;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 100f;
                previewCamera.cullingMask = cullingMask;
                previewCamera.targetTexture = renderTexture;
            }

            if (keyLight == null)
            {
                GameObject lightObject = new GameObject("InfoPanelTargetPreviewKeyLight") { hideFlags = HideFlags.HideAndDontSave };
                lightObject.transform.SetParent(previewRoot, false);
                lightObject.transform.localRotation = Quaternion.Euler(45f, -35f, 0f);
                keyLight = lightObject.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.25f;
                keyLight.shadows = LightShadows.None;
                keyLight.cullingMask = cullingMask;
            }

            if (fillLight == null)
            {
                GameObject lightObject = new GameObject("InfoPanelTargetPreviewFillLight") { hideFlags = HideFlags.HideAndDontSave };
                lightObject.transform.SetParent(previewRoot, false);
                fillLight = lightObject.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 8f;
                fillLight.intensity = 0.55f;
                fillLight.shadows = LightShadows.None;
                fillLight.cullingMask = cullingMask;
            }
        }

        private void Frame(Bounds bounds, float cameraScale)
        {
            Vector3 center = bounds.center;
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            Vector3 forward = Quaternion.Euler(CameraPitchDegrees, CameraYawDegrees, 0f) * Vector3.forward;
            float distance = Mathf.Max(2f, radius * 3f);

            previewCamera.transform.position = center - forward * distance;
            previewCamera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            previewCamera.orthographicSize = Mathf.Max(0.45f, radius * Mathf.Max(0.1f, cameraScale));
            previewCamera.farClipPlane = Mathf.Max(100f, radius * 8f);

            if (fillLight != null)
            {
                fillLight.transform.position = center + new Vector3(-radius * 1.8f, radius * 1.5f, -radius * 1.5f);
            }
        }

        private void ClearPreviewObject()
        {
            if (previewObject != null)
            {
                Object.Destroy(previewObject);
                previewObject = null;
            }

            if (previewRoot != null)
            {
                previewRoot.gameObject.SetActive(true);
            }
        }
    }
}
