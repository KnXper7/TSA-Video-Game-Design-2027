// An old CRT television in a dark room, showing 1990s home video. Put it on a full-screen UI RawImage:
// the picture is drawn on a curved 4:3 glass screen inside a plastic TV body, with colour bleed, scan lines,
// tape noise and a rolling tracking band; the rest of the frame is the dim room lit by the screen's glow.
Shader "TSA/Vhs"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Strength ("Tape wear", Range(0, 2)) = 1
        _Aspect ("Screen aspect (width / height)", Float) = 1.7778
        _ScreenHeight ("Picture height (0-1 of the frame)", Range(0.4, 1)) = 0.78
        _Curve ("Glass curvature", Range(0, 0.3)) = 0.11
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Strength, _Aspect, _ScreenHeight, _Curve;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // Distance to a box with rounded corners (negative inside).
            float roundedBox(float2 p, float2 hs, float radius)
            {
                float2 d = abs(p) - hs + radius;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }

            float3 tape(float2 uv, float t)
            {
                // Each scan line wobbles sideways a little; a tracking band rolls slowly up the picture.
                float row = floor(uv.y * 240.0);
                float wobble = (hash(float2(row, floor(t * 24.0))) - 0.5) * 0.003;
                float band = smoothstep(0.05, 0.0, abs(uv.y - frac(t * 0.09)));
                wobble += band * 0.018 * sin(uv.y * 300.0 + t * 40.0);
                uv.x += wobble * _Strength;

                // Colour bleed: red and blue sit slightly apart, and the picture is a little soft.
                float bleed = 0.0042 * _Strength;
                float3 col;
                col.r = tex2D(_MainTex, uv + float2(bleed, 0)).r;
                col.g = (tex2D(_MainTex, uv).g + tex2D(_MainTex, uv + float2(0.0016, 0)).g) * 0.5;
                col.b = tex2D(_MainTex, uv - float2(bleed, 0)).b;

                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(float3(luma, luma, luma), col, 1.2);          // tape oversaturates
                col = col * float3(1.05, 1.0, 0.9) + 0.035;              // warm cast, washed-out blacks
                col *= 0.84 + 0.16 * sin(uv.y * 480.0 * 3.14159);        // scan lines
                col += (hash(uv * float2(640.0, 480.0) + t * 61.0) - 0.5) * 0.1 * _Strength;
                col += band * 0.12;
                if (uv.y < 0.025) col = lerp(col, hash(float2(uv.x * 90.0 + t * 50.0, row)), 0.7);   // head-switch noise
                return col;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0);           // frame space: height runs -0.5..0.5
                p.y -= 0.03;                                              // the set sits a little above centre
                float2 hs = float2(_ScreenHeight * 0.5 * 4.0 / 3.0, _ScreenHeight * 0.5);
                float glass = roundedBox(p, hs, 0.06);
                float flicker = 0.94 + 0.06 * sin(t * 50.0) * sin(t * 13.0);

                float3 col;
                if (glass < 0.0)
                {
                    // On the glass: bulge the picture like a CRT tube.
                    float2 q = p / hs;
                    q *= 1.0 + _Curve * dot(q, q);
                    float2 uv = q * 0.5 + 0.5;
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) col = float3(0.01, 0.01, 0.012);
                    else col = tape(uv, t) * flicker;
                    col *= 1.0 - 0.35 * dot(q, q) * 0.5;                                  // dimmer toward the corners
                    col += float3(0.5, 0.55, 0.6) * pow(saturate(1.0 - length(q - float2(-0.55, 0.6))), 5.0) * 0.12;   // room light on the glass
                    col *= smoothstep(0.0, -0.012, glass);                                // soft dark edge where the tube meets the case
                }
                else
                {
                    // The plastic case, then the dark room.
                    float2 bodyHalf = hs + float2(0.055, 0.055);
                    float body = roundedBox(p + float2(0.0, 0.035), bodyHalf + float2(0.0, 0.035), 0.05);
                    float glow = exp(-max(glass, 0.0) * 5.5) * 0.11 * flicker;            // light spilling off the screen
                    if (body < 0.0)
                    {
                        float shade = 0.01 + 0.008 * saturate(p.y + 0.5) + 0.02 * smoothstep(0.03, 0.0, glass);
                        col = float3(shade, shade, shade * 1.05) + float3(0.5, 0.6, 0.75) * glow * 0.3;
                        // controls under the screen: a speaker grille and a power light
                        float below = -(p.y + hs.y);
                        if (below > 0.062)
                        {
                            float grille = step(0.5, frac(p.x * 70.0)) * step(abs(p.x + hs.x * 0.45), hs.x * 0.42);
                            col *= 1.0 - 0.45 * grille;
                            float led = smoothstep(0.012, 0.004, length(p - float2(hs.x * 0.82, -hs.y - 0.09)));
                            col += float3(1.0, 0.12, 0.08) * led;
                        }
                    }
                    else
                    {
                        col = float3(0.002, 0.002, 0.003) + float3(0.35, 0.45, 0.6) * glow * 0.35 * exp(-body * 3.0);
                    }
                }
                return fixed4(saturate(col), 1.0) * i.color;
            }
            ENDCG
        }
    }
}
