// A simple sky: one colour overhead fading to another at the horizon, with a hazy glow around the sun.
Shader "TSA/GradientSky"
{
    Properties
    {
        _Top ("Overhead", Color) = (0.42, 0.3, 0.22, 1)
        _Horizon ("Horizon", Color) = (0.8, 0.62, 0.44, 1)
        _Bottom ("Below horizon", Color) = (0.5, 0.38, 0.27, 1)
        _Power ("Fade", Float) = 0.55
        _SunColor ("Sun glow", Color) = (1, 0.8, 0.55, 1)
        _SunDir ("Direction to the sun", Vector) = (0, 0.5, 0.8, 0)
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

            fixed4 _Top, _Horizon, _Bottom, _SunColor;
            float4 _SunDir;
            float _Power;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 col = dir.y > 0.0 ? lerp(_Horizon.rgb, _Top.rgb, pow(dir.y, _Power)) : lerp(_Horizon.rgb, _Bottom.rgb, pow(-dir.y, 0.5));
                float towardSun = saturate(dot(dir, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(towardSun, 8.0) * 0.25 + pow(towardSun, 400.0) * 1.5);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
