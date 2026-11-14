Shader "MarsSentinel/MarsAtmosphereRim"
{
    Properties
    {
        _DayRimColor ("Day Rim Color", Color) = (0.96, 0.62, 0.36, 1)
        _TwilightRimColor ("Twilight Rim Color", Color) = (0.45, 0.76, 1.0, 1)
        _SunDir ("Sun Direction", Vector) = (0.34, 0.34, 0.88, 0)
        _Intensity ("Intensity", Range(0.0, 4.0)) = 1.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        Cull Back
        ZWrite Off

        Pass
        {
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

            float4 _DayRimColor;
            float4 _TwilightRimColor;
            float4 _SunDir;
            float _Intensity;

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float3 L = normalize(_SunDir.xyz);

                float ndotv = saturate(dot(N, V));
                float fresnel = pow(1.0 - ndotv, 3.6);
                // Smooth outer falloff to eliminate hard shell outline
                float outerFade = smoothstep(0.02, 0.32, ndotv);
                float sunTerm = dot(N, L);
                float dayMask = smoothstep(-0.22, 0.45, sunTerm);
                float terminator = exp(-pow((sunTerm - 0.05) * 3.8, 2.0));

                float3 col = lerp(_DayRimColor.rgb, _TwilightRimColor.rgb, saturate(0.55 + terminator * 0.45));
                float alpha = fresnel * (0.35 + 0.65 * outerFade) * (0.10 + 0.90 * dayMask) * _Intensity;
                return half4(col * alpha, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
