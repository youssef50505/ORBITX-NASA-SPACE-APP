Shader "MarsSentinel/MartianAtmosphereSkybox"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (-0.4, 0.55, 0.72, 0.0)
        _ZenithColor ("Zenith Color", Color) = (0.56, 0.34, 0.21, 1.0)
        _HorizonColor ("Horizon Color", Color) = (0.86, 0.57, 0.37, 1.0)
        _SunHaloColor ("Blue Forward Scatter Halo", Color) = (0.55, 0.78, 0.98, 1.0)
        _SunDiscColor ("Sun Photosphere Color", Color) = (4.2, 3.7, 2.9, 1.0)
        _DustTau ("Dust Optical Depth Tau", Range(0.05, 2.5)) = 0.45
        _SpaceBlend ("Deep Space Starfield Blend", Range(0.0, 1.0)) = 0.0
        _StarIntensity ("Starfield Intensity", Range(0.0, 2.0)) = 1.15
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            Name "MartianSkyPass"
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
                float3 viewDirWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDir;
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _SunHaloColor;
                half4 _SunDiscColor;
                float _DustTau;
                float _SpaceBlend;
                float _StarIntensity;
            CBUFFER_END

            float hash31(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            float starLayer(float3 dir, float cellScale, float threshold)
            {
                float3 grid = dir * cellScale;
                float3 cell = floor(grid);
                float3 local = frac(grid) - 0.5;
                float h = hash31(cell);
                if (h < threshold) return 0.0;
                float3 offset = float3(hash31(cell + 1.3), hash31(cell + 2.7), hash31(cell + 4.1)) - 0.5;
                float d = length(local - offset * 0.65);
                float spark = smoothstep(0.16, 0.01, d);
                return spark * spark * ((h - threshold) / (1.0 - threshold));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.viewDirWS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.viewDirWS);
                float3 sun = normalize(_SunDir.xyz);

                float elev = clamp(d.y, -0.3, 1.0);
                float horizMix = pow(1.0 - saturate(elev), 1.55 + _DustTau * 0.85);
                half3 marsSky = lerp(_ZenithColor.rgb, _HorizonColor.rgb, (half)horizMix);

                // Ground bounce below horizon
                if (d.y < 0.0)
                {
                    marsSky = lerp(_HorizonColor.rgb, _HorizonColor.rgb * (half)0.36, (half)saturate(-d.y * 5.0));
                }

                // Martian Rayleigh + Mie forward dust scattering (famous Curiosity steel-blue sunset glow)
                float sunDot = max(dot(d, sun), 0.0);
                float wideMie = pow(sunDot, 3.2) * (0.24 + _DustTau * 0.22);
                float blueAureole = pow(sunDot, 22.0) * (0.35 + (1.0 - saturate(sun.y * 2.2)) * 0.55);

                marsSky = lerp(marsSky, _SunHaloColor.rgb, (half)saturate(blueAureole));
                marsSky += half3(0.95, 0.72, 0.45) * (half)wideMie;

                // Crisp deep-space starfield & faint Milky Way band
                float mwBand = exp(-pow((d.y * 0.65 - d.x * 0.45 + 0.1) * 4.5, 2.0));
                half3 deepSpace = half3(0.012, 0.015, 0.024) + half3(0.032, 0.036, 0.055) * (half)mwBand;
                float s1 = starLayer(d, 130.0, 0.968) * 1.4;
                float s2 = starLayer(d * 1.37 + 0.21, 210.0, 0.955) * 0.75;
                deepSpace += half3(0.92, 0.95, 1.0) * (half)((s1 + s2) * _StarIntensity * (1.0 + mwBand * 0.8));

                // Blend daytime Martian atmosphere smoothly into cosmic Space / Night
                half3 sky = lerp(marsSky, deepSpace, (half)_SpaceBlend);

                // Blazing HDR Solar Photosphere & Corona visible in BOTH Surface sky & Space!
                float angularDist = acos(clamp(dot(d, sun), -1.0, 1.0));
                float coreMask = smoothstep(0.022, 0.011, angularDist);
                float coronaMask = pow(sunDot, 260.0) * 1.8 + pow(sunDot, 48.0) * 0.42;

                // Keep stars suppressed right inside Sun glow
                sky += _SunDiscColor.rgb * (half)(coreMask + coronaMask * 0.45);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
