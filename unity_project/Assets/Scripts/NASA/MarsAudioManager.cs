using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Procedural WebGL/Desktop-compatible Mars Audio Engine.
    /// Synthesizes thin Martian CO2 wind, EVA spacesuit breathing, pressurized facility HVAC,
    /// electric rover drivetrain hum, terrain footsteps, UI feedback, and scientific scan chimes.
    /// Follows direct AudioSource routing (no brittle hand-edited .mixer YAML).
    /// </summary>
    public class MarsAudioManager : MonoBehaviour
    {
        public static MarsAudioManager Instance { get; private set; }

        [Header("Audio State")]
        [SerializeField] private bool isMuted = false;
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.72f;

        public bool IsMuted => isMuted;
        public bool IsInsideFacility { get; private set; }
        public string ActiveFacilityZone { get; private set; } = "Martian Surface (EVA)";

        public event Action<bool> OnMuteChanged;

        private AudioSource windSource;
        private AudioSource breathingSource;
        private AudioSource hvacSource;
        private AudioSource roverSource;
        private AudioSource sfxSource;

        private AudioClip windClip;
        private AudioClip breathingClip;
        private AudioClip hvacClip;
        private AudioClip roverClip;
        private AudioClip regolithStepClip;
        private AudioClip deckStepClip;
        private AudioClip uiClickClip;
        private AudioClip scanPingClip;
        private AudioClip discoveryChimeClip;
        private AudioClip hazardBeepClip;

        private float targetWindVol = 0.32f;
        private float targetHvacVol = 0.0f;
        private float targetBreathingVol = 0.14f;
        private float targetRoverVol = 0.0f;
        private float targetRoverPitch = 0.85f;
        private float lastHazardTime = -10f;
        private float dustLevel = 0.45f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitializeAudioSourcesAndClips();
        }

        private void InitializeAudioSourcesAndClips()
        {
            const int sr = 22050;

            windClip = SynthesizeMartianWindClip(sr, 4.0f);
            breathingClip = SynthesizeBreathingClip(sr, 3.6f);
            hvacClip = SynthesizeHvacClip(sr, 3.0f);
            roverClip = SynthesizeRoverMotorClip(sr, 1.5f);
            regolithStepClip = SynthesizeFootstepClip(sr, false);
            deckStepClip = SynthesizeFootstepClip(sr, true);
            uiClickClip = SynthesizeUiClickClip(sr);
            scanPingClip = SynthesizeScanPingClip(sr);
            discoveryChimeClip = SynthesizeDiscoveryChimeClip(sr);
            hazardBeepClip = SynthesizeHazardBeepClip(sr);

            windSource = CreateLoopSource("Audio_MarsWind", windClip, 0.28f, 1.0f);
            breathingSource = CreateLoopSource("Audio_SuitBreathing", breathingClip, 0.12f, 1.0f);
            hvacSource = CreateLoopSource("Audio_FacilityHVAC", hvacClip, 0.0f, 1.0f);
            roverSource = CreateLoopSource("Audio_RoverDrive", roverClip, 0.0f, 0.85f);

            GameObject sfxObj = new GameObject("Audio_SFX_Channel");
            sfxObj.transform.SetParent(transform, false);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = masterVolume;

            ApplyMuteState();
        }

        private AudioSource CreateLoopSource(string childName, AudioClip clip, float initialVol, float pitch)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = initialVol * masterVolume;
            src.pitch = pitch;
            if (clip != null) src.Play();
            return src;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.kKey.wasPressedThisFrame)
            {
                ToggleMute();
            }

            float dt = Time.deltaTime;
            float m = isMuted ? 0f : masterVolume;

            if (windSource != null)
            {
                float stormFactor = Mathf.Clamp01(0.45f + dustLevel * 0.35f);
                float desiredWind = (IsInsideFacility ? 0.05f : targetWindVol * stormFactor) * m;
                windSource.volume = Mathf.Lerp(windSource.volume, desiredWind, dt * 3.0f);
            }

            if (hvacSource != null)
            {
                float desiredHvac = (IsInsideFacility ? 0.24f : targetHvacVol) * m;
                hvacSource.volume = Mathf.Lerp(hvacSource.volume, desiredHvac, dt * 3.0f);
            }

            if (breathingSource != null)
            {
                float desiredBreath = (IsInsideFacility ? 0.04f : targetBreathingVol) * m;
                breathingSource.volume = Mathf.Lerp(breathingSource.volume, desiredBreath, dt * 3.0f);
            }

            if (roverSource != null)
            {
                roverSource.volume = Mathf.Lerp(roverSource.volume, targetRoverVol * m, dt * 5.0f);
                roverSource.pitch = Mathf.Lerp(roverSource.pitch, targetRoverPitch, dt * 5.0f);
            }
        }

        public void SetMuted(bool muted)
        {
            isMuted = muted;
            ApplyMuteState();
            OnMuteChanged?.Invoke(isMuted);
        }

        public void ToggleMute()
        {
            SetMuted(!isMuted);
            if (!isMuted) PlayUiClick();
        }

        private void ApplyMuteState()
        {
            AudioListener.pause = false;
            if (windSource != null) windSource.mute = isMuted;
            if (breathingSource != null) breathingSource.mute = isMuted;
            if (hvacSource != null) hvacSource.mute = isMuted;
            if (roverSource != null) roverSource.mute = isMuted;
            if (sfxSource != null) sfxSource.mute = isMuted;
        }

        public void SetDustOpticalDepth(float tau)
        {
            dustLevel = Mathf.Clamp(tau, 0.2f, 4.0f);
        }

        public void SetInteriorState(bool inside, string zoneLabel)
        {
            IsInsideFacility = inside;
            ActiveFacilityZone = string.IsNullOrEmpty(zoneLabel) ? (inside ? "Pressurized Habitat" : "Martian Surface (EVA)") : zoneLabel;
        }

        public void SetBreathingIntensity(bool sprinting)
        {
            targetBreathingVol = sprinting ? 0.20f : 0.12f;
            if (breathingSource != null)
            {
                breathingSource.pitch = Mathf.Lerp(breathingSource.pitch, sprinting ? 1.25f : 1.0f, Time.deltaTime * 3.0f);
            }
        }

        public void UpdateRoverAudio(bool drivingActive, float speedMps)
        {
            if (!drivingActive)
            {
                targetRoverVol = 0f;
                targetRoverPitch = 0.85f;
                return;
            }
            float normSpeed = Mathf.Clamp01(Mathf.Abs(speedMps) / 10.0f);
            targetRoverVol = 0.08f + normSpeed * 0.26f;
            targetRoverPitch = 0.82f + normSpeed * 0.65f;
        }

        public void PlayFootstep(bool interiorDeck)
        {
            if (isMuted || sfxSource == null) return;
            AudioClip clip = interiorDeck ? deckStepClip : regolithStepClip;
            if (clip == null) return;
            sfxSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            sfxSource.PlayOneShot(clip, (interiorDeck ? 0.28f : 0.34f) * masterVolume);
        }

        public void PlayUiClick()
        {
            if (isMuted || sfxSource == null || uiClickClip == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(uiClickClip, 0.38f * masterVolume);
        }

        public void PlayScienceScan(bool newlyDiscovered)
        {
            if (isMuted || sfxSource == null) return;
            sfxSource.pitch = 1.0f;
            if (newlyDiscovered && discoveryChimeClip != null)
            {
                sfxSource.PlayOneShot(discoveryChimeClip, 0.50f * masterVolume);
            }
            else if (scanPingClip != null)
            {
                sfxSource.PlayOneShot(scanPingClip, 0.36f * masterVolume);
            }
        }

        public void PlayHazardBeep()
        {
            if (isMuted || sfxSource == null || hazardBeepClip == null) return;
            if (Time.unscaledTime - lastHazardTime < 3.5f) return;
            lastHazardTime = Time.unscaledTime;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(hazardBeepClip, 0.32f * masterVolume);
        }

        // --- Procedural PCM Sound Synthesizers ---

        private static AudioClip SynthesizeMartianWindClip(int sr, float seconds)
        {
            int n = Mathf.RoundToInt(sr * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(2026);
            float b0 = 0f, b1 = 0f, b2 = 0f;
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                b0 = 0.997f * b0 + white * 0.029f;
                b1 = 0.985f * b1 + white * 0.032f;
                b2 = 0.950f * b2 + white * 0.048f;
                float pink = (b0 + b1 + b2) * 0.45f;
                lp = lp * 0.92f + pink * 0.08f;

                float phase = (float)i / n;
                float gust = 0.55f + 0.45f * Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Cos(phase * Mathf.PI * 4f + 0.7f);
                float rumble = Mathf.Sin(2f * Mathf.PI * 64f * i / sr) * 0.05f;
                data[i] = Mathf.Clamp((lp * gust * 1.8f + rumble) * 0.7f, -0.9f, 0.9f);
            }

            SmoothLoopBoundaries(data, sr / 10);
            AudioClip clip = AudioClip.Create("MarsWindPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeBreathingClip(int sr, float seconds)
        {
            int n = Mathf.RoundToInt(sr * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(1844);
            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 = lp1 * 0.88f + white * 0.12f;
                lp2 = lp2 * 0.72f + (lp1 - lp2) * 0.28f;

                float cycle = t / seconds;
                float inhale = cycle < 0.38f ? Mathf.Sin((cycle / 0.38f) * Mathf.PI) : 0f;
                float exhale = (cycle >= 0.48f && cycle < 0.88f) ? Mathf.Sin(((cycle - 0.48f) / 0.40f) * Mathf.PI) * 0.85f : 0f;
                data[i] = Mathf.Clamp((lp2 * 2.4f + lp1 * 0.5f) * (inhale + exhale) * 0.5f, -0.8f, 0.8f);
            }

            SmoothLoopBoundaries(data, sr / 20);
            AudioClip clip = AudioClip.Create("SuitBreathingPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeHvacClip(int sr, float seconds)
        {
            int n = Mathf.RoundToInt(sr * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(7745);
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.94f + white * 0.06f;
                float hum = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.20f + Mathf.Sin(2f * Mathf.PI * 116f * t) * 0.10f;
                float airflow = lp * 0.85f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
                data[i] = Mathf.Clamp((hum + airflow) * 0.65f, -0.8f, 0.8f);
            }

            SmoothLoopBoundaries(data, sr / 15);
            AudioClip clip = AudioClip.Create("FacilityHvacPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeRoverMotorClip(int sr, float seconds)
        {
            int n = Mathf.RoundToInt(sr * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(3292);
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.85f + white * 0.15f;
                float motor = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.25f
                            + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.14f
                            + Mathf.Sin(2f * Mathf.PI * 285f * t) * 0.06f;
                float treadRumble = lp * (0.4f + 0.3f * Mathf.Sin(2f * Mathf.PI * 14f * t));
                data[i] = Mathf.Clamp((motor + treadRumble) * 0.6f, -0.85f, 0.85f);
            }

            SmoothLoopBoundaries(data, sr / 20);
            AudioClip clip = AudioClip.Create("RoverMotorPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeFootstepClip(int sr, bool metallicDeck)
        {
            int n = Mathf.RoundToInt(sr * 0.16f);
            float[] data = new float[n];
            System.Random rng = new System.Random(metallicDeck ? 901 : 404);
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * (metallicDeck ? 34f : 22f));
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.72f + white * 0.28f;
                if (metallicDeck)
                {
                    float clink = Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.25f;
                    data[i] = Mathf.Clamp((clink + lp * 0.35f) * env, -0.9f, 0.9f);
                }
                else
                {
                    float thud = Mathf.Sin(2f * Mathf.PI * 85f * t * (1f - t * 2f)) * 0.40f;
                    float crunch = (white - lp) * 0.55f + lp * 0.65f;
                    data[i] = Mathf.Clamp((thud + crunch) * env, -0.9f, 0.9f);
                }
            }

            AudioClip clip = AudioClip.Create(metallicDeck ? "StepDeckPCM" : "StepRegolithPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeUiClickClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.06f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Exp(-t * 68f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 980f * t) * env * 0.6f;
            }
            AudioClip clip = AudioClip.Create("UiClickPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeScanPingClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.28f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float freq = Mathf.Lerp(620f, 1180f, t / 0.28f);
                float env = Mathf.Sin(Mathf.Min(1f, t / 0.03f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 8.5f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.25f) * env;
            }
            AudioClip clip = AudioClip.Create("ScanPingPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeDiscoveryChimeClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.65f);
            float[] data = new float[n];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float sum = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float start = k * 0.075f;
                    if (t >= start)
                    {
                        float dtNote = t - start;
                        float env = Mathf.Min(1f, dtNote / 0.015f) * Mathf.Exp(-dtNote * 5.2f);
                        sum += Mathf.Sin(2f * Mathf.PI * notes[k] * dtNote) * env * 0.28f;
                    }
                }
                data[i] = Mathf.Clamp(sum, -0.9f, 0.9f);
            }
            AudioClip clip = AudioClip.Create("DiscoveryChimePCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeHazardBeepClip(int sr)
        {
            int n = Mathf.RoundToInt(sr * 0.34f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                bool p1 = t < 0.11f;
                bool p2 = t > 0.17f && t < 0.29f;
                float gate = (p1 || p2) ? 1f : 0f;
                data[i] = Mathf.Sin(2f * Mathf.PI * 840f * t) * gate * 0.42f;
            }
            AudioClip clip = AudioClip.Create("HazardBeepPCM", n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void SmoothLoopBoundaries(float[] data, int fadeSamples)
        {
            int len = data.Length;
            int fade = Mathf.Min(fadeSamples, len / 4);
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                data[i] *= w;
                data[len - 1 - i] *= w;
            }
        }
    }
}
