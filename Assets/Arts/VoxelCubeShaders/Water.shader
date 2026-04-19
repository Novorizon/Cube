Shader "Custom/Voxel/Water"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.1, 0.45, 0.9, 0.55)
        _WaveStrength ("Wave Strength", Float) = 0.035
        _WaveSpeed ("Wave Speed", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
                float4 _WaterColor;
                float _WaveStrength;
                float _WaveSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionOS = input.positionOS.xyz;
                float3 positionWS = TransformObjectToWorld(positionOS);

                float topMask = step(0.45, input.normalOS.y);
                float wave = sin(positionWS.x * 4.0 + positionWS.z * 3.0 + _Time.y * _WaveSpeed) * _WaveStrength;

                positionOS.y += wave * topMask;

                output.positionHCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(positionOS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float topMask = saturate((normalWS.y - 0.1) / 0.9);

                float4 color = _WaterColor;
                color.rgb += topMask * 0.12;

                float waveLine = sin(input.positionWS.x * 8.0 + input.positionWS.z * 8.0 + _Time.y * 2.0);
                color.rgb += waveLine * 0.025;

                return color;
            }

            ENDHLSL
        }
    }
}
