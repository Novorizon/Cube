#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Art
{
    public sealed class RoadSoftTileAssetBuilder : EditorWindow
    {
        private const string MaterialPath = "Assets/Arts/Map/Tiles/Materials/RoadTop_SoftClean.mat";
        private const string ShaderName = "CubeTD/Map/RoadSoftClean";
        private const string BaseTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Road_SoftClean_Albedo_1024.png";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int TextureBrightnessId = Shader.PropertyToID("_TextureBrightness");
        private static readonly int TextureContrastId = Shader.PropertyToID("_TextureContrast");
        private static readonly int TextureSaturationId = Shader.PropertyToID("_TextureSaturation");
        private static readonly int EdgeDarknessId = Shader.PropertyToID("_EdgeDarkness");
        private static readonly int EdgeDarkWidthId = Shader.PropertyToID("_EdgeDarkWidth");
        private static readonly int EdgeHighlightId = Shader.PropertyToID("_EdgeHighlight");
        private static readonly int EdgeHighlightWidthId = Shader.PropertyToID("_EdgeHighlightWidth");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
        private static readonly int LightWrapId = Shader.PropertyToID("_LightWrap");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");

        private Material roadMaterial;
        private Texture2D baseTexture;

        [MenuItem("CubeTD/Art/Road Material Tool")]
        public static void Open()
        {
            RoadSoftTileAssetBuilder window = GetWindow<RoadSoftTileAssetBuilder>();
            window.titleContent = new GUIContent("Road Material");
            window.minSize = new Vector2(380f, 220f);
            window.Show();
        }

        [MenuItem("CubeTD/Art/Generate Reference Road Top And Apply")]
        public static void GenerateReferenceRoadAndApplyMenu()
        {
            GenerateReferenceRoadAndApplyBatch();
        }

        public static void GenerateReferenceRoadAndApplyBatch()
        {
            Texture2D generatedBase = GenerateSinglePaverTextureAsset();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                Debug.LogError($"Road material not found: {MaterialPath}");
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Road shader not found: {ShaderName}");
                return;
            }

            Undo.RecordObject(material, "Apply reference road material");
            material.shader = shader;
            material.SetTexture(BaseMapId, generatedBase);
            ApplyTextureFirstValues(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated single-paver reference road top texture and material: {MaterialPath}");
        }

        private void OnEnable()
        {
            roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexturePath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Reference Road Top", EditorStyles.boldLabel);
            roadMaterial = (Material)EditorGUILayout.ObjectField("Material", roadMaterial, typeof(Material), false);
            baseTexture = (Texture2D)EditorGUILayout.ObjectField("Single Paver Base Map", baseTexture, typeof(Texture2D), false);

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Generate Single Paver And Apply", GUILayout.Height(32f)))
            {
                GenerateReferenceRoadAndApplyBatch();
                OnEnable();
            }
        }

        private static void ApplyTextureFirstValues(Material material)
        {
            material.SetFloat(TextureBrightnessId, 1f);
            material.SetFloat(TextureContrastId, 1f);
            material.SetFloat(TextureSaturationId, 1f);
            material.SetFloat(EdgeDarknessId, 0f);
            material.SetFloat(EdgeDarkWidthId, 0.044f);
            material.SetFloat(EdgeHighlightId, 0f);
            material.SetFloat(EdgeHighlightWidthId, 0.076f);
            material.SetFloat(AmbientId, 1f);
            material.SetFloat(LightStrengthId, 0f);
            material.SetFloat(LightWrapId, 0.65f);
            material.SetFloat(ShadowStrengthId, 0f);
            material.SetFloat(MaxBrightnessId, 2f);
        }

        private static Texture2D GenerateSinglePaverTextureAsset()
        {
            const int size = 1024;
            EnsureDirectory(BaseTexturePath);

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    pixels[y * size + x] = EvaluatePaverPixel(u, v);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(BaseTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(BaseTexturePath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(BaseTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexturePath);
        }

        private static Color32 EvaluatePaverPixel(float u, float v)
        {
            Color baseSand = Srgb(241, 207, 138);
            Color lightSand = Srgb(249, 226, 161);
            Color darkSand = Srgb(218, 181, 111);
            Color edgeSand = Srgb(201, 157, 92);

            float broad = TileableValueNoise(u, v, 3, 1209);
            float mid = TileableValueNoise(u, v, 7, 4317);
            float fine = TileableValueNoise(u, v, 19, 8219);
            float tone = (broad - 0.5f) * 0.030f + (mid - 0.5f) * 0.012f + (fine - 0.5f) * 0.004f;
            Color color = tone >= 0f
                ? Color.Lerp(baseSand, lightSand, tone * 1.1f)
                : Color.Lerp(baseSand, darkSand, -tone * 1.0f);

            float edgeDistance = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float edgeLine = 1f - SmoothStep(0.000f, 0.018f, edgeDistance);
            float edgeFade = 1f - SmoothStep(0.012f, 0.042f, edgeDistance);
            float edgeHighlight = SmoothStep(0.040f, 0.085f, edgeDistance) *
                (1f - SmoothStep(0.085f, 0.140f, edgeDistance));

            color = Color.Lerp(color, edgeSand, edgeFade * 0.20f + edgeLine * 0.45f);
            color = Color.Lerp(color, lightSand, edgeHighlight * 0.035f);

            return ToColor32(color);
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

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Color Srgb(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static Color AdjustValue(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                255);
        }

        private static void ConfigureTextureImporter(string path)
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
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}

#endif
