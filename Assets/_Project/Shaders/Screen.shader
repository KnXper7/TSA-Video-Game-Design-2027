// A lit-up display: shows a picture (or a live camera feed) at full brightness with faint scan lines, a slow
// refresh sweep and a little flicker, so a still image reads as a working screen.
Shader "TSA/Screen"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "black" {}
        _Brightness ("Brightness", Float) = 1.1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Lines ("Scan lines", Float) = 220
        _Phase ("Flicker offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            float _Brightness, _Lines, _Phase;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + _Phase * 7.3;
                float3 col = tex2D(_MainTex, i.uv).rgb * _Tint.rgb;
                col *= 0.9 + 0.1 * sin(i.uv.y * _Lines * 3.14159);                           // scan lines
                float sweep = smoothstep(0.06, 0.0, abs(frac(i.uv.y + t * 0.07) - 0.5));       // a band rolling up the screen
                col += col * sweep * 0.35;
                col *= 0.96 + 0.04 * sin(t * 37.0) * sin(t * 11.0);                            // mains flicker
                float2 edge = abs(i.uv * 2.0 - 1.0);
                col *= 1.0 - 0.25 * pow(max(edge.x, edge.y), 6.0);                             // darker toward the bezel
                return fixed4(col * _Brightness, 1.0);
            }
            ENDCG
        }
    }
}
