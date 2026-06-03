#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopMaterialCreator
    {
        private const string ShaderName = "CubeTD/Map/TileTopSoftLit";

        [MenuItem("Tools/Map Art/Tile Top/Create All Tile Top Materials")]
        public static void CreateAll()
        {
            CreateGrass();
            CreateSnow();
            CreateRoad();
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Grass Top Material")]
        public static void CreateGrass()
        {
            CreateMaterial(new MaterialSpec(
                "GrassTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Normal_1024.png",
                0.26f,
                1.02f,
                0.56f,
                0.40f,
                0.42f,
                1.08f,
                0.32f,
                0.16f,
                0.08f));
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Snow Top Material")]
        public static void CreateSnow()
        {
            CreateMaterial(new MaterialSpec(
                "SnowTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/SnowTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Normal_1024.png",
                0.18f,
                1.00f,
                0.66f,
                0.32f,
                0.48f,
                1.18f,
                0.24f,
                0.10f,
                0.12f));
        }

        [MenuItem("Tools/Map Art/Tile Top/Create Road Top Material")]
        public static void CreateRoad()
        {
            CreateMaterial(new MaterialSpec(
                "RoadTop_Stylized",
                "Assets/Arts/Map/Tiles/Materials/RoadTop_Stylized.mat",
                "Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Albedo_Tileable_1024.png",
                "Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Normal_1024.png",
                0.22f,
                1.00f,
                0.58f,
                0.38f,
                0.38f,
                1.10f,
                0.30f,
                0.13f,
                0.08f));
        }

        private static void CreateMaterial(MaterialSpec spec)
        {
            ConfigureTextureImporter(spec.AlbedoPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(spec.NormalPath, TextureImporterType.NormalMap, false);

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.NormalPath);
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
            SetColorIfExists(material, "_BaseColor", Color.white);
            SetFloatIfExists(material, "_NormalStrength", spec.NormalStrength);
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
            public readonly float NormalStrength;
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
                float normalStrength,
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
                NormalStrength = normalStrength;
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
    }
}

#endif
