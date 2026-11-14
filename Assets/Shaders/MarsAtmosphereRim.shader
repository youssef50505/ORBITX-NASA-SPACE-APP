Shader "MarsSentinel/MarsAtmosphereRim"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (-0.4, 0.55, 0.72, 0.0)
        _DayRimColor ("Day Scatter Color", Color) = (0.96, 0.62, 0.36, 1.0)
        _TwilightRimColor ("Terminator Scatter Color", Color) = (0.42, 0.72, 0.98, 1.0)
        _Intensity ("Atmosphere Intensity", Range(0.1, 3.0)) = 1.25
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "MarsAtmoRimPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDir;
                half4 _DayRimColor;
                half4 _TwilightRimColor;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                float3 s = normalize(_SunDir.xyz);

                float fresnel = pow(saturate(1.0 - max(dot(n, v), 0.0)), 3.2);
                float sunDot = dot(n, s);
                float daySide = smoothstep(-0.28, 0.65, sunDot);
                float terminator = exp(-pow((sunDot + 0.04) * 4.2, 2.0));

                half3 col = lerp(_DayRimColor.rgb, _TwilightRimColor.rgb, (half)(terminator * 0.78));
                float alpha = fresnel * (0.06 + daySide * 0.68 + terminator * 0.30) * _Intensity;
                return half4(col * (half)alpha, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
