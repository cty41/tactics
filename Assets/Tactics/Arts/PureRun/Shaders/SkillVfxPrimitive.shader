Shader "Tactics/PureRun/SkillVfxPrimitive"
{
    Properties
    {
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [PerRendererData] _Tint ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _Alpha ("Alpha", Range(0,1)) = 1
        [PerRendererData] _Emission ("Emission", Range(0,8)) = 1
        [PerRendererData] _ShapeMode ("Shape Mode", Float) = 0
        [PerRendererData] _RadialInner ("Radial Inner", Range(0,1)) = 0.5
        [PerRendererData] _RadialOuter ("Radial Outer", Range(0,1)) = 1
        [PerRendererData] _Softness ("Softness", Range(0.001,0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            float4 _Tint;
            float _Alpha;
            float _Emission;
            float _ShapeMode;
            float _RadialInner;
            float _RadialOuter;
            float _Softness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float outer = 1.0 - smoothstep(
                    max(0.0, _RadialOuter - _Softness),
                    _RadialOuter,
                    radius);
                float inner = smoothstep(
                    max(0.0, _RadialInner - _Softness),
                    _RadialInner,
                    radius);
                float softDisc = outer;
                float ring = inner * outer;
                float radialAlpha = _ShapeMode < 0.5
                    ? 1.0
                    : (_ShapeMode < 1.5 ? softDisc : ring);
                float4 tint = _Tint * input.color;
                return half4(tint.rgb * (1.0 + _Emission), tint.a * _Alpha * radialAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}