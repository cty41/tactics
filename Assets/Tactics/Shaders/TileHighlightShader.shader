Shader "Custom/TileHighlightShader"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseMinOpacity ("Pulse Min Opacity", Float) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _PulseSpeed;
                float _PulseMinOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 baseCol = input.color * _BaseColor;

                if (_PulseSpeed > 0.0)
                {
                    float pulse = (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5;
                    float opacityMul = lerp(_PulseMinOpacity, 1.0, pulse);
                    baseCol.a *= opacityMul;
                }

                return baseCol;
            }
            ENDHLSL
        }
    }
}
