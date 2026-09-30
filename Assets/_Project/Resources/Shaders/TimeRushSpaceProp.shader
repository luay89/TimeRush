// Fog-free lit surface for distant space props (battle station, fighters). Lighting comes from a
// single script-provided sun direction instead of Unity's light passes, so these far objects are
// one cheap draw each and never get washed out by the track's distance fog.
// _MainTex: rgb = albedo detail, a = emissive mask (window lights / engine ports).
Shader "TimeRush/SpaceProp"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.72, 0.76, 1)
        _MainTex ("Albedo (RGB) Emission Mask (A)", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _SunDir ("Direction To Sun (world)", Vector) = (-0.5, 0.45, -0.6, 0)
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.92, 1)
        _Ambient ("Ambient", Color) = (0.035, 0.04, 0.06, 1)
        _RimColor ("Rim Color", Color) = (0.25, 0.35, 0.55, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half4 _EmissionColor;
            float4 _SunDir;
            fixed4 _SunColor;
            fixed4 _Ambient;
            fixed4 _RimColor;
            float _RimPower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 n = normalize(i.normal);
                float3 l = normalize(_SunDir.xyz);
                float ndl = dot(n, l);
                float diffuse = saturate(ndl);
                // Soft terminator so the crescent edge reads as a curved surface, not a hard cut.
                diffuse = diffuse * diffuse * (3.0 - 2.0 * diffuse);
                float rim = pow(1.0 - saturate(dot(n, normalize(i.viewDir))), _RimPower) * saturate(ndl + 0.35);

                float3 albedo = _Color.rgb * tex.rgb;
                float3 col = albedo * (_Ambient.rgb + _SunColor.rgb * diffuse) + _RimColor.rgb * rim;
                // Window lights mostly show on the night side, like a real city-lit hull.
                float nightSide = 1.0 - saturate(ndl * 2.5 + 0.2);
                col += _EmissionColor.rgb * tex.a * (0.25 + 0.75 * nightSide);
                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
