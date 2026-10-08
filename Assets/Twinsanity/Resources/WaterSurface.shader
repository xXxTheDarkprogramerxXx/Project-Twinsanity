Shader "Twinsanity/Moving Ocean"
{
    Properties
    {
        _Deep ("Deep blue", Color) = (0.01,0.07,0.45,1)
        _Bright ("Sunlit blue", Color) = (0.04,0.36,0.88,1)
        _Opacity ("Water opacity - looking down", Range(0,1)) = 0.28
        _GrazingOpacity ("Water opacity - horizon", Range(0,1)) = 0.88
        _FadeDistance ("Distance to opaque ocean", Float) = 140
        _WaveHeight ("Wave height multiplier", Range(0,3)) = 1
        _GlintStrength ("Surface glints", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Deep, _Bright;
            float _Opacity, _GrazingOpacity, _FadeDistance, _WaveHeight, _GlintStrength;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float wave = sin(p.x * 0.24 + p.z * 0.09 - t * 0.8) * 0.07
                           + sin(p.z * 0.44 + p.x * 0.12 + t * 1.2) * 0.035;
                // World-space height stays stable when the plane's scale changes.
                p.y += wave * _WaveHeight;
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1));
                o.world = p;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float a = i.world.x * 0.24 + i.world.z * 0.09 - t * 0.8;
                float b = i.world.z * 0.44 + i.world.x * 0.12 + t * 1.2;
                float wave = (sin(a) * 0.07 + sin(b) * 0.035) * _WaveHeight;
                // Per-pixel ripple normal; avoids depending on the giant plane's coarse grid.
                float slopeX = (cos(a) * 0.07 * 0.24 + cos(b) * 0.035 * 0.12) * _WaveHeight;
                float slopeZ = (cos(a) * 0.07 * 0.09 + cos(b) * 0.035 * 0.44) * _WaveHeight;
                float3 normal = normalize(float3(-slopeX, 1, -slopeZ));
                float3 viewDelta = _WorldSpaceCameraPos.xyz - i.world;
                float3 viewDir = viewDelta * rsqrt(max(dot(viewDelta, viewDelta), 0.00001));
                float fresnel = pow(1 - saturate(abs(dot(normal, viewDir))), 3);
                // Fade relative to the camera, not the world's Z direction.
                float distant = saturate(length(viewDelta.xz) / max(_FadeDistance, 1));
                float band = sin(i.world.z * 0.17 + sin(i.world.x * 0.09 + t * 0.7) * 0.9 - t * 0.44);
                float broadLight = 0.50 + 0.19 * band + 0.12 * sin(i.world.x * 0.18 - i.world.z * 0.07 + t * 0.8);
                float softGlint = smoothstep(0.82, 0.97, band) * (1 - distant) * _GlintStrength;
                fixed3 sea = lerp(_Deep.rgb, _Bright.rgb,
                    saturate(broadLight * (1 - distant * 0.68) + wave * 0.9));
                float opacity = lerp(saturate(_Opacity), saturate(_GrazingOpacity),
                    saturate(fresnel + distant * distant));
                return fixed4(sea + softGlint * fixed3(0.38, 0.62, 1), opacity);
            }
            ENDCG
        }
    }
}
