Shader "Tactics/PureRun/GoatBodyTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Sprite Color", Color) = (1,1,1,1)
        _BodyTint ("Body Tint", Color) = (0.36,0.24,0.40,1)
        _BaseBodyColor ("Base Body Color", Color) = (0.36,0.24,0.40,1)
        _BodyThreshold ("Body Hue Threshold", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _BodyTint;
            float4 _BaseBodyColor;
            float _BodyThreshold;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half BodyMask(half3 color)
            {
                // Match the authored purple body by color distance so small
                // anti-aliased hue shifts still recolor while charcoal outlines,
                // bone horns, rust handle and steel blade stay unchanged.
                half sourceDistance = distance(color, _BaseBodyColor.rgb);
                return 1.0h - smoothstep(0.10h, 0.28h, sourceDistance);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half mask = BodyMask(source.rgb);
                half sourceLuminance = dot(source.rgb, half3(0.299h, 0.587h, 0.114h));
                half baseLuminance = max(dot(_BaseBodyColor.rgb, half3(0.299h, 0.587h, 0.114h)), 0.01h);
                half3 recoloredBody = _BodyTint.rgb * (sourceLuminance / baseLuminance);
                half3 finalRgb = lerp(source.rgb, recoloredBody, mask);
                return half4(finalRgb, source.a) * input.color;
            }
            ENDHLSL
        }
    }
}
