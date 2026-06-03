Shader "CubeTD/Map/GrassTopSoftLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.35
        _Ambient ("Ambient", Range(0, 1)) = 0.58
        _LightStrength ("Light Strength", Range(0, 1)) = 0.38
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.42
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.10
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.30
        _Saturation ("Saturation", Range(0, 2)) = 1.05
        _EdgeDarkness ("Edge Darkness", Range(0, 1)) = 0.16
        _EdgeDarkWidth ("Edge Dark Width", Range(0.001, 0.2)) = 0.070
        _EdgeHighlight ("Edge Highlight", Range(0, 1)) = 0.10
        _EdgeHighlightWidth ("Edge Highlight Width", Range(0.001, 0.2)) = 0.115
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                half4 _BaseColor;
                half _NormalStrength;
                half _Ambient;
                half _LightStrength;
                half _LightWrap;
                half _MaxBrightness;
                half _ShadowStrength;
                half _Saturation;
                half _EdgeDarkness;
                half _EdgeDarkWidth;
                half _EdgeHighlight;
                half _EdgeHighlightWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                return output;
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(luminance.xxx, color, saturation);
            }

            half3 GetNormalWS(Varyings input)
            {
                half4 packedNormal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormalScale(packedNormal, _NormalStrength);
                half3x3 tangentToWorld = half3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS)
                );
                return normalize(TransformTangentToWorld(normalTS, tangentToWorld));
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                Light mainLight = GetMainLight();
                half3 normalWS = GetNormalWS(input);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.42h);
                half3 color = tex.rgb * lightColor * light;

                half topMask = saturate((normalize(input.normalWS).y - 0.25h) * 2.5h);
                half2 edgeUv = min(input.uv, 1.0h - input.uv);
                half edgeDistance = min(edgeUv.x, edgeUv.y);
                half edgeDark = 1.0h - smoothstep(0.0h, _EdgeDarkWidth, edgeDistance);
                half edgeHighlight = smoothstep(_EdgeDarkWidth * 0.45h, _EdgeHighlightWidth, edgeDistance) *
                    (1.0h - smoothstep(_EdgeHighlightWidth, _EdgeHighlightWidth * 1.45h, edgeDistance));

                color *= 1.0h - edgeDark * _EdgeDarkness * topMask;
                color += color * edgeHighlight * _EdgeHighlight * topMask;
                color = min(color, _MaxBrightness.xxx);
                color = ApplySaturation(color, _Saturation);

                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
