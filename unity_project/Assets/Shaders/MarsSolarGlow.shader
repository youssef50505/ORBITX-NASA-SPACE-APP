Shader "MarsSentinel/SolarPhotosphereGlow"
{
    Properties
    {
        _MainTex ("Glow Map", 2D) = "white" {}
        _Color ("Glow Color (HDR)", Color) = (2.8, 2.2, 1.3, 1.0)
        _CorePower ("Core Falloff", Range(1.0, 8.0)) = 2.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _CorePower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 d = IN.uv * 2.0 - 1.0;
                float r = length(d);
                if (r >= 1.0) return half4(0, 0, 0, 0);

                float edgeFade = smoothstep(1.0, 0.55, r);
                float core = exp(-r * r * (4.0 * _CorePower));
                float aureole = pow(saturate(1.0 - r), _CorePower) * edgeFade;
                float intensity = core * 1.45 + aureole * 0.72;
                half3 col = _Color.rgb * intensity;
                return half4(col, saturate(intensity));
            }
            ENDHLSL
        }
    }
}
