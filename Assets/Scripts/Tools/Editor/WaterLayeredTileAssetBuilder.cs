#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.Art
{
    public sealed class WaterLayeredTileAssetBuilder : EditorWindow
    {
        private const string WaterPrefabPath = "Assets/Arts/Map/Tiles/Water.prefab";
        private const string BedMaterialPath = "Assets/Arts/Map/Tiles/Materials/WaterBed_Stylized.mat";
        private const string SurfaceMaterialPath = "Assets/Arts/Map/Tiles/Materials/WaterTop_SoftCartoon.mat";
        private const string BedShaderName = "CubeTD/Map/WaterBedSoftTile";
        private const string SurfaceShaderName = "CubeTD/Map/WaterTopSoftCartoon";

        private const string BedBaseTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Water_BedTile_Albedo_1024.png";
        private const string BedDetailTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Water_BedTile_Detail_1024.png";
        private const string SurfaceBaseTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Water_Surface_Soft_512.png";
        private const string SurfaceDetailTexturePath = "Assets/Arts/Map/Tiles/Textures/Generated/Water_Surface_Detail_512.png";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int DetailMapId = Shader.PropertyToID("_DetailMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int GapColorId = Shader.PropertyToID("_GapColor");
        private static readonly int SideColorId = Shader.PropertyToID("_SideColor");
        private static readonly int TextureBrightnessId = Shader.PropertyToID("_TextureBrightness");
        private static readonly int TextureContrastId = Shader.PropertyToID("_TextureContrast");
        private static readonly int TextureSaturationId = Shader.PropertyToID("_TextureSaturation");
        private static readonly int DetailStrengthId = Shader.PropertyToID("_DetailStrength");
        private static readonly int DetailScaleId = Shader.PropertyToID("_DetailScale");
        private static readonly int TileCountId = Shader.PropertyToID("_TileCount");
        private static readonly int TileGapWidthId = Shader.PropertyToID("_TileGapWidth");
        private static readonly int TileLineStrengthId = Shader.PropertyToID("_TileLineStrength");
        private static readonly int TileHighlightStrengthId = Shader.PropertyToID("_TileHighlightStrength");
        private static readonly int TileVariationId = Shader.PropertyToID("_TileVariation");
        private static readonly int TileProjectionScaleId = Shader.PropertyToID("_TileProjectionScale");
        private static readonly int TileProjectionOffsetId = Shader.PropertyToID("_TileProjectionOffset");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int GapWidthId = Shader.PropertyToID("_GapWidth");
        private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
        private static readonly int EdgeDarknessId = Shader.PropertyToID("_EdgeDarkness");
        private static readonly int EdgeDarkWidthId = Shader.PropertyToID("_EdgeDarkWidth");
        private static readonly int EdgeHighlightId = Shader.PropertyToID("_EdgeHighlight");
        private static readonly int EdgeHighlightWidthId = Shader.PropertyToID("_EdgeHighlightWidth");
        private static readonly int SideBlendId = Shader.PropertyToID("_SideBlend");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
        private static readonly int LightWrapId = Shader.PropertyToID("_LightWrap");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");
        private static readonly int FlowStrengthId = Shader.PropertyToID("_FlowStrength");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int PatchStrengthId = Shader.PropertyToID("_PatchStrength");
        private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
        private static readonly int RippleStrengthId = Shader.PropertyToID("_RippleStrength");
        private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
        private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
        private static readonly int WaterBrightnessId = Shader.PropertyToID("_WaterBrightness");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SurfaceLineColorId = Shader.PropertyToID("_SurfaceLineColor");
        private static readonly int SurfaceLineStrengthId = Shader.PropertyToID("_SurfaceLineStrength");
        private static readonly int SurfaceLineOpacityId = Shader.PropertyToID("_SurfaceLineOpacity");
        private static readonly int SurfaceLineWidthId = Shader.PropertyToID("_SurfaceLineWidth");
        private static readonly int SurfaceLineScaleId = Shader.PropertyToID("_SurfaceLineScale");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
        private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");
        private static readonly int FresnelStrengthId = Shader.PropertyToID("_FresnelStrength");

        [MenuItem("CubeTD/Art/Water Layered Tile Tool")]
        public static void Open()
        {
            WaterLayeredTileAssetBuilder window = GetWindow<WaterLayeredTileAssetBuilder>();
            window.titleContent = new GUIContent("Water Tile");
            window.minSize = new Vector2(360f, 160f);
            window.Show();
        }

        [MenuItem("CubeTD/Art/Rebuild Layered Water Tile")]
        public static void RebuildWaterBatch()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture2D bedBase = GenerateTextureAsset(BedBaseTexturePath, 1024, EvaluateBedBasePixel);
            ConfigureTextureImporter(BedBaseTexturePath, TextureWrapMode.Clamp, TextureImporterAlphaSource.None, TextureImporterCompression.Uncompressed, 1024);

            Texture2D bedDetail = GenerateTextureAsset(BedDetailTexturePath, 1024, EvaluateBedDetailPixel);
            ConfigureTextureImporter(BedDetailTexturePath, TextureWrapMode.Repeat, TextureImporterAlphaSource.None, TextureImporterCompression.CompressedHQ, 1024);

            Texture2D surfaceBase = GenerateTextureAsset(SurfaceBaseTexturePath, 512, EvaluateSurfaceBasePixel);
            ConfigureTextureImporter(SurfaceBaseTexturePath, TextureWrapMode.Repeat, TextureImporterAlphaSource.None, TextureImporterCompression.CompressedHQ, 512);

            Texture2D surfaceDetail = GenerateTextureAsset(SurfaceDetailTexturePath, 512, EvaluateSurfaceDetailPixel);
            ConfigureTextureImporter(SurfaceDetailTexturePath, TextureWrapMode.Repeat, TextureImporterAlphaSource.None, TextureImporterCompression.CompressedHQ, 512);

            Material bedMaterial = LoadOrCreateMaterial(BedMaterialPath, BedShaderName);
            Material surfaceMaterial = LoadOrCreateMaterial(SurfaceMaterialPath, SurfaceShaderName);

            ApplyBedMaterial(bedMaterial, bedBase, bedDetail);
            ApplySurfaceMaterial(surfaceMaterial, surfaceBase, surfaceDetail);
            UpdateWaterPrefab(bedMaterial, surfaceMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Rebuilt layered water tile art: {WaterPrefabPath}");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Layered Water Tile", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Generates the blue-green water bed material for Topic/Base and applies the transparent water surface material to Topic/Top without changing meshes or transforms.", MessageType.Info);

            if (GUILayout.Button("Rebuild Layered Water Tile", GUILayout.Height(34f)))
            {
                RebuildWaterBatch();
            }
        }

        private static void ApplyBedMaterial(Material material, Texture2D baseTexture, Texture2D detailTexture)
        {
            material.SetTexture(BaseMapId, baseTexture);
            material.SetTexture(DetailMapId, detailTexture);
            material.SetTextureScale(BaseMapId, Vector2.one);
            material.SetTextureScale(DetailMapId, Vector2.one);
            material.SetColor(BaseColorId, new Color(0.54f, 0.92f, 1.0f, 1f));
            material.SetColor(GapColorId, Srgb(9, 83, 128));
            material.SetColor(SideColorId, Srgb(9, 83, 128));
            material.SetFloat(TextureBrightnessId, 0.92f);
            material.SetFloat(TextureContrastId, 1.08f);
            material.SetFloat(TextureSaturationId, 1.05f);
            material.SetFloat(DetailStrengthId, 0.16f);
            material.SetFloat(DetailScaleId, 1.0f);
            material.SetFloat(TileCountId, 5f);
            material.SetFloat(TileGapWidthId, 0.042f);
            material.SetFloat(TileLineStrengthId, 0.36f);
            material.SetFloat(TileHighlightStrengthId, 0.055f);
            material.SetFloat(TileVariationId, 0.16f);
            material.SetFloat(TileProjectionScaleId, 1f);
            material.SetVector(TileProjectionOffsetId, new Vector4(0.5f, 0.5f, 0f, 0f));
            material.SetFloat(GapWidthId, 0.000f);
            material.SetFloat(CornerRadiusId, 0.160f);
            material.SetFloat(EdgeDarknessId, 0.000f);
            material.SetFloat(EdgeDarkWidthId, 0.030f);
            material.SetFloat(EdgeHighlightId, 0.025f);
            material.SetFloat(EdgeHighlightWidthId, 0.115f);
            material.SetFloat(SideBlendId, 0.08f);
            material.SetFloat(AmbientId, 0.68f);
            material.SetFloat(LightStrengthId, 0.28f);
            material.SetFloat(LightWrapId, 0.54f);
            material.SetFloat(ShadowStrengthId, 0.24f);
            material.SetFloat(MaxBrightnessId, 1.02f);
            EditorUtility.SetDirty(material);
        }

        private static void ApplySurfaceMaterial(Material material, Texture2D baseTexture, Texture2D detailTexture)
        {
            material.SetTexture(BaseMapId, baseTexture);
            material.SetTexture(DetailMapId, detailTexture);
            material.SetTextureScale(BaseMapId, new Vector2(1.05f, 1.05f));
            material.SetTextureScale(DetailMapId, new Vector2(0.82f, 0.82f));
            material.SetColor(BaseColorId, new Color(0.62f, 0.94f, 1.0f, 1f));
            material.SetColor(HighlightColorId, new Color(0.90f, 1.0f, 1.0f, 1f));
            material.SetFloat(DetailStrengthId, 0.015f);
            material.SetFloat(FlowStrengthId, 0.055f);
            material.SetVector(FlowSpeedId, new Vector4(0.004f, 0.002f, -0.003f, 0.001f));
            material.SetFloat(PatchStrengthId, 0.035f);
            material.SetFloat(HighlightStrengthId, 0.075f);
            material.SetFloat(RippleStrengthId, 0.005f);
            material.SetFloat(RippleScaleId, 5.5f);
            material.SetFloat(RippleSpeedId, 0.18f);
            material.SetFloat(OpacityId, 0.115f);
            material.SetFloat(EdgeFadeId, 0.018f);
            material.SetFloat(EdgeHighlightId, 0.075f);
            material.SetFloat(FresnelPowerId, 3.2f);
            material.SetFloat(FresnelStrengthId, 0.18f);
            material.SetFloat(MaxBrightnessId, 1.14f);
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"Shader not found: {shaderName}");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureDirectory(path);
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path)
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            return material;
        }

        private static void UpdateWaterPrefab(Material bedMaterial, Material surfaceMaterial)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WaterPrefabPath);
            try
            {
                Transform topic = FindChildRecursive(prefabRoot.transform, "Topic");
                Transform bed = FindDirectChild(topic, "Base");
                Transform surface = FindDirectChild(topic, "Top");

                if (bed == null)
                {
                    Debug.LogError("Water prefab Topic/Base child was not found.");
                }
                else
                {
                    MeshRenderer[] bedRenderers = bed.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < bedRenderers.Length; i++)
                    {
                        AssignMaterialToAllSlots(bedRenderers[i], bedMaterial);
                        bedRenderers[i].shadowCastingMode = ShadowCastingMode.Off;
                        bedRenderers[i].receiveShadows = true;
                    }
                }

                if (surface == null)
                {
                    Debug.LogError("Water prefab Topic/Top child was not found.");
                }
                else
                {
                    MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
                    if (surfaceRenderer != null)
                    {
                        surfaceRenderer.sharedMaterial = surfaceMaterial;
                        surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        surfaceRenderer.receiveShadows = false;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, WaterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void AssignMaterialToAllSlots(Renderer renderer, Material material)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Texture2D GenerateTextureAsset(string path, int size, System.Func<float, float, Color32> evaluatePixel)
        {
            EnsureDirectory(path);

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    pixels[y * size + x] = evaluatePixel(u, v);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color32 EvaluateBedBasePixel(float u, float v)
        {
            Color deep = Srgb(17, 98, 112);
            Color mid = Srgb(31, 139, 148);
            Color light = Srgb(48, 161, 159);
            Color cool = Srgb(13, 118, 136);

            float broad = TileableValueNoise(u, v, 3, 701);
            float midNoise = TileableValueNoise(u, v, 7, 1709);
            float fine = TileableValueNoise(u, v, 21, 3109);
            float tone = (broad - 0.5f) * 0.42f + (midNoise - 0.5f) * 0.16f + (fine - 0.5f) * 0.035f;

            Color color = tone >= 0f
                ? Color.Lerp(mid, light, Mathf.Clamp01(tone * 1.45f))
                : Color.Lerp(mid, deep, Mathf.Clamp01(-tone * 1.65f));

            float diagonalWash = Mathf.Sin((u * 1.75f + v * 1.15f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            color = Color.Lerp(color, cool, diagonalWash * 0.045f);

            float edgeDistance = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float edgeShade = 1f - SmoothStep(0.015f, 0.18f, edgeDistance);
            color = Color.Lerp(color, deep, edgeShade * 0.055f);

            return ToColor32(color);
        }

        private static Color32 EvaluateBedDetailPixel(float u, float v)
        {
            float broad = TileableValueNoise(u, v, 5, 8803);
            float mid = TileableValueNoise(u, v, 13, 2141);
            float fine = TileableValueNoise(u, v, 37, 911);
            float value = 0.54f + (broad - 0.5f) * 0.20f + (mid - 0.5f) * 0.10f + (fine - 0.5f) * 0.035f;
            value = Mathf.Clamp(value, 0.34f, 0.72f);
            byte b = ToByte(value);
            return new Color32(b, b, b, 255);
        }

        private static Color32 EvaluateSurfaceBasePixel(float u, float v)
        {
            Color baseBlue = Srgb(120, 219, 238);
            Color lightBlue = Srgb(189, 245, 252);
            Color softBlue = Srgb(92, 199, 228);

            float lowNoise = TileableValueNoise(u, v, 4, 4049);
            float midNoise = TileableValueNoise(u, v, 9, 7559);
            float fineNoise = TileableValueNoise(u, v, 25, 9187);
            float wash = (lowNoise - 0.5f) * 0.050f + (midNoise - 0.5f) * 0.026f;
            float shimmer = Mathf.Clamp01((fineNoise - 0.66f) * 0.18f);

            Color color = Color.Lerp(baseBlue, softBlue, wash + 0.035f);
            color = Color.Lerp(color, lightBlue, shimmer);
            return ToColor32(color);
        }

        private static Color32 EvaluateSurfaceDetailPixel(float u, float v)
        {
            float broad = TileableValueNoise(u, v, 5, 2333);
            float noise = TileableValueNoise(u, v, 19, 6203);
            float drift = Mathf.Sin((u * 1.55f + v * 0.85f + broad * 0.22f) * Mathf.PI * 2f);
            float value = 0.50f + drift * 0.018f + (broad - 0.5f) * 0.035f + (noise - 0.5f) * 0.035f;
            value = Mathf.Clamp(value, 0.43f, 0.61f);
            byte b = ToByte(value);
            return new Color32(b, b, b, 255);
        }

        private static float SoftLine(float sineValue, float width)
        {
            float distance = Mathf.Abs(sineValue);
            float line = 1f - SmoothStep(width, width * 2.8f, distance);
            return line * line;
        }

        private static void ConfigureTextureImporter(
            string path,
            TextureWrapMode wrapMode,
            TextureImporterAlphaSource alphaSource,
            TextureImporterCompression compression,
            int maxTextureSize)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"TextureImporter not found: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaSource = alphaSource;
            importer.wrapMode = wrapMode;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = maxTextureSize;
            importer.textureCompression = compression;
            importer.compressionQuality = 72;
            importer.SaveAndReimport();
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
            unchecked
            {
                int wrappedX = PositiveModulo(x, frequency);
                int wrappedY = PositiveModulo(y, frequency);
                int n = wrappedX * 374761393 + wrappedY * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x00ffffff) / 16777215f;
            }
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

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                ToByte(color.r),
                ToByte(color.g),
                ToByte(color.b),
                255);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
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
