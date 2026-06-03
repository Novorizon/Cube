Shader "CubeTD/Map/TerrainBlendTop"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _NorthMap ("North Map", 2D) = "white" {}
        _EastMap ("East Map", 2D) = "white" {}
        _SouthMap ("South Map", 2D) = "white" {}
        _WestMap ("West Map", 2D) = "white" {}
        _NorthTransitionMap ("North Transition Map", 2D) = "white" {}
        _EastTransitionMap ("East Transition Map", 2D) = "white" {}
        _SouthTransitionMap ("South Transition Map", 2D) = "white" {}
        _WestTransitionMap ("West Transition Map", 2D) = "white" {}
        _UseNorthTransition ("Use North Transition", Float) = 0
        _UseEastTransition ("Use East Transition", Float) = 0
        _UseSouthTransition ("Use South Transition", Float) = 0
        _UseWestTransition ("Use West Transition", Float) = 0
        [Normal] _BaseNormalMap ("Base Normal Map", 2D) = "bump" {}
        [Normal] _NorthNormalMap ("North Normal Map", 2D) = "bump" {}
        [Normal] _EastNormalMap ("East Normal Map", 2D) = "bump" {}
        [Normal] _SouthNormalMap ("South Normal Map", 2D) = "bump" {}
        [Normal] _WestNormalMap ("West Normal Map", 2D) = "bump" {}
        _BlendNoise ("Blend Noise", 2D) = "gray" {}
        _EdgeBlendWidth ("Edge Blend Width", Range(0.01, 0.24)) = 0.1
        _NoiseStrength ("Noise Strength", Range(0, 0.35)) = 0.03
        _NeighborBlendStrength ("Neighbor Blend Strength", Range(0, 1)) = 0.45
        _NoiseScale ("Noise Scale", Float) = 1
        _UseNormalMap ("Use Normal Map", Float) = 0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _LightStrength ("Light Strength", Range(0, 1)) = 0.42
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
        _MaxBrightness ("Max Brightness", Range(0.5, 2)) = 1.08
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _Saturation ("Saturation", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
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
            TEXTURE2D(_NorthMap);
            SAMPLER(sampler_NorthMap);
            TEXTURE2D(_EastMap);
            SAMPLER(sampler_EastMap);
            TEXTURE2D(_SouthMap);
            SAMPLER(sampler_SouthMap);
            TEXTURE2D(_WestMap);
            SAMPLER(sampler_WestMap);
            TEXTURE2D(_NorthTransitionMap);
            SAMPLER(sampler_NorthTransitionMap);
            TEXTURE2D(_EastTransitionMap);
            SAMPLER(sampler_EastTransitionMap);
            TEXTURE2D(_SouthTransitionMap);
            SAMPLER(sampler_SouthTransitionMap);
            TEXTURE2D(_WestTransitionMap);
            SAMPLER(sampler_WestTransitionMap);
            TEXTURE2D(_BaseNormalMap);
            SAMPLER(sampler_BaseNormalMap);
            TEXTURE2D(_NorthNormalMap);
            SAMPLER(sampler_NorthNormalMap);
            TEXTURE2D(_EastNormalMap);
            SAMPLER(sampler_EastNormalMap);
            TEXTURE2D(_SouthNormalMap);
            SAMPLER(sampler_SouthNormalMap);
            TEXTURE2D(_WestNormalMap);
            SAMPLER(sampler_WestNormalMap);
            TEXTURE2D(_BlendNoise);
            SAMPLER(sampler_BlendNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NorthMap_ST;
                float4 _EastMap_ST;
                float4 _SouthMap_ST;
                float4 _WestMap_ST;
                float4 _NorthTransitionMap_ST;
                float4 _EastTransitionMap_ST;
                float4 _SouthTransitionMap_ST;
                float4 _WestTransitionMap_ST;
                half _UseNorthTransition;
                half _UseEastTransition;
                half _UseSouthTransition;
                half _UseWestTransition;
                float4 _BaseNormalMap_ST;
                float4 _NorthNormalMap_ST;
                float4 _EastNormalMap_ST;
                float4 _SouthNormalMap_ST;
                float4 _WestNormalMap_ST;
                float4 _BlendNoise_ST;
                half _EdgeBlendWidth;
                half _NoiseStrength;
                half _NeighborBlendStrength;
                half _NoiseScale;
                half _UseNormalMap;
                half _NormalStrength;
                half _Ambient;
                half _LightStrength;
                half _LightWrap;
                half _MaxBrightness;
                half _ShadowStrength;
                half _Saturation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                return output;
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(luminance.xxx, color, saturation);
            }

            half EdgeMask(half distanceToEdge, half noise)
            {
                half edgeWidth = min(_EdgeBlendWidth, 0.24h);
                half noiseStrength = min(_NoiseStrength, edgeWidth * 0.45h);
                half warpedDistance = distanceToEdge + (noise - 0.5h) * noiseStrength;
                return 1.0h - smoothstep(0.0h, max(0.001h, edgeWidth), warpedDistance);
            }

            half4 BlendDirection(half4 current, half4 neighbor, half mask)
            {
                return lerp(current, neighbor, saturate(mask));
            }

            half3 BlendDirectionNormal(half3 current, half3 neighbor, half mask)
            {
                return normalize(lerp(current, neighbor, saturate(mask)));
            }

            float2 RotateNorthSourceUv(float2 uv, half direction)
            {
                if (direction < 0.5h)
                {
                    return uv;
                }

                if (direction < 1.5h)
                {
                    return float2(1.0 - uv.y, uv.x);
                }

                if (direction < 2.5h)
                {
                    return float2(uv.x, 1.0 - uv.y);
                }

                return float2(uv.y, 1.0 - uv.x);
            }

            half4 SampleTransition(TEXTURE2D_PARAM(map, sampler_map), float2 uv, half direction)
            {
                return SAMPLE_TEXTURE2D(map, sampler_map, RotateNorthSourceUv(uv, direction));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                half noise = SAMPLE_TEXTURE2D(_BlendNoise, sampler_BlendNoise, input.uv * _NoiseScale).r;

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv);
                half4 north = SAMPLE_TEXTURE2D(_NorthMap, sampler_NorthMap, TRANSFORM_TEX(input.uv, _NorthMap));
                half4 east = SAMPLE_TEXTURE2D(_EastMap, sampler_EastMap, TRANSFORM_TEX(input.uv, _EastMap));
                half4 south = SAMPLE_TEXTURE2D(_SouthMap, sampler_SouthMap, TRANSFORM_TEX(input.uv, _SouthMap));
                half4 west = SAMPLE_TEXTURE2D(_WestMap, sampler_WestMap, TRANSFORM_TEX(input.uv, _WestMap));
                half4 northTransition = SampleTransition(TEXTURE2D_ARGS(_NorthTransitionMap, sampler_NorthTransitionMap), TRANSFORM_TEX(input.uv, _NorthTransitionMap), 0.0h);
                half4 eastTransition = SampleTransition(TEXTURE2D_ARGS(_EastTransitionMap, sampler_EastTransitionMap), TRANSFORM_TEX(input.uv, _EastTransitionMap), 1.0h);
                half4 southTransition = SampleTransition(TEXTURE2D_ARGS(_SouthTransitionMap, sampler_SouthTransitionMap), TRANSFORM_TEX(input.uv, _SouthTransitionMap), 2.0h);
                half4 westTransition = SampleTransition(TEXTURE2D_ARGS(_WestTransitionMap, sampler_WestTransitionMap), TRANSFORM_TEX(input.uv, _WestTransitionMap), 3.0h);

                half westMask = EdgeMask(input.uv.x, noise);
                half eastMask = EdgeMask(1.0h - input.uv.x, noise);
                half southMask = EdgeMask(input.uv.y, noise);
                half northMask = EdgeMask(1.0h - input.uv.y, noise);

                half westTransitionMask = westMask * saturate(_UseWestTransition);
                half eastTransitionMask = eastMask * saturate(_UseEastTransition);
                half southTransitionMask = southMask * saturate(_UseSouthTransition);
                half northTransitionMask = northMask * saturate(_UseNorthTransition);
                half transitionMask = westTransitionMask + eastTransitionMask + southTransitionMask + northTransitionMask;

                if (transitionMask > 0.001h)
                {
                    half4 transitionColor =
                        (westTransition * westTransitionMask +
                         eastTransition * eastTransitionMask +
                         southTransition * southTransitionMask +
                         northTransition * northTransitionMask) / transitionMask;
                    color = BlendDirection(color, transitionColor, saturate(transitionMask));
                }

                half fallbackWestMask = westMask * (1.0h - saturate(_UseWestTransition));
                half fallbackEastMask = eastMask * (1.0h - saturate(_UseEastTransition));
                half fallbackSouthMask = southMask * (1.0h - saturate(_UseSouthTransition));
                half fallbackNorthMask = northMask * (1.0h - saturate(_UseNorthTransition));
                half totalMask = fallbackWestMask + fallbackEastMask + fallbackSouthMask + fallbackNorthMask;
                if (totalMask > 0.001h)
                {
                    half4 neighborColor =
                        (west * fallbackWestMask +
                         east * fallbackEastMask +
                         south * fallbackSouthMask +
                         north * fallbackNorthMask) / totalMask;
                    color = BlendDirection(color, neighborColor, saturate(totalMask) * _NeighborBlendStrength);
                }

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                if (_UseNormalMap > 0.5h)
                {
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormalMap, sampler_BaseNormalMap, TRANSFORM_TEX(input.uv, _BaseNormalMap)), _NormalStrength);
                    half3 northNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NorthNormalMap, sampler_NorthNormalMap, TRANSFORM_TEX(input.uv, _NorthNormalMap)), _NormalStrength);
                    half3 eastNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_EastNormalMap, sampler_EastNormalMap, TRANSFORM_TEX(input.uv, _EastNormalMap)), _NormalStrength);
                    half3 southNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_SouthNormalMap, sampler_SouthNormalMap, TRANSFORM_TEX(input.uv, _SouthNormalMap)), _NormalStrength);
                    half3 westNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_WestNormalMap, sampler_WestNormalMap, TRANSFORM_TEX(input.uv, _WestNormalMap)), _NormalStrength);

                    if (totalMask > 0.001h)
                    {
                        half3 neighborNormalTS =
                            normalize((westNormalTS * fallbackWestMask +
                                       eastNormalTS * fallbackEastMask +
                                       southNormalTS * fallbackSouthMask +
                                       northNormalTS * fallbackNorthMask) / totalMask);
                        normalTS = BlendDirectionNormal(normalTS, neighborNormalTS, saturate(totalMask) * _NeighborBlendStrength);
                    }

                    half3x3 tangentToWorld = half3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS));
                    normalWS = normalize(mul(normalTS, tangentToWorld));
                }

                half ndotl = dot(normalWS, mainLight.direction);
                half wrapped = saturate((ndotl + _LightWrap) / max(0.001h, 1.0h + _LightWrap));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half light = saturate(_Ambient + wrapped * _LightStrength * shadow);
                half3 lightColor = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.45h);
                half3 lit = color.rgb * lightColor * light;
                lit = min(lit, half3(_MaxBrightness, _MaxBrightness, _MaxBrightness));
                lit = ApplySaturation(lit, _Saturation);

                return half4(lit, color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
