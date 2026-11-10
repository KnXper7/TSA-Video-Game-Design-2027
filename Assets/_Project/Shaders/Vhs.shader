// 1990s home-video look for a UI RawImage: colour bleed, scan lines, tape noise and a rolling tracking band.
Shader "TSA/Vhs"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Strength ("Strength", Range(0, 2)) = 1
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
            float _Strength;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

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

                col *= 0.86 + 0.14 * sin(uv.y * 480.0 * 3.14159);        // scan lines
                col += (hash(uv * float2(640.0, 480.0) + t * 61.0) - 0.5) * 0.1 * _Strength;
                col += band * 0.12;
                if (uv.y < 0.025) col = lerp(col, hash(float2(uv.x * 90.0 + t * 50.0, row)), 0.7);   // head-switch noise at the bottom

                float2 centred = i.uv * 2.0 - 1.0;
                col *= 1.0 - 0.28 * dot(centred, centred);
                return fixed4(saturate(col), 1.0) * i.color;
            }
            ENDCG
        }
    }
}
