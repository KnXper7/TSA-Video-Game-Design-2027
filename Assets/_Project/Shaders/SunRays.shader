// The starburst around a bright star: long thin rays of uneven length that shimmer slowly, plus a soft halo.
// Put it on a camera-facing quad (see SunGlare).
Shader "TSA/SunRays"
{
    Properties
    {
        _Color ("Colour", Color) = (0.45, 0.7, 1, 1)
        _Rays ("Ray count", Float) = 28
        _Intensity ("Intensity", Float) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Rays, _Intensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy * 2.0 - 1.0;
                return o;
            }

            float hash(float x) { return frac(sin(x * 91.7) * 43758.5453); }
            float noise(float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(hash(i), hash(i + 1.0), f); }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float a = atan2(i.uv.y, i.uv.x) / 6.2831853 + 0.5;            // 0..1 round the circle
                float t = _Time.y * 0.05;
                // Two layers of rays with different counts, so no two are alike; each ray has its own length.
                float fine = pow(noise(a * _Rays * 4.0 + t * 3.0), 6.0);
                float broad = pow(noise(a * _Rays + 40.0 - t), 3.0);
                float reach = 0.35 + 0.65 * noise(a * _Rays * 0.5 + 11.0);
                float rays = (fine * 0.7 + broad) * smoothstep(reach, 0.0, r);
                float halo = pow(saturate(1.0 - r), 3.0) * 0.5 + pow(saturate(1.0 - r * 3.0), 2.0) * 1.5;
                // The star's own surface: boiling cells of brighter and darker plasma inside the disc.
                float2 q = i.uv * 22.0;
                float boil = noise(q.x + noise(q.y + t * 9.0) * 3.0) * noise(q.y * 1.3 - t * 7.0 + noise(q.x) * 3.0);
                halo += smoothstep(0.2, 0.12, r) * (0.6 + 2.2 * boil);
                float ring = smoothstep(0.03, 0.0, abs(r - 0.42)) * 0.06;         // faint lens ring
                float edge = smoothstep(1.0, 0.8, r);
                return fixed4(_Color.rgb * (rays + halo + ring) * edge * _Intensity, 1.0);
            }
            ENDCG
        }
    }
}
