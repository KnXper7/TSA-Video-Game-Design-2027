// Planetary rings drawn on a flat quad: many thin bands of ice and dust with gaps between them.
Shader "TSA/PlanetRing"
{
    Properties
    {
        _Color ("Colour", Color) = (0.78, 0.84, 0.96, 1)
        _SunDir ("Direction to the sun", Vector) = (0, 0.3, -1, 0)
        _PlanetRadius ("Planet radius (0-1 of the ring)", Float) = 0.4
        _Inner ("Inner edge (0-1)", Range(0, 1)) = 0.5
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _Bands ("Band count", Float) = 110
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Inner, _Opacity, _Bands, _PlanetRadius;
            float4 _SunDir;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy * 2.0 - 1.0;
                return o;
            }

            float hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float noise(float x) { float i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f); return lerp(hash(i), hash(i + 1.0), f); }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float k = (r - _Inner) / (1.0 - _Inner);                       // 0 at the inner edge, 1 at the outer
                float inside = smoothstep(0.0, 0.03, k) * smoothstep(1.0, 0.94, k);
                float bands = noise(k * _Bands) * 0.6 + noise(k * _Bands * 4.3) * 0.4;
                float gap = smoothstep(0.02, 0.05, abs(k - 0.62));             // one clear gap, like Saturn's
                float fineBands = noise(k * _Bands * 13.0) * 0.5 + 0.5;
                float alpha = inside * gap * (0.25 + 0.75 * bands) * fineBands * _Opacity;

                // The planet's shadow falls across the rings on the side away from the sun.
                float3 sun = normalize(mul((float3x3)unity_WorldToObject, _SunDir.xyz));
                float3 p = float3(i.uv, 0.0);
                float along = dot(p, sun);
                float off = length(p - along * sun);
                float shadow = along < 0.0 ? smoothstep(_PlanetRadius * 0.92, _PlanetRadius * 1.02, off) : 1.0;

                // Brighter where the ice scatters light back toward the sun side.
                float lit = 0.75 + 0.5 * saturate(dot(normalize(p + 0.0001), sun));
                return fixed4(_Color.rgb * (0.85 + 1.0 * bands) * lit * lerp(0.06, 1.0, shadow), alpha);
            }
            ENDCG
        }
    }
}
