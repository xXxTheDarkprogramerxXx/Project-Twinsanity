Shader "Project Twinsanity/Crash Opaque Double Sided"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0,3)) = 2
        _VertexColorStrength ("Vertex Color Strength", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Brightness;
            float _VertexColorStrength;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 textureColor = tex2D(_MainTex, i.uv).rgb;
                fixed3 vertexColor = lerp(fixed3(1,1,1), i.color.rgb, _VertexColorStrength);
                return fixed4(saturate(textureColor * vertexColor * _Color.rgb * _Brightness), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
