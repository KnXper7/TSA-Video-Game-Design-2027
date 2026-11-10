// The swirling energy inside the portal. Put it on a quad; it draws a round, glowing whirlpool.
Shader "TSA/PortalVortex"
{
    Properties
    {
        _Color ("Colour", Color) = (0.25, 0.65, 1, 1)
        _Core ("Core colour", Color) = (1, 1, 1, 1)
        _Open ("Open", Range(0, 1)) = 1
        _Speed ("Speed", Float) = 1
        _Intensity ("Intensity", Float) = 2.2
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

            fixed4 _Color, _Core;
            float _Open, _Speed, _Intensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy * 2.0 - 1.0;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float sum = 0.0, amp = 0.5;
                for (int i = 0; i < 4; i++) { sum += noise(p) * amp; p *= 2.03; amp *= 0.5; }
                return sum;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float t = _Time.y * _Speed;
                float angle = atan2(i.uv.y, i.uv.x) + 2.4 / (r + 0.22) + t * 0.7;     // twist harder toward the middle
                float2 spun = float2(cos(angle), sin(angle));
                float arms = pow(fbm(spun * r * 3.2 + float2(0.0, -t * 0.6)), 1.6) * 2.4;
                float streaks = fbm(spun * 5.0 + r * 7.0 - t * 2.2);
                float edge = smoothstep(1.0, 0.86, r);
                float rim = smoothstep(0.62, 0.98, r);
                float core = smoothstep(0.6, 0.0, r);
                float3 col = _Color.rgb * (arms + streaks * 0.5 + rim * 1.6) + _Core.rgb * core * core * core * 1.6;
                return fixed4(col * edge * _Intensity * _Open, 1.0);
            }
            ENDCG
        }
    }
}
