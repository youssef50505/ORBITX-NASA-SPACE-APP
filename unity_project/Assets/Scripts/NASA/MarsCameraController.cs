using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace MarsSentinel.NASA
{
    public enum CameraViewpoint
    {
        SurfaceFleetOverview = 0,
        PerseveranceCloseUp = 1,
        IngenuityVantage = 2,
        TraverseRouteOrbit = 3,
        OrbitalFleetView = 4
    }

    /// <summary>
    /// MarsCameraController implements Cinemachine 3.x camera rig management
    /// for MarsSentinel capability showcase. Controls 5 cinematic viewpoints
    /// with smooth procedural blending and keyboard hotkey navigation [1]-[5].
    /// </summary>
    [RequireComponent(typeof(CinemachineBrain))]
    public class MarsCameraController : MonoBehaviour
    {
        public static MarsCameraController Instance { get; private set; }

        [Header("Cinemachine Cameras")]
        [SerializeField] private CinemachineCamera camSurfaceFleet;
        [SerializeField] private CinemachineCamera camPerseveranceCloseUp;
        [SerializeField] private CinemachineCamera camIngenuityVantage;
        [SerializeField] private CinemachineCamera camRouteOrbit;
        [SerializeField] private CinemachineCamera camOrbitalSky;

        [Header("State")]
        [SerializeField] private CameraViewpoint currentView = CameraViewpoint.SurfaceFleetOverview;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        private CinemachineCamera[] cameras;
        public CameraViewpoint CurrentView => currentView;
        public event Action<CameraViewpoint> OnViewpointChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCinemachineBrain();
        }

        private void Start()
        {
            InitializeCameraRig();
            SwitchToView(CameraViewpoint.SurfaceFleetOverview);
        }

        private void Update()
        {
            HandleInput();
            UpdateStormTurbulence();
        }

        private void UpdateStormTurbulence()
        {
            if (cameras == null) return;
            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : 0.45f;
            float stormAmp = Mathf.Clamp01((tau - 0.85f) / 1.95f) * 0.35f;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;
                var noise = cameras[i].GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (noise != null)
                {
                    noise.AmplitudeGain = stormAmp;
                    noise.FrequencyGain = 1.2f + stormAmp * 2.5f;
                }
            }
        }

        private void EnsureCinemachineBrain()
        {
            CinemachineBrain brain = GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = gameObject.AddComponent<CinemachineBrain>();
            }
            brain.DefaultBlend.Time = 1.5f;
            brain.DefaultBlend.Style = CinemachineBlendDefinition.Styles.EaseInOut;
        }

        public void InitializeCameraRig()
        {
            GameObject existingGlobal = GameObject.Find("MarsCameraRigs");
            Transform rigsRoot = existingGlobal != null ? existingGlobal.transform : null;
            if (rigsRoot == null)
            {
                GameObject go = new GameObject("MarsCameraRigs");
                rigsRoot = go.transform;
            }
            if (rigsRoot.parent != null)
            {
                rigsRoot.SetParent(null, false);
            }

            // 1. SurfaceFleetOverview: elevated perspective looking down across rover fleet and landing site
            if (camSurfaceFleet == null)
            {
                camSurfaceFleet = CreateVirtualCamera("CM_SurfaceFleetOverview", rigsRoot,
                    new Vector3(0f, 15f, -35f),
                    Quaternion.Euler(22f, 0f, 0f));
            }

            // 2. PerseveranceCloseUp: low-angle dynamic shot framing rover chassis and Mastcam-Z
            if (camPerseveranceCloseUp == null)
            {
                camPerseveranceCloseUp = CreateVirtualCamera("CM_PerseveranceCloseUp", rigsRoot,
                    new Vector3(-2.8f, 2.2f, 4.5f),
                    Quaternion.Euler(15f, 150f, 0f));
            }

            // 3. IngenuityVantage: high-angle scouting view overlooking helicopter flight corridor
            if (camIngenuityVantage == null)
            {
                camIngenuityVantage = CreateVirtualCamera("CM_IngenuityVantage", rigsRoot,
                    new Vector3(12f, 8f, 15f),
                    Quaternion.Euler(30f, -135f, 0f));
            }

            // 4. TraverseRouteOrbit: sweeping panoramic overview tracking A* route towards Rochette
            if (camRouteOrbit == null)
            {
                camRouteOrbit = CreateVirtualCamera("CM_TraverseRouteOrbit", rigsRoot,
                    new Vector3(-80f, 45f, -120f),
                    Quaternion.Euler(20f, 35f, 0f));
            }

            // 5. OrbitalFleetView: skyward orbital constellation camera (MRO, MAVEN, Odyssey)
            if (camOrbitalSky == null)
            {
                camOrbitalSky = CreateVirtualCamera("CM_OrbitalFleetView", rigsRoot,
                    new Vector3(0f, 2f, 0f),
                    Quaternion.Euler(-65f, 20f, 0f));
            }

            cameras = new[]
            {
                camSurfaceFleet,
                camPerseveranceCloseUp,
                camIngenuityVantage,
                camRouteOrbit,
                camOrbitalSky
            };
        }

        private CinemachineCamera CreateVirtualCamera(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;

            CinemachineCamera cam = go.GetComponent<CinemachineCamera>();
            if (cam == null) cam = go.AddComponent<CinemachineCamera>();

            var perlin = go.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (perlin == null) perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
            perlin.AmplitudeGain = 0f;
            perlin.FrequencyGain = 1.2f;

            cam.Priority = inactivePriority;
            return cam;
        }

        public void SwitchToView(CameraViewpoint view)
        {
            currentView = view;
            int targetIdx = (int)view;

            if (cameras == null || cameras.Length == 0) return;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    if (i == targetIdx)
                    {
                        cameras[i].Priority = activePriority;
                        cameras[i].Prioritize();
                    }
                    else
                    {
                        cameras[i].Priority = inactivePriority;
                    }
                }
            }

            Debug.Log($"[MarsCameraController] Switched active camera viewpoint to: {view}");
            OnViewpointChanged?.Invoke(view);
        }

        private void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SwitchToView(CameraViewpoint.SurfaceFleetOverview);
                else if (kb.digit2Key.wasPressedThisFrame) SwitchToView(CameraViewpoint.PerseveranceCloseUp);
                else if (kb.digit3Key.wasPressedThisFrame) SwitchToView(CameraViewpoint.IngenuityVantage);
                else if (kb.digit4Key.wasPressedThisFrame) SwitchToView(CameraViewpoint.TraverseRouteOrbit);
                else if (kb.digit5Key.wasPressedThisFrame) SwitchToView(CameraViewpoint.OrbitalFleetView);
            }
#endif
        }
    }
}
