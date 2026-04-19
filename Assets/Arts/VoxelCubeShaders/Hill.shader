Shader "Custom/Voxel/Hill"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.55, 0.45, 0.22, 1)
        _SideColor ("Side Color", Color) = (0.38, 0.30, 0.18, 1)
        _RockColor ("Rock Color", Color) = (0.45, 0.43, 0.38, 1)
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
                float4 _TopColor;
                float4 _SideColor;
                float4 _RockColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float topMask = saturate((normalWS.y - 0.2) / 0.8);

                float pattern = frac(input.positionWS.x * 1.7 + input.positionWS.z * 2.3 + input.positionWS.y * 0.6);
                float rockMask = step(0.62, pattern) * (1.0 - topMask * 0.5);

                float4 color = lerp(_SideColor, _TopColor, topMask);
                color = lerp(color, _RockColor, rockMask * 0.45);

                float3 lightDir = normalize(float3(0.4, 0.8, 0.3));
                float light = saturate(dot(normalWS, lightDir)) * 0.4 + 0.6;

                color.rgb *= light;
                return color;
            }

            ENDHLSL
        }
    }
}
