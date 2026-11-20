using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// URP 17.6 Render Graph ScriptableRendererFeature delivering a live world-space
    /// RIMFAX Subsurface Radar / Slope-Hazard / Thermal Inertia X-Ray scanning wave
    /// triggered by Omni-Probe [X] or persistent tactical overlay mode.
    /// Validated against Unity URP Render Graph best practices (graph-derived descriptors,
    /// built-in AddBlitPass helpers, explicit ConfigureInput, zero global texture leakage).
    /// </summary>
    public class MarsXRayScannerRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
            public Shader scannerShader;
            public float maxScanRadius = 95f;
            public float scanSpeedMps = 42f;
            public float ringWidth = 3.4f;
        }

        public Settings settings = new Settings();

        // Runtime static bridge driven by MarsInteractionSystem / MarsSceneManager
        private static bool persistentOverlayActive = false;
        private static float pulseTimer = 0f;
        private static float pulseDuration = 3.2f;
        private static Vector3 scanOriginWS = Vector3.zero;
        private static int activeScanMode = 0; // 0=RIMFAX+Slope, 1=Hazard, 2=Thermal

        public static bool IsPersistentOverlayActive => persistentOverlayActive;
        public static int ActiveScanMode => activeScanMode;
        public static bool IsAnyScanVisible => persistentOverlayActive || pulseTimer > 0f;

        public static void TriggerScanPulse(Vector3 originWS, int mode = 0)
        {
            scanOriginWS = originWS;
            activeScanMode = Mathf.Clamp(mode, 0, 2);
            pulseTimer = pulseDuration * 0.88f; // start immediately visible (>0 intensity on frame 1)
        }

        public static void ForcePreviewPulse(Vector3 originWS, float progress = 0.45f, int mode = 0)
        {
            scanOriginWS = originWS;
            activeScanMode = Mathf.Clamp(mode, 0, 2);
            pulseTimer = Mathf.Clamp01(1f - progress) * pulseDuration;
        }

        public static bool TogglePersistentOverlay(Vector3 originWS)
        {
            persistentOverlayActive = !persistentOverlayActive;
            scanOriginWS = originWS;
            if (persistentOverlayActive)
            {
                activeScanMode = (activeScanMode + 1) % 3;
                pulseTimer = pulseDuration;
            }
            return persistentOverlayActive;
        }

        public static void CycleScanMode(Vector3 originWS)
        {
            activeScanMode = (activeScanMode + 1) % 3;
            scanOriginWS = originWS;
            pulseTimer = pulseDuration;
        }

        private Material runtimeMaterial;
        private MarsXRayRenderGraphPass renderPass;

        private static readonly int ScanIntensityId = Shader.PropertyToID("_ScanIntensity");
        private static readonly int ScanRadiusId = Shader.PropertyToID("_ScanRadius");
        private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        private static readonly int ScanOriginId = Shader.PropertyToID("_ScanOrigin");
        private static readonly int ScanModeId = Shader.PropertyToID("_ScanMode");
        private static readonly int ScanTimeId = Shader.PropertyToID("_ScanTime");

        public override void Create()
        {
            Shader shader = settings.scannerShader != null
                ? settings.scannerShader
                : Shader.Find("MarsSentinel/MarsXRayScanner");

            if (shader != null)
            {
                runtimeMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            renderPass = new MarsXRayRenderGraphPass(settings);
            renderPass.renderPassEvent = settings.injectionPoint;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
            {
                return;
            }

            if (pulseTimer > 0f)
            {
                pulseTimer = Mathf.Max(0f, pulseTimer - Time.unscaledDeltaTime);
            }

            if (!IsAnyScanVisible)
            {
                return;
            }

            if (runtimeMaterial == null)
            {
                Shader shader = settings.scannerShader != null
                    ? settings.scannerShader
                    : Shader.Find("MarsSentinel/MarsXRayScanner");
                if (shader == null) return;
                runtimeMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            float normalizedProgress = pulseTimer > 0f ? 1f - (pulseTimer / Mathf.Max(0.1f, pulseDuration)) : 1f;
            float radius = persistentOverlayActive
                ? Mathf.Lerp(18f, settings.maxScanRadius, Mathf.PingPong(Time.unscaledTime * 0.22f, 1f))
                : Mathf.Lerp(2f, settings.maxScanRadius, normalizedProgress);

            float intensity = persistentOverlayActive
                ? 0.72f
                : Mathf.Sin(Mathf.Clamp01(normalizedProgress) * Mathf.PI) * 0.92f;

            if (intensity <= 0.005f)
            {
                return;
            }

            runtimeMaterial.SetFloat(ScanIntensityId, intensity);
            runtimeMaterial.SetFloat(ScanRadiusId, radius);
            runtimeMaterial.SetFloat(RingWidthId, settings.ringWidth);
            runtimeMaterial.SetVector(ScanOriginId, new Vector4(scanOriginWS.x, scanOriginWS.y, scanOriginWS.z, 1f));
            runtimeMaterial.SetFloat(ScanModeId, activeScanMode);
            runtimeMaterial.SetFloat(ScanTimeId, Time.unscaledTime);

            renderPass.Setup(runtimeMaterial);
            renderer.EnqueuePass(renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        private sealed class MarsXRayRenderGraphPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private Material passMaterial;

            public MarsXRayRenderGraphPass(Settings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler("MarsXRayScannerPass");
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
            }

            public void Setup(Material mat)
            {
                passMaterial = mat;
                renderPassEvent = settings.injectionPoint;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (passMaterial == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData == null || resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle sourceColor = resourceData.activeColorTexture;
                if (!sourceColor.IsValid())
                {
                    return;
                }

                // Derive intermediate descriptor directly from the active render graph color resource
                TextureDesc tempDesc = renderGraph.GetTextureDesc(sourceColor);
                tempDesc.name = "_MarsXRayScannerTemp";
                tempDesc.clearBuffer = false;
                tempDesc.depthBufferBits = DepthBits.None;
                TextureHandle tempColor = renderGraph.CreateTexture(tempDesc);

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(sourceColor, tempColor, passMaterial, 0);
                renderGraph.AddBlitPass(blitParams, passName: "MarsXRayScannerEffect");

                // Copy processed color cleanly back to active camera color target
                renderGraph.AddBlitPass(tempColor, sourceColor, Vector2.one, Vector2.zero, passName: "MarsXRayScannerCopyBack");
            }
        }
    }
}
