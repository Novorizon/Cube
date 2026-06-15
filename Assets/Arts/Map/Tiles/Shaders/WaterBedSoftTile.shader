Shader "CubeTD/Map/WaterBedSoftTile"
{
    Properties
    {
        _BaseMap ("Bed Albedo", 2D) = "white" {}
        _DetailMap ("Bed Detail", 2D) = "gray" {}
        _BaseColor ("Bed Color", Color) = (0.16, 0.62, 0.66, 1)
        _GapColor ("Groove Color", Color) = (0.04, 0.29, 0.34, 1)
        _SideColor ("Side Color", Color) = (0.04, 0.24, 0.28, 1)
        _TextureBrightness ("Texture Brightness", Range(0.5, 1.5)) = 0.9
        _TextureContrast ("Texture Contrast", Range(0, 2)) = 0.95
        _TextureSaturation ("Texture Saturation", Range(0, 2)) = 0.9
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.12
        _DetailScale ("Detail Scale", Range(0.25, 8)) = 1.0
        _TileCount ("Bed Tile Count", Range(1, 12)) = 5
        _TileGapWidth ("Bed Tile Groove Width", Range(0, 0.18)) = 0.040
        _TileLineStrength ("Bed Tile Groove Strength", Range(0, 1)) = 0.34
        _TileHighlightStrength ("Bed Tile Highlight Strength", Range(0, 1)) = 0.08
        _TileVariation ("Bed Tile Variation", Range(0, 1)) = 0.18
        _TileProjectionScale ("Bed Tile Projection Scale", Range(0.1, 20)) = 1
        _TileProjectionOffset ("Bed Tile Projection Offset", Vector) = (0.5, 0.5, 0, 0)
        _GapWidth ("Edge Groove Width", Range(0, 0.12)) = 0.030
        _CornerRadius ("Corner Softness", Range(0, 0.24)) = 0.105
        _EdgeDarkness ("Tile Edge Darkness", Range(0, 1)) = 0.24
        _EdgeDarkWidth ("Tile Edge Dark Width", Range(0.001, 0.2)) = 0.068
        _EdgeHighlight ("Tile Edge Highlight", Range(0, 1)) = 0.060
        _EdgeHighlightWidth ("Tile Edge Highlight Width", Range(0.001, 0.2)) = 0.120
        _SideBlend ("Side Blend", Range(0, 1)) = 0.78
        _Ambient ("Ambient", Range(0, 1)) = 0.66
        _LightStrength ("Light Strength", Range(0, 1)) = 0.30
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.52
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.26
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "WaterBed"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailMap_ST;
                half4 _BaseColor;
                half4 _GapColor;
                half4 _SideColor;
                half _TextureBrightness;
                half _TextureContrast;
                half _TextureSaturation;
                half _DetailStrength;
                half _DetailScale;
                half _TileCount;
                half _TileGapWidth;
                half _TileLineStrength;
                half _TileHighlightStrength;
                half _TileVariation;
                half _TileProjectionScale;
                half4 _TileProjectionOffset;
                half _GapWidth;
                half _CornerRadius;
                half _EdgeDarkness;
                half _EdgeDarkWidth;
                half _EdgeHighlight;
                half _EdgeHighlightWidth;
                half _SideBlend;
                half _Ambient;
                half _LightStrength;
                half _LightWrap;
                half _ShadowStrength;
                half _MaxBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half3 ApplyTextureControls(half3 color)
            {
                color *= _TextureBrightness;
                color = (color - 0.5h) * _TextureContrast + 0.5h;

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(luminance.xxx, color, _TextureSaturation);
                return saturate(color);
            }

            half3 ApplyLighting(half3 color, half3 normalWS)
            {
                Light mainLight = GetMainLight();
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);
                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.36h);
                return color * lightColor * light;
            }

            half TileHash(half2 value)
            {
                return frac(sin(dot(value, half2(12.989h, 78.233h))) * 43758.545h);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                float2 detailUv = TRANSFORM_TEX(input.uv, _DetailMap) * _DetailScale;
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv);
                half detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUv).r;

                half3 normalWS = normalize(input.normalWS);
                half3 baseColor = ApplyTextureControls(baseMap.rgb * _BaseColor.rgb);
                baseColor *= 1.0h + (detail - 0.5h) * _DetailStrength;

                half2 bedUv = frac(input.positionOS.xz * _TileProjectionScale + _TileProjectionOffset.xy);
                half2 gridUv = bedUv * max(_TileCount, 1.0h);
                half2 cellId = floor(gridUv);
                half2 cellUv = frac(gridUv);
                half2 cellEdgeUv = min(cellUv, 1.0h - cellUv);
                half cellEdgeDistance = min(cellEdgeUv.x, cellEdgeUv.y);
                half cellAa = max(fwidth(cellEdgeDistance) * 1.5h, 0.001h);
                half tileGroove = 1.0h - smoothstep(_TileGapWidth, _TileGapWidth + cellAa * 2.0h, cellEdgeDistance);
                tileGroove *= step(0.001h, _TileGapWidth);
                half tileBevel = smoothstep(_TileGapWidth * 1.25h, _TileGapWidth * 3.2h + cellAa, cellEdgeDistance) *
                    (1.0h - smoothstep(0.36h, 0.50h, cellEdgeDistance));
                half tileTone = (TileHash(cellId) - 0.5h) * _TileVariation;
                baseColor *= 1.0h + tileTone;
                baseColor = lerp(baseColor, _GapColor.rgb, tileGroove * _TileLineStrength);
                baseColor += baseColor * tileBevel * _TileHighlightStrength * (1.0h - tileGroove);

                half2 tileUv = bedUv;
                half2 edgeUv = min(tileUv, 1.0h - tileUv);
                half edgeDistance = min(edgeUv.x, edgeUv.y);
                half edgeAa = max(fwidth(edgeDistance) * 1.5h, 0.001h);
                half cornerDistance = length(edgeUv);
                half cornerFade = smoothstep(_CornerRadius * 0.30h, max(_CornerRadius, 0.001h), cornerDistance);
                half cornerAttenuation = lerp(0.12h, 1.0h, cornerFade);
                half grooveEnabled = step(0.001h, _GapWidth);
                half borderGroove = 1.0h - smoothstep(_GapWidth * 0.35h, _GapWidth + edgeAa * 2.5h, edgeDistance);
                borderGroove *= grooveEnabled;
                borderGroove *= cornerAttenuation;
                half edgeCore = 1.0h - smoothstep(0.0h, _EdgeDarkWidth + edgeAa, edgeDistance);
                half bevel = 1.0h - smoothstep(0.0h, _EdgeHighlightWidth + edgeAa, edgeDistance);
                half edgeDark = edgeCore * edgeCore;
                half edgeHighlight = bevel * (1.0h - bevel) * 4.0h;
                edgeDark *= cornerAttenuation;

                half3 topColor = lerp(baseColor, _GapColor.rgb, borderGroove * 0.22h);
                topColor *= 1.0h - edgeDark * _EdgeDarkness;
                topColor += topColor * edgeHighlight * _EdgeHighlight * cornerAttenuation;

                half3 sideColor = lerp(baseColor, _SideColor.rgb, _SideBlend);
                sideColor *= 0.98h + (detail - 0.5h) * 0.035h;

                half topMask = smoothstep(0.28h, 0.72h, normalWS.y);
                half3 color = lerp(sideColor, topColor, topMask);
                color = ApplyLighting(color, normalWS);
                color = min(color, _MaxBrightness.xxx);

                return half4(color, baseMap.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
