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
                float  fogFactor    : TEXCOORD4;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm3(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(0.8, 0.6, -0.6, 0.8);
                for (int k = 0; k < 3; k++)
                {
                    v += a * ValueNoise(p);
                    p = mul(rot, p) * 2.07 + float2(13.1, 7.7);
                    a *= 0.5;
                }
                return v;
            }

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 pos = input.positionWS;
                float3 viewDirWS = normalize(GetCameraPositionWS() - pos);
                float camDist = length(GetCameraPositionWS() - pos);
                float detailFade = saturate(1.0 - (camDist - 18.0) / 140.0);

                // 1. World normal slope detection with noise breakup so transitions look geological
                float macroNoise = Fbm3(pos.xz * 0.035);
                float gritNoise = Fbm3(pos.xz * 0.55);
                float pebbleNoise = ValueNoise(pos.xz * 3.8);
                float baseSlope = saturate(dot(normalWS, float3(0.0, 1.0, 0.0)));
                float slope = saturate(baseSlope + (macroNoise - 0.5) * 0.08);
                float blend = smoothstep(_SlopeThreshold - _SlopeBlendWidth, _SlopeThreshold + _SlopeBlendWidth, slope);

                // 2. Curved wind-sculpted aeolian sand ripples + fine basalt grit micro-normals
                float warp = (macroNoise - 0.5) * 2.4;
                float ripplePhase = pos.x * _RippleFrequency * 0.72 + pos.z * _RippleFrequency * 0.58 + warp;
                float rippleDx = cos(ripplePhase) * 0.16 * blend * _RippleStrength;
                float rippleDz = cos(ripplePhase * 1.05 + 0.4) * 0.12 * blend * _RippleStrength;
                float gritDx = (gritNoise - 0.5) * 0.30 + (pebbleNoise - 0.5) * 0.18 * detailFade;
                float gritDz = (ValueNoise(pos.xz * 0.55 + float2(4.7, 9.2)) - 0.5) * 0.30 + (pebbleNoise - 0.5) * 0.18 * detailFade;

                float3 rippleOffset = float3(rippleDx + gritDx, 0.0, rippleDz + gritDz);
                float3 perturbedNormal = normalize(normalWS + rippleOffset);

                // 3. Multi-tone Jezero regolith: ochre dust drifts, hematite dark patches & basaltic strata
                float3 dustTint = _SandColor.rgb * float3(1.12, 1.02, 0.90);
                float3 darkGravel = _RockColor.rgb * float3(0.68, 0.62, 0.58);
                float3 sandAlbedo = lerp(darkGravel, dustTint, saturate(macroNoise * 0.75 + gritNoise * 0.35));
                float3 rockAlbedo = lerp(_RockColor.rgb * 0.75, _RockColor.rgb * 1.18, gritNoise);
                float strata = sin(pos.y * 7.5 + macroNoise * 4.0) * 0.09;
                rockAlbedo += strata;
                float3 albedo = lerp(rockAlbedo, sandAlbedo, blend);
                albedo *= lerp(0.90, 1.08, pebbleNoise * detailFade + 0.5 * (1.0 - detailFade));

                float smoothness = lerp(_RockSmoothness, _SandSmoothness, blend) * lerp(0.8, 1.25, pebbleNoise);

                // 4. URP Lighting + Blinn-Phong rock glint + warm dusty Mars rim scatter
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(perturbedNormal, mainLight.direction));
                float shadow = mainLight.shadowAttenuation;
                float3 halfVec = normalize(mainLight.direction + viewDirWS);
                float specPow = lerp(12.0, 48.0, smoothness);
                float spec = pow(saturate(dot(perturbedNormal, halfVec)), specPow) * smoothness * 0.24 * shadow * NdotL;
                float3 directLighting = mainLight.color * (NdotL * shadow);

                float rim = pow(1.0 - saturate(dot(perturbedNormal, viewDirWS)), 3.5) * 0.08;
                float3 ambient = SampleSH(perturbedNormal) * albedo;
                float3 finalColor = albedo * directLighting + ambient + mainLight.color * spec + float3(0.76, 0.45, 0.28) * rim;
                finalColor = MixFog(finalColor, input.fogFactor);

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
