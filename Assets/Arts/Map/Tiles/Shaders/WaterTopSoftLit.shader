Shader "CubeTD/Map/WaterTopSoftLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _DetailMap ("Detail Map", 2D) = "gray" {}
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.22
        _Ambient ("Ambient", Range(0, 1)) = 0.62
        _LightStrength ("Light Strength", Range(0, 1)) = 0.42
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.48
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.16
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.18
        _Saturation ("Saturation", Range(0, 2)) = 1.04
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.55
        _FlowSpeed ("Flow Speed", Vector) = (0.030, 0.012, -0.018, 0.014)
        _RippleStrength ("Ripple Strength", Range(0, 0.35)) = 0.045
        _RippleScale ("Ripple Scale", Range(1, 40)) = 16
        _RippleSpeed ("Ripple Speed", Range(0, 6)) = 0.85
        _WaterBrightness ("Water Brightness", Range(0.5, 2)) = 1.12
        _Opacity ("Opacity", Range(0, 1)) = 0.28
        _SurfaceLineColor ("Surface Line Color", Color) = (0.78, 1.0, 1.0, 1.0)
        _SurfaceLineStrength ("Surface Line Strength", Range(0, 1)) = 0.32
        _SurfaceLineOpacity ("Surface Line Opacity", Range(0, 1)) = 0.16
        _SurfaceLineWidth ("Surface Line Width", Range(0.005, 0.2)) = 0.045
        _SurfaceLineScale ("Surface Line Scale", Range(0.5, 8)) = 2.4
        _EdgeDarkness ("Edge Darkness", Range(0, 1)) = 0.08
        _EdgeDarkWidth ("Edge Dark Width", Range(0.001, 0.2)) = 0.065
        _EdgeHighlight ("Edge Highlight", Range(0, 1)) = 0.18
        _EdgeHighlightWidth ("Edge Highlight Width", Range(0.001, 0.2)) = 0.12
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
                half _LightStrength;
                half _LightWrap;
                half _MaxBrightness;
                half _ShadowStrength;
                half _Saturation;
                half _FlowStrength;
                half4 _FlowSpeed;
                half _RippleStrength;
                half _RippleScale;
                half _RippleSpeed;
                half _WaterBrightness;
                half _Opacity;
                half4 _SurfaceLineColor;
                half _SurfaceLineStrength;
                half _SurfaceLineOpacity;
                half _SurfaceLineWidth;
                half _SurfaceLineScale;
                half _EdgeDarkness;
                half _EdgeDarkWidth;
                half _EdgeHighlight;
                half _EdgeHighlightWidth;
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

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(luminance.xxx, color, saturation);
            }

            half SoftWaveLine(half value, half width)
            {
                half waveLine = 1.0h - smoothstep(width, width * 2.8h, abs(value));
                return waveLine * waveLine;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half time = _Time.y;
                half2 flowA = _FlowSpeed.xy * time * _FlowStrength;
                half2 flowB = _FlowSpeed.zw * time * _FlowStrength;

                half waveA = sin((input.baseUv.x + input.baseUv.y) * _RippleScale + time * _RippleSpeed);
                half waveB = sin((input.baseUv.x - input.baseUv.y) * (_RippleScale * 0.73h) - time * (_RippleSpeed * 1.23h));
                half ripple = (waveA + waveB) * 0.5h * _RippleStrength;

                half2 baseUv = input.baseUv + flowA + ripple * 0.018h;
                half2 detailUvA = input.detailUv + flowA;
                half2 detailUvB = input.detailUv * 1.37h + flowB;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv) * _BaseColor;
                half detailA = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUvA).r;
                half detailB = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUvB).r;
                half detail = lerp(detailA, detailB, _FlowStrength * 0.5h);
                tex.rgb *= max(0.55h, 1.0h + (detail - 0.5h) * _DetailStrength * 0.42h + ripple);

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);

                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.42h);
                half3 color = tex.rgb * lightColor * light * _WaterBrightness;

                half2 surfaceUv = input.baseUv * _SurfaceLineScale;
                half drift = (detail - 0.5h) * 0.35h + ripple * 1.4h;
                half waveLineA = SoftWaveLine(sin((surfaceUv.x * 1.13h + surfaceUv.y * 0.37h + time * 0.075h + drift) * 6.28318h), _SurfaceLineWidth);
                half waveLineB = SoftWaveLine(sin((surfaceUv.x * -0.42h + surfaceUv.y * 1.08h - time * 0.052h - drift) * 6.28318h), _SurfaceLineWidth * 0.82h);
                half surfaceLineMask = saturate(waveLineA * 0.72h + waveLineB * 0.45h);
                surfaceLineMask *= saturate(1.0h - _Opacity * 0.35h);

                half topMask = saturate((normalWS.y - 0.25h) * 2.5h);
                half2 edgeUv = min(input.baseUv, 1.0h - input.baseUv);
                half edgeDistance = min(edgeUv.x, edgeUv.y);
                half edgeDark = 1.0h - smoothstep(0.0h, _EdgeDarkWidth, edgeDistance);
                half edgeHighlight = smoothstep(_EdgeDarkWidth * 0.45h, _EdgeHighlightWidth, edgeDistance) *
                    (1.0h - smoothstep(_EdgeHighlightWidth, _EdgeHighlightWidth * 1.45h, edgeDistance));

                color *= 1.0h - edgeDark * _EdgeDarkness * topMask;
                color += color * edgeHighlight * _EdgeHighlight * topMask;
                color = lerp(color, _SurfaceLineColor.rgb, surfaceLineMask * _SurfaceLineStrength * topMask);
                color = min(color, _MaxBrightness.xxx);
                color = ApplySaturation(color, _Saturation);

                half alpha = saturate(tex.a * _BaseColor.a * _Opacity);
                alpha = saturate(alpha + surfaceLineMask * _SurfaceLineOpacity * topMask + edgeHighlight * _EdgeHighlight * 0.035h * topMask);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
