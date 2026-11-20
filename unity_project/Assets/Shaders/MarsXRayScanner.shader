Shader "MarsSentinel/MarsXRayScanner"
{
    Properties
    {
        _ScanIntensity ("Scan Intensity", Range(0, 1)) = 0.0
        _ScanRadius ("Scan Radius", Float) = 18.0
        _RingWidth ("Pulse Ring Width", Float) = 3.2
        _ScanOrigin ("Scan Origin WS", Vector) = (0, 0, 0, 0)
        _ScanMode ("Scan Mode (0=RIMFAX, 1=Hazard, 2=Thermal)", Float) = 0.0
        _ScanTime ("Scan Time", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "MarsXRayScannerPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ScanIntensity;
            float _ScanRadius;
            float _RingWidth;
            float4 _ScanOrigin;
            float _ScanMode;
            float _ScanTime;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 srcCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (_ScanIntensity <= 0.001)
                {
                    return srcCol;
                }

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001) return srcCol;
                #else
                    if (rawDepth >= 0.9999) return srcCol;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 dpdx = ddx(worldPos);
                float3 dpdy = ddy(worldPos);
                float3 worldNormal = normalize(cross(dpdy, dpdx) + float3(0.0, 0.0001, 0.0));
                float upDot = saturate(abs(worldNormal.y));
                float slopeDeg = degrees(acos(clamp(upDot, 0.0, 1.0)));

                float dist = distance(worldPos.xz, _ScanOrigin.xz);
                float ringMask = smoothstep(_RingWidth, 0.0, abs(dist - _ScanRadius));
                float insideMask = smoothstep(_ScanRadius + 1.5, max(0.0, _ScanRadius - 14.0), dist);

                // World-space tactical 4m coordinate grid + 2m elevation contours
                float2 gridUV = abs(frac(worldPos.xz * 0.25 - 0.5) - 0.5) / max(fwidth(worldPos.xz * 0.25), 0.001);
                float gridLine = 1.0 - saturate(min(gridUV.x, gridUV.y));
                float contour = 1.0 - saturate(abs(frac(worldPos.y * 0.5 - 0.5) - 0.5) / max(fwidth(worldPos.y * 0.5), 0.001));

                // Slope hazard & RIMFAX subsurface tint
                float hazardT = smoothstep(8.0, 22.0, slopeDeg);
                float3 safeTint = float3(0.08, 0.88, 0.92);
                float3 warnTint = float3(1.0, 0.55, 0.14);
                float3 critTint = float3(1.0, 0.20, 0.24);
                float3 modeColor = lerp(safeTint, lerp(warnTint, critTint, smoothstep(16.0, 28.0, slopeDeg)), hazardT);

                if (_ScanMode > 1.5)
                {
                    // Thermal Inertia IR false color
                    float therm = saturate(0.25 + 0.55 * (1.0 - upDot) + 0.2 * sin(worldPos.x * 0.12 + worldPos.z * 0.12));
                    modeColor = lerp(float3(0.12, 0.24, 0.78), lerp(float3(0.95, 0.35, 0.12), float3(1.0, 0.92, 0.45), therm), therm);
                }

                float pulseShimmer = 0.85 + 0.15 * sin(_ScanTime * 6.0 - dist * 0.45);
                float3 scanOverlay = modeColor * (gridLine * 0.38 + contour * 0.28 + 0.14) * insideMask * pulseShimmer;
                float3 ringGlow = float3(0.28, 0.96, 1.0) * ringMask * 1.15;

                float blend = saturate(_ScanIntensity * (insideMask * 0.48 + ringMask * 0.85));
                float3 finalCol = lerp(srcCol.rgb, srcCol.rgb * 0.72 + scanOverlay + ringGlow, blend);
                return half4(finalCol, srcCol.a);
            }
            ENDHLSL
        }
    }
}
