// Planetary rings drawn on a flat quad: many thin bands of ice and dust with gaps between them.
Shader "TSA/PlanetRing"
{
    Properties
    {
        _Color ("Colour", Color) = (0.62, 0.66, 0.78, 1)
        _Inner ("Inner edge (0-1)", Range(0, 1)) = 0.6
        _Opacity ("Opacity", Range(0, 1)) = 0.38
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
            float _Inner, _Opacity, _Bands;

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
                float alpha = inside * gap * (0.25 + 0.75 * bands) * _Opacity;
                return fixed4(_Color.rgb * (0.6 + 0.6 * bands), alpha);
            }
            ENDCG
        }
    }
}
