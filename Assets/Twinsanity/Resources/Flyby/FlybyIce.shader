Shader "Twinsanity/Flyby Ice"
{
    Properties { _MainTex ("Ice texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 view : TEXCOORD2; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float edge = pow(1 - saturate(abs(dot(normalize(i.normal), normalize(i.view)))), 2);
                fixed3 icy = lerp(tex.rgb * 0.85 + fixed3(0.05, 0.19, 0.24), fixed3(0.73, 0.98, 1), edge * 0.65);
                return fixed4(icy, saturate(0.22 + tex.a * 0.15 + edge * 0.30));
            }
            ENDCG
        }
    }
}
