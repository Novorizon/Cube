using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MapTerrainBlendConfig", menuName = "Cube/Map/Terrain Blend Config")]
    public class MapTerrainBlendConfig : ScriptableObject
    {
        [Serializable]
        public class TerrainTextureItem
        {
            public MapTileType Type;
            public Texture2D TopTexture;
            public Texture2D NormalTexture;
        }

        [Serializable]
        public class TerrainTransitionTextureItem
        {
            public MapTileType FromType;
            public MapTileType ToType;
            public Texture2D EdgeTexture;
        }

        public Material BlendMaterial;
        public Texture2D BlendNoise;
        [Range(0.01f, 0.24f)]
        public float EdgeBlendWidth = 0.1f;
        [Range(0f, 0.35f)]
        public float NoiseStrength = 0.03f;
        [Range(0f, 1f)]
        public float NeighborBlendStrength = 0.45f;
        public float NoiseScale = 1f;
        public bool UseNormalMaps = false;
        [Range(0f, 2f)]
        public float NormalStrength = 1f;
        [Range(0f, 1f)]
        public float Ambient = 0.55f;
        [Range(0f, 1f)]
        public float LightStrength = 0.42f;
        [Range(0f, 1f)]
        public float LightWrap = 0.35f;
        [Range(0.5f, 2f)]
        public float MaxBrightness = 1.08f;
        [Range(0f, 1f)]
        public float ShadowStrength = 0.35f;
        [Range(0f, 2f)]
        public float Saturation = 1f;
        public List<TerrainTextureItem> Textures = new List<TerrainTextureItem>();
        public List<TerrainTransitionTextureItem> TransitionTextures = new List<TerrainTransitionTextureItem>();

        private Dictionary<MapTileType, TerrainTextureItem> textureMap;
        private Dictionary<int, TerrainTransitionTextureItem> transitionTextureMap;

        public TerrainTextureItem GetItem(MapTileType type)
        {
            if (textureMap == null)
            {
                RebuildCache();
            }

            textureMap.TryGetValue(type, out TerrainTextureItem item);
            return item;
        }

        public Texture2D GetTopTexture(MapTileType type)
        {
            TerrainTextureItem item = GetItem(type);
            return item != null ? item.TopTexture : null;
        }

        public Texture2D GetNormalTexture(MapTileType type)
        {
            TerrainTextureItem item = GetItem(type);
            return item != null ? item.NormalTexture : null;
        }

        public TerrainTransitionTextureItem GetTransitionItem(MapTileType fromType, MapTileType toType)
        {
            if (transitionTextureMap == null)
            {
                RebuildCache();
            }

            transitionTextureMap.TryGetValue(GetTransitionKey(fromType, toType), out TerrainTransitionTextureItem item);
            return item;
        }

        public Texture2D GetTransitionTexture(MapTileType fromType, MapTileType toType)
        {
            TerrainTransitionTextureItem item = GetTransitionItem(fromType, toType);
            return item != null ? item.EdgeTexture : null;
        }

        public void RebuildCache()
        {
            textureMap = new Dictionary<MapTileType, TerrainTextureItem>();
            transitionTextureMap = new Dictionary<int, TerrainTransitionTextureItem>();

            if (Textures != null)
            {
                for (int i = 0; i < Textures.Count; i++)
                {
                    TerrainTextureItem item = Textures[i];
                    if (item == null)
                    {
                        continue;
                    }

                    textureMap[item.Type] = item;
                }
            }

            if (TransitionTextures == null)
            {
                return;
            }

            for (int i = 0; i < TransitionTextures.Count; i++)
            {
                TerrainTransitionTextureItem item = TransitionTextures[i];
                if (item == null)
                {
                    continue;
                }

                transitionTextureMap[GetTransitionKey(item.FromType, item.ToType)] = item;
            }
        }

        private static int GetTransitionKey(MapTileType fromType, MapTileType toType)
        {
            return ((int)fromType << 16) ^ (int)toType;
        }
    }
}
