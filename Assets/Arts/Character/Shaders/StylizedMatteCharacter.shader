Shader "CubeTD/Character/StylizedMatte"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _LightStrength ("Light Strength", Range(0, 1)) = 0.48
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _Saturation ("Saturation", Range(0, 2)) = 1.04
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.08
        _RimColor ("Rim Color", Color) = (1, 0.86, 0.62, 1)
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

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
                half _ShadowStrength;
                half _Saturation;
                half _RimStrength;
                half4 _RimColor;
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
                float3 viewDirWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half gray = dot(color, half3(0.299h, 0.587h, 0.114h));
                return lerp(gray.xxx, color, saturation);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                albedo.rgb = ApplySaturation(albedo.rgb, _Saturation);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);

                half wrapped = saturate((dot(normalWS, mainLight.direction) + _LightWrap) / (1.0h + _LightWrap));
                half shadow = lerp(1.0h - _ShadowStrength, 1.0h, mainLight.shadowAttenuation);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), 3.0h) * _RimStrength;
                half3 color = albedo.rgb * light * mainLight.color + rim * _RimColor.rgb;
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
