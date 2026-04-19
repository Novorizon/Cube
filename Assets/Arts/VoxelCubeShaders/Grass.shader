Shader "Custom/Voxel/Grass"
{
    Properties
    {
        _TopColor ("Top Grass Color", Color) = (0.25, 0.75, 0.25, 1)
        _SideColor ("Side Dirt Color", Color) = (0.42, 0.28, 0.12, 1)
        _BottomColor ("Bottom Color", Color) = (0.20, 0.13, 0.07, 1)
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _SideColor;
                float4 _BottomColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float topMask = saturate((normalWS.y - 0.25) / 0.75);
                float bottomMask = saturate((-normalWS.y - 0.25) / 0.75);

                float4 color = lerp(_SideColor, _TopColor, topMask);
                color = lerp(color, _BottomColor, bottomMask);

                float3 lightDir = normalize(float3(0.4, 0.8, 0.3));
                float light = saturate(dot(normalWS, lightDir)) * 0.35 + 0.65;

                color.rgb *= light;
                return color;
            }

            ENDHLSL
        }
    }
}
