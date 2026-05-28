Shader "CubeTD/SoftLitTexture"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _LightStrength ("Light Strength", Range(0, 1)) = 0.42
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.08
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
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
                half _Ambient;
                half _LightStrength;
                half _LightWrap;
                half _MaxBrightness;
                half _ShadowStrength;
                half _Saturation;
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
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(luminance.xxx, color, saturation);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.45h);
                half3 maxBrightness = half3(_MaxBrightness, _MaxBrightness, _MaxBrightness);
                half3 color = tex.rgb * lightColor * light;
                color = min(color, maxBrightness);
                color = ApplySaturation(color, _Saturation);

                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
