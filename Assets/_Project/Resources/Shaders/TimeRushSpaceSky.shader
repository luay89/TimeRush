// Procedural deep-space skybox: near-black backdrop, crisp multi-layer stars and a faint
// nebula haze. No textures, cheap enough for mobile (one hash per star layer, 3 noise octaves).
Shader "TimeRush/SpaceSky"
{
    Properties
    {
        _Tint ("Space Tint", Color) = (0.02, 0.025, 0.05, 1)
        _NebulaColorA ("Nebula Color A", Color) = (0.28, 0.12, 0.42, 1)
        _NebulaColorB ("Nebula Color B", Color) = (0.08, 0.22, 0.42, 1)
        _NebulaStrength ("Nebula Strength", Range(0, 1)) = 0.35
        _StarDensity ("Star Density", Range(0, 1)) = 0.55
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            fixed4 _NebulaColorA;
            fixed4 _NebulaColorB;
            float _NebulaStrength;
            float _StarDensity;
            float _StarBrightness;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // One star per 3D cell near the unit sphere; star centred away from cell edges so a
            // single cell lookup never clips a star.
            float starLayer(float3 dir, float scale, float threshold, float sharpness)
            {
                float3 p = dir * scale;
                float3 cell = floor(p);
                float3 rnd = hash33(cell);
                float3 starPos = cell + 0.25 + rnd * 0.5;
                float d = length(p - starPos);
                float present = step(threshold, hash13(cell + 17.0));
                float twinkle = 0.6 + 0.4 * hash13(cell + 5.0);
                return present * twinkle * saturate(1.0 - d * sharpness);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                float n = valueNoise(dir * 2.2) * 0.55 + valueNoise(dir * 4.7) * 0.3 + valueNoise(dir * 9.3) * 0.15;
                float band = saturate(1.0 - abs(dir.y + 0.15 - 0.35 * sin(dir.x * 2.1)) * 1.6);
                float neb = saturate(n * n * 1.8 * band) * _NebulaStrength;
                float3 nebula = lerp(_NebulaColorB.rgb, _NebulaColorA.rgb, saturate(n * 1.4 - 0.2)) * neb;

                float threshold = 1.0 - _StarDensity * 0.35;
                float stars = starLayer(dir, 220.0, threshold, 16.0) * 1.1
                            + starLayer(dir, 120.0, threshold + 0.05, 13.0) * 1.5
                            + starLayer(dir, 60.0, threshold + 0.12, 11.0) * 2.4;
                float3 starColor = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.92, 0.8), hash13(floor(dir * 80.0)));

                float3 col = _Tint.rgb + nebula + starColor * stars * _StarBrightness * (1.0 - neb * 0.5);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
