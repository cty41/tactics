Shader "Custom/FloatingUnitShader"
{
    Properties
    {
        _MainTex     ("Sprite Texture", 2D) = "white" {}
        _Amplitude   ("Vertical Amplitude", Float) = 0.1
        _Frequency   ("Oscillation Frequency (Hz)", Float) = 1.0
        _FrameStep   ("Time Step (s)", Float) = 0.2
        _PhaseScale  ("Phase Scale (cycles per world unit)", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4   _MainTex_ST;
            float    _Amplitude;
            float    _Frequency;
            float    _FrameStep;
            float    _PhaseScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv     : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Quantized global time for low-frame stutter
                float t_q = floor(_Time.y / _FrameStep) * _FrameStep;

                // Pull object’s world-space X translation only (constant per sprite)
                float objectX = unity_ObjectToWorld._m03;

                // Compute phase offset (radians) using objectX
                float phaseOffset = objectX * _PhaseScale * UNITY_TWO_PI;

                // Final vertical offset
                float offsetY = sin(t_q * _Frequency * UNITY_TWO_PI + phaseOffset) * _Amplitude;

                // Apply to all vertices equally
                float4 pos = v.vertex;
                pos.y += offsetY;

                o.vertex = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
