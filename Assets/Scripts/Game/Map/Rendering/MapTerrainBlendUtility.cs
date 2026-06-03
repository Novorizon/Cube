using UnityEngine;

namespace Game
{
    public static class MapTerrainBlendUtility
    {
        public const string TopicTopName = "TopicTop";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int NorthMapId = Shader.PropertyToID("_NorthMap");
        private static readonly int EastMapId = Shader.PropertyToID("_EastMap");
        private static readonly int SouthMapId = Shader.PropertyToID("_SouthMap");
        private static readonly int WestMapId = Shader.PropertyToID("_WestMap");
        private static readonly int NorthTransitionMapId = Shader.PropertyToID("_NorthTransitionMap");
        private static readonly int EastTransitionMapId = Shader.PropertyToID("_EastTransitionMap");
        private static readonly int SouthTransitionMapId = Shader.PropertyToID("_SouthTransitionMap");
        private static readonly int WestTransitionMapId = Shader.PropertyToID("_WestTransitionMap");
        private static readonly int UseNorthTransitionId = Shader.PropertyToID("_UseNorthTransition");
        private static readonly int UseEastTransitionId = Shader.PropertyToID("_UseEastTransition");
        private static readonly int UseSouthTransitionId = Shader.PropertyToID("_UseSouthTransition");
        private static readonly int UseWestTransitionId = Shader.PropertyToID("_UseWestTransition");
        private static readonly int BaseNormalMapId = Shader.PropertyToID("_BaseNormalMap");
        private static readonly int NorthNormalMapId = Shader.PropertyToID("_NorthNormalMap");
        private static readonly int EastNormalMapId = Shader.PropertyToID("_EastNormalMap");
        private static readonly int SouthNormalMapId = Shader.PropertyToID("_SouthNormalMap");
        private static readonly int WestNormalMapId = Shader.PropertyToID("_WestNormalMap");
        private static readonly int BlendNoiseId = Shader.PropertyToID("_BlendNoise");
        private static readonly int EdgeBlendWidthId = Shader.PropertyToID("_EdgeBlendWidth");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int NeighborBlendStrengthId = Shader.PropertyToID("_NeighborBlendStrength");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
        private static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
        private static readonly int LightWrapId = Shader.PropertyToID("_LightWrap");
        private static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static int applyLogCount;

        public static bool TryGetTopicTopRenderer(GameObject tileObject, out Renderer renderer)
        {
            renderer = null;
            if (tileObject == null)
            {
                return false;
            }

            Transform[] children = tileObject.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child.name != TopicTopName)
                {
                    continue;
                }

                renderer = child.GetComponent<Renderer>();
                return renderer != null;
            }

