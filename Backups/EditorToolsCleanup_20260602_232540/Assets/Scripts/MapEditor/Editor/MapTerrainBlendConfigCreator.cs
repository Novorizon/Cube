#if UNITY_EDITOR

using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class MapTerrainBlendConfigCreator
    {
        private const string ConfigPath = "Assets/Data/Cube/Configs/MapTerrainBlendConfig.asset";
        private const string MaterialPath = "Assets/Arts/Map/Tiles/Materials/TerrainBlendTop.mat";
        private const string ShaderName = "CubeTD/Map/TerrainBlendTop";
        private const string TextureRoot = "Assets/Arts/Map/Tiles/Textures/toshader";
        private const string TransitionTextureRoot = "Assets/Arts/Map/Tiles/Textures/Transitions";

        [MenuItem("Tools/Map/Create Terrain Blend Test Config")]
        public static void CreateOrUpdateTestConfig()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Terrain blend shader not found: {ShaderName}");
                return;
            }

            EnsureDirectory(Path.GetDirectoryName(MaterialPath));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            EnsureDirectory(Path.GetDirectoryName(ConfigPath));
            MapTerrainBlendConfig config = AssetDatabase.LoadAssetAtPath<MapTerrainBlendConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MapTerrainBlendConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.BlendMaterial = material;
            config.BlendNoise = LoadTexture("BlendNoise_procedural_seamless_512.png", false);
            config.EdgeBlendWidth = 0.1f;
            config.NoiseStrength = 0.03f;
            config.NeighborBlendStrength = 0.45f;
            config.NoiseScale = 1f;
            config.UseNormalMaps = false;
            config.NormalStrength = 1f;

            if (config.Textures == null)
            {
                config.Textures = new List<MapTerrainBlendConfig.TerrainTextureItem>();
            }
            else
            {
                config.Textures.Clear();
            }
            AddItem(config, MapTileType.Grass, "Grass");
            AddItem(config, MapTileType.Hill, "Hill");
            AddItem(config, MapTileType.Snow, "Snow");
            AddItem(config, MapTileType.Water, "Water");

            if (config.TransitionTextures == null)
            {
                config.TransitionTextures = new List<MapTerrainBlendConfig.TerrainTransitionTextureItem>();
            }
            else
            {
                config.TransitionTextures.Clear();
            }

            AddTransitionItem(config, MapTileType.Grass, MapTileType.Water, "GrassToWater_Edge_South_1024.png");
            AddTransitionItem(config, MapTileType.Grass, MapTileType.Snow, "GrassToSnow_Edge_South_1024.png");
            AddTransitionItem(config, MapTileType.Grass, MapTileType.Hill, "GrassToHill_Edge_South_1024.png");

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"Terrain blend config created/updated: {ConfigPath}");
        }

        private static void AddItem(MapTerrainBlendConfig config, MapTileType type, string filePrefix)
        {
            config.Textures.Add(new MapTerrainBlendConfig.TerrainTextureItem
            {
                Type = type,
                TopTexture = LoadTexture($"{filePrefix}_Top_procedural_seamless_1024.png", false),
                NormalTexture = LoadTexture($"{filePrefix}_Normal_procedural_seamless_1024.png", true)
            });
        }

        private static void AddTransitionItem(MapTerrainBlendConfig config, MapTileType fromType, MapTileType toType, string fileName)
        {
            config.TransitionTextures.Add(new MapTerrainBlendConfig.TerrainTransitionTextureItem
            {
                FromType = fromType,
                ToType = toType,
                EdgeTexture = LoadTextureAtPath($"{TransitionTextureRoot}/{fileName}", false)
            });
        }

        private static Texture2D LoadTexture(string fileName, bool normalMap)
        {
            return LoadTextureAtPath($"{TextureRoot}/{fileName}", normalMap);
        }

        private static Texture2D LoadTextureAtPath(string path, bool normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                TextureImporterType targetType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != targetType)
                {
                    importer.textureType = targetType;
                    importer.SaveAndReimport();
                }
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"Terrain blend texture missing: {path}");
            }

            return texture;
        }

        private static void EnsureDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
        }
    }
}

#endif
