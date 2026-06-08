using UnityEngine;

namespace Game
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GrassTileMaterialOverride : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DarkGreenId = Shader.PropertyToID("_DarkGreen");
        private static readonly int LightGreenId = Shader.PropertyToID("_LightGreen");
        private static readonly int VariationStrengthId = Shader.PropertyToID("_VariationStrength");
        private static readonly int VariationScaleId = Shader.PropertyToID("_VariationScale");
        private static readonly int VariationSoftnessId = Shader.PropertyToID("_VariationSoftness");

        [SerializeField]
        private MeshRenderer targetRenderer;

        [SerializeField]
        private bool overrideEnabled;

        [SerializeField]
        private Color baseGreen = new Color(0.43f, 0.66f, 0.09f, 1f);

        [SerializeField]
        private Color darkGreen = new Color(0.34f, 0.56f, 0.055f, 1f);

        [SerializeField]
        private Color lightGreen = new Color(0.56f, 0.76f, 0.15f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        private float variationStrength = 0.12f;

        [SerializeField]
        [Range(0.25f, 8f)]
        private float variationScale = 1.35f;

        [SerializeField]
        [Range(0.01f, 1f)]
        private float variationSoftness = 0.72f;

        private MaterialPropertyBlock propertyBlock;

        public bool OverrideEnabled
        {
            get
            {
                return overrideEnabled;
            }
            set
            {
                overrideEnabled = value;
                Apply();
            }
        }

        public void SetBaseGreen(Color color)
        {
            baseGreen = color;
            overrideEnabled = true;
            Apply();
        }

        public void SetPalette(Color baseColor, Color darkColor, Color lightColor)
        {
            baseGreen = baseColor;
            darkGreen = darkColor;
            lightGreen = lightColor;
            overrideEnabled = true;
            Apply();
        }

        public void SetVariationStrength(float value)
        {
            variationStrength = Mathf.Clamp01(value);
            overrideEnabled = true;
            Apply();
        }

        public void SetVariationScale(float value)
        {
            variationScale = Mathf.Clamp(value, 0.25f, 8f);
            overrideEnabled = true;
            Apply();
        }

        public void ApplyVisualData(MapGrassVisualData visualData)
        {
            if (visualData == null)
            {
                overrideEnabled = false;
                Clear();
                return;
            }

            baseGreen = visualData.BaseGreen;
            darkGreen = visualData.DarkGreen;
            lightGreen = visualData.LightGreen;
            variationStrength = Mathf.Clamp01(visualData.VariationStrength);
            variationScale = Mathf.Clamp(visualData.VariationScale, 0.25f, 8f);
            variationSoftness = Mathf.Clamp(visualData.VariationSoftness, 0.01f, 1f);
            overrideEnabled = true;
            Apply();
        }

        private void Reset()
        {
            FindTargetRenderer();
        }

        private void OnEnable()
        {
            FindTargetRenderer();
            Apply();
        }

        private void OnValidate()
        {
            FindTargetRenderer();
            Apply();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void FindTargetRenderer()
        {
            if (targetRenderer != null)
            {
                return;
            }

            Transform grass = transform.Find("Grass");
            if (grass != null)
            {
                targetRenderer = grass.GetComponent<MeshRenderer>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<MeshRenderer>(true);
            }
        }

        private void Apply()
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (!overrideEnabled)
            {
                Clear();
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, baseGreen);
            propertyBlock.SetColor(DarkGreenId, darkGreen);
            propertyBlock.SetColor(LightGreenId, lightGreen);
            propertyBlock.SetFloat(VariationStrengthId, variationStrength);
            propertyBlock.SetFloat(VariationScaleId, variationScale);
            propertyBlock.SetFloat(VariationSoftnessId, variationSoftness);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Clear()
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.SetPropertyBlock(null);
        }
    }
}