            return false;
        }

        public static void Apply(
            GameObject tileObject,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType north,
            MapTileType east,
            MapTileType south,
            MapTileType west)
        {
            if (!TryGetTopicTopRenderer(tileObject, out Renderer renderer))
            {
                return;
            }

            Apply(renderer, config, self, north, east, south, west);
        }

        public static void Apply(
            Renderer renderer,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType north,
            MapTileType east,
            MapTileType south,
            MapTileType west)
        {
            if (renderer == null || config == null || config.BlendMaterial == null)
            {
                return;
            }

            config.RebuildCache();
            Texture2D baseTexture = GetRequiredTexture(config, self);
            if (baseTexture == null)
            {
                return;
            }

            MapTerrainBlendDebugState debugState = EnsureDebugState(renderer, self, north, east, south, west, baseTexture);

            if (!Application.isPlaying)
            {
                ApplyWithPropertyBlock(renderer, config, self, north, east, south, west, baseTexture);
                FillDebugState(debugState, renderer.sharedMaterial, false, config, self, north, east, south, west, baseTexture);
                return;
            }

            Material material = GetBlendMaterial(renderer, config);
            Texture2D baseNormal = config.GetNormalTexture(self);
            bool useNormalMap = config.UseNormalMaps && baseNormal != null;

            material.SetTexture(BaseMapId, baseTexture);
            material.SetTexture(NorthMapId, GetTextureOrBase(config, north, baseTexture));
            material.SetTexture(EastMapId, GetTextureOrBase(config, east, baseTexture));
            material.SetTexture(SouthMapId, GetTextureOrBase(config, south, baseTexture));
            material.SetTexture(WestMapId, GetTextureOrBase(config, west, baseTexture));
            SetTransitionTexture(material, config, self, north, baseTexture, NorthTransitionMapId, UseNorthTransitionId);
            SetTransitionTexture(material, config, self, east, baseTexture, EastTransitionMapId, UseEastTransitionId);
            SetTransitionTexture(material, config, self, south, baseTexture, SouthTransitionMapId, UseSouthTransitionId);
            SetTransitionTexture(material, config, self, west, baseTexture, WestTransitionMapId, UseWestTransitionId);
            if (useNormalMap)
            {
                material.SetTexture(BaseNormalMapId, baseNormal);
                material.SetTexture(NorthNormalMapId, GetNormalOrBase(config, north, baseNormal));
                material.SetTexture(EastNormalMapId, GetNormalOrBase(config, east, baseNormal));
                material.SetTexture(SouthNormalMapId, GetNormalOrBase(config, south, baseNormal));
                material.SetTexture(WestNormalMapId, GetNormalOrBase(config, west, baseNormal));
            }

            if (config.BlendNoise != null) material.SetTexture(BlendNoiseId, config.BlendNoise);
            material.SetFloat(EdgeBlendWidthId, config.EdgeBlendWidth);
            material.SetFloat(NoiseStrengthId, config.NoiseStrength);
            material.SetFloat(NeighborBlendStrengthId, config.NeighborBlendStrength);
            material.SetFloat(NoiseScaleId, config.NoiseScale);
            material.SetFloat(UseNormalMapId, useNormalMap ? 1f : 0f);
            material.SetFloat(NormalStrengthId, config.NormalStrength);
            material.SetFloat(AmbientId, config.Ambient);
            material.SetFloat(LightStrengthId, config.LightStrength);
            material.SetFloat(LightWrapId, config.LightWrap);
            material.SetFloat(MaxBrightnessId, config.MaxBrightness);
            material.SetFloat(ShadowStrengthId, config.ShadowStrength);
            material.SetFloat(SaturationId, config.Saturation);

            FillDebugState(debugState, material, true, config, self, north, east, south, west, baseTexture);

            if (applyLogCount < 5)
            {
                applyLogCount++;
                Debug.Log($"[TerrainBlend] Applied runtime material. Tile={self}, N/E/S/W={north}/{east}/{south}/{west}, Base={baseTexture.name}, Material={material.name}, Renderer={renderer.name}");
            }
        }

        private static void ApplyWithPropertyBlock(
            Renderer renderer,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType north,
            MapTileType east,
            MapTileType south,
            MapTileType west,
            Texture2D baseTexture)
        {
            renderer.sharedMaterial = config.BlendMaterial;
            Texture2D baseNormal = config.GetNormalTexture(self);
            bool useNormalMap = config.UseNormalMaps && baseNormal != null;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, baseTexture);
            block.SetTexture(NorthMapId, GetTextureOrBase(config, north, baseTexture));
            block.SetTexture(EastMapId, GetTextureOrBase(config, east, baseTexture));
            block.SetTexture(SouthMapId, GetTextureOrBase(config, south, baseTexture));
            block.SetTexture(WestMapId, GetTextureOrBase(config, west, baseTexture));
            SetTransitionTexture(block, config, self, north, baseTexture, NorthTransitionMapId, UseNorthTransitionId);
            SetTransitionTexture(block, config, self, east, baseTexture, EastTransitionMapId, UseEastTransitionId);
            SetTransitionTexture(block, config, self, south, baseTexture, SouthTransitionMapId, UseSouthTransitionId);
            SetTransitionTexture(block, config, self, west, baseTexture, WestTransitionMapId, UseWestTransitionId);

            if (useNormalMap)
            {
                block.SetTexture(BaseNormalMapId, baseNormal);
                block.SetTexture(NorthNormalMapId, GetNormalOrBase(config, north, baseNormal));
                block.SetTexture(EastNormalMapId, GetNormalOrBase(config, east, baseNormal));
                block.SetTexture(SouthNormalMapId, GetNormalOrBase(config, south, baseNormal));
                block.SetTexture(WestNormalMapId, GetNormalOrBase(config, west, baseNormal));
            }

            if (config.BlendNoise != null) block.SetTexture(BlendNoiseId, config.BlendNoise);
            block.SetFloat(EdgeBlendWidthId, config.EdgeBlendWidth);
            block.SetFloat(NoiseStrengthId, config.NoiseStrength);
            block.SetFloat(NeighborBlendStrengthId, config.NeighborBlendStrength);
            block.SetFloat(NoiseScaleId, config.NoiseScale);
            block.SetFloat(UseNormalMapId, useNormalMap ? 1f : 0f);
            block.SetFloat(NormalStrengthId, config.NormalStrength);
            block.SetFloat(AmbientId, config.Ambient);
            block.SetFloat(LightStrengthId, config.LightStrength);
            block.SetFloat(LightWrapId, config.LightWrap);
            block.SetFloat(MaxBrightnessId, config.MaxBrightness);
            block.SetFloat(ShadowStrengthId, config.ShadowStrength);
            block.SetFloat(SaturationId, config.Saturation);
            renderer.SetPropertyBlock(block);
        }

        private static Material GetBlendMaterial(Renderer renderer, MapTerrainBlendConfig config)
        {
            Material current = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
            if (current != null && current.shader == config.BlendMaterial.shader && current != config.BlendMaterial)
            {
                return current;
            }

            Material material = Application.isPlaying ? new Material(config.BlendMaterial) : config.BlendMaterial;
            material.name = Application.isPlaying ? $"{config.BlendMaterial.name}_RuntimeInstance" : config.BlendMaterial.name;

            if (Application.isPlaying)
            {
                renderer.material = material;
            }
            else
            {
                renderer.sharedMaterial = material;
            }

            return material;
        }

        private static MapTerrainBlendDebugState EnsureDebugState(
            Renderer renderer,
            MapTileType self,
            MapTileType north,
            MapTileType east,
            MapTileType south,
            MapTileType west,
            Texture2D baseTexture)
        {
            MapTerrainBlendDebugState state = renderer.GetComponent<MapTerrainBlendDebugState>();
            if (state == null)
            {
                state = renderer.gameObject.AddComponent<MapTerrainBlendDebugState>();
            }

            state.Self = self;
            state.North = north;
            state.East = east;
            state.South = south;
            state.West = west;
            state.BaseTextureName = baseTexture != null ? baseTexture.name : string.Empty;
            return state;
        }

        private static void FillDebugState(
            MapTerrainBlendDebugState state,
            Material material,
            bool runtimeMaterialInstance,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType north,
            MapTileType east,
            MapTileType south,
            MapTileType west,
            Texture2D baseTexture)
        {
            if (state == null)
            {
                return;
            }

            state.Applied = true;
            state.UsedRuntimeMaterialInstance = runtimeMaterialInstance;
            state.MaterialName = material != null ? material.name : string.Empty;
            state.BaseTextureName = baseTexture != null ? baseTexture.name : string.Empty;
            state.NorthTransitionName = GetTransitionName(config, self, north);
            state.EastTransitionName = GetTransitionName(config, self, east);
            state.SouthTransitionName = GetTransitionName(config, self, south);
            state.WestTransitionName = GetTransitionName(config, self, west);
        }

        private static string GetTransitionName(MapTerrainBlendConfig config, MapTileType self, MapTileType neighbor)
        {
            Texture2D texture = config != null ? config.GetTransitionTexture(self, neighbor) : null;
            return texture != null ? texture.name : string.Empty;
        }

        private static Texture2D GetRequiredTexture(MapTerrainBlendConfig config, MapTileType type)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                return null;
            }

            return config.GetTopTexture(type);
        }

        private static Texture2D GetTextureOrBase(MapTerrainBlendConfig config, MapTileType type, Texture2D baseTexture)
        {
            Texture2D texture = GetRequiredTexture(config, type);
            return texture != null ? texture : baseTexture;
        }

        private static void SetTransitionTexture(
            Material material,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType neighbor,
            Texture2D baseTexture,
            int textureId,
            int enabledId)
        {
            Texture2D transitionTexture = null;
            if (self != neighbor && MapTileRule.IsEditableBaseTile(self) && MapTileRule.IsEditableBaseTile(neighbor))
            {
                transitionTexture = config.GetTransitionTexture(self, neighbor);
            }

            material.SetTexture(textureId, transitionTexture != null ? transitionTexture : baseTexture);
            material.SetFloat(enabledId, transitionTexture != null ? 1f : 0f);
        }

        private static void SetTransitionTexture(
            MaterialPropertyBlock block,
            MapTerrainBlendConfig config,
            MapTileType self,
            MapTileType neighbor,
            Texture2D baseTexture,
            int textureId,
            int enabledId)
        {
            Texture2D transitionTexture = null;
            if (self != neighbor && MapTileRule.IsEditableBaseTile(self) && MapTileRule.IsEditableBaseTile(neighbor))
            {
                transitionTexture = config.GetTransitionTexture(self, neighbor);
            }

            block.SetTexture(textureId, transitionTexture != null ? transitionTexture : baseTexture);
            block.SetFloat(enabledId, transitionTexture != null ? 1f : 0f);
        }

        private static Texture2D GetNormalOrBase(MapTerrainBlendConfig config, MapTileType type, Texture2D baseNormal)
        {
            if (!MapTileRule.IsEditableBaseTile(type))
            {
                return baseNormal;
            }

            Texture2D texture = config.GetNormalTexture(type);
            return texture != null ? texture : baseNormal;
        }
    }
}
