Shader "Map/RoadTopSoftTile"
{
    Properties
    {
        _MainTex ("Reference Surface", 2D) = "white" {}
        _MudOverlay ("Patch Map", 2D) = "white" {}
        _BaseColor ("Tile Base", Color) = (0.9451, 0.8118, 0.5373, 1)
        _HighlightColor ("Soft Highlight", Color) = (0.9725, 0.8431, 0.5922, 1)
        _DarkColor ("Soft Edge Dark", Color) = (0.8588, 0.7176, 0.4627, 1)
        _BevelShadowColor ("Bevel Shadow", Color) = (0.7608, 0.5961, 0.3451, 1)
        _TextureStrength ("Texture Strength", Range(0.0, 1.0)) = 0.62
        _PatchStrength ("Patch Strength", Range(0.0, 1.0)) = 0.92
        _PatchTileScale ("Patch Tile Scale", Range(0.25, 4.0)) = 1.0
        _LightStrength ("Light Strength", Range(0.0, 1.0)) = 0.42
        _SideDarkening ("Side Darkening", Range(0.0, 2.0)) = 1.15
        _SoftSpecular ("Soft Plastic", Range(0.0, 1.0)) = 0.18
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MudOverlay;
            float4 _MudOverlay_ST;
            fixed4 _BaseColor;
            fixed4 _HighlightColor;
            fixed4 _DarkColor;
            fixed4 _BevelShadowColor;
            half _TextureStrength;
            half _PatchStrength;
            half _PatchTileScale;
            half _LightStrength;
            half _SideDarkening;
            half _SoftSpecular;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float2 patchUv : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.patchUv = v.uv2;
                o.viewDir = UnityWorldSpaceViewDir(worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(float3(-0.35, 0.78, 0.52));
                float ndl = saturate(dot(normal, lightDir));
                float topness = saturate(normal.y * 0.5 + 0.5);
                float topSurface = smoothstep(0.62, 0.90, topness);

                float3 sampled = tex2D(_MainTex, i.uv).rgb;
                float3 color = lerp(_BaseColor.rgb, sampled, _TextureStrength);
                float2 patchUv = (i.patchUv - 0.5) * max(0.001h, _PatchTileScale) + 0.5;
                patchUv = patchUv * _MudOverlay_ST.xy + _MudOverlay_ST.zw;
                float3 patch = tex2D(_MudOverlay, patchUv).rgb;
                float patchLuminance = dot(patch, float3(0.2126, 0.7152, 0.0722));
                float patchMask = saturate((1.0 - patchLuminance) * 5.4);
                float patchStrength = saturate(_PatchStrength * lerp(1.0, 1.45, _PatchStrength));
                color = lerp(color, patch, patchMask * patchStrength * topSurface);

                float softLight = lerp(0.86, 1.06, smoothstep(0.0, 1.0, ndl));
                color *= lerp(1.0, softLight, _LightStrength * (1.0 - topSurface));
                float sideMask = saturate((1.0 - topness) * 2.15) * (1.0 - topSurface * 0.85);
                float sideStrength = saturate(sideMask * _SideDarkening);
                float3 sideShadow = lerp(_DarkColor.rgb, _BevelShadowColor.rgb * 0.72, saturate(_SideDarkening));
                color = lerp(color, sideShadow, sideStrength);

                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), 18.0) * _SoftSpecular * (1.0 - topSurface);
                color = lerp(color, _HighlightColor.rgb, spec);
                color = lerp(color, _HighlightColor.rgb, _SoftSpecular * 0.035 * topSurface);

                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
