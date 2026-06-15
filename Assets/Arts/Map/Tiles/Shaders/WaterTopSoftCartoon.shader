Shader "CubeTD/Map/WaterTopSoftCartoon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _DetailMap ("Detail Map", 2D) = "gray" {}
        _BaseColor ("Base Color", Color) = (0.56, 0.93, 1.0, 1)
        _HighlightColor ("Highlight Color", Color) = (0.88, 1.0, 1.0, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.14
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.015
        _PatchStrength ("Soft Patch Strength", Range(0, 1)) = 0.035
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.075
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.06
        _FlowSpeed ("Flow Speed", Vector) = (0.004, 0.002, -0.003, 0.001)
        _RippleStrength ("Ripple Strength", Range(0, 0.1)) = 0.010
        _RippleScale ("Ripple Scale", Range(1, 20)) = 5.5
        _RippleSpeed ("Ripple Speed", Range(0, 2)) = 0.18
        _EdgeFade ("Edge Highlight Width", Range(0, 0.3)) = 0.018
        _EdgeHighlight ("Edge Highlight", Range(0, 1)) = 0.075
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.2
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.18
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.14
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
            Name "SoftCartoonWater"
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
                half4 _HighlightColor;
                half _Opacity;
                half _DetailStrength;
                half _PatchStrength;
                half _HighlightStrength;
                half _FlowStrength;
                half4 _FlowSpeed;
                half _RippleStrength;
                half _RippleScale;
                half _RippleSpeed;
                half _EdgeFade;
                half _EdgeHighlight;
                half _FresnelPower;
                half _FresnelStrength;
                half _MaxBrightness;
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
                float3 positionWS : TEXCOORD3;
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
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half time = _Time.y;
                half2 flowA = _FlowSpeed.xy * time * _FlowStrength;
                half2 flowB = _FlowSpeed.zw * time * _FlowStrength;

                half waveA = sin((input.baseUv.x * 1.25h + input.baseUv.y * 0.55h) * _RippleScale + time * _RippleSpeed);
                half waveB = sin((input.baseUv.x * -0.45h + input.baseUv.y * 1.10h) * (_RippleScale * 0.78h) - time * (_RippleSpeed * 0.8h));
                half softRipple = (waveA + waveB) * 0.5h * _RippleStrength;

                half2 baseUv = input.baseUv + flowA + softRipple * 0.012h;
                half2 detailUvA = input.detailUv * 0.82h + flowA;
                half2 detailUvB = input.detailUv * 1.31h + flowB;

                half3 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv).rgb;
                half detailA = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUvA).r;
                half detailB = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUvB).r;
                half detail = lerp(detailA, detailB, 0.35h);

                half softPatch = smoothstep(0.50h, 0.78h, detail);
                half highlightPatch = smoothstep(0.70h, 0.92h, detailB);
                half wideShimmer = smoothstep(0.74h, 0.96h, sin((input.baseUv.x * 0.72h + input.baseUv.y * 0.38h + time * 0.018h) * 6.28318h) * 0.5h + 0.5h);
                wideShimmer *= smoothstep(0.18h, 0.55h, detailA);

                half3 color = _BaseColor.rgb;
                color = lerp(color, baseTex * _BaseColor.rgb, _DetailStrength);
                color *= 1.0h + (softPatch - 0.5h) * _PatchStrength + softRipple * 0.55h;
                half highlightMask = saturate(highlightPatch * _HighlightStrength + wideShimmer * _HighlightStrength * 0.28h);
                color = lerp(color, _HighlightColor.rgb, highlightMask);

                Light mainLight = GetMainLight();
                color *= lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.10h);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                color = lerp(color, _HighlightColor.rgb, fresnel * _FresnelStrength);

                half2 edgeUv = min(input.baseUv, 1.0h - input.baseUv);
                half edgeDistance = min(edgeUv.x, edgeUv.y);
                half edgeHighlight = 1.0h - smoothstep(0.0h, max(_EdgeFade, 0.001h), edgeDistance);
                edgeHighlight *= edgeHighlight;
                color += _HighlightColor.rgb * edgeHighlight * _EdgeHighlight;
                color = min(color, _MaxBrightness.xxx);

                half alpha = saturate(_Opacity * (0.58h + softPatch * 0.06h + highlightPatch * 0.05h));
                alpha = saturate(alpha + edgeHighlight * 0.026h + fresnel * _FresnelStrength * 0.09h + highlightMask * 0.014h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
