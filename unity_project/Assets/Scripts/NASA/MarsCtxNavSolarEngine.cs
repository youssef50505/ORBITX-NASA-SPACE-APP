using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Full C# port of the nav-module regional navigation, Mars orbital solar optics,
    /// terrain shadow ray-marching, RF Line-of-Sight occlusion, and Outpost Site Check
    /// running over the real 399x378 USGS CTX DTM Mosaic (jez_ctx_20m_grid.json).
    /// </summary>
    public class MarsCtxNavSolarEngine : MonoBehaviour
    {
        public static MarsCtxNavSolarEngine Instance { get; private set; }

        // Physical & routing parameters matching nav-module/nav.py & sun_core.js
        public const float MaxSlopeDeg = 25.0f;
        public const float RoughRefMeters = 4.0f;
        public const float WeightSlope = 8.0f;
        public const float WeightRough = 4.0f;
        public const float EvaSpeedMps = 0.8f;
        public const float SimplifyToleranceM = 120.0f;

        private const double ObliquityDeg = 25.19;
        private const double Eccentricity = 0.0934;
        private const double SemiMajorAu = 1.5237;
        private const double PerihelionLsDeg = 251.0;
        private const double SolarConst1Au = 1361.0;
        private const double ClearSkyTau = 0.5;
        private const double JezeroLatDeg = 18.4447;

        public struct CtxRouteStats
        {
            public bool valid;
            public string mode;
            public float distanceMeters;
            public float etaMinutes;
            public float maxSlopeDeg;
            public float meanRisk;
            public int waypointCount;
            public List<int> pathCells;
            public List<int> simplifiedCells;
        }

        public struct SiteCheckResult
        {
            public bool pass;
            public float slopeDeg;
            public float roughMeters;
            public int daylightPercent;
            public bool slopeOk;
            public bool roughOk;
            public bool sunOk;
        }

        public struct RfLosResult
        {
            public bool clear;
            public float distanceMeters;
            public float signalPercent;
        }

        private float[] elevations;
        private float[] slopeDeg;
        private float[] roughM;
        private float[] risk;
        private float[] aspectDeg;
        private int rows;
        private int cols;
        private float cellMeters = 80f;

        private static readonly (string label, int sr, int sc, int gr, int gc)[] Presets =
        {
            ("Delta Rim Bypass", 281, 142, 221, 125),
            ("Western Channel Ridge", 190, 49, 177, 7),
            ("Neretva Outcrop Approach", 153, 127, 100, 142)
        };

        public bool IsLoaded { get; private set; }
        public int ActivePresetIndex { get; private set; } = 0;
        public string ActivePresetName => Presets[ActivePresetIndex].label;
        public float SolarLongitudeLs { get; private set; } = 90f;
        public float AntennaHeightM { get; private set; } = 6f;

        public CtxRouteStats ShortestRoute { get; private set; }
        public CtxRouteStats SafestRoute { get; private set; }
        public SiteCheckResult ActiveSiteCheck { get; private set; }
        public RfLosResult ActiveRfLos { get; private set; }
        public float CurrentSolarElevationDeg { get; private set; } = 45f;
        public float CurrentSolarAzimuthDeg { get; private set; } = 180f;
        public float CurrentSurfaceIrradianceWm2 { get; private set; } = 420f;
        public bool IsPlayerTerrainLit { get; private set; } = true;

        private float liveUpdateTimer;

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
            LoadCtxGrid();
            if (IsLoaded)
            {
                RecomputePreset(0);
            }
        }

        private void Update()
        {
            if (!IsLoaded) return;
            liveUpdateTimer += Time.deltaTime;
            if (liveUpdateTimer >= 0.4f)
            {
                liveUpdateTimer = 0f;
                RefreshLivePlayerOpticsAndRf();
            }
        }

        public void CyclePreset()
        {
            if (!IsLoaded) return;
            ActivePresetIndex = (ActivePresetIndex + 1) % Presets.Length;
            RecomputePreset(ActivePresetIndex);
            MarsAudioManager.Instance?.PlayUiClick();
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        public void AdvanceSolarSeason(float deltaLs = 30f)
        {
            SolarLongitudeLs = (SolarLongitudeLs + deltaLs) % 360f;
            if (SolarLongitudeLs < 0f) SolarLongitudeLs += 360f;
            if (IsLoaded)
            {
                RecomputePreset(ActivePresetIndex);
                RefreshLivePlayerOpticsAndRf();
            }
            MarsAudioManager.Instance?.PlayUiClick();
            MarsSceneManager.Instance?.RefreshLiveExplorationHud();
        }

        public void RecomputePreset(int presetIdx)
        {
            if (!IsLoaded) return;
            ActivePresetIndex = Mathf.Clamp(presetIdx, 0, Presets.Length - 1);
            var p = Presets[ActivePresetIndex];
            int s = p.sr * cols + p.sc;
            int g = p.gr * cols + p.gc;
            ShortestRoute = ComputeAStar(s, g, false);
            SafestRoute = ComputeAStar(s, g, true);
            ActiveSiteCheck = EvaluateSiteSuitability(g, JezeroLatDeg, SolarLongitudeLs);
            ActiveRfLos = CheckRadioLos(s, g, AntennaHeightM);
        }

        public void RefreshLivePlayerOpticsAndRf()
        {
            if (!IsLoaded) return;
            Vector3 playerPos = Vector3.zero;
            if (MarsRoverVehicleController.Instance != null && MarsRoverVehicleController.Instance.IsDriverSeated)
                playerPos = MarsRoverVehicleController.Instance.transform.position;
            else if (MarsAstronautController.Instance != null)
                playerPos = MarsAstronautController.Instance.transform.position;

            var cel = FindFirstObjectByType<MarsCelestialOrbitController>();
            float solHour = cel != null ? cel.SolHour : 12.0f;
            float tau = MarsSceneManager.Instance != null ? MarsSceneManager.Instance.DustOpticalDepthTau : (float)ClearSkyTau;

            // Map player local Jezero world coordinates onto CTX cell space anchored at Octavia E. Butler Landing
            int baseRow = 200;
            int baseCol = 195;
            int pRow = Mathf.Clamp(baseRow - Mathf.RoundToInt(playerPos.z * 0.25f), 0, rows - 1);
            int pCol = Mathf.Clamp(baseCol + Mathf.RoundToInt(playerPos.x * 0.25f), 0, cols - 1);
            int pIdx = pRow * cols + pCol;
            int baseIdx = baseRow * cols + baseCol;

            ComputeSolarPos(JezeroLatDeg, SolarLongitudeLs, solHour, out double elDeg, out double azDeg);
            CurrentSolarElevationDeg = (float)elDeg;
            CurrentSolarAzimuthDeg = (float)azDeg;
            IsPlayerTerrainLit = IsCellLit(pIdx, azDeg, elDeg);
            CurrentSurfaceIrradianceWm2 = IsPlayerTerrainLit
                ? (float)ComputeIrradiance(pIdx, SolarLongitudeLs, azDeg, elDeg, tau)
                : 0f;

            // Combine CTX terrain LOS + live 3D Unity terrain physics raycast against Base HDU antenna!
            RfLosResult ctxLos = CheckRadioLos(baseIdx, pIdx, AntennaHeightM);
            bool physClear = true;
            Vector3 baseWorld = new Vector3(
                MarsBaseFacilitiesBuilder.POS_HABITAT_HDU.x,
                MarsVisualRealismPolish.SampleTerrainElevation(MarsBaseFacilitiesBuilder.POS_HABITAT_HDU.x, MarsBaseFacilitiesBuilder.POS_HABITAT_HDU.y) + AntennaHeightM,
                MarsBaseFacilitiesBuilder.POS_HABITAT_HDU.y);
            Vector3 playerAnt = playerPos + Vector3.up * 2.2f;
            Vector3 delta = playerAnt - baseWorld;
            float physDist = delta.magnitude;
            if (physDist > 12f)
            {
                if (Physics.Raycast(baseWorld, delta.normalized, out RaycastHit hit, physDist - 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && (hit.collider.gameObject.name.Contains("Terrain") || hit.collider.gameObject.name.Contains("Outcrop")))
                    {
                        physClear = false;
                    }
                }
            }

            bool clear = ctxLos.clear && physClear;
            float distAtten = Mathf.Clamp01(1f - physDist / 450f);
            float dustAtten = Mathf.Clamp01(1f - (tau - 0.45f) * 0.14f);
            float sig = clear ? Mathf.Clamp(distAtten * dustAtten * 100f, 38f, 100f) : Mathf.Clamp(18f * distAtten, 5f, 24f);
            ActiveRfLos = new RfLosResult
            {
                clear = clear,
                distanceMeters = physDist,
                signalPercent = sig
            };
        }

        public string GetCompactHudSummary()
        {
            if (!IsLoaded) return "CTX Nav Engine: Loading...";
            string rfTxt = ActiveRfLos.clear ? $"RF {ActiveRfLos.signalPercent:F0}% CLR" : $"RF {ActiveRfLos.signalPercent:F0}% OCCLUDED";
            string siteTxt = ActiveSiteCheck.pass ? "SITE PASS" : $"SITE WARN({ActiveSiteCheck.slopeDeg:F1}°)";
            return $"CTX A* [{ActivePresetName}] Safest {SafestRoute.distanceMeters:F0}m ({SafestRoute.maxSlopeDeg:F1}°) vs Short {ShortestRoute.distanceMeters:F0}m ({ShortestRoute.maxSlopeDeg:F1}°) | Ls {SolarLongitudeLs:F0}° {CurrentSurfaceIrradianceWm2:F0}W/m² | {rfTxt} | {siteTxt}";
        }

        private void LoadCtxGrid()
        {
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "NASA_Data", "jez_ctx_20m_grid.json"),
                Path.Combine(Application.dataPath, "..", "..", "data", "jez_ctx_20m_grid.json")
            };
            string path = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i])) { path = candidates[i]; break; }
            }
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[MarsCtxNavSolarEngine] jez_ctx_20m_grid.json not found.");
                return;
            }

            try
            {
                string raw = File.ReadAllText(path);
                cols = ExtractIntField(raw, "\"grid_width\":", 399);
                rows = ExtractIntField(raw, "\"grid_height\":", 378);
                cellMeters = ExtractFloatField(raw, "\"mesh_sample_spacing_m\":", 80f);

                int arrStart = raw.IndexOf("\"elevations_m\":", StringComparison.Ordinal);
                if (arrStart < 0) return;
                int open = raw.IndexOf('[', arrStart);
                if (open < 0) return;

                elevations = new float[rows * cols];
                int idx = 0;
                int len = raw.Length;
                int i = open;
                while (i < len && idx < elevations.Length)
                {
                    char ch = raw[i];
                    if ((ch >= '0' && ch <= '9') || ch == '-' || ch == '.')
                    {
                        int numStart = i;
                        i++;
                        while (i < len && ((raw[i] >= '0' && raw[i] <= '9') || raw[i] == '.' || raw[i] == '-' || raw[i] == 'e' || raw[i] == 'E' || raw[i] == '+'))
                            i++;
                        if (float.TryParse(raw.Substring(numStart, i - numStart), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                        {
                            elevations[idx++] = val;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }

                if (idx < rows * cols)
                {
                    Debug.LogWarning($"[MarsCtxNavSolarEngine] Parsed {idx}/{rows * cols} elevations.");
                    return;
                }

                PrecomputeTerrainDerivatives();
                IsLoaded = true;
                Debug.Log($"[MarsCtxNavSolarEngine] Loaded {rows}x{cols} USGS CTX DTM ({cellMeters}m cells).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MarsCtxNavSolarEngine] Failed loading CTX grid: {ex.Message}");
            }
        }

        private void PrecomputeTerrainDerivatives()
        {
            int n = rows * cols;
            slopeDeg = new float[n];
            roughM = new float[n];
            risk = new float[n];
            aspectDeg = new float[n];

            for (int r = 0; r < rows; r++)
            {
                int r0 = Math.Max(r - 1, 0);
                int r1 = Math.Min(r + 1, rows - 1);
                for (int c = 0; c < cols; c++)
                {
                    int c0 = Math.Max(c - 1, 0);
                    int c1 = Math.Min(c + 1, cols - 1);
                    int i = r * cols + c;

                    float gx = (elevations[r * cols + c1] - elevations[r * cols + c0]) / ((c1 - c0) * cellMeters);
                    float gy = (elevations[r1 * cols + c] - elevations[r0 * cols + c]) / ((r1 - r0) * cellMeters);
                    float sDeg = Mathf.Atan(Mathf.Sqrt(gx * gx + gy * gy)) * Mathf.Rad2Deg;
                    slopeDeg[i] = sDeg;
                    aspectDeg[i] = (Mathf.Atan2(-gx, gy) * Mathf.Rad2Deg + 360f) % 360f;

                    float nbSum = 0f;
                    int nbCount = 0;
                    for (int a = r0; a <= r1; a++)
                    {
                        for (int b = c0; b <= c1; b++)
                        {
                            if (a != r || b != c)
                            {
                                nbSum += elevations[a * cols + b];
                                nbCount++;
                            }
                        }
                    }
                    float ro = Math.Abs(elevations[i] - (nbSum / Math.Max(1, nbCount)));
                    roughM[i] = ro;
                    risk[i] = 0.6f * Math.Min(sDeg / MaxSlopeDeg, 1f) + 0.4f * Math.Min(ro / RoughRefMeters, 1f);
                }
            }
        }

        public CtxRouteStats ComputeAStar(int startCell, int goalCell, bool safest)
        {
            CtxRouteStats empty = new CtxRouteStats { valid = false, mode = safest ? "safest" : "shortest" };
            if (!IsLoaded || slopeDeg[startCell] > MaxSlopeDeg || slopeDeg[goalCell] > MaxSlopeDeg)
                return empty;

            float ws = safest ? WeightSlope : 0f;
            float wr = safest ? WeightRough : 0f;
            int n = rows * cols;
            float[] best = new float[n];
            int[] prev = new int[n];
            for (int i = 0; i < n; i++) { best[i] = float.PositiveInfinity; prev[i] = -1; }

            int gr = goalCell / cols, gc = goalCell % cols;
            var heap = new MinHeap();
            best[startCell] = 0f;
            float h0 = cellMeters * Mathf.Sqrt((startCell / cols - gr) * (startCell / cols - gr) + (startCell % cols - gc) * (startCell % cols - gc));
            heap.Push(h0, 0f, startCell);

            while (heap.Count > 0)
            {
                var cur = heap.Pop();
                if (cur.cell == goalCell)
                {
                    var path = new List<int>();
                    int c = cur.cell;
                    while (c >= 0) { path.Add(c); c = prev[c]; }
                    path.Reverse();
                    return BuildStats(path, safest ? "safest" : "shortest");
                }
                if (cur.g > best[cur.cell]) continue;

                int r = cur.cell / cols;
                int col = cur.cell % cols;
                float zCur = elevations[cur.cell];

                for (int dr = -1; dr <= 1; dr++)
                {
                    int nr = r + dr;
                    if (nr < 0 || nr >= rows) continue;
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nc = col + dc;
                        if (nc < 0 || nc >= cols) continue;
                        int j = nr * cols + nc;
                        float sj = slopeDeg[j];
                        if (sj > MaxSlopeDeg) continue;

                        float dz = elevations[j] - zCur;
                        float step3d = Mathf.Sqrt((dr * cellMeters) * (dr * cellMeters) + (dc * cellMeters) * (dc * cellMeters) + dz * dz);
                        float sr = sj / MaxSlopeDeg;
                        float rr = Math.Min(roughM[j] / RoughRefMeters, 1f);
                        float ng = cur.g + step3d * (1f + ws * sr * sr + wr * rr);
                        if (ng < best[j])
                        {
                            best[j] = ng;
                            prev[j] = cur.cell;
                            float h = cellMeters * Mathf.Sqrt((nr - gr) * (nr - gr) + (nc - gc) * (nc - gc));
                            heap.Push(ng + h, ng, j);
                        }
                    }
                }
            }
            return empty;
        }

        private CtxRouteStats BuildStats(List<int> path, string mode)
        {
            float dist = 0f;
            float maxSlope = 0f;
            float riskSum = 0f;
            var pts = new List<(float x, float y, int idx)>(path.Count);

            for (int i = 0; i < path.Count; i++)
            {
                int cell = path[i];
                maxSlope = Math.Max(maxSlope, slopeDeg[cell]);
                riskSum += risk[cell];
                float x = (cell % cols) * cellMeters;
                float y = (rows - 1 - (cell / cols)) * cellMeters;
                pts.Add((x, y, i));
                if (i > 0)
                {
                    int prevCell = path[i - 1];
                    float px = (prevCell % cols) * cellMeters;
                    float py = (rows - 1 - (prevCell / cols)) * cellMeters;
                    float dz = elevations[cell] - elevations[prevCell];
                    dist += Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py) + dz * dz);
                }
            }

            var kept = DouglasPeucker(pts, SimplifyToleranceM);
            var wp = new List<int>(kept.Count);
            for (int i = 0; i < kept.Count; i++) wp.Add(path[kept[i].idx]);

            return new CtxRouteStats
            {
                valid = true,
                mode = mode,
                distanceMeters = dist,
                etaMinutes = dist / EvaSpeedMps / 60f,
                maxSlopeDeg = maxSlope,
                meanRisk = riskSum / Math.Max(1, path.Count),
                waypointCount = wp.Count,
                pathCells = path,
                simplifiedCells = wp
            };
        }

        private static List<(float x, float y, int idx)> DouglasPeucker(List<(float x, float y, int idx)> pts, float tol)
        {
            if (pts.Count < 3) return pts;
            var a = pts[0];
            var b = pts[pts.Count - 1];
            float lineLen = Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
            if (lineLen < 1e-6f) lineLen = 1e-6f;

            float maxD = -1f;
            int maxI = 0;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float d = Math.Abs((b.x - a.x) * (a.y - pts[i].y) - (a.x - pts[i].x) * (b.y - a.y)) / lineLen;
                if (d > maxD) { maxD = d; maxI = i; }
            }
            if (maxD <= tol) return new List<(float, float, int)> { a, b };

            var left = DouglasPeucker(pts.GetRange(0, maxI + 1), tol);
            var right = DouglasPeucker(pts.GetRange(maxI, pts.Count - maxI), tol);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }

        public static double ComputeTopOfAtmIrradiance(double lsDeg)
        {
            double rad = (lsDeg - PerihelionLsDeg) * Math.PI / 180.0;
            double r = SemiMajorAu * (1.0 - Eccentricity * Eccentricity) / (1.0 + Eccentricity * Math.Cos(rad));
            return SolarConst1Au / (r * r);
        }

        public static void ComputeSolarPos(double latDeg, double lsDeg, double hour, out double elDeg, out double azDeg)
        {
            double d2r = Math.PI / 180.0;
            double dec = Math.Asin(Math.Sin(ObliquityDeg * d2r) * Math.Sin(lsDeg * d2r));
            double H = (hour - 12.0) * 15.0 * d2r;
            double p = latDeg * d2r;
            double el = Math.Asin(Math.Sin(p) * Math.Sin(dec) + Math.Cos(p) * Math.Cos(dec) * Math.Cos(H));
            double az = Math.Atan2(-Math.Cos(dec) * Math.Sin(H), Math.Sin(dec) * Math.Cos(p) - Math.Cos(dec) * Math.Sin(p) * Math.Cos(H));
            elDeg = el / d2r;
            azDeg = (az / d2r + 360.0) % 360.0;
        }

        private double ComputeCosIncidence(int cellIdx, double azDeg, double elDeg)
        {
            double d2r = Math.PI / 180.0;
            double s = slopeDeg[cellIdx] * d2r;
            return Math.Cos(s) * Math.Sin(elDeg * d2r) + Math.Sin(s) * Math.Cos(elDeg * d2r) * Math.Cos((azDeg - aspectDeg[cellIdx]) * d2r);
        }

        public double ComputeIrradiance(int cellIdx, double lsDeg, double azDeg, double elDeg, double tau = ClearSkyTau)
        {
            if (elDeg <= 0.0) return 0.0;
            double ci = ComputeCosIncidence(cellIdx, azDeg, elDeg);
            if (ci <= 0.0) return 0.0;
            double sinEl = Math.Max(0.05, Math.Sin(elDeg * Math.PI / 180.0));
            return ComputeTopOfAtmIrradiance(lsDeg) * Math.Exp(-tau / sinEl) * ci;
        }

        public bool IsCellLit(int cellIdx, double azDeg, double elDeg)
        {
            if (elDeg <= 0.0 || ComputeCosIncidence(cellIdx, azDeg, elDeg) <= 0.0) return false;
            double d2r = Math.PI / 180.0;
            int r0 = cellIdx / cols;
            int c0 = cellIdx % cols;
            double dc = Math.Sin(azDeg * d2r);
            double dr = -Math.Cos(azDeg * d2r);
            double z0 = elevations[cellIdx] + 1.5;
            double te = Math.Tan(elDeg * d2r);

            for (int k = 1; k <= 75; k++)
            {
                int r = (int)Math.Round(r0 + dr * k);
                int c = (int)Math.Round(c0 + dc * k);
                if (r < 0 || r >= rows || c < 0 || c >= cols) return true;
                if (elevations[r * cols + c] - z0 > te * k * cellMeters) return false;
            }
            return true;
        }

        public RfLosResult CheckRadioLos(int cellA, int cellB, float antennaMeters)
        {
            int ra = cellA / cols, ca = cellA % cols;
            int rb = cellB / cols, cb = cellB % cols;
            float gridDist = Mathf.Sqrt((rb - ra) * (rb - ra) + (cb - ca) * (cb - ca));
            int steps = Math.Max(1, Mathf.CeilToInt(gridDist));
            float za = elevations[cellA] + antennaMeters;
            float zb = elevations[cellB] + antennaMeters;
            float distM = gridDist * cellMeters;

            for (int k = 1; k < steps; k++)
            {
                float t = (float)k / steps;
                int rr = Mathf.Clamp(Mathf.RoundToInt(ra + (rb - ra) * t), 0, rows - 1);
                int cc = Mathf.Clamp(Mathf.RoundToInt(ca + (cb - ca) * t), 0, cols - 1);
                if (elevations[rr * cols + cc] > za + (zb - za) * t)
                {
                    return new RfLosResult { clear = false, distanceMeters = distM, signalPercent = 12f };
                }
            }
            return new RfLosResult { clear = true, distanceMeters = distM, signalPercent = 96f };
        }

        public SiteCheckResult EvaluateSiteSuitability(int cellIdx, double latDeg, double lsDeg)
        {
            float s = slopeDeg[cellIdx];
            float ro = roughM[cellIdx];
            int litHours = 0;
            for (int h = 0; h < 24; h++)
            {
                ComputeSolarPos(latDeg, lsDeg, h + 0.5, out double el, out double az);
                if (IsCellLit(cellIdx, az, el)) litHours++;
            }
            float frac = litHours / 24f;
            bool sOk = s <= 10.0f;
            bool rOk = ro <= 2.0f;
            bool sunOk = frac >= 0.50f;
            return new SiteCheckResult
            {
                pass = sOk && rOk && sunOk,
                slopeDeg = s,
                roughMeters = ro,
                daylightPercent = Mathf.RoundToInt(frac * 100f),
                slopeOk = sOk,
                roughOk = rOk,
                sunOk = sunOk
            };
        }

        private static int ExtractIntField(string raw, string key, int fallback)
        {
            int idx = raw.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            idx += key.Length;
            while (idx < raw.Length && (raw[idx] == ' ' || raw[idx] == '\t')) idx++;
            int end = idx;
            while (end < raw.Length && (char.IsDigit(raw[end]) || raw[end] == '-')) end++;
            return int.TryParse(raw.Substring(idx, end - idx), out int val) ? val : fallback;
        }

        private static float ExtractFloatField(string raw, string key, float fallback)
        {
            int idx = raw.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            idx += key.Length;
            while (idx < raw.Length && (raw[idx] == ' ' || raw[idx] == '\t')) idx++;
            int end = idx;
            while (end < raw.Length && (char.IsDigit(raw[end]) || raw[end] == '.' || raw[end] == '-')) end++;
            return float.TryParse(raw.Substring(idx, end - idx), NumberStyles.Float, CultureInfo.InvariantCulture, out float val) ? val : fallback;
        }

        private struct HeapNode
        {
            public float f;
            public float g;
            public int cell;
        }

        private sealed class MinHeap
        {
            private readonly List<HeapNode> items = new List<HeapNode>(1024);
            public int Count => items.Count;

            public void Push(float f, float g, int cell)
            {
                items.Add(new HeapNode { f = f, g = g, cell = cell });
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (items[i].f < items[p].f)
                    {
                        var tmp = items[i]; items[i] = items[p]; items[p] = tmp;
                        i = p;
                    }
                    else break;
                }
            }

            public HeapNode Pop()
            {
                var top = items[0];
                int lastIdx = items.Count - 1;
                var last = items[lastIdx];
                items.RemoveAt(lastIdx);
                if (items.Count > 0)
                {
                    items[0] = last;
                    int i = 0;
                    for (;;)
                    {
                        int l = 2 * i + 1;
                        int r = l + 1;
                        int m = i;
                        if (l < items.Count && items[l].f < items[m].f) m = l;
                        if (r < items.Count && items[r].f < items[m].f) m = r;
                        if (m == i) break;
                        var tmp = items[i]; items[i] = items[m]; items[m] = tmp;
                        i = m;
                    }
                }
                return top;
            }
        }
    }
}
