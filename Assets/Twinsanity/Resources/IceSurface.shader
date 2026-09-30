Shader "Twinsanity/Translucent Ice"
{
    Properties { _Color ("Ice tint", Color) = (0.58,0.94,0.99,0.75) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos.xyz - world;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float fresnel = pow(1 - abs(dot(normalize(i.normal), normalize(i.view))), 2);
                float light = saturate(dot(normalize(i.normal), normalize(float3(-0.4, 0.9, -0.2))) * 0.5 + 0.5);
                fixed3 rgb = _Color.rgb * (0.65 + light * 0.32) + fresnel * fixed3(0.35,0.55,0.7);
                return fixed4(rgb, saturate(_Color.a * (0.65 + fresnel * 0.6)));
            }
            ENDCG
        }
    }
}
