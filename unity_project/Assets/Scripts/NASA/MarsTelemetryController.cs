using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    [Serializable]
    public struct RoverTelemetry
    {
        public int sol;
        public string siteName;
        public double longitudeEastDeg;
        public double latitudeNorthDeg;
        public float elevationMeters;
        public float currentSpeedMps;
        public float headingDeg;
        public float currentSlopeDeg;
        public float slopeWarningThresholdDeg;
        public float distanceTraveledMeters;
        public float batteryPercent;
        public string batteryStatus;
        public string trafficabilityStatus;
    }

    /// <summary>
    /// MarsTelemetryController visualizes NASA traverse routes via LineRenderers,
    /// places sample tube and landmark waypoints, and exposes live rover telemetry.
    /// </summary>
    public class MarsTelemetryController : MonoBehaviour
    {
        public static MarsTelemetryController Instance { get; private set; }

        [Header("Route Line Renderers")]
        [SerializeField] private LineRenderer lineSafest;
        [SerializeField] private LineRenderer lineShortest;
        [SerializeField] private LineRenderer lineScience;
        [SerializeField] private LineRenderer lineIngenuity;
        [SerializeField] private LineRenderer linePerseveranceHistory;
        [SerializeField] private float lineWidth = 0.8f;

        [Header("Colors")]
        [SerializeField] private Color colorSafest = new Color(0.0f, 0.95f, 0.75f, 1.0f);   // Cyan / Green
        [SerializeField] private Color colorShortest = new Color(1.0f, 0.84f, 0.0f, 1.0f);  // Yellow
        [SerializeField] private Color colorScience = new Color(0.9f, 0.25f, 0.98f, 1.0f);  // Magenta
        [SerializeField] private Color colorIngenuity = new Color(0.25f, 0.85f, 1.0f, 0.9f); // Sky Blue
        [SerializeField] private Color colorPerseveranceHistory = new Color(1.0f, 0.52f, 0.22f, 0.85f); // Rusty Orange

        [Header("Waypoints")]
        [SerializeField] private Transform waypointContainer;
        [SerializeField] private bool showSampleTubes = true;
        [SerializeField] private bool showHistoricalOverlays = true;

        [Header("Active Rover Telemetry")]
        [SerializeField] private RoverTelemetry telemetry = new RoverTelemetry
        {
            sol = 100,
            siteName = "Octavia E. Butler Landing",
            longitudeEastDeg = 77.4508,
            latitudeNorthDeg = 18.4447,
            elevationMeters = -2569.9f,
            currentSpeedMps = 0.12f,
            headingDeg = 284.5f,
            currentSlopeDeg = 4.2f,
            slopeWarningThresholdDeg = 15.0f,
            distanceTraveledMeters = 1159.95f,
            batteryPercent = 94.0f,
            batteryStatus = "RTG Nominal (94%)",
            trafficabilityStatus = "Safe (USGS AHi Regolith)"
        };

        private readonly List<LineRenderer> persevExtraRenderers = new List<LineRenderer>();

        public RoverTelemetry CurrentTelemetry => telemetry;
        public event Action<RoverTelemetry> OnTelemetryUpdated;

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
            if (MarsDatasetLoader.Instance != null)
            {
                if (MarsDatasetLoader.Instance.IsLoaded)
                {
                    BuildVisualizations();
                }
                else
                {
                    MarsDatasetLoader.Instance.OnDataLoaded += BuildVisualizations;
                }
            }
        }

        public void BuildVisualizations()
        {
            EnsureLineRenderers();
            PopulateRouteLines();
            SpawnWaypoints();
            OnTelemetryUpdated?.Invoke(telemetry);
        }

        private void EnsureLineRenderers()
        {
            if (lineSafest == null) lineSafest = CreateRouteRenderer("LineRenderer_SafestRoute", colorSafest);
            if (lineShortest == null) lineShortest = CreateRouteRenderer("LineRenderer_ShortestRoute", colorShortest);
            if (lineScience == null) lineScience = CreateRouteRenderer("LineRenderer_ScienceRoute", colorScience);
            if (lineIngenuity == null) lineIngenuity = CreateRouteRenderer("LineRenderer_IngenuityFlights", colorIngenuity, 0.35f);
            if (linePerseveranceHistory == null) linePerseveranceHistory = CreateRouteRenderer("LineRenderer_PerseveranceHistory", colorPerseveranceHistory, 0.45f);
        }

        private LineRenderer CreateRouteRenderer(string objectName, Color color, float overrideWidth = -1f)
        {
            Transform existing = transform.Find(objectName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) lr = go.AddComponent<LineRenderer>();

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
            Material lineMat = new Material(lineShader);
            lineMat.color = color;
            lr.material = lineMat;

            lr.startColor = color;
            lr.endColor = color;
            float w = overrideWidth > 0f ? overrideWidth : lineWidth;
            lr.startWidth = w;
            lr.endWidth = w;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            return lr;
        }

        private void PopulateRouteLines()
        {
            var data = MarsDatasetLoader.Instance?.RoutesData;
            if (data != null)
            {
                SetLinePoints(lineSafest, data.safest.unityPositions);
                SetLinePoints(lineShortest, data.shortest.unityPositions);
                SetLinePoints(lineScience, data.science.unityPositions);
            }

            if (MarsDatasetLoader.Instance != null)
            {
                var ingPts = new List<Vector3>();
                foreach (var fl in MarsDatasetLoader.Instance.IngenuityFlights)
                {
                    foreach (var p in fl.points)
                    {
                        ingPts.Add(new Vector3(p.x, p.y + 1.8f, p.z));
                    }
                }
                SetLinePoints(lineIngenuity, ingPts);
                if (lineIngenuity != null) lineIngenuity.enabled = showHistoricalOverlays;

                foreach (var oldExtra in persevExtraRenderers)
                {
                    if (oldExtra != null) Destroy(oldExtra.gameObject);
                }
                persevExtraRenderers.Clear();

                var persevSegments = new List<List<Vector3>>();
                var activeSeg = new List<Vector3>();
                Vector3? prevPt = null;
                foreach (var leg in MarsDatasetLoader.Instance.PerseveranceTraverses)
                {
                    if (leg.points == null || leg.points.Count == 0) continue;
                    if (prevPt.HasValue && Vector3.Distance(prevPt.Value, leg.points[0]) > 25.0f && activeSeg.Count >= 2)
                    {
                        persevSegments.Add(activeSeg);
                        activeSeg = new List<Vector3>();
                    }
                    foreach (var p in leg.points)
                    {
                        if (prevPt.HasValue && Vector3.Distance(prevPt.Value, p) > 25.0f && activeSeg.Count >= 2)
                        {
                            persevSegments.Add(activeSeg);
                            activeSeg = new List<Vector3>();
                        }
                        activeSeg.Add(p);
                        prevPt = p;
                    }
                }
                if (activeSeg.Count >= 2) persevSegments.Add(activeSeg);

                if (persevSegments.Count > 0)
                {
                    SetLinePoints(linePerseveranceHistory, persevSegments[0]);
                    for (int sIdx = 1; sIdx < persevSegments.Count; sIdx++)
                    {
                        LineRenderer extraLr = CreateRouteRenderer($"PersevSegment_{sIdx}", colorPerseveranceHistory, 0.45f);
                        if (linePerseveranceHistory != null)
                        {
                            extraLr.transform.SetParent(linePerseveranceHistory.transform, false);
                        }
                        SetLinePoints(extraLr, persevSegments[sIdx]);
                        extraLr.enabled = showHistoricalOverlays;
                        persevExtraRenderers.Add(extraLr);
                    }
                }
                if (linePerseveranceHistory != null) linePerseveranceHistory.enabled = showHistoricalOverlays;
            }
        }

        private void SetLinePoints(LineRenderer lr, List<Vector3> points)
        {
            if (lr == null || points == null || points.Count == 0) return;

            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                // Elevate slightly above ground to prevent z-fighting
                Vector3 pt = points[i];
                pt.y += 0.25f;
                lr.SetPosition(i, pt);
            }
        }

        private void SpawnWaypoints()
        {
            if (waypointContainer == null)
            {
                Transform existing = transform.Find("WaypointMarkersRoot");
                if (existing != null) waypointContainer = existing;
                else
                {
                    GameObject go = new GameObject("WaypointMarkersRoot");
                    go.transform.SetParent(transform, false);
                    waypointContainer = go.transform;
                }
            }

            // Spawn Landing Site Marker
            CreateBeacon("Octavia_E_Butler_Landing", Vector3.zero, new Color(1.0f, 0.8f, 0.2f, 1.0f), 1.5f);

            // Spawn Science Site Marker (Rochette)
            var sci = MarsDatasetLoader.Instance?.RoutesData?.science_site;
            if (sci != null)
            {
                float uX = (float)(sci.local_east_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_east_m ?? 512.73));
                float uZ = (float)(sci.local_north_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_north_m ?? 945.43));
                CreateBeacon("Science_Site_Rochette", new Vector3(uX, 0f, uZ), colorScience, 1.2f);
            }

            // Spawn ALL 22 NASA Perseverance Sample Tubes
            if (showSampleTubes && MarsDatasetLoader.Instance != null)
            {
                foreach (var tube in MarsDatasetLoader.Instance.SampleTubes)
                {
                    CreateSampleMarker($"SampleTube_{tube.sample_num}_{tube.sample_name}", tube.unityPosition, tube);
                }
            }
        }

        private void CreateBeacon(string name, Vector3 pos, Color color, float scale)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = name;
            beacon.transform.SetParent(waypointContainer, false);
            beacon.transform.position = pos + new Vector3(0, scale * 1.5f, 0);
            beacon.transform.localScale = new Vector3(scale * 0.4f, scale * 1.5f, scale * 0.4f);

            Collider col = beacon.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = beacon.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = color;
                r.material = mat;
            }
        }

        private void CreateSampleMarker(string name, Vector3 pos, SampleTubeData tube)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(waypointContainer, false);
            float groundY = MarsVisualRealismPolish.SampleTerrainElevation(pos.x, pos.z);
            marker.transform.position = new Vector3(pos.x, groundY + 0.55f, pos.z);
            marker.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            SphereCollider col = marker.GetComponent<SphereCollider>();
            if (col != null)
            {
                col.isTrigger = false;
                col.radius = 0.9f;
            }

            Renderer r = marker.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                Material mat = new Material(s);
                Color c = tube.rock_type.Contains("Sedimentary") ? Color.cyan : new Color(1f, 0.5f, 0f);
                mat.color = c;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.65f);
                r.material = mat;
            }

            var target = marker.GetComponent<MarsInspectableTarget>() ?? marker.AddComponent<MarsInspectableTarget>();
            string locLower = (tube.sample_location + " " + tube.outcrop_name + " " + tube.sample_name).ToLowerInvariant();
            string imgFile = "perseverance_sample_raw.jpg";
            if (locLower.Contains("witch") || locLower.Contains("silver") || locLower.Contains("bell") || locLower.Contains("mainz"))
                imgFile = "witch_hazel_hill_sol1401.jpg";
            else if (locLower.Contains("cheyava") || locLower.Contains("neretva") || locLower.Contains("bright"))
                imgFile = "cheyava_falls_sol1215.jpg";
            else if (locLower.Contains("berea") || locLower.Contains("fan") || locLower.Contains("margin"))
                imgFile = "berea_sol749.jpg";
            else if (locLower.Contains("wildcat") || locLower.Contains("skinner") || locLower.Contains("hazelwood"))
                imgFile = "wildcat_ridge_sol509.jpg";
            else if (locLower.Contains("rochette") || locLower.Contains("maaz") || locLower.Contains("artuby") || locLower.Contains("brac") || locLower.Contains("montdenier"))
                imgFile = "rochette_sol194.jpg";

            target.Configure(
                id: "sample_tube_" + tube.sample_num,
                title: $"Sample Tube #{tube.sample_num} · {tube.sample_name}",
                category: "NASA Perseverance Sample Core",
                subtitle: $"{tube.sample_type} · {tube.rock_type}",
                coords: $"{tube.lat:F5}°N, {tube.lon:F5}°E · Elev {tube.elev:F1} m MOLA",
                geology: $"Outcrop: {tube.outcrop_name} ({tube.sample_location})\nCore Length: {(tube.sample_ht_cm > 0 ? tube.sample_ht_cm.ToString("F1") + " cm" : "Sealed Tube")} · Date: {tube.date}",
                mineralogy: tube.rock_type.Contains("Sedimentary")
                    ? "Deltaic mudstone/sandstone: smectite phyllosilicates, Fe/Mg-carbonate, sulfate veins (high biosignature preservation potential)."
                    : "Igneous/volcanic cumulate & basalt: olivine, pyroxene, plagioclase with aqueous alteration salts in voids.",
                environment: "Jezero Crater Surface · MEDA P ~715 Pa · RAD ~0.67 mSv/day · M2020 Cached Core",
                summary: $"Real NASA Mars 2020 sample tube archived for Mars Sample Return / Sentinel field reference at Jezero Crater ({tube.sample_location}).",
                sourceUrl: "https://science.nasa.gov/mission/mars-2020-perseverance/mars-rock-samples/",
                imageFile: imgFile
            );
        }

        public void SetRouteVisibility(bool safest, bool shortest, bool science)
        {
            if (lineSafest != null) lineSafest.enabled = safest;
            if (lineShortest != null) lineShortest.enabled = shortest;
            if (lineScience != null) lineScience.enabled = science;
        }

        public void SetHistoricalOverlaysVisibility(bool ingenuityVisible, bool perseveranceHistoryVisible)
        {
            showHistoricalOverlays = ingenuityVisible || perseveranceHistoryVisible;
            if (lineIngenuity != null) lineIngenuity.enabled = ingenuityVisible;
            if (linePerseveranceHistory != null) linePerseveranceHistory.enabled = perseveranceHistoryVisible;
            foreach (var extra in persevExtraRenderers)
            {
                if (extra != null) extra.enabled = perseveranceHistoryVisible;
            }
        }

        public void UpdateRoverMotionTelemetry(float speedMps, float headingDeg, float distanceTraveled)
        {
            telemetry.currentSpeedMps = speedMps;
            telemetry.headingDeg = headingDeg;
            telemetry.distanceTraveledMeters = distanceTraveled;
            OnTelemetryUpdated?.Invoke(telemetry);
        }
    }
}
