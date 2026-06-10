#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Art
{
    public sealed class GrassSoftTileAssetBuilder : EditorWindow
    {
        private const string MaterialPath = "Assets/Arts/Map/Tiles/Materials/Grass_Stylized.mat";
        private const string PatchTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Patch_V08.png";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int PatchMapId = Shader.PropertyToID("_PatchMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DarkGreenId = Shader.PropertyToID("_DarkGreen");
        private static readonly int LightGreenId = Shader.PropertyToID("_LightGreen");
        private static readonly int PatchStrengthId = Shader.PropertyToID("_PatchStrength");
        private static readonly int PatchWorldScaleId = Shader.PropertyToID("_PatchWorldScale");
        private static readonly int PatchCellRandomnessId = Shader.PropertyToID("_PatchCellRandomness");
        private static readonly int VariationStrengthId = Shader.PropertyToID("_VariationStrength");
        private static readonly int VariationScaleId = Shader.PropertyToID("_VariationScale");
        private static readonly int VariationSoftnessId = Shader.PropertyToID("_VariationSoftness");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
        private static readonly int LightWrapId = Shader.PropertyToID("_LightWrap");
        private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int SlopeDarknessId = Shader.PropertyToID("_SlopeDarkness");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        private Material grassMaterial;
        private Texture2D patchTexture;

        private Color baseGreen = new Color(0.43f, 0.66f, 0.09f, 1f);
        private Color darkGreen = new Color(0.34f, 0.56f, 0.055f, 1f);
        private Color lightGreen = new Color(0.56f, 0.76f, 0.15f, 1f);
        private float patchStrength = 0.32f;
        private float patchWorldScale = 0.82f;
        private float patchCellRandomness = 1f;
        private float variationStrength = 0.12f;
        private float variationScale = 1.35f;
        private float variationSoftness = 0.72f;
        private float ambient = 0.76f;
        private float lightStrength = 0.22f;
        private float lightWrap = 0.68f;
        private float maxBrightness = 1.12f;
        private float shadowStrength = 0.08f;
        private float slopeDarkness = 0.16f;
        private float saturation = 1f;

        [MenuItem("CubeTD/Art/Grass Material Tool")]
        public static void Open()
        {
            GrassSoftTileAssetBuilder window = GetWindow<GrassSoftTileAssetBuilder>();
            window.titleContent = new GUIContent("Grass Material");
            window.minSize = new Vector2(380f, 620f);
            window.Show();
        }

        [MenuItem("CubeTD/Art/Generate Grass Patch Texture And Apply")]
        public static void GeneratePatchTextureAndApplyMenu()
        {
            GeneratePatchTextureAndApplyBatch();
        }

        public static void GeneratePatchTextureAndApplyBatch()
        {
            Texture2D texture = GeneratePatchTextureAsset();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                Debug.LogError($"Grass material not found: {MaterialPath}");
                return;
            }

            Undo.RecordObject(material, "Apply grass patch defaults");
            ApplyRecommendedValues(material, texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated grass patch texture and applied material settings: {PatchTexturePath}");
        }

        private void OnEnable()
        {
            grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            patchTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PatchTexturePath);
            ReadFromMaterial();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Grass Material", EditorStyles.boldLabel);
            grassMaterial = (Material)EditorGUILayout.ObjectField("Material", grassMaterial, typeof(Material), false);
            patchTexture = (Texture2D)EditorGUILayout.ObjectField("Patch Map", patchTexture, typeof(Texture2D), false);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            baseGreen = EditorGUILayout.ColorField("Base Green", baseGreen);
            darkGreen = EditorGUILayout.ColorField("Dark Green", darkGreen);
            lightGreen = EditorGUILayout.ColorField("Light Green", lightGreen);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Patch Texture", EditorStyles.boldLabel);
            patchStrength = EditorGUILayout.Slider("Patch Strength", patchStrength, 0f, 1f);
            patchWorldScale = EditorGUILayout.Slider("Patch World Scale", patchWorldScale, 0.05f, 4f);
            patchCellRandomness = EditorGUILayout.Slider("Patch Cell Randomness", patchCellRandomness, 0f, 1f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Soft Variation", EditorStyles.boldLabel);
            variationStrength = EditorGUILayout.Slider("Variation Strength", variationStrength, 0f, 1f);
            variationScale = EditorGUILayout.Slider("Variation Scale", variationScale, 0.25f, 8f);
            variationSoftness = EditorGUILayout.Slider("Variation Softness", variationSoftness, 0.01f, 1f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            ambient = EditorGUILayout.Slider("Ambient", ambient, 0f, 1f);
            lightStrength = EditorGUILayout.Slider("Light Strength", lightStrength, 0f, 1f);
            lightWrap = EditorGUILayout.Slider("Light Wrap", lightWrap, 0f, 1f);
            maxBrightness = EditorGUILayout.Slider("Max Brightness", maxBrightness, 0.5f, 2f);
            shadowStrength = EditorGUILayout.Slider("Shadow Strength", shadowStrength, 0f, 1f);
            slopeDarkness = EditorGUILayout.Slider("Slope Darkness", slopeDarkness, 0f, 1f);
            saturation = EditorGUILayout.Slider("Saturation", saturation, 0f, 2f);

            EditorGUILayout.Space(12f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Patch Texture", GUILayout.Height(32f)))
                {
                    patchTexture = GeneratePatchTextureAsset();
                }

                if (GUILayout.Button("Apply To Material", GUILayout.Height(32f)))
                {
                    ApplyToMaterial();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load From Material"))
                {
                    ReadFromMaterial();
                }

                if (GUILayout.Button("Recommended Defaults"))
                {
                    SetRecommendedValues();
                }
            }
        }

        private static Texture2D GeneratePatchTextureAsset()
        {
            const int size = 1024;
            string directory = Path.GetDirectoryName(PatchTexturePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            PatchBlob[] patches = CreatePatchBlobs();
            PatchSpot[] spots = CreatePatchSpots();

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float value = 0.985f;
                    float broad = TileableValueNoise(u, v, 3, 17);
                    float mid = TileableValueNoise(u, v, 9, 47);
                    float fine = TileableValueNoise(u, v, 28, 89);

                    value -= Mathf.Lerp(-0.012f, 0.026f, broad);
                    value -= Mathf.Lerp(0.000f, 0.020f, mid);
                    value -= Mathf.Lerp(0.000f, 0.006f, fine);

                    for (int i = 0; i < patches.Length; i++)
                    {
                        float mask = patches[i].Evaluate(u, v);
                        value = Mathf.Lerp(value, value * patches[i].Multiplier, mask);
                    }

                    for (int i = 0; i < spots.Length; i++)
                    {
                        float mask = spots[i].Evaluate(u, v);
                        value = Mathf.Lerp(value, value * spots[i].Multiplier, mask);
                    }

                    value = Mathf.Clamp(value, 0.76f, 1f);
                    float greenBias = Mathf.Clamp01((1f - value) * 1.7f);
                    byte r = ToByte(Mathf.Lerp(value, value * 0.96f, greenBias));
                    byte g = ToByte(Mathf.Lerp(value, Mathf.Min(1f, value * 1.03f), greenBias));
                    byte b = ToByte(Mathf.Lerp(value, value * 0.78f, greenBias));
                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(PatchTexturePath, texture.EncodeToPNG());
            DestroyImmediate(texture);

            AssetDatabase.ImportAsset(PatchTexturePath, ImportAssetOptions.ForceUpdate);
            ConfigurePatchTextureImporter(PatchTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PatchTexturePath);
        }

        private static void ConfigurePatchTextureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"TextureImporter not found: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 70;
            importer.SaveAndReimport();
        }

        private static void ApplyRecommendedValues(Material material, Texture2D texture)
        {
            material.SetTexture(PatchMapId, texture);
            material.SetColor(BaseColorId, new Color(0.43f, 0.66f, 0.09f, 1f));
            material.SetColor(DarkGreenId, new Color(0.34f, 0.56f, 0.055f, 1f));
            material.SetColor(LightGreenId, new Color(0.56f, 0.76f, 0.15f, 1f));
            material.SetFloat(PatchStrengthId, 0.32f);
            material.SetFloat(PatchWorldScaleId, 0.82f);
            material.SetFloat(PatchCellRandomnessId, 1f);
            material.SetFloat(VariationStrengthId, 0.14f);
            material.SetFloat(VariationScaleId, 1.15f);
            material.SetFloat(VariationSoftnessId, 0.78f);
            material.SetFloat(AmbientId, 0.72f);
            material.SetFloat(LightStrengthId, 0.28f);
            material.SetFloat(LightWrapId, 0.58f);
            material.SetFloat(MaxBrightnessId, 1.16f);
            material.SetFloat(ShadowStrengthId, 0.12f);
            material.SetFloat(SlopeDarknessId, 0.18f);
            material.SetFloat(SaturationId, 1.04f);
        }

        private void ApplyToMaterial()
        {
            if (grassMaterial == null)
            {
                Debug.LogError("Grass material is not assigned.");
                return;
            }

            Undo.RecordObject(grassMaterial, "Apply grass material parameters");
            grassMaterial.SetTexture(PatchMapId, patchTexture);
            grassMaterial.SetColor(BaseColorId, baseGreen);
            grassMaterial.SetColor(DarkGreenId, darkGreen);
            grassMaterial.SetColor(LightGreenId, lightGreen);
            grassMaterial.SetFloat(PatchStrengthId, patchStrength);
            grassMaterial.SetFloat(PatchWorldScaleId, patchWorldScale);
            grassMaterial.SetFloat(PatchCellRandomnessId, patchCellRandomness);
            grassMaterial.SetFloat(VariationStrengthId, variationStrength);
            grassMaterial.SetFloat(VariationScaleId, variationScale);
            grassMaterial.SetFloat(VariationSoftnessId, variationSoftness);
            grassMaterial.SetFloat(AmbientId, ambient);
            grassMaterial.SetFloat(LightStrengthId, lightStrength);
            grassMaterial.SetFloat(LightWrapId, lightWrap);
            grassMaterial.SetFloat(MaxBrightnessId, maxBrightness);
            grassMaterial.SetFloat(ShadowStrengthId, shadowStrength);
            grassMaterial.SetFloat(SlopeDarknessId, slopeDarkness);
            grassMaterial.SetFloat(SaturationId, saturation);
            EditorUtility.SetDirty(grassMaterial);
            AssetDatabase.SaveAssets();
        }

        private void ReadFromMaterial()
        {
            if (grassMaterial == null)
            {
                return;
            }

            patchTexture = grassMaterial.GetTexture(PatchMapId) as Texture2D ?? patchTexture;
            baseGreen = grassMaterial.GetColor(BaseColorId);
            darkGreen = grassMaterial.GetColor(DarkGreenId);
            lightGreen = grassMaterial.GetColor(LightGreenId);
            patchStrength = grassMaterial.GetFloat(PatchStrengthId);
            patchWorldScale = grassMaterial.GetFloat(PatchWorldScaleId);
            patchCellRandomness = grassMaterial.GetFloat(PatchCellRandomnessId);
            variationStrength = grassMaterial.GetFloat(VariationStrengthId);
            variationScale = grassMaterial.GetFloat(VariationScaleId);
            variationSoftness = grassMaterial.GetFloat(VariationSoftnessId);
            ambient = grassMaterial.GetFloat(AmbientId);
            lightStrength = grassMaterial.GetFloat(LightStrengthId);
            lightWrap = grassMaterial.GetFloat(LightWrapId);
            maxBrightness = grassMaterial.GetFloat(MaxBrightnessId);
            shadowStrength = grassMaterial.GetFloat(ShadowStrengthId);
            slopeDarkness = grassMaterial.GetFloat(SlopeDarknessId);
            saturation = grassMaterial.GetFloat(SaturationId);
        }

        private void SetRecommendedValues()
        {
            baseGreen = new Color(0.43f, 0.66f, 0.09f, 1f);
            darkGreen = new Color(0.34f, 0.56f, 0.055f, 1f);
            lightGreen = new Color(0.56f, 0.76f, 0.15f, 1f);
            patchStrength = 0.32f;
            patchWorldScale = 0.82f;
            patchCellRandomness = 1f;
            variationStrength = 0.14f;
            variationScale = 1.15f;
            variationSoftness = 0.78f;
            ambient = 0.72f;
            lightStrength = 0.28f;
            lightWrap = 0.58f;
            maxBrightness = 1.16f;
            shadowStrength = 0.12f;
            slopeDarkness = 0.18f;
            saturation = 1.04f;
        }

        private static PatchBlob[] CreatePatchBlobs()
        {
            return new[]
            {
                new PatchBlob(0.19f, 0.24f, 0.13f, 0.09f, -24f, 0.86f, 0.18f, 101),
                new PatchBlob(0.45f, 0.30f, 0.18f, 0.08f, 13f, 0.88f, 0.16f, 223),
                new PatchBlob(0.69f, 0.22f, 0.11f, 0.12f, 41f, 0.84f, 0.20f, 347),
                new PatchBlob(0.31f, 0.67f, 0.19f, 0.10f, -11f, 0.90f, 0.22f, 461),
                new PatchBlob(0.63f, 0.74f, 0.16f, 0.09f, 27f, 0.87f, 0.18f, 587),
                new PatchBlob(0.83f, 0.55f, 0.12f, 0.15f, -37f, 0.91f, 0.20f, 691),
                new PatchBlob(0.09f, 0.82f, 0.11f, 0.08f, 35f, 0.92f, 0.18f, 743),
                new PatchBlob(0.94f, 0.12f, 0.10f, 0.12f, 18f, 0.89f, 0.20f, 809),
            };
        }

        private static PatchSpot[] CreatePatchSpots()
        {
            System.Random random = new System.Random(6208);
            PatchSpot[] spots = new PatchSpot[34];

            for (int i = 0; i < spots.Length; i++)
            {
                float u = (float)random.NextDouble();
                float v = (float)random.NextDouble();
                float radius = Mathf.Lerp(0.008f, 0.028f, (float)random.NextDouble());
                float multiplier = Mathf.Lerp(0.90f, 0.97f, (float)random.NextDouble());
                spots[i] = new PatchSpot(u, v, radius, multiplier);
            }

            return spots;
        }

        private static float TileableValueNoise(float u, float v, int frequency, int seed)
        {
            float x = u * frequency;
            float y = v * frequency;
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float tx = Smooth01(x - ix);
            float ty = Smooth01(y - iy);

            float a = Hash(ix, iy, frequency, seed);
            float b = Hash(ix + 1, iy, frequency, seed);
            float c = Hash(ix, iy + 1, frequency, seed);
            float d = Hash(ix + 1, iy + 1, frequency, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y, int frequency, int seed)
        {
            int wrappedX = PositiveModulo(x, frequency);
            int wrappedY = PositiveModulo(y, frequency);
            int n = wrappedX * 374761393 + wrappedY * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x00ffffff) / 16777215f;
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private readonly struct PatchBlob
        {
            private readonly float centerU;
            private readonly float centerV;
            private readonly float radiusU;
            private readonly float radiusV;
            private readonly float cos;
            private readonly float sin;
            private readonly float softEdge;
            private readonly int seed;

            public PatchBlob(
                float centerU,
                float centerV,
                float radiusU,
                float radiusV,
                float rotationDegrees,
                float multiplier,
                float softEdge,
                int seed)
            {
                this.centerU = centerU;
                this.centerV = centerV;
                this.radiusU = radiusU;
                this.radiusV = radiusV;
                float radians = rotationDegrees * Mathf.Deg2Rad;
                cos = Mathf.Cos(radians);
                sin = Mathf.Sin(radians);
                Multiplier = multiplier;
                this.softEdge = softEdge;
                this.seed = seed;
            }

            public float Multiplier { get; }

            public float Evaluate(float u, float v)
            {
                float dx = WrappedDistance(u, centerU);
                float dy = WrappedDistance(v, centerV);
                float localX = dx * cos - dy * sin;
                float localY = dx * sin + dy * cos;
                float distance = Mathf.Max(
                    Mathf.Abs(localX) / Mathf.Max(0.0001f, radiusU),
                    Mathf.Abs(localY) / Mathf.Max(0.0001f, radiusV));
                float boundaryNoise = TileableValueNoise(u + seed * 0.013f, v + seed * 0.017f, 7, seed);
                float boundary = 1f + (boundaryNoise - 0.5f) * 0.22f;
                return 1f - SmoothStep(boundary, boundary + softEdge, distance);
            }
        }

        private readonly struct PatchSpot
        {
            private readonly float centerU;
            private readonly float centerV;
            private readonly float radius;

            public PatchSpot(float centerU, float centerV, float radius, float multiplier)
            {
                this.centerU = centerU;
                this.centerV = centerV;
                this.radius = radius;
                Multiplier = multiplier;
            }

            public float Multiplier { get; }

            public float Evaluate(float u, float v)
            {
                float dx = WrappedDistance(u, centerU);
                float dy = WrappedDistance(v, centerV);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                return 1f - SmoothStep(radius * 0.45f, radius, distance);
            }
        }

        private static float WrappedDistance(float a, float b)
        {
            float delta = a - b;
            return delta - Mathf.Round(delta);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}

#endif
