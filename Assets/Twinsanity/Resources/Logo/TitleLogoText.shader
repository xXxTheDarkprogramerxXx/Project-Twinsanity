Shader "Twinsanity/Title Logo Text"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Tint ("Letter colour", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Tint;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed brightness = saturate(dot(tex.rgb, fixed3(0.24, 0.63, 0.13)) * 3.4);
                fixed3 letters = saturate(_Tint.rgb * (0.64 + brightness * 0.36) + tex.rgb * 0.25);
                return fixed4(letters, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
