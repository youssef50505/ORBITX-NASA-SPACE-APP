Shader "MarsSentinel/MartianAtmosphereSkybox"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.34, 0.34, 0.88, 0)
        _SunDiscColor ("Sun Disc Color", Color) = (6.5, 5.4, 4.1, 1)
        _SunHaloColor ("Sun Halo Color", Color) = (0.58, 0.78, 0.98, 1)
        _ZenithColor ("Zenith Color", Color) = (0.34, 0.25, 0.21, 1)
        _HorizonColor ("Horizon Color", Color) = (0.78, 0.50, 0.34, 1)
        _GroundColor ("Ground Color", Color) = (0.20, 0.08, 0.04, 1)
        _DustTau ("Dust Tau", Range(0.05, 2.0)) = 0.42
        _SpaceBlend ("Space Blend", Range(0.0, 1.0)) = 0.0
        _StarIntensity ("Star Intensity", Range(0.0, 4.0)) = 1.25
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float4 _SunDir;
            float4 _SunDiscColor;
            float4 _SunHaloColor;
            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float _DustTau;
            float _SpaceBlend;
            float _StarIntensity;

            float3 Hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453123);
            }

            float StarLayer(float3 dir, float scale, float threshold, float sharpness)
            {
                float3 p = dir * scale;
                float3 id = floor(p);
                float3 f = frac(p) - 0.5;
                float3 rnd = Hash33(id);
                if (rnd.x < threshold) return 0.0;
                float3 offset = (rnd - 0.5) * 0.65;
                float d = length(f - offset);
                float size = lerp(0.035, 0.11, rnd.y);
                float star = saturate(1.0 - d / size);
                return pow(star, sharpness) * (0.45 + 0.85 * rnd.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = dot(d, sunDir);
                float sunElev = sunDir.y;

                // Atmospheric Martian surface sky
                float horizonCurve = pow(1.0 - saturate(d.y), 3.1);
                float antiSun = saturate(0.5 - 0.5 * sunDot);
                float dustFactor = saturate(_DustTau * 0.72);

                float3 skyDome = lerp(_ZenithColor.rgb, _HorizonColor.rgb, saturate(horizonCurve * (0.85 + 0.25 * dustFactor)));
                // Subtle anti-solar shadow darkening opposite the sun (Belt of Venus / dust belt)
                skyDome *= lerp(1.0, 0.80, antiSun * saturate(1.0 - abs(d.y) * 2.2));

                // Martian forward-scattering blue Mie halo around Sun + subtle warm dust scatter (compact 1.52 AU disc, zero HUD blowout)
                float warmScatter = pow(saturate(sunDot), 16.0) * (0.16 + 0.14 * dustFactor);
                float blueMieAureole = pow(saturate(sunDot), 180.0) * (0.36 + 0.18 * (1.0 - saturate(sunElev)));
                float innerCorona = pow(saturate(sunDot), 950.0) * 0.58;

                // Crisp Martian 0.35-deg solar photosphere disc with slight limb darkening
                float sunCoreEdge = smoothstep(0.99982, 0.99993, sunDot);
                float sunLimb = pow(saturate((sunDot - 0.99982) / (1.0 - 0.99982)), 0.35);

                float sunAboveHorizon = smoothstep(-0.08, 0.04, sunElev);
                float sunMaskGround = smoothstep(-0.025, 0.02, d.y);

                skyDome += _HorizonColor.rgb * warmScatter * sunAboveHorizon;
                skyDome += _SunHaloColor.rgb * blueMieAureole * sunAboveHorizon * 0.65;
                skyDome += _SunDiscColor.rgb * innerCorona * sunAboveHorizon * 0.25;
                skyDome += _SunDiscColor.rgb * sunCoreEdge * (0.85 + 0.55 * sunLimb) * sunAboveHorizon * sunMaskGround * 1.35;

                // Seam-proof deep ground scatter below y = 0
                float belowHorizon = smoothstep(0.015, -0.055, d.y);
                float3 groundRefl = lerp(_HorizonColor.rgb * 0.42, _GroundColor.rgb, saturate(-d.y * 8.0));
                float3 surfaceSky = lerp(skyDome, groundRefl, belowHorizon);

                // Deep space starfield + Milky Way subtle dust band
                float galacticPlane = exp(-pow(abs(dot(d, normalize(float3(0.42, 0.76, -0.49)))) * 3.4, 2.0));
                float3 spaceBg = float3(0.005, 0.007, 0.014) + float3(0.014, 0.016, 0.026) * galacticPlane;
                float s1 = StarLayer(d, 115.0, 0.968, 2.6);
                float s2 = StarLayer(d, 210.0, 0.978, 3.0);
                float3 starTint1 = lerp(float3(1.0, 0.88, 0.74), float3(0.76, 0.88, 1.0), frac(d.x * 19.7 + d.z * 31.3));
                float3 spaceStars = (s1 * starTint1 * 1.25 + s2 * float3(0.88, 0.93, 1.0) * 0.75) * _StarIntensity;
                float nightStarBoost = smoothstep(0.08, -0.18, sunElev) * smoothstep(-0.02, 0.12, d.y);
                float3 fullSpaceSky = spaceBg + spaceStars;

                // Night stars gently pierce Martian sky; SpaceBlend swaps cleanly to deep space
                surfaceSky += spaceStars * nightStarBoost * 0.9;
                float3 finalCol = lerp(surfaceSky, fullSpaceSky, saturate(_SpaceBlend));

                // In orbit space, render crisp directional star-sun glints from skybox if aligned
                return half4(max(finalCol, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
