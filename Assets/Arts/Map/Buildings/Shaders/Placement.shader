Shader "CubeTD/World/Placement"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.25, 0.8, 0.35, 0.48)
        _RimColor ("Rim Color", Color) = (0.85, 1.0, 0.9, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.75
        _GridColor ("Grid Color", Color) = (1, 1, 1, 0.35)
        _GridScale ("Grid Scale", Range(0.5, 12)) = 3.0
        _GridWidth ("Grid Width", Range(0.005, 0.2)) = 0.035
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.28
        _Alpha ("Alpha", Range(0, 1)) = 0.46
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
            Name "Placement"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _GridColor;
                half _GridScale;
                half _GridWidth;
                half _GridStrength;
                half _Alpha;
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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = input.uv;
                return output;
            }

            half GridLine(half value, half width)
            {
                half cell = abs(frac(value) - 0.5h);
                return 1.0h - smoothstep(0.5h - width, 0.5h, cell);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight();

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half light = saturate(0.55h + ndotl * 0.45h);

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                half grid = max(GridLine(input.uv.x * _GridScale, _GridWidth), GridLine(input.uv.y * _GridScale, _GridWidth)) * _GridStrength;

                half3 color = _TintColor.rgb * light;
                color = lerp(color, _GridColor.rgb, grid * _GridColor.a);
                color += _RimColor.rgb * rim;

                half alpha = saturate(_TintColor.a * _Alpha + rim * 0.22h + grid * _GridColor.a);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
