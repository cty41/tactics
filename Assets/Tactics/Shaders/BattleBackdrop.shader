Shader "Tactics/Battle/BackdropGradient"
{
    Properties
    {
        [MainColor] _CenterColor ("Center Color", Color) = (0.0706, 0.1961, 0.2784, 1)
        _EdgeColor ("Edge Color", Color) = (0.0235, 0.0627, 0.0824, 1)
        _BottomColor ("Bottom Color", Color) = (0.0353, 0.1373, 0.1882, 1)
        _CenterOffset ("Center Offset", Vector) = (0, 0.02, 0, 0)
        _EllipseRadius ("Ellipse Radius", Vector) = (0.62, 0.55, 0, 0)
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.45
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.015
        _NoiseScale ("Noise Scale", Range(1, 20)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CenterColor;
                half4 _EdgeColor;
                half4 _BottomColor;
                float4 _CenterOffset;
                float4 _EllipseRadius;
                float _VignetteStrength;
                float _NoiseStrength;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 fraction = frac(value);
                float2 blend = fraction * fraction * (3.0 - 2.0 * fraction);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), blend.x);
                return lerp(bottom, top, blend.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 center = float2(0.5, 0.5) + _CenterOffset.xy;
                float2 radius = max(_EllipseRadius.xy, float2(0.001, 0.001));
                float radialDistance = length((input.uv - center) / radius);
                float radialBlend = smoothstep(0.0, 1.0, radialDistance);

                float edgeDistance = min(
                    min(input.uv.x, 1.0 - input.uv.x),
                    min(input.uv.y, 1.0 - input.uv.y));
                float vignette = 1.0 - smoothstep(0.0, 0.35, edgeDistance);
                float edgeBlend = saturate(radialBlend + vignette * _VignetteStrength);

                half3 color = lerp(_CenterColor.rgb, _EdgeColor.rgb, edgeBlend);

                float bottomBlend = 1.0 - smoothstep(0.0, 0.45, input.uv.y);
                color = lerp(color, _BottomColor.rgb, bottomBlend * 0.35);

                float noise = (ValueNoise(input.uv * _NoiseScale) - 0.5) * 2.0;
                color += noise * _NoiseStrength;

                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
}
