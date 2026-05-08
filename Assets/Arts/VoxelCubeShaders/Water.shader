// Shader "Custom/Voxel/Water"
// {
//     Properties
//     {
//         _WaterColor ("Water Color", Color) = (0.1, 0.45, 0.9, 0.55)
//         _WaveStrength ("Wave Strength", Float) = 0.035
//         _WaveSpeed ("Wave Speed", Float) = 1.5
//     }

//     SubShader
//     {
//         Tags
//         {
//             "RenderPipeline" = "UniversalPipeline"
//             "RenderType" = "Transparent"
//             "Queue" = "Transparent"
//         }

//         Blend SrcAlpha OneMinusSrcAlpha
//         ZWrite Off
//         Cull Back

//         Pass
//         {
//             Name "ForwardUnlit"

//             HLSLPROGRAM

//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float3 normalOS : NORMAL;
//             };

//             struct Varyings
//             {
//                 float4 positionHCS : SV_POSITION;
//                 float3 normalWS : TEXCOORD0;
//                 float3 positionWS : TEXCOORD1;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _WaterColor;
//                 float _WaveStrength;
//                 float _WaveSpeed;
//             CBUFFER_END

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;

//                 float3 positionOS = input.positionOS.xyz;
//                 float3 positionWS = TransformObjectToWorld(positionOS);

//                 float topMask = step(0.45, input.normalOS.y);
//                 float wave = sin(positionWS.x * 4.0 + positionWS.z * 3.0 + _Time.y * _WaveSpeed) * _WaveStrength;

//                 positionOS.y += wave * topMask;

//                 output.positionHCS = TransformObjectToHClip(positionOS);
//                 output.normalWS = TransformObjectToWorldNormal(input.normalOS);
//                 output.positionWS = TransformObjectToWorld(positionOS);

//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float3 normalWS = normalize(input.normalWS);

//                 float topMask = saturate((normalWS.y - 0.1) / 0.9);

//                 float4 color = _WaterColor;
//                 color.rgb += topMask * 0.12;

//                 float waveLine = sin(input.positionWS.x * 8.0 + input.positionWS.z * 8.0 + _Time.y * 2.0);
//                 color.rgb += waveLine * 0.025;

//                 return color;
//             }

//             ENDHLSL
//         }
//     }
// }
Shader "Custom/Voxel/WaterOpaque"
{
    Properties
    {
        _SideColor ("Side Color", Color) = (0.05, 0.28, 0.65, 1.0)
        _TopColor ("Top Color", Color) = (0.1, 0.45, 0.9, 1.0)

        _WaveStrength ("Wave Strength", Float) = 0.035
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _TopNormalThreshold ("Top Normal Threshold", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Blend Off
        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardUnlit"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SideColor;
                float4 _TopColor;
                float _WaveStrength;
                float _WaveSpeed;
                float _TopNormalThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionOS = input.positionOS.xyz;

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(positionOS);

                // 只有顶面做轻微上下波动
                float topMask = step(_TopNormalThreshold, normalWS.y);

                float wave =
                    sin(positionWS.x * 4.0 + positionWS.z * 3.0 + _Time.y * _WaveSpeed)
                    * _WaveStrength;

                positionOS.y += wave * topMask;

                output.positionHCS = TransformObjectToHClip(positionOS);
                output.normalWS = normalWS;
                output.positionWS = TransformObjectToWorld(positionOS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float topMask = step(_TopNormalThreshold, normalWS.y);

                float4 sideColor = _SideColor;
                float4 topColor = _TopColor;

                // 顶面水纹
                float waveLine =
                    sin(input.positionWS.x * 8.0 + input.positionWS.z * 8.0 + _Time.y * 2.0);

                topColor.rgb += waveLine * 0.035;
                topColor.rgb += 0.12;

                // 侧面稍微根据法线亮暗变化
                float sideLight = saturate(normalWS.y * 0.25 + 0.75);
                sideColor.rgb *= sideLight;

                float4 color = lerp(sideColor, topColor, topMask);

                // 关键：强制不透明
                color.a = 1.0;

                return color;
            }

            ENDHLSL
        }
    }
}