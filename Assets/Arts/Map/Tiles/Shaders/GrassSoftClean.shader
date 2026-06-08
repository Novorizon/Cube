Shader "CubeTD/Map/GrassSoftClean"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Green", Color) = (0.42, 0.66, 0.11, 1)
        _DarkGreen ("Dark Green", Color) = (0.30, 0.54, 0.05, 1)
        _LightGreen ("Light Green", Color) = (0.55, 0.76, 0.16, 1)
        _VariationStrength ("Variation Strength", Range(0, 1)) = 0.18
        _VariationScale ("Variation Scale", Range(0.25, 8)) = 2.0
        _VariationSoftness ("Variation Softness", Range(0.01, 1)) = 0.42
        _Ambient ("Ambient", Range(0, 1)) = 0.76
        _LightStrength ("Light Strength", Range(0, 1)) = 0.22
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.68
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.12
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.08
        _SlopeDarkness ("Slope Darkness", Range(0, 1)) = 0.16
        _Saturation ("Saturation", Range(0, 2)) = 1.0
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DarkGreen;
                half4 _LightGreen;
                half _VariationStrength;
                half _VariationScale;
                half _VariationSoftness;
                half _Ambient;
                half _LightStrength;
                half _LightWrap;
                half _MaxBrightness;
                half _ShadowStrength;
                half _SlopeDarkness;
                half _Saturation;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half Hash(float2 p)
            {
                p = frac(p * half2(123.34h, 456.21h));
                p += dot(p, p + 45.32h);
                return frac(p.x * p.y);
            }

            half SmoothValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0h - 2.0h * f);

                half a = Hash(cell);
                half b = Hash(cell + float2(1.0h, 0.0h));
                half c = Hash(cell + float2(0.0h, 1.0h));
                half d = Hash(cell + float2(1.0h, 1.0h));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(luminance.xxx, color, saturation);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half broad = SmoothValueNoise(input.uv * _VariationScale);
                half soft = smoothstep(0.5h - _VariationSoftness * 0.5h, 0.5h + _VariationSoftness * 0.5h, broad);
                half signedVariation = (soft - 0.5h) * 2.0h;
                half3 targetGreen = lerp(_DarkGreen.rgb, _LightGreen.rgb, soft);
                half3 albedo = lerp(_BaseColor.rgb, targetGreen, abs(signedVariation) * _VariationStrength);
                albedo *= tex.rgb;

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.28h);
                half3 color = albedo * lightColor * light;
                half slope = 1.0h - saturate(normalWS.y);
                color *= 1.0h - slope * _SlopeDarkness;
                color = min(color, _MaxBrightness.xxx);
                color = ApplySaturation(color, _Saturation);

                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
