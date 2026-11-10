// A thin glowing shell of air around a planet: brightest at the edge and on the sunlit side.
// Put it on a sphere a little larger than the planet.
Shader "TSA/Atmosphere"
{
    Properties
    {
        _Color ("Colour", Color) = (0.35, 0.6, 1, 1)
        _Power ("Edge sharpness", Float) = 3
        _Intensity ("Intensity", Float) = 1.6
        _SunDir ("Direction to the sun", Vector) = (0, 0.3, -1, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Power, _Intensity;
            float4 _SunDir;

            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float rim = pow(1.0 - saturate(dot(n, normalize(i.view))), _Power);
                float lit = saturate(dot(n, normalize(_SunDir.xyz)) * 0.8 + 0.35);
                return fixed4(_Color.rgb * rim * lit * _Intensity, 1.0);
            }
            ENDCG
        }
    }
}
