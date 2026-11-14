using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// MarsSimulationAnimator orchestrates continuous multi-component animation loops
    /// across celestial, orbital, and surface bodies in the MarsSentinel showcase.
    /// Drives Mars planetary axial rotation (25.19 deg obliquity), continuous Keplerian
    /// satellite constellation propagation, and Perseverance rover exploration movement.
    /// </summary>
    public class MarsSimulationAnimator : MonoBehaviour
    {
        [Header("Planetary Globe Rotation")]
        [SerializeField] private Transform marsGlobeTransform;
        [SerializeField] private float globeRotationSpeedDegPerSec = 1.5f; // ~240s full cycle
        [SerializeField] private float marsObliquityTiltDeg = 25.19f;

        [Header("Perseverance Rover Exploration Dynamics")]
        [SerializeField] private Transform roverTransform;
        [SerializeField] private float roverSpeedMetersPerSec = 1.2f;
        [SerializeField] private float suspensionRockingFreq = 2.4f;
        [SerializeField] private float suspensionRockingAmp = 0.5f;
        [SerializeField] private bool loopTraverse = true;

        [Header("Simulation State")]
        [SerializeField] private bool isSimulating = true;

        private MarsSatelliteConstellation constellation;
        private MarsCelestialOrbitController celestialController;
        private MarsTelemetryController telemetry;
        private MarsDatasetLoader datasetLoader;

        private Vector3 globeRotationAxis;
        private List<Vector3> traverseWaypoints = new List<Vector3>();
        private int currentWaypointIndex = 0;
        private float distanceTraveled = 0f;
        private Vector3 roverBasePosition;
        private Quaternion roverBaseRotation;

        public Transform MarsGlobeTransform
        {
            get => marsGlobeTransform;
            set => marsGlobeTransform = value;
        }

        public Transform RoverTransform
        {
            get => roverTransform;
            set => roverTransform = value;
        }

        public float GlobeRotationSpeedDegPerSec
        {
            get => globeRotationSpeedDegPerSec;
            set => globeRotationSpeedDegPerSec = value;
        }

        public float RoverSpeedMetersPerSec
        {
            get => roverSpeedMetersPerSec;
            set => roverSpeedMetersPerSec = value;
        }

        public bool IsSimulating
        {
            get => isSimulating;
            set => isSimulating = value;
        }

        public float DistanceTraveled => distanceTraveled;

        private void Awake()
        {
            // Mars obliquity axis tilt (25.19 deg relative to ecliptic north)
            float tiltRad = marsObliquityTiltDeg * Mathf.Deg2Rad;
            globeRotationAxis = new Vector3(Mathf.Sin(tiltRad), Mathf.Cos(tiltRad), 0f).normalized;

            constellation = GetComponent<MarsSatelliteConstellation>();
            celestialController = GetComponent<MarsCelestialOrbitController>();
            telemetry = GetComponent<MarsTelemetryController>();
            datasetLoader = GetComponent<MarsDatasetLoader>();

            LocateSceneTargets();
        }

        private void Start()
        {
            if (roverTransform != null)
            {
                roverBasePosition = roverTransform.localPosition;
                roverBaseRotation = roverTransform.localRotation;
            }

            InitializeTraverseWaypoints();
        }

        public void LocateSceneTargets()
        {
            if (marsGlobeTransform == null)
            {
                var globeObj = GameObject.Find("MARS_NEW_Globe") ?? GameObject.Find("Mars_Globe") ?? GameObject.Find("NEW_Mars_Globe");
                if (globeObj != null) marsGlobeTransform = globeObj.transform;
            }

            if (roverTransform == null)
            {
                var roverObj = GameObject.Find("MS_Perseverance_Root");
                if (roverObj != null) roverTransform = roverObj.transform;
            }
        }

        private void InitializeTraverseWaypoints()
        {
            traverseWaypoints.Clear();

            // 1. Try to load waypoints from MarsDatasetLoader
            if (datasetLoader != null && datasetLoader.RoutesData != null && datasetLoader.RoutesData.safest != null)
            {
                var pts = datasetLoader.RoutesData.safest.unityPositions;
                if (pts != null && pts.Count > 1)
                {
                    foreach (var p in pts) traverseWaypoints.Add(p);
                }
            }

            // 2. Fallback to local exploration loop around Jezero crater
            if (traverseWaypoints.Count < 2)
            {
                Vector3 origin = roverTransform != null ? roverTransform.localPosition : Vector3.zero;
                traverseWaypoints.Add(origin);
                traverseWaypoints.Add(origin + new Vector3(8f, 0.2f, 12f));
                traverseWaypoints.Add(origin + new Vector3(16f, 0.5f, 22f));
                traverseWaypoints.Add(origin + new Vector3(6f, 0.3f, 30f));
                traverseWaypoints.Add(origin + new Vector3(-8f, 0.1f, 18f));
                traverseWaypoints.Add(origin + new Vector3(-4f, 0.0f, 6f));
            }
        }

        private void Update()
        {
            if (!isSimulating) return;

            float dt = Time.deltaTime;

            // 1. Continuous Planetary Axial Rotation
            UpdateGlobeRotation(dt);

            // 2. Rover Exploration Movement & Rocker-Bogie Suspension Oscillation
            UpdateRoverTraverse(dt);
        }

        private void UpdateGlobeRotation(float dt)
        {
            if (marsGlobeTransform != null)
            {
                float angle = globeRotationSpeedDegPerSec * dt;
                marsGlobeTransform.Rotate(globeRotationAxis, angle, Space.World);
            }
        }

        private void UpdateRoverTraverse(float dt)
        {
            if (roverTransform == null || traverseWaypoints.Count < 2) return;

            Vector3 currentPos = roverTransform.localPosition;
            Vector3 targetWp = traverseWaypoints[currentWaypointIndex];

            Vector3 toTarget = targetWp - currentPos;
            toTarget.y = 0f; // Planar distance
            float dist = toTarget.magnitude;
            float step = roverSpeedMetersPerSec * dt;

            if (dist < 0.2f || dist <= step)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= traverseWaypoints.Count)
                {
                    currentWaypointIndex = loopTraverse ? 0 : traverseWaypoints.Count - 1;
                }
            }
            else
            {
                Vector3 moveDir = toTarget.normalized;
                Vector3 newPos = currentPos + moveDir * step;
                roverTransform.localPosition = newPos;
                distanceTraveled += step;

                // Smoothly orient heading
                if (moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    roverTransform.localRotation = Quaternion.Slerp(roverTransform.localRotation, targetRot, dt * 3.5f);
                }

                // Rocker-bogie pitch and roll oscillations traversing rugged terrain
                float time = Time.time;
                float pitch = Mathf.Sin(time * suspensionRockingFreq) * suspensionRockingAmp;
                float roll = Mathf.Cos(time * suspensionRockingFreq * 0.85f) * (suspensionRockingAmp * 0.6f);
                roverTransform.localRotation *= Quaternion.Euler(pitch, 0f, roll);

                // Update live telemetry if available
                if (telemetry != null)
                {
                    float heading = roverTransform.eulerAngles.y;
                    telemetry.UpdateRoverMotionTelemetry(roverSpeedMetersPerSec, heading, distanceTraveled);
                }
            }
        }
    }
}
