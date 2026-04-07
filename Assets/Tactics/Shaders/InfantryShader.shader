Shader "Custom/InfantryShader"
{
    Properties
    {
        _MainTex        ("Sprite Texture",           2D)    = "white" {}
        _FrameStep      ("Time Step (s)",            Float) = 0.2
        _PhaseScale     ("Phase Scale (cycles per world unit)", Float) = 0.5

        _BaseAmplitude  ("Vertical Amplitude",       Float) = 0.05
        _BaseFrequency  ("Vertical Frequency (Hz)",  Float) = 1.0

        _BobAngle       ("Max Bob Angle (rad)",      Float) = 0.05
        _PivotY         ("Bob Pivot Y (local)",      Float) = 0.0
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

            float _FrameStep, _PhaseScale;
            float _BaseAmplitude, _BaseFrequency;
            float _BobAngle, _PivotY;

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

                // quantized time for that stuttery low-frame feel
                float t_q = floor(_Time.y / _FrameStep) * _FrameStep;

                // per-object phase so units desync in world X
                float objectX  = unity_ObjectToWorld._m03;
                float phaseOff = objectX * _PhaseScale * UNITY_TWO_PI;

                // simple vertical bob
                float baseY = sin(t_q * _BaseFrequency * UNITY_TWO_PI + phaseOff) * _BaseAmplitude;

                // apply vertical shift
                float3 lp = v.vertex.xyz;
                lp.y += baseY;

                // head-bob rotation around pivot Y
                float angle = sin(t_q * _BaseFrequency * UNITY_TWO_PI + phaseOff) * _BobAngle;
                float s = sin(angle), c = cos(angle);

                // translate into pivot space, rotate, then back
                float localY = lp.y - _PivotY;
                float localX = lp.x;
                lp.x = localX * c - localY * s;
                lp.y = localX * s + localY * c + _PivotY;

                o.vertex = UnityObjectToClipPos(float4(lp,1));
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
