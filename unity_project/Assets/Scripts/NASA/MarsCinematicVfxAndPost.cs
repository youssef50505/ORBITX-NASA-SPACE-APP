using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Configures cinematic URP Post-Processing (ACES Tonemapping, restrained HDR Bloom,
    /// ColorAdjustments, WhiteBalance, and survival-reactive Vignette) and pooled Martian
    /// atmospheric dust motes, footstep/wheel dust puffs, and science pickup bursts.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class MarsCinematicVfxAndPost : MonoBehaviour
    {
        public static MarsCinematicVfxAndPost Instance { get; private set; }

        private Volume globalVolume;
        private Vignette vignetteOverride;
        private Bloom bloomOverride;
        private ColorAdjustments colorAdjOverride;
        private WhiteBalance whiteBalanceOverride;

        private ParticleSystem ambientDustPs;
        private ParticleSystem dustPuffPs;
        private ParticleSystem scienceBurstPs;
        private Texture2D softParticleTex;
        private Material particleMat;

        private readonly Color marsDustWarm = new Color(0.84f, 0.54f, 0.36f, 0.24f);
        private readonly Color marsRegolithPuff = new Color(0.68f, 0.38f, 0.22f, 0.55f);
        private readonly Color scienceCyan = new Color(0.18f, 0.90f, 0.92f, 0.92f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ConfigureUrpPostProcessing();
            BuildPooledParticles();
        }

        private void Update()
        {
            UpdateAmbientDustAndReactiveVignette();
        }

        private void ConfigureUrpPostProcessing()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camData.volumeLayerMask = ~0;
            }

            globalVolume = FindFirstObjectByType<Volume>();
            if (globalVolume == null)
            {
                var vGo = new GameObject("MS_CinematicGlobalVolume");
                globalVolume = vGo.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                globalVolume.priority = 10f;
            }

            globalVolume.enabled = true;
            globalVolume.isGlobal = true;

            // Runtime-safe cloned profile so we never mutate disk assets unexpectedly during play
            VolumeProfile prof = globalVolume.profile;
            if (prof == null)
            {
                prof = ScriptableObject.CreateInstance<VolumeProfile>();
                globalVolume.profile = prof;
            }

            if (!prof.TryGet(out Tonemapping tone)) tone = prof.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            if (!prof.TryGet(out bloomOverride)) bloomOverride = prof.Add<Bloom>(true);
            bloomOverride.active = true;
            bloomOverride.threshold.overrideState = true;
            bloomOverride.threshold.value = 1.15f;
            bloomOverride.intensity.overrideState = true;
            bloomOverride.intensity.value = 0.34f;
            bloomOverride.scatter.overrideState = true;
            bloomOverride.scatter.value = 0.55f;

            if (!prof.TryGet(out colorAdjOverride)) colorAdjOverride = prof.Add<ColorAdjustments>(true);
            colorAdjOverride.active = true;
            colorAdjOverride.postExposure.overrideState = true;
            colorAdjOverride.postExposure.value = 0.08f;
            colorAdjOverride.contrast.overrideState = true;
            colorAdjOverride.contrast.value = 7.0f;
            colorAdjOverride.saturation.overrideState = true;
            colorAdjOverride.saturation.value = 8.0f;

            if (!prof.TryGet(out whiteBalanceOverride)) whiteBalanceOverride = prof.Add<WhiteBalance>(true);
            whiteBalanceOverride.active = true;
            whiteBalanceOverride.temperature.overrideState = true;
            whiteBalanceOverride.temperature.value = 6.0f;

            if (!prof.TryGet(out vignetteOverride)) vignetteOverride = prof.Add<Vignette>(true);
            vignetteOverride.active = true;
            vignetteOverride.intensity.overrideState = true;
            vignetteOverride.intensity.value = 0.24f;
            vignetteOverride.smoothness.overrideState = true;
            vignetteOverride.smoothness.value = 0.35f;
            vignetteOverride.color.overrideState = true;
            vignetteOverride.color.value = new Color(0.06f, 0.03f, 0.02f, 1f);
        }

        private void BuildPooledParticles()
        {
            softParticleTex = CreateSoftDiscTexture(64);
            Shader spriteShader = Shader.Find("Sprites/Default");
            particleMat = new Material(spriteShader != null ? spriteShader : Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particleMat.mainTexture = softParticleTex;

            // 1. Ambient Martian Blowing Dust Motes (pooled single system following camera)
            ambientDustPs = CreateBaseParticleSystem("MS_VFX_AmbientDustMotes", true);
            var amMain = ambientDustPs.main;
            amMain.maxParticles = 160;
            amMain.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            amMain.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            amMain.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.26f);
            amMain.startColor = marsDustWarm;
            amMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var amEmit = ambientDustPs.emission;
            amEmit.rateOverTime = 24f;
            var amShape = ambientDustPs.shape;
            amShape.shapeType = ParticleSystemShapeType.Box;
            amShape.scale = new Vector3(36f, 10f, 36f);
            var amVel = ambientDustPs.velocityOverLifetime;
            amVel.enabled = true;
            amVel.x = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            amVel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
            amVel.z = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            ApplyFadeOutCurve(ambientDustPs);
            ambientDustPs.Play();

            // 2. Pooled Footstep / Rover-Wheel Regolith Dust Puffs
            dustPuffPs = CreateBaseParticleSystem("MS_VFX_RegolithDustPuffs", false);
            var dpMain = dustPuffPs.main;
            dpMain.maxParticles = 120;
            dpMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            dpMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            dpMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            dpMain.startColor = marsRegolithPuff;
            dpMain.gravityModifier = 0.12f;
            dpMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var dpEmit = dustPuffPs.emission;
            dpEmit.rateOverTime = 0f;
            var dpShape = dustPuffPs.shape;
            dpShape.shapeType = ParticleSystemShapeType.Hemisphere;
            dpShape.radius = 0.22f;
            ApplyFadeOutCurve(dustPuffPs);

            // 3. Pooled Science Sample Pickup / Omni-Probe Lock Burst
            scienceBurstPs = CreateBaseParticleSystem("MS_VFX_ScienceBurst", false);
            var sbMain = scienceBurstPs.main;
            sbMain.maxParticles = 80;
            sbMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.80f);
            sbMain.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
            sbMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            sbMain.startColor = scienceCyan;
            sbMain.gravityModifier = -0.05f;
            sbMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sbEmit = scienceBurstPs.emission;
            sbEmit.rateOverTime = 0f;
            var sbShape = scienceBurstPs.shape;
            sbShape.shapeType = ParticleSystemShapeType.Sphere;
            sbShape.radius = 0.35f;
            ApplyFadeOutCurve(scienceBurstPs);
        }

        public void EmitDustPuff(Vector3 worldPos, int count = 4, float scale = 1.0f)
        {
            if (dustPuffPs == null) return;
            var ep = new ParticleSystem.EmitParams
            {
                position = worldPos + Vector3.up * 0.08f,
                startSize = Random.Range(0.20f, 0.38f) * scale,
                applyShapeToPosition = true
            };
            dustPuffPs.transform.position = worldPos;
            dustPuffPs.Emit(ep, Mathf.Clamp(count, 1, 14));
        }

        public void EmitScienceBurst(Vector3 worldPos, int count = 14)
        {
            if (scienceBurstPs == null) return;
            scienceBurstPs.transform.position = worldPos + Vector3.up * 0.65f;
            scienceBurstPs.Emit(Mathf.Clamp(count, 6, 30));
        }

        private void UpdateAmbientDustAndReactiveVignette()
        {
            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            bool inSpace = Camera.main != null && Camera.main.transform.position.y > 400f;

            if (ambientDustPs != null)
            {
                if (Camera.main != null && !inSpace)
                {
                    ambientDustPs.transform.position = Camera.main.transform.position + Vector3.up * 2.5f;
                }
                var em = ambientDustPs.emission;
                em.rateOverTime = inSpace ? 0f : Mathf.Lerp(18f, 110f, Mathf.InverseLerp(0.45f, 2.5f, tau));
            }

            if (vignetteOverride != null)
            {
                float o2 = MarsSurvivalMissionController.Instance != null ? MarsSurvivalMissionController.Instance.OxygenPercent : 100f;
                float radTrauma = MarsSurvivalMissionController.Instance != null ? MarsSurvivalMissionController.Instance.RadiationTraumaPercent : 0f;
                float stormFactor = Mathf.InverseLerp(1.0f, 2.5f, tau);

                bool critical = o2 < 25f || radTrauma > 65f;
                float pulse = critical ? (0.5f + 0.5f * Mathf.Sin(Time.time * 6.0f)) * 0.14f : 0f;
                vignetteOverride.intensity.value = Mathf.Clamp(0.24f + stormFactor * 0.14f + pulse, 0.22f, 0.52f);

                Color targetCol = new Color(0.06f, 0.03f, 0.02f, 1f);
                if (critical)
                    targetCol = new Color(0.55f, 0.07f, 0.05f, 1f);
                else if (stormFactor > 0.1f)
                    targetCol = Color.Lerp(targetCol, new Color(0.42f, 0.20f, 0.08f, 1f), stormFactor);
                vignetteOverride.color.value = Color.Lerp(vignetteOverride.color.value, targetCol, Time.deltaTime * 4f);
            }
        }

        private ParticleSystem CreateBaseParticleSystem(string name, bool looping)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = looping;
            main.playOnAwake = false;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = particleMat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return ps;
        }

        private static void ApplyFadeOutCurve(ParticleSystem ps)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1.0f),
                new Keyframe(1f, 0.0f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
        }

        private static Texture2D CreateSoftDiscTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
