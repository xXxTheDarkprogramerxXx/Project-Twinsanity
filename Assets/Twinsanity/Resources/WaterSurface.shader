Shader "Twinsanity/Moving Ocean"
{
    Properties { _Deep ("Deep blue", Color) = (0.01,0.07,0.45,1) _Bright ("Sunlit blue", Color) = (0.04,0.36,0.88,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Deep, _Bright;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float wave : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float wave = sin(p.x * 0.24 + p.z * 0.09 - t * 0.8) * 0.07 + sin(p.z * 0.44 + p.x * 0.12 + t * 1.2) * 0.035;
                v.vertex.y += wave;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = p;
                o.wave = wave;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float distant = saturate((i.world.z - 10) / 160);
                float band = sin(i.world.z * 0.17 + sin(i.world.x * 0.09 + t * 0.7) * 0.9 - t * 0.44);
                float broadLight = 0.50 + 0.19 * band + 0.12 * sin(i.world.x * 0.18 - i.world.z * 0.07 + t * 0.8);
                float softGlint = smoothstep(0.82, 0.97, band) * (1 - distant) * 0.11;
                fixed3 sea = lerp(_Deep.rgb, _Bright.rgb, saturate(broadLight * (1 - distant * 0.68) + i.wave * 0.9));
                return fixed4(sea + softGlint * fixed3(0.38, 0.62, 1), 1);
            }
            ENDCG
        }
    }
}
