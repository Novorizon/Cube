Shader "CubeTD/Map/RoadSoftClean"
{
    Properties
    {
        _BaseMap ("Albedo Texture", 2D) = "white" {}
        _TextureBrightness ("Texture Brightness", Range(0.5, 1.5)) = 1
        _TextureContrast ("Texture Contrast", Range(0, 2)) = 1
        _TextureSaturation ("Texture Saturation", Range(0, 2)) = 1
        _EdgeDarkness ("Tile Edge Darkness", Range(0, 1)) = 0.32
        _EdgeDarkWidth ("Tile Edge Dark Width", Range(0.001, 0.2)) = 0.044
        _EdgeHighlight ("Tile Edge Highlight", Range(0, 1)) = 0.040
        _EdgeHighlightWidth ("Tile Edge Highlight Width", Range(0.001, 0.2)) = 0.076
        _Ambient ("Ambient", Range(0, 1)) = 1
        _LightStrength ("Light Strength", Range(0, 1)) = 0
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.65
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 2
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
            Name "TextureFirst"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _TextureBrightness;
                half _TextureContrast;
                half _TextureSaturation;
                half _EdgeDarkness;
                half _EdgeDarkWidth;
                half _EdgeHighlight;
                half _EdgeHighlightWidth;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 color = ApplyTextureControls(baseMap.rgb);

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                color *= light;

                half2 edgeUv = min(input.uv, 1.0h - input.uv);
                half edgeDistance = min(edgeUv.x, edgeUv.y);
                half edgeAa = max(fwidth(edgeDistance) * 1.5h, 0.001h);
                half edgeLine = 1.0h - smoothstep(0.0h, _EdgeDarkWidth * 0.42h + edgeAa, edgeDistance);
                half edgeFade = 1.0h - smoothstep(_EdgeDarkWidth * 0.42h, _EdgeDarkWidth + edgeAa, edgeDistance);
                half edgeDark = saturate(edgeFade * 0.30h + edgeLine * 0.70h);
                half edgeHighlight = smoothstep(_EdgeDarkWidth * 0.92h, _EdgeHighlightWidth, edgeDistance) *
                    (1.0h - smoothstep(_EdgeHighlightWidth, _EdgeHighlightWidth * 1.28h + edgeAa, edgeDistance));

                color *= 1.0h - edgeDark * _EdgeDarkness;
                color += color * edgeHighlight * _EdgeHighlight;
                color = min(color, _MaxBrightness.xxx);
                return half4(color, baseMap.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
