Shader "MarsSentinel/MartianSandSlopeBlend"
{
    Properties
    {
        _SandColor ("Regolith Sand Albedo", Color) = (0.761, 0.439, 0.239, 1.0) // #C2703D
        _RockColor ("Basaltic Rock Albedo", Color) = (0.420, 0.220, 0.141, 1.0) // #6B3824
        _SandSmoothness ("Sand Smoothness", Range(0.0, 1.0)) = 0.15
        _RockSmoothness ("Rock Smoothness", Range(0.0, 1.0)) = 0.35
        _SlopeThreshold ("Slope Transition Threshold", Range(0.0, 1.0)) = 0.85
        _SlopeBlendWidth ("Slope Blend Width", Range(0.01, 0.5)) = 0.15
        _RippleFrequency ("Aeolian Ripple Frequency", Float) = 3.5
        _RippleStrength ("Aeolian Micro-Normal Ripple Strength", Range(0.0, 1.0)) = 0.4
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 shadowCoord  : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SandColor;
                float4 _RockColor;
                float _SandSmoothness;
                float _RockSmoothness;
                float _SlopeThreshold;
                float _SlopeBlendWidth;
                float _RippleFrequency;
                float _RippleStrength;
            CBUFFER_END

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 pos = input.positionWS;

                // 1. World normal slope detection (1.0 = flat dune, <0.85 = steep escarpment)
                float slope = saturate(dot(normalWS, float3(0.0, 1.0, 0.0)));
                float blend = smoothstep(_SlopeThreshold - _SlopeBlendWidth, _SlopeThreshold + _SlopeBlendWidth, slope);

                // 2. High-frequency tangent-space micro-normal ripples on flat regolith dunes
                float bump = sin(pos.x * _RippleFrequency) + cos(pos.z * _RippleFrequency * 1.2);
                float3 rippleOffset = float3(
                    cos(pos.x * _RippleFrequency) * 0.15,
                    0.0,
                    -sin(pos.z * _RippleFrequency * 1.2) * 0.15
                ) * (blend * _RippleStrength);

                float3 perturbedNormal = normalize(normalWS + rippleOffset);

                // 3. Slope-blended albedo and smoothness
                float3 albedo = lerp(_RockColor.rgb, _SandColor.rgb, blend);
                float smoothness = lerp(_RockSmoothness, _SandSmoothness, blend);

                // Subtle strata banding on steep rock slopes
                if (blend < 0.95)
                {
                    float strata = sin(pos.y * 8.0) * 0.08;
                    albedo += strata * (1.0 - blend);
                }

                // 4. URP Lighting evaluation
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(perturbedNormal, mainLight.direction));
                float shadow = mainLight.shadowAttenuation;
                float3 directLighting = mainLight.color * (NdotL * shadow);

                // Ambient contribution
                float3 ambient = SampleSH(perturbedNormal) * albedo;
                float3 finalColor = albedo * directLighting + ambient;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            float4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
