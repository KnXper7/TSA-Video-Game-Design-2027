// River and lake water: moving ripples, sky reflection that strengthens at shallow angles, a sun glint,
// clearer water in the shallows and a line of foam where it meets the bank.
Shader "TSA/Water"
{
    Properties
    {
        _Shallow ("Shallow colour", Color) = (0.25, 0.55, 0.5, 1)
        _Deep ("Deep colour", Color) = (0.03, 0.14, 0.2, 1)
        _Sky ("Reflected sky tint", Color) = (1, 1, 1, 1)
        _SkyTex ("Sky picture to reflect (360 panorama)", 2D) = "white" {}
        _SkyExposure ("Sky reflection brightness", Float) = 1.1
        _SunColor ("Sun glint", Color) = (1, 0.95, 0.8, 1)
        _SunDir ("Direction to the sun", Vector) = (0.3, 0.6, 0.5, 0)
        _Flow ("Flow (x, z) metres per second", Vector) = (0.6, 0.1, 0, 0)
        _Scale ("Ripple size", Float) = 0.55
        _Clarity ("Depth until opaque (m)", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Shallow, _Deep, _Sky, _SunColor;
            float4 _SunDir, _Flow;
            float _Scale, _Clarity;
            sampler2D _CameraDepthTexture, _SkyTex;
            float _SkyExposure;

            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float4 screen : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float waves(float2 p)
            {
                float2 flow = _Flow.xy * _Time.y;
                return noise((p - flow) * _Scale * 2.0) * 0.6 + noise((p - flow * 1.7 + 13.0) * _Scale * 5.0) * 0.3
                     + noise((p + flow.yx * 0.8) * _Scale * 11.0) * 0.1;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world.xz;
                float e = 0.08;
                float h = waves(p);
                float3 n = normalize(float3((h - waves(p + float2(e, 0))) * 1.1, e * 4.0, (h - waves(p + float2(0, e))) * 1.1));

                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float fresnel = pow(1.0 - saturate(dot(n, view)), 4.0);

                // How much water is in front of whatever is behind this pixel.
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float depth = max(0.0, scene - i.screen.z);
                float murk = saturate(depth / _Clarity);

                float3 body = lerp(_Shallow.rgb, _Deep.rgb, murk);
                // Mirror the real sky: look up the reflected direction in the panorama.
                float3 r = reflect(-view, n);
                r.y = abs(r.y);
                float2 skyUv = float2(atan2(r.x, r.z) / 6.2831853 + 0.5, asin(saturate(r.y)) / 3.14159265 + 0.5);
                float3 sky = tex2Dlod(_SkyTex, float4(skyUv, 0, 0)).rgb * _Sky.rgb * _SkyExposure;
                float3 col = lerp(body, sky, saturate(fresnel * 0.85 + 0.25));
                float3 sun = normalize(_SunDir.xyz);
                col += _SunColor.rgb * pow(saturate(dot(reflect(-sun, n), view)), 180.0) * 2.5;
                float foam = smoothstep(0.22, 0.0, depth) * (0.5 + 0.5 * noise(p * 6.0 - _Flow.xy * _Time.y * 2.0));
                col = lerp(col, float3(0.92, 0.95, 0.95), foam * 0.7);
                float alpha = saturate(0.12 + murk * 0.75 + fresnel * 0.6) * smoothstep(0.0, 0.05, depth);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
