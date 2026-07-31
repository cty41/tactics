Shader "Tactics/PureRun/GlowOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            VertexToFragment Vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, input.uv).a * input.color.a;
                return fixed4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}