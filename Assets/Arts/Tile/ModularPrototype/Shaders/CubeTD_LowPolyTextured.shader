Shader "CubeTD/LowPolyTextured"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _DetailMap ("Detail Map", 2D) = "gray" {}
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.18
        _Ambient ("Ambient", Range(0, 1)) = 0.42
        _LightSteps ("Light Steps", Range(1, 6)) = 3
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
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DetailMap_ST;
                half4 _BaseColor;
                half _DetailStrength;
                half _Ambient;
                half _LightSteps;
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
                float2 baseUv : TEXCOORD0;
                float2 detailUv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.detailUv = TRANSFORM_TEX(input.uv, _DetailMap);
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUv) * _BaseColor;
                half detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, input.detailUv).r;
                half detailFactor = lerp(1.0h, detail * 1.35h + 0.34h, _DetailStrength);
                baseSample.rgb *= detailFactor;

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half steps = max(1.0h, _LightSteps);
                half stepped = floor(ndotl * steps) / max(1.0h, steps - 1.0h);
                half light = max(_Ambient, stepped * mainLight.shadowAttenuation);

                half3 color = baseSample.rgb * mainLight.color * light;
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
