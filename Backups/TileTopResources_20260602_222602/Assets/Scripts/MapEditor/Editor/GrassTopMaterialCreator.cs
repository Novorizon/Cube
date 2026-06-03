#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class GrassTopMaterialCreator
    {
        private const string SourceGrassMaterialPath = "Assets/Arts/Map/Tiles/Materials/Grass.mat";
        private const string OutputMaterialPath = "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat";
        private const string AlbedoPath = "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Albedo_Tileable_1024.png";
        private const string NormalPath = "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Normal_1024.png";
        private const string HeightPath = "Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_V2_Height_1024.png";
        private const string TestPrefabPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab";

        [MenuItem("Tools/Map Art/Reference Grass/Create Grass Top Material")]
        public static void CreateMaterialOnly()
        {
            ConfigureTextureImporters();

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D height = AssetDatabase.LoadAssetAtPath<Texture2D>(HeightPath);
            Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(SourceGrassMaterialPath);

            if (albedo == null)
            {
                Debug.LogError($"Missing Grass top albedo: {AlbedoPath}");
                return;
            }

            Shader shader = Shader.Find("CubeTD/Map/GrassTopSoftLit");
            if (shader == null && sourceMaterial != null)
            {
                shader = sourceMaterial.shader;
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("Could not find a usable shader for Grass top material.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutputMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "GrassTop_Stylized"
                };
                AssetDatabase.CreateAsset(material, OutputMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfExists(material, "_BaseMap", albedo);
            SetTextureIfExists(material, "_MainTex", albedo);
            SetTextureIfExists(material, "_NormalMap", normal);
            SetTextureIfExists(material, "_BumpMap", normal);
            SetTextureIfExists(material, "_ParallaxMap", height);

            SetColorIfExists(material, "_BaseColor", Color.white);
            SetColorIfExists(material, "_Color", Color.white);
            SetFloatIfExists(material, "_BumpScale", 0.32f);
            SetFloatIfExists(material, "_NormalStrength", 0.26f);
            SetFloatIfExists(material, "_Smoothness", 0.18f);
            SetFloatIfExists(material, "_Glossiness", 0.18f);
            SetFloatIfExists(material, "_Metallic", 0f);
            SetFloatIfExists(material, "_Saturation", 1.02f);
            SetFloatIfExists(material, "_Ambient", 0.56f);
            SetFloatIfExists(material, "_LightStrength", 0.40f);
            SetFloatIfExists(material, "_LightWrap", 0.42f);
            SetFloatIfExists(material, "_MaxBrightness", 1.08f);
            SetFloatIfExists(material, "_ShadowStrength", 0.32f);
            SetFloatIfExists(material, "_EdgeDarkness", 0.16f);
            SetFloatIfExists(material, "_EdgeDarkWidth", 0.070f);
            SetFloatIfExists(material, "_EdgeHighlight", 0.08f);
            SetFloatIfExists(material, "_EdgeHighlightWidth", 0.115f);

            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created Grass top material: {OutputMaterialPath}");
        }

        [MenuItem("Tools/Map Art/Reference Grass/Create And Assign Grass Top Material")]
        public static void CreateAndAssignMaterial()
        {
            CreateMaterialOnly();
            AssignMaterialToTestPrefab();
        }

        [MenuItem("Tools/Map Art/Reference Grass/Assign Grass Top Material To Test Prefab")]
        public static void AssignMaterialToTestPrefab()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutputMaterialPath);
            if (material == null)
            {
                Debug.LogError($"Missing material. Run Create Grass Top Material first: {OutputMaterialPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(TestPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"Missing test prefab: {TestPrefabPath}");
                return;
            }

            bool changed = false;
            Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.name != "TopBody")
                {
                    continue;
                }

                renderer.sharedMaterial = material;
                changed = true;
            }

            if (!changed)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Debug.LogError($"Could not find a TopBody renderer in prefab: {TestPrefabPath}");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, TestPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Assigned Grass top material to TopBody in: {TestPrefabPath}");
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTextureImporter(AlbedoPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(HeightPath, TextureImporterType.Default, false);
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
    }
}

#endif
