Shader "Custom/Voxel/Snow"
{
    Properties
    {
        _SnowColor ("Snow Color", Color) = (0.95, 0.97, 1.0, 1)
        _SideColor ("Side Color", Color) = (0.58, 0.65, 0.70, 1)
        _DarkColor ("Dark Side Color", Color) = (0.35, 0.42, 0.48, 1)
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
                float4 _SnowColor;
                float4 _SideColor;
                float4 _DarkColor;
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

                float topMask = saturate((normalWS.y - 0.15) / 0.85);
                float bottomMask = saturate((-normalWS.y - 0.25) / 0.75);

                float4 color = lerp(_SideColor, _SnowColor, topMask);
                color = lerp(color, _DarkColor, bottomMask * 0.7);

                float sparkle = frac(input.positionWS.x * 13.7 + input.positionWS.z * 9.3);
                color.rgb += step(0.96, sparkle) * topMask * 0.08;

                float3 lightDir = normalize(float3(0.4, 0.8, 0.3));
                float light = saturate(dot(normalWS, lightDir)) * 0.3 + 0.7;

                color.rgb *= light;
                return color;
            }

            ENDHLSL
        }
    }
}
