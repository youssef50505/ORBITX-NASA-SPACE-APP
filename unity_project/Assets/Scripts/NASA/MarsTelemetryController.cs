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
    public enum NasaDataLayerMode
    {
        SafestRouteFocus = 0,
        AllThreeAStarRoutes = 1,
        HistoricalM20AndIngenuity = 2,
        FullLayeredMap = 3,
        CleanCinemaView = 4
    }

    public class MarsTelemetryController : MonoBehaviour
    {
        public static MarsTelemetryController Instance { get; private set; }

        [Header("Route Line Renderers")]
        [SerializeField] private LineRenderer lineSafest;
        [SerializeField] private LineRenderer lineShortest;
        [SerializeField] private LineRenderer lineScience;
        [SerializeField] private LineRenderer lineIngenuity;
        [SerializeField] private LineRenderer linePerseveranceHistory;
        [SerializeField] private float lineWidth = 0.55f;

        [Header("Colors")]
        [SerializeField] private Color colorSafest = new Color(0.0f, 0.95f, 0.75f, 1.0f);   // Cyan / Green
        [SerializeField] private Color colorShortest = new Color(1.0f, 0.84f, 0.0f, 1.0f);  // Yellow
        [SerializeField] private Color colorScience = new Color(0.9f, 0.25f, 0.98f, 1.0f);  // Magenta
        [SerializeField] private Color colorIngenuity = new Color(0.25f, 0.85f, 1.0f, 0.9f); // Sky Blue
        [SerializeField] private Color colorPerseveranceHistory = new Color(1.0f, 0.52f, 0.22f, 0.85f); // Rusty Orange

        [Header("Waypoints")]
        [SerializeField] private Transform waypointContainer;
        [SerializeField] private bool showSampleTubes = true;
        [SerializeField] private bool showHistoricalOverlays = false;

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
        private readonly List<Renderer> campusSampleRenderers = new List<Renderer>();
        private readonly List<Renderer> distantSampleRenderers = new List<Renderer>();
        private readonly List<Renderer> waypointBeaconRenderers = new List<Renderer>();

        public NasaDataLayerMode CurrentLayerMode { get; private set; } = NasaDataLayerMode.SafestRouteFocus;
        public int ChecklistStepsCompleted { get; private set; } = 1;
        public string NearestSampleSummary { get; private set; } = "Sample Cache Active (22 Tubes)";

        public RoverTelemetry CurrentTelemetry => telemetry;
        public event Action<RoverTelemetry> OnTelemetryUpdated;

        public void SetChecklistProgress(int completedCount)
        {
            ChecklistStepsCompleted = Mathf.Clamp(completedCount, 0, 5);
        }

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
            ApplyDataLayerMode(CurrentLayerMode);
            OnTelemetryUpdated?.Invoke(telemetry);
        }

        private void EnsureLineRenderers()
        {
            if (lineSafest == null) lineSafest = CreateRouteRenderer("LineRenderer_SafestRoute", colorSafest, 0.55f);
            if (lineShortest == null) lineShortest = CreateRouteRenderer("LineRenderer_ShortestRoute", colorShortest, 0.38f);
            if (lineScience == null) lineScience = CreateRouteRenderer("LineRenderer_ScienceRoute", colorScience, 0.42f);
            if (lineIngenuity == null) lineIngenuity = CreateRouteRenderer("LineRenderer_IngenuityFlights", colorIngenuity, 0.28f);
            if (linePerseveranceHistory == null) linePerseveranceHistory = CreateRouteRenderer("LineRenderer_PerseveranceHistory", colorPerseveranceHistory, 0.32f);

            ApplyLineWidth(lineSafest, 0.55f);
            ApplyLineWidth(lineShortest, 0.38f);
            ApplyLineWidth(lineScience, 0.42f);
            ApplyLineWidth(lineIngenuity, 0.28f);
            ApplyLineWidth(linePerseveranceHistory, 0.32f);
        }

        private static void ApplyLineWidth(LineRenderer lr, float width)
        {
            if (lr == null) return;
            lr.startWidth = width;
            lr.endWidth = width;
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
                        LineRenderer extraLr = CreateRouteRenderer($"PersevSegment_{sIdx}", colorPerseveranceHistory, 0.32f);
                        if (linePerseveranceHistory != null)
                        {
                            extraLr.transform.SetParent(linePerseveranceHistory.transform, false);
                        }
                        extraLr.startWidth = 0.32f;
                        extraLr.endWidth = 0.32f;
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

            bool isIngenuity = lr == lineIngenuity || lr.name.IndexOf("Ingenuity", StringComparison.OrdinalIgnoreCase) >= 0;
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                // Elevate 0.35m above Jezero terrain elevation to prevent z-fighting
                Vector3 pt = points[i];
                float terrainY = MarsVisualRealismPolish.SampleTerrainElevation(pt.x, pt.z);
                pt.y = isIngenuity ? Mathf.Max(pt.y, terrainY) + 0.35f : (terrainY + 0.35f);
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
            campusSampleRenderers.Clear();
            distantSampleRenderers.Clear();
            waypointBeaconRenderers.Clear();

            CreateBeacon("Octavia_E_Butler_Landing", Vector3.zero, new Color(1.0f, 0.8f, 0.2f, 1.0f), 1.5f);

            // Spawn Science Site Marker (Rochette)
            var sci = MarsDatasetLoader.Instance?.RoutesData?.science_site;
            if (sci != null)
            {
                float uX = (float)(sci.local_east_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_east_m ?? 512.73));
                float uZ = (float)(sci.local_north_m - (MarsDatasetLoader.Instance?.RoutesData?.landing?.local_north_m ?? 945.43));
                CreateBeacon("Science_Site_Rochette", new Vector3(uX, 0f, uZ), colorScience, 1.2f);
            }

            // Spawn ALL 22 NASA Perseverance Sample Tubes (distant markers hidden cleanly in focused view)
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
                waypointBeaconRenderers.Add(r);
            }
        }

        private void CreateSampleMarker(string name, Vector3 pos, SampleTubeData tube)
        {
            float horizDist = new Vector2(pos.x, pos.z).magnitude;
            bool withinCampus = horizDist <= 90.0f;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(waypointContainer, false);
            float groundY = MarsVisualRealismPolish.SampleTerrainElevation(pos.x, pos.z);
            marker.transform.position = new Vector3(pos.x, groundY + 0.35f, pos.z);
            float markerScale = withinCampus ? 0.42f : 0.32f;
            marker.transform.localScale = new Vector3(markerScale, markerScale, markerScale);

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

                if (withinCampus)
                {
                    campusSampleRenderers.Add(r);
                    r.enabled = CurrentLayerMode != NasaDataLayerMode.CleanCinemaView;
                }
                else
                {
                    distantSampleRenderers.Add(r);
                    r.enabled = CurrentLayerMode == NasaDataLayerMode.HistoricalM20AndIngenuity
                        || CurrentLayerMode == NasaDataLayerMode.FullLayeredMap;
                }
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

        public void SetHistoricalOverlaysVisibility(bool showIngenuity, bool showPerseverance)
        {
            showHistoricalOverlays = showIngenuity || showPerseverance;
            if (lineIngenuity != null) lineIngenuity.enabled = showIngenuity;
            if (linePerseveranceHistory != null) linePerseveranceHistory.enabled = showPerseverance;
            foreach (var extra in persevExtraRenderers)
            {
                if (extra != null) extra.enabled = showPerseverance;
            }
        }

        public void ApplyDataLayerMode(NasaDataLayerMode mode)
        {
            CurrentLayerMode = mode;

            bool showSafest = mode == NasaDataLayerMode.SafestRouteFocus
                || mode == NasaDataLayerMode.AllThreeAStarRoutes
                || mode == NasaDataLayerMode.FullLayeredMap;
            bool showShortest = mode == NasaDataLayerMode.AllThreeAStarRoutes
                || mode == NasaDataLayerMode.FullLayeredMap;
            bool showScience = mode == NasaDataLayerMode.AllThreeAStarRoutes
                || mode == NasaDataLayerMode.FullLayeredMap;
            bool showIngenuity = mode == NasaDataLayerMode.HistoricalM20AndIngenuity
                || mode == NasaDataLayerMode.FullLayeredMap;
            bool showPerseverance = mode == NasaDataLayerMode.HistoricalM20AndIngenuity
                || mode == NasaDataLayerMode.FullLayeredMap;

            SetRouteVisibility(showSafest, showShortest, showScience);
            SetHistoricalOverlaysVisibility(showIngenuity, showPerseverance);

            bool showCampusSamples = mode != NasaDataLayerMode.CleanCinemaView;
            bool showDistantSamples = mode == NasaDataLayerMode.HistoricalM20AndIngenuity
                || mode == NasaDataLayerMode.FullLayeredMap;

            foreach (var r in campusSampleRenderers)
            {
                if (r != null) r.enabled = showCampusSamples;
            }
            foreach (var r in distantSampleRenderers)
            {
                if (r != null) r.enabled = showDistantSamples;
            }
            foreach (var r in waypointBeaconRenderers)
            {
                if (r != null) r.enabled = showCampusSamples;
            }
        }

        public static string GetLayerModeDisplayName(NasaDataLayerMode mode)
        {
            switch (mode)
            {
                case NasaDataLayerMode.SafestRouteFocus: return "Safest A* Route";
                case NasaDataLayerMode.AllThreeAStarRoutes: return "All 3 A* Routes";
                case NasaDataLayerMode.HistoricalM20AndIngenuity: return "M20 + Ingenuity 72";
                case NasaDataLayerMode.FullLayeredMap: return "Full NASA Overlay";
                case NasaDataLayerMode.CleanCinemaView: return "Clean Cinema View";
                default: return mode.ToString();
            }
        }

        public void GetLiveNavigationMetrics(
            Vector3 playerWorldPos,
            out string nearestOutcropName,
            out string nearestGeologySymbol,
            out float distToActiveRouteM,
            out int completedChecklistSteps)
        {
            GetLiveNavigationMetrics(
                playerWorldPos,
                out nearestOutcropName,
                out _,
                out nearestGeologySymbol,
                out _,
                out _,
                out _,
                out distToActiveRouteM,
                out _,
                out completedChecklistSteps);
        }

        public void GetLiveNavigationMetrics(
            Vector3 playerWorldPos,
            out string nearestOutcropName,
            out float nearestOutcropDistM,
            out string nearestGeologySymbol,
            out string nearestGeologyName,
            out int nearestGeologyTrafficability,
            out string nearestMountSharpNote,
            out float distToActiveRouteM,
            out float distToGoalM,
            out int completedChecklistSteps)
        {
            nearestOutcropName = "Octavia E. Butler";
            nearestOutcropDistM = 0f;
            nearestGeologySymbol = "Nh_lf";
            nearestGeologyName = "Noachian Jezero Layered Delta";
            nearestGeologyTrafficability = 4;
            nearestMountSharpNote = "Gale Curiosity Analog · Carolyn's Shoals";
            distToActiveRouteM = 0f;
            distToGoalM = 0f;
            completedChecklistSteps = ChecklistStepsCompleted;

            var loader = MarsDatasetLoader.Instance;
            if (loader == null) return;

            Vector2 playerXZ = new Vector2(playerWorldPos.x, playerWorldPos.z);

            // 1. Spatial lookup across NASA Jezero Outcrops
            JezeroOutcropData bestOutcrop = null;
            float bestOutcropDist = float.MaxValue;
            int bestOutcropIdx = 0;
            if (loader.Outcrops != null && loader.Outcrops.Count > 0)
            {
                for (int i = 0; i < loader.Outcrops.Count; i++)
                {
                    var o = loader.Outcrops[i];
                    if (o == null) continue;
                    float d = Vector2.Distance(playerXZ, o.unityPosXZ);
                    if (d < bestOutcropDist)
                    {
                        bestOutcropDist = d;
                        bestOutcrop = o;
                        bestOutcropIdx = i;
                    }
                }
                if (bestOutcrop != null)
                {
                    nearestOutcropName = bestOutcrop.name;
                    nearestOutcropDistM = bestOutcropDist;
                }
            }

            // 2. Spatial lookup across 22 M20 Sample Tubes
            if (loader.SampleTubes != null && loader.SampleTubes.Count > 0)
            {
                SampleTubeData bestTube = null;
                float bestTubeDist = float.MaxValue;
                for (int i = 0; i < loader.SampleTubes.Count; i++)
                {
                    var tube = loader.SampleTubes[i];
                    if (tube == null) continue;
                    float d = Vector2.Distance(playerXZ, new Vector2(tube.unityPosition.x, tube.unityPosition.z));
                    if (d < bestTubeDist)
                    {
                        bestTubeDist = d;
                        bestTube = tube;
                    }
                }
                if (bestTube != null)
                {
                    NearestSampleSummary = $"Tube #{bestTube.sample_num} {bestTube.sample_name} ({bestTubeDist:F1}m)";
                }
            }

            // 3. Matching USGS SIM 3292 Geology Unit from outcrop context or Jezero zone
            if (loader.GeologyUnits != null && loader.GeologyUnits.Count > 0)
            {
                GeologyUnit matched = null;
                if (bestOutcrop != null && !string.IsNullOrEmpty(bestOutcrop.usgs_unit))
                {
                    for (int i = 0; i < loader.GeologyUnits.Count; i++)
                    {
                        var gu = loader.GeologyUnits[i];
                        if (gu != null && string.Equals(gu.symbol, bestOutcrop.usgs_unit, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = gu;
                            break;
                        }
                    }
                }
                if (matched == null)
                {
                    int sectorIdx = Mathf.Abs(Mathf.RoundToInt(playerXZ.x * 0.25f) + Mathf.RoundToInt(playerXZ.y * 0.35f) + bestOutcropIdx) % loader.GeologyUnits.Count;
                    matched = loader.GeologyUnits[sectorIdx];
                }
                if (matched != null)
                {
                    nearestGeologySymbol = matched.symbol;
                    nearestGeologyName = matched.name;
                    nearestGeologyTrafficability = matched.trafficability;
                }
            }

            // 4. Matching Curiosity Gale/Mount Sharp comparison stop
            if (loader.MountSharpStops != null && loader.MountSharpStops.Count > 0)
            {
                string searchCtx = ((bestOutcrop != null ? (bestOutcrop.formation + " " + bestOutcrop.crism_mineralogy + " " + bestOutcrop.rock_type) : "") + " " + nearestGeologyName).ToLowerInvariant();
                CuriosityMountSharpStop bestStop = null;
                int bestScore = -1;
                for (int i = 0; i < loader.MountSharpStops.Count; i++)
                {
                    var st = loader.MountSharpStops[i];
                    if (st == null) continue;
                    string stopText = (st.name + " " + st.unit + " " + st.finding).ToLowerInvariant();
                    int score = 0;
                    if (searchCtx.Contains("sulfate") && stopText.Contains("sulfate")) score += 3;
                    if (searchCtx.Contains("clay") && (stopText.Contains("clay") || stopText.Contains("smectite"))) score += 3;
                    if (searchCtx.Contains("carbonate") && stopText.Contains("carbonate")) score += 2;
                    if (searchCtx.Contains("delta") && (stopText.Contains("delta") || stopText.Contains("lacustrine"))) score += 2;
                    if (searchCtx.Contains("sand") && stopText.Contains("sand")) score += 2;
                    if (searchCtx.Contains("mud") && stopText.Contains("mud")) score += 2;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestStop = st;
                    }
                }
                if (bestStop == null)
                {
                    bestStop = loader.MountSharpStops[bestOutcropIdx % loader.MountSharpStops.Count];
                }
                nearestMountSharpNote = $"MSL Sol {bestStop.sol} {bestStop.name} ({bestStop.unit})";
            }

            // 5. Distance to active A* route & traverse goal
            if (loader.RoutesData != null)
            {
                float dSafest = DistanceToRouteXZ(playerXZ, loader.RoutesData.safest?.unityPositions, out float goalSafest);
                float dShortest = DistanceToRouteXZ(playerXZ, loader.RoutesData.shortest?.unityPositions, out float goalShortest);
                float dScience = DistanceToRouteXZ(playerXZ, loader.RoutesData.science?.unityPositions, out float goalScience);

                if (CurrentLayerMode == NasaDataLayerMode.AllThreeAStarRoutes || CurrentLayerMode == NasaDataLayerMode.FullLayeredMap)
                {
                    distToActiveRouteM = Mathf.Min(dSafest, Mathf.Min(dShortest, dScience));
                    distToGoalM = goalSafest;
                }
                else
                {
                    distToActiveRouteM = dSafest;
                    distToGoalM = goalSafest;
                }
            }
        }

        private static float DistanceToRouteXZ(Vector2 p, List<Vector3> routePts, out float distToGoalM)
        {
            distToGoalM = 0f;
            if (routePts == null || routePts.Count == 0) return 0f;

            Vector3 goal = routePts[routePts.Count - 1];
            distToGoalM = Vector2.Distance(p, new Vector2(goal.x, goal.z));

            if (routePts.Count == 1)
            {
                return Vector2.Distance(p, new Vector2(routePts[0].x, routePts[0].z));
            }

            float minDistSq = float.MaxValue;
            for (int i = 0; i < routePts.Count - 1; i++)
            {
                Vector2 a = new Vector2(routePts[i].x, routePts[i].z);
                Vector2 b = new Vector2(routePts[i + 1].x, routePts[i + 1].z);
                Vector2 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
                Vector2 proj = a + ab * t;
                float dSq = (p - proj).sqrMagnitude;
                if (dSq < minDistSq) minDistSq = dSq;
            }
            return Mathf.Sqrt(minDistSq);
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
