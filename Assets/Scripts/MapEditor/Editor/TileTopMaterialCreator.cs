#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopMaterialCreator
    {
        private const string ShaderName = "CubeTD/Map/TileTopSoftLit";
        private const string WaterShaderName = "CubeTD/Map/WaterTopSoftLit";

        [MenuItem("Tools/Map Art/Tile Top/Create All Tile Top Materials")]
        public static void CreateAll()
        {
            CreateGrass();
            CreateSnow();
            CreateRoad();
            CreateWater();
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Grass Top Material")]
        public static void CreateGrass()
        {
            CreateMaterial(new MaterialSpec(
                "GrassTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Normal_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_DetailOverlay_1024.png",
                new Color(0.98f, 1.02f, 0.90f, 1f),
                0.30f,
                0.22f,
                0.98f,
                0.56f,
                0.44f,
                0.42f,
                1.08f,
                0.32f,
                0.16f,
                0.12f));
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Snow Top Material")]
        public static void CreateSnow()
        {
            CreateMaterial(new MaterialSpec(
                "SnowTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/SnowTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Normal_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_DetailOverlay_1024.png",
                new Color(0.86f, 0.92f, 1.00f, 1f),
                0.22f,
                0.18f,
                0.96f,
                0.54f,
                0.34f,
                0.48f,
                0.98f,
                0.34f,
                0.16f,
                0.10f));
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Road Top Material")]
        public static void CreateRoad()
        {
            CreateMaterial(new MaterialSpec(
                "RoadTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/RoadTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Normal_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_DetailOverlay_1024.png",
                new Color(1.06f, 0.96f, 0.82f, 1f),
                0.34f,
                0.26f,
                1.04f,
                0.55f,
                0.46f,
                0.38f,
                1.08f,
                0.38f,
                0.18f,
                0.11f));
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Water Top Material")]
        public static void CreateWater()
        {
            CreateWaterMaterial(new WaterMaterialSpec(
                "WaterTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/WaterTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Water_Top_V1_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Water_Top_V1_DetailOverlay_1024.png",
                new Color(0.72f, 0.94f, 1.02f, 1f),
                0.15f,
                0.60f,
                0.34f,
                0.48f,
                1.02f,
                0.16f,
                0.92f,
                0.38f,
                new Vector4(0.018f, 0.008f, -0.012f, 0.010f),
                0.024f,
                14f,
                0.55f,
                0.94f,
                0.06f,
                0.08f,
                1.65f));
        }

        private static void CreateMaterial(MaterialSpec spec)
        {
            ConfigureTextureImporter(spec.AlbedoPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(spec.NormalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(spec.DetailPath, TextureImporterType.Default, false);

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.NormalPath);
            Texture2D detail = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.DetailPath);
            if (albedo == null)
            {
                Debug.LogError($"Missing tile top albedo: {spec.AlbedoPath}");
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Missing shader: {ShaderName}");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = spec.Name
                };
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfExists(material, "_BaseMap", albedo);
            SetTextureIfExists(material, "_NormalMap", normal);
            SetTextureIfExists(material, "_DetailMap", detail);
            SetColorIfExists(material, "_BaseColor", spec.BaseColor);
            SetFloatIfExists(material, "_NormalStrength", spec.NormalStrength);
            SetFloatIfExists(material, "_DetailStrength", spec.DetailStrength);
            SetFloatIfExists(material, "_DetailScale", 1f);
            SetFloatIfExists(material, "_Saturation", spec.Saturation);
            SetFloatIfExists(material, "_Ambient", spec.Ambient);
            SetFloatIfExists(material, "_LightStrength", spec.LightStrength);
            SetFloatIfExists(material, "_LightWrap", spec.LightWrap);
            SetFloatIfExists(material, "_MaxBrightness", spec.MaxBrightness);
            SetFloatIfExists(material, "_ShadowStrength", spec.ShadowStrength);
            SetFloatIfExists(material, "_EdgeDarkness", spec.EdgeDarkness);
            SetFloatIfExists(material, "_EdgeDarkWidth", 0.070f);
            SetFloatIfExists(material, "_EdgeHighlight", spec.EdgeHighlight);
            SetFloatIfExists(material, "_EdgeHighlightWidth", 0.115f);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created tile top material: {spec.MaterialPath}");
        }

        private static void CreateWaterMaterial(WaterMaterialSpec spec)
        {
            ConfigureTextureImporter(spec.AlbedoPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(spec.DetailPath, TextureImporterType.Default, false);

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AlbedoPath);
            Texture2D detail = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.DetailPath);
            if (albedo == null)
            {
                Debug.LogError($"Missing water top albedo: {spec.AlbedoPath}");
                return;
            }

            Shader shader = Shader.Find(WaterShaderName);
            if (shader == null)
            {
                Debug.LogError($"Missing shader: {WaterShaderName}");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = spec.Name
                };
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfExists(material, "_BaseMap", albedo);
            SetTextureIfExists(material, "_DetailMap", detail);
            material.SetTextureScale("_BaseMap", Vector2.one * spec.TextureScale);
            material.SetTextureScale("_DetailMap", Vector2.one * spec.TextureScale);
            SetColorIfExists(material, "_BaseColor", spec.BaseColor);
            SetFloatIfExists(material, "_DetailStrength", spec.DetailStrength);
            SetFloatIfExists(material, "_Ambient", spec.Ambient);
            SetFloatIfExists(material, "_LightStrength", spec.LightStrength);
            SetFloatIfExists(material, "_LightWrap", spec.LightWrap);
            SetFloatIfExists(material, "_MaxBrightness", spec.MaxBrightness);
            SetFloatIfExists(material, "_ShadowStrength", spec.ShadowStrength);
            SetFloatIfExists(material, "_Saturation", spec.Saturation);
            SetFloatIfExists(material, "_FlowStrength", spec.FlowStrength);
            if (material.HasProperty("_FlowSpeed"))
            {
                material.SetVector("_FlowSpeed", spec.FlowSpeed);
            }

            SetFloatIfExists(material, "_RippleStrength", spec.RippleStrength);
            SetFloatIfExists(material, "_RippleScale", spec.RippleScale);
            SetFloatIfExists(material, "_RippleSpeed", spec.RippleSpeed);
            SetFloatIfExists(material, "_WaterBrightness", spec.WaterBrightness);
            SetFloatIfExists(material, "_EdgeDarkness", spec.EdgeDarkness);
            SetFloatIfExists(material, "_EdgeDarkWidth", 0.065f);
            SetFloatIfExists(material, "_EdgeHighlight", spec.EdgeHighlight);
            SetFloatIfExists(material, "_EdgeHighlightWidth", 0.12f);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created water top material: {spec.MaterialPath}");
        }

        private static void ConfigureTextureImporter(string path, TextureImporterType textureType, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = textureType;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }

        private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetColorIfExists(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetFloatIfExists(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private readonly struct MaterialSpec
        {
            public readonly string Name;
            public readonly string MaterialPath;
            public readonly string AlbedoPath;
            public readonly string NormalPath;
            public readonly string DetailPath;
            public readonly Color BaseColor;
            public readonly float NormalStrength;
            public readonly float DetailStrength;
            public readonly float Saturation;
            public readonly float Ambient;
            public readonly float LightStrength;
            public readonly float LightWrap;
            public readonly float MaxBrightness;
            public readonly float ShadowStrength;
            public readonly float EdgeDarkness;
            public readonly float EdgeHighlight;

            public MaterialSpec(
                string name,
                string materialPath,
                string albedoPath,
                string normalPath,
                string detailPath,
                Color baseColor,
                float normalStrength,
                float detailStrength,
                float saturation,
                float ambient,
                float lightStrength,
                float lightWrap,
                float maxBrightness,
                float shadowStrength,
                float edgeDarkness,
                float edgeHighlight)
            {
                Name = name;
                MaterialPath = materialPath;
                AlbedoPath = albedoPath;
                NormalPath = normalPath;
                DetailPath = detailPath;
                BaseColor = baseColor;
                NormalStrength = normalStrength;
                DetailStrength = detailStrength;
                Saturation = saturation;
                Ambient = ambient;
                LightStrength = lightStrength;
                LightWrap = lightWrap;
                MaxBrightness = maxBrightness;
                ShadowStrength = shadowStrength;
                EdgeDarkness = edgeDarkness;
                EdgeHighlight = edgeHighlight;
            }
        }

        private readonly struct WaterMaterialSpec
        {
            public readonly string Name;
            public readonly string MaterialPath;
            public readonly string AlbedoPath;
            public readonly string DetailPath;
            public readonly Color BaseColor;
            public readonly float DetailStrength;
            public readonly float Ambient;
            public readonly float LightStrength;
            public readonly float LightWrap;
            public readonly float MaxBrightness;
            public readonly float ShadowStrength;
            public readonly float Saturation;
            public readonly float FlowStrength;
            public readonly Vector4 FlowSpeed;
            public readonly float RippleStrength;
            public readonly float RippleScale;
            public readonly float RippleSpeed;
            public readonly float WaterBrightness;
            public readonly float EdgeDarkness;
            public readonly float EdgeHighlight;
            public readonly float TextureScale;

            public WaterMaterialSpec(
                string name,
                string materialPath,
                string albedoPath,
                string detailPath,
                Color baseColor,
                float detailStrength,
                float ambient,
                float lightStrength,
                float lightWrap,
                float maxBrightness,
                float shadowStrength,
                float saturation,
                float flowStrength,
                Vector4 flowSpeed,
                float rippleStrength,
                float rippleScale,
                float rippleSpeed,
                float waterBrightness,
                float edgeDarkness,
                float edgeHighlight,
                float textureScale)
            {
                Name = name;
                MaterialPath = materialPath;
                AlbedoPath = albedoPath;
                DetailPath = detailPath;
                BaseColor = baseColor;
                DetailStrength = detailStrength;
                Ambient = ambient;
                LightStrength = lightStrength;
                LightWrap = lightWrap;
                MaxBrightness = maxBrightness;
                ShadowStrength = shadowStrength;
                Saturation = saturation;
                FlowStrength = flowStrength;
                FlowSpeed = flowSpeed;
                RippleStrength = rippleStrength;
                RippleScale = rippleScale;
                RippleSpeed = rippleSpeed;
                WaterBrightness = waterBrightness;
                EdgeDarkness = edgeDarkness;
                EdgeHighlight = edgeHighlight;
                TextureScale = textureScale;
            }
        }
    }
}

#endif
