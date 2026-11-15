using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsSentinel.NASA
{
    [Serializable]
    public class SiteInfo
    {
        public string name;
        public double latitude_north_deg;
        public double longitude_east_deg;
        public double local_east_m;
        public double local_north_m;
    }

    [Serializable]
    public class DistinctRoute
    {
        public string name;
        public float distance_m;
        public float elevation_change_m;
        public float max_slope_deg;
        public List<Vector2> rawGridPoints = new List<Vector2>();
        public List<Vector3> unityPositions = new List<Vector3>();
    }

    [Serializable]
    public class DistinctRoutesData
    {
        public SiteInfo landing = new SiteInfo();
        public SiteInfo science_site = new SiteInfo();
        public DistinctRoute shortest = new DistinctRoute { name = "Shortest" };
        public DistinctRoute safest = new DistinctRoute { name = "Safest" };
        public DistinctRoute science = new DistinctRoute { name = "Science" };
        public float safe_block_deg = 15.0f;
        public float short_block_deg = 28.0f;
        public float lateral_separation_m = 67.88f;
    }

    [Serializable]
    public class SampleTubeData
    {
        public string sample_num;
        public string sample_name;
        public string outcrop_name;
        public string sample_location;
        public string sample_type;
        public string rock_type;
        public string date;
        public double sample_ht_cm;
        public double lon;
        public double lat;
        public double elev;
        public Vector3 unityPosition;
    }

    [Serializable]
    public class TraverseLeg
    {
        public int sol;
        public float length;
        public List<Vector3> points = new List<Vector3>();
    }

    [Serializable]
    public class GeologyUnit
    {
        public string symbol;
        public string name;
        public string epoch;
        public string system;
        public string hex;
        public int trafficability;
        public string trafficability_desc;
        public string water_ice_potential;
        public string description;
    }

    [Serializable]
    public class GeologyEpoch
    {
        public string name;
        public string symbol;
        public string age_range_ga;
        public string climate;
        public List<string> major_processes = new List<string>();
        public string trafficability_implication;
        public string isru_water_implication;
        public string radiation_implication;
    }

    [Serializable]
    public class GeologyStratigraphy
    {
        public string title;
        public string authors;
        public string scale;
        public string reference_ellipsoid;
        public List<GeologyEpoch> epochs = new List<GeologyEpoch>();
    }

    [Serializable]
    public class IngenuityFlightData
    {
        public int flightNumber;
        public int sol;
        public float lengthMeters;
        public List<Vector3> points = new List<Vector3>();
    }

    [Serializable]
    public class CuriosityTraverseStats
    {
        public int featureCount;
        public int vertexCount;
        public float totalLengthMeters;
    }

    [Serializable]
    public class PerseveranceImageryMeta
    {
        public string imageId;
        public int sol;
        public string instrument;
        public float mastAzDeg;
        public float mastElDeg;
        public string dimension;
    }

    [Serializable]
    public class JezeroOutcropData
    {
        public string id;
        public string name;
        public string formation;
        public string rock_type;
        public int sol;
        public string date;
        public double lat;
        public double lon;
        public double elev_m;
        public string usgs_unit;
        public string crism_mineralogy;
        public string geochemistry_wt_pct;
        public float thermal_inertia_tiu;
        public string discovery_summary;
        public string source;
        public string source_url;
        public Vector2 unityPosXZ;
        public Vector3 scale = new Vector3(2.0f, 1.0f, 1.8f);
        public string style = "basalt";
        public string image_file = "";
    }

    [Serializable]
    public class MarsEnvironmentData
    {
        public string siteName = "Jezero Crater Field Site";
        public double gravityMs2 = 3.72076;
        public float solarConstantWm2 = 589f;
        public float pressureMinPa = 610f;
        public float pressureMeanPa = 715f;
        public float pressureMaxPa = 785f;
        public float airTempMinC = -83f;
        public float airTempMeanC = -56f;
        public float airTempMaxC = -14f;
        public float windTypicalMs = 4.8f;
        public float windGustMaxMs = 22.4f;
        public float dustTauNominal = 0.45f;
        public float dustTauStorm = 1.8f;
        public float radGcrMsvPerDay = 0.67f;
        public float radCareerLimitMsv = 600f;
        public int donkiSepEventCount = 6;
        public string insightSummary = "InSight Elysium Planitia (Sols 675-681) LIVE snapshot cached";
        public string moxieNote = "MOXIE demonstrated 12 g/hr O2 production (>98% purity) from CO2";
        public int marsTrekServiceCount = 49;
    }

    [Serializable]
    public class CuriosityMountSharpStop
    {
        public int stop;
        public string name;
        public int sol;
        public double lat;
        public double lon;
        public double elev_m;
        public string unit;
        public string finding;
    }

    /// <summary>
    /// Loads and manages NASA planetary science datasets from StreamingAssets/NASA_Data/.
    /// Converts IAU2000 Mars planetocentric coordinates to Unity Left-Handed Cartesian space.
    /// </summary>
    public class MarsDatasetLoader : MonoBehaviour
    {
        public static MarsDatasetLoader Instance { get; private set; }

        public const double IAU2000_MARS_RADIUS = 3396190.0;
        public const double DEFAULT_ORIGIN_LAT = 18.4447;
        public const double DEFAULT_ORIGIN_LON = 77.4508;
        public const double DEFAULT_ORIGIN_ELEV = -2569.9;

        [Header("State")]
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private bool isLoaded = false;

        public bool IsLoaded => isLoaded;
        public DistinctRoutesData RoutesData { get; private set; } = new DistinctRoutesData();
        public List<SampleTubeData> SampleTubes { get; private set; } = new List<SampleTubeData>();
        public List<TraverseLeg> PerseveranceTraverses { get; private set; } = new List<TraverseLeg>();
        public List<IngenuityFlightData> IngenuityFlights { get; private set; } = new List<IngenuityFlightData>();
        public CuriosityTraverseStats CuriosityStats { get; private set; } = new CuriosityTraverseStats();
        public PerseveranceImageryMeta SampleImageryMeta { get; private set; } = new PerseveranceImageryMeta();
        public int PerseveranceDropoutsFiltered { get; private set; }
        public int OfficialResourcesCount { get; private set; }
        public List<GeologyUnit> GeologyUnits { get; private set; } = new List<GeologyUnit>();
        public GeologyStratigraphy Stratigraphy { get; private set; } = new GeologyStratigraphy();
        public List<JezeroOutcropData> Outcrops { get; private set; } = new List<JezeroOutcropData>();
        public MarsEnvironmentData EnvironmentData { get; private set; } = new MarsEnvironmentData();
        public List<CuriosityMountSharpStop> MountSharpStops { get; private set; } = new List<CuriosityMountSharpStop>();

        public string GetOutcropImagePath(string imageFile)
        {
            if (string.IsNullOrEmpty(imageFile)) return null;
            string basePath = Path.Combine(Application.streamingAssetsPath, "NASA_Data", "imagery");
            string sub = Path.Combine(basePath, "outcrops", imageFile);
            if (File.Exists(sub)) return sub;
            string direct = Path.Combine(basePath, imageFile);
            return direct;
        }

        public event Action OnDataLoaded;

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
            if (autoLoadOnStart && !isLoaded)
            {
                StartCoroutine(LoadAllDatasetsCoroutine());
            }
        }

        public void LoadAllDatasets()
        {
            StartCoroutine(LoadAllDatasetsCoroutine());
        }

        public IEnumerator LoadAllDatasetsCoroutine()
        {
            string basePath = Path.Combine(Application.streamingAssetsPath, "NASA_Data");

            yield return StartCoroutine(LoadDistinctRoutes(Path.Combine(basePath, "jez_distinct_routes.json")));
            yield return StartCoroutine(LoadSampleTubes(Path.Combine(basePath, "routes_traverses", "sample_tubes_m20.geojson")));
            yield return StartCoroutine(LoadPerseveranceTraverse(Path.Combine(basePath, "routes_traverses", "perseverance_traverse_m20.geojson")));
            yield return StartCoroutine(LoadIngenuityFlights(Path.Combine(basePath, "routes_traverses", "ingenuity_flights_m20.geojson")));
            yield return StartCoroutine(LoadCuriosityTraverseSummary(Path.Combine(basePath, "routes_traverses", "curiosity_traverse_msl.geojson")));
            yield return StartCoroutine(LoadGeologyUnits(Path.Combine(basePath, "geology", "usgs_sim3292_units.json")));
            yield return StartCoroutine(LoadGeologyStratigraphy(Path.Combine(basePath, "geology", "usgs_sim3292_stratigraphy.json")));
            yield return StartCoroutine(LoadSampleImageryMetadata(Path.Combine(basePath, "imagery", "perseverance_sample_metadata.json")));
            yield return StartCoroutine(LoadApiManifest(Path.Combine(basePath, "apis", "nasa_endpoints_manifest.json")));
            yield return StartCoroutine(LoadJezeroOutcrops(Path.Combine(basePath, "geology", "jezero_outcrops_and_rocks.json")));
            yield return StartCoroutine(LoadEnvironmentData(Path.Combine(basePath, "environment", "mars_environmental_science.json")));
            yield return StartCoroutine(LoadCuriosityMountSharpStops(Path.Combine(basePath, "missions", "curiosity_mount_sharp_waypoints.json")));

            isLoaded = true;
            Debug.Log($"[MarsDatasetLoader] All datasets loaded successfully! " +
                      $"Routes: Safest({RoutesData.safest.unityPositions.Count}pts), " +
                      $"Shortest({RoutesData.shortest.unityPositions.Count}pts), " +
                      $"Science({RoutesData.science.unityPositions.Count}pts). " +
                      $"Sample Tubes: {SampleTubes.Count}, Outcrops: {Outcrops.Count}, Mount Sharp: {MountSharpStops.Count}, Ingenuity Flights: {IngenuityFlights.Count}, " +
                      $"Persev Dropouts Filtered: {PerseveranceDropoutsFiltered}, Curiosity Features: {CuriosityStats.featureCount}, " +
                      $"Geology Units: {GeologyUnits.Count}, Stratigraphy Epochs: {Stratigraphy.epochs.Count}, NASA Resources: {OfficialResourcesCount}.");

            OnDataLoaded?.Invoke();
        }

        private IEnumerator ReadTextFile(string path, Action<string> callback)
        {
            if (path.Contains("://") || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        callback(req.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogWarning($"[MarsDatasetLoader] WebRequest failed for {path}: {req.error}");
                        callback(null);
                    }
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    callback(File.ReadAllText(path));
                }
                else
                {
                    Debug.LogWarning($"[MarsDatasetLoader] File not found: {path}");
                    callback(null);
                }
                yield return null;
            }
        }

        private IEnumerator LoadDistinctRoutes(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                ParseDistinctRoutes(json);
            }));
        }

        private void ParseDistinctRoutes(string json)
        {
            // Landing Site
            Match landMatch = Regex.Match(json, @"""landing""\s*:\s*\{([^}]+)\}");
            if (landMatch.Success)
            {
                string b = landMatch.Groups[1].Value;
                RoutesData.landing.name = ExtractString(b, "name", "Octavia E. Butler Landing");
                RoutesData.landing.latitude_north_deg = ExtractDouble(b, "latitude_north_deg", DEFAULT_ORIGIN_LAT);
                RoutesData.landing.longitude_east_deg = ExtractDouble(b, "longitude_east_deg", DEFAULT_ORIGIN_LON);
                RoutesData.landing.local_east_m = ExtractDouble(b, "local_east_m", 512.73);
                RoutesData.landing.local_north_m = ExtractDouble(b, "local_north_m", 945.43);
            }

            // Science Site
            Match sciMatch = Regex.Match(json, @"""science_site""\s*:\s*\{([^}]+)\}");
            if (sciMatch.Success)
            {
                string b = sciMatch.Groups[1].Value;
                RoutesData.science_site.name = ExtractString(b, "name", "Rochette");
                RoutesData.science_site.latitude_north_deg = ExtractDouble(b, "latitude_north_deg", 18.430744);
                RoutesData.science_site.longitude_east_deg = ExtractDouble(b, "longitude_east_deg", 77.444349);
                RoutesData.science_site.local_east_m = ExtractDouble(b, "local_east_m", 130.33);
                RoutesData.science_site.local_north_m = ExtractDouble(b, "local_north_m", 118.2);
            }

            ParseRouteBlock(json, "shortest", RoutesData.shortest);
            ParseRouteBlock(json, "safest", RoutesData.safest);
            ParseRouteBlock(json, "science", RoutesData.science);
        }

        private void ParseRouteBlock(string json, string key, DistinctRoute target)
        {
            Match m = Regex.Match(json, $"\"{key}\"\\s*:\\s*\\{{(.*?\"points\"\\s*:\\s*\\[\\[.*?\\]\\]\\s*)\\}}");
            if (!m.Success)
            {
                // Fallback relaxed pattern
                m = Regex.Match(json, $"\"{key}\"\\s*:\\s*\\{{(.*?)\"threshold_note\"");
            }

            if (m.Success)
            {
                string block = m.Groups[1].Value;
                target.distance_m = (float)ExtractDouble(block, "distance_m", 0);
                target.elevation_change_m = (float)ExtractDouble(block, "elevation_change_m", 0);
                target.max_slope_deg = (float)ExtractDouble(block, "max_slope_deg", 0);

                target.rawGridPoints.Clear();
                target.unityPositions.Clear();

                Match ptsMatch = Regex.Match(block, @"""points""\s*:\s*(\[\[.*?\]\])");
                if (ptsMatch.Success)
                {
                    MatchCollection coordMatches = Regex.Matches(ptsMatch.Groups[1].Value, @"\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]");
                    foreach (Match c in coordMatches)
                    {
                        if (double.TryParse(c.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double east) &&
                            double.TryParse(c.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double north))
                        {
                            target.rawGridPoints.Add(new Vector2((float)east, (float)north));
                            // Center relative to landing local coordinates
                            float uX = (float)(east - RoutesData.landing.local_east_m);
                            float uZ = (float)(north - RoutesData.landing.local_north_m);
                            target.unityPositions.Add(new Vector3(uX, 0f, uZ));
                        }
                    }
                }
            }
        }

        private IEnumerator LoadSampleTubes(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                SampleTubes.Clear();
                MatchCollection featureMatches = Regex.Matches(json, @"\{\s*""type""\s*:\s*""Feature""\s*,\s*""properties""\s*:\s*\{([^}]+)\}\s*,\s*""geometry""\s*:\s*\{[^}]+""coordinates""\s*:\s*\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]\s*\}\s*\}");
                foreach (Match m in featureMatches)
                {
                    string props = m.Groups[1].Value;
                    double lon = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    double lat = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    double elev = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);

                    SampleTubeData tube = new SampleTubeData
                    {
                        sample_num = ExtractString(props, "sample_num", ""),
                        sample_name = ExtractString(props, "sample_name", "Sample"),
                        outcrop_name = ExtractString(props, "outcrop_name", ""),
                        sample_location = ExtractString(props, "sample_location", ""),
                        sample_type = ExtractString(props, "sample_type", "Rock Core"),
                        rock_type = ExtractString(props, "rock_type", "Igneous"),
                        date = ExtractString(props, "date", ""),
                        sample_ht_cm = ExtractDouble(props, "sample_ht_cm", 0.0),
                        lon = lon,
                        lat = lat,
                        elev = elev,
                        unityPosition = PlanetocentricToUnity(lat, lon, elev, DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV)
                    };
                    SampleTubes.Add(tube);
                }
            }));
        }

        /// <summary>
        /// Filters NASA Jezero telemetry elevation dropouts (e.g., ~0m points when local terrain sits at ~-2570m).
        /// </summary>
        public static double FilterJezeroAltitudeDropout(double rawAlt, double lastValidAlt, double originAlt, out bool wasFiltered)
        {
            if (rawAlt > -1000.0 && originAlt < -2000.0)
            {
                wasFiltered = true;
                return lastValidAlt;
            }
            wasFiltered = false;
            return rawAlt;
        }

        private IEnumerator LoadPerseveranceTraverse(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                PerseveranceTraverses.Clear();
                PerseveranceDropoutsFiltered = 0;
                double lastValidElev = DEFAULT_ORIGIN_ELEV;

                MatchCollection featureMatches = Regex.Matches(json, @"\{\s*""type""\s*:\s*""Feature""\s*,\s*""properties""\s*:\s*\{([^}]+)\}\s*,\s*""geometry""\s*:\s*\{\s*""type""\s*:\s*""LineString""\s*,\s*""coordinates""\s*:\s*\[(.*?)\]\s*\}\s*\}");
                foreach (Match m in featureMatches)
                {
                    string props = m.Groups[1].Value;
                    string coordsStr = m.Groups[2].Value;

                    TraverseLeg leg = new TraverseLeg
                    {
                        sol = (int)ExtractDouble(props, "sol", 0),
                        length = (float)ExtractDouble(props, "length", 0)
                    };

                    MatchCollection coordMatches = Regex.Matches(coordsStr, @"\[\s*(-?[\d\.eE+-]+)\s*,\s*(-?[\d\.eE+-]+)(?:\s*,\s*(-?[\d\.eE+-]+))?\s*\]");
                    foreach (Match cm in coordMatches)
                    {
                        double lon = double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
                        double lat = double.Parse(cm.Groups[2].Value, CultureInfo.InvariantCulture);
                        double elev = cm.Groups[3].Success && !string.IsNullOrEmpty(cm.Groups[3].Value)
                            ? double.Parse(cm.Groups[3].Value, CultureInfo.InvariantCulture)
                            : lastValidElev;

                        elev = FilterJezeroAltitudeDropout(elev, lastValidElev, DEFAULT_ORIGIN_ELEV, out bool filtered);
                        if (filtered) PerseveranceDropoutsFiltered++;
                        else lastValidElev = elev;

                        leg.points.Add(PlanetocentricToUnity(lat, lon, elev, DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV));
                    }

                    if (leg.points.Count > 0)
                    {
                        PerseveranceTraverses.Add(leg);
                    }
                }
            }));
        }

        private IEnumerator LoadIngenuityFlights(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                IngenuityFlights.Clear();
                MatchCollection featureMatches = Regex.Matches(json, @"\{\s*""type""\s*:\s*""Feature""\s*,\s*""properties""\s*:\s*\{([^}]+)\}\s*,\s*""geometry""\s*:\s*\{\s*""type""\s*:\s*""LineString""\s*,\s*""coordinates""\s*:\s*\[(.*?)\]\s*\}\s*\}", RegexOptions.Singleline);
                foreach (Match m in featureMatches)
                {
                    string props = m.Groups[1].Value;
                    string coordsStr = m.Groups[2].Value;

                    IngenuityFlightData fl = new IngenuityFlightData
                    {
                        flightNumber = (int)ExtractDouble(props, "Flight", 0),
                        sol = (int)ExtractDouble(props, "Sol", 0),
                        lengthMeters = (float)ExtractDouble(props, "Length_m", 0)
                    };

                    // Ingenuity coordinates are 2D [lon, lat]; safely fall back to DEFAULT_ORIGIN_ELEV
                    MatchCollection coordMatches = Regex.Matches(coordsStr, @"\[\s*(-?[\d\.eE+-]+)\s*,\s*(-?[\d\.eE+-]+)(?:\s*,\s*(-?[\d\.eE+-]+))?\s*\]");
                    foreach (Match cm in coordMatches)
                    {
                        double lon = double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
                        double lat = double.Parse(cm.Groups[2].Value, CultureInfo.InvariantCulture);
                        double elev = cm.Groups[3].Success && !string.IsNullOrEmpty(cm.Groups[3].Value)
                            ? double.Parse(cm.Groups[3].Value, CultureInfo.InvariantCulture)
                            : DEFAULT_ORIGIN_ELEV;
                        fl.points.Add(PlanetocentricToUnity(lat, lon, elev, DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV));
                    }

                    if (fl.points.Count > 0)
                    {
                        IngenuityFlights.Add(fl);
                    }
                }
            }));
        }

        private IEnumerator LoadCuriosityTraverseSummary(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                MatchCollection features = Regex.Matches(json, @"""type""\s*:\s*""Feature""");
                MatchCollection lengths = Regex.Matches(json, @"""Length""\s*:\s*(-?[\d\.]+)");
                float sumLen = 0f;
                foreach (Match lm in lengths)
                {
                    if (float.TryParse(lm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float l))
                        sumLen += l;
                }

                CuriosityStats = new CuriosityTraverseStats
                {
                    featureCount = features.Count,
                    vertexCount = Regex.Matches(json, @"\[\s*137\.\d+\s*,\s*-4\.\d+").Count,
                    totalLengthMeters = sumLen
                };
            }));
        }

        private IEnumerator LoadSampleImageryMetadata(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                SampleImageryMeta = new PerseveranceImageryMeta
                {
                    imageId = ExtractString(json, "imageid", "ZR2_0100_0675835095"),
                    sol = (int)ExtractDouble(json, "sol", 100),
                    instrument = ExtractString(json, "instrument", "MASTCAM_Z_RIGHT"),
                    mastAzDeg = (float)ExtractDouble(json, "mastAz", 82.51),
                    mastElDeg = (float)ExtractDouble(json, "mastEl", 19.09),
                    dimension = ExtractString(json, "dimension", "(1648,1200)")
                };
            }));
        }

        private IEnumerator LoadApiManifest(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                OfficialResourcesCount = Regex.Matches(json, @"""id""\s*:\s*""").Count;
            }));
        }

        private IEnumerator LoadGeologyUnits(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                GeologyUnits.Clear();
                MatchCollection unitMatches = Regex.Matches(json, @"\{\s*""symbol""\s*:\s*""([^""]+)""(.*?)\}", RegexOptions.Singleline);
                foreach (Match m in unitMatches)
                {
                    string sym = m.Groups[1].Value;
                    string body = m.Groups[2].Value;

                    GeologyUnit unit = new GeologyUnit
                    {
                        symbol = sym,
                        name = ExtractString(body, "name", "Geologic Unit"),
                        epoch = ExtractString(body, "epoch", "Amazonian-Hesperian"),
                        system = ExtractString(body, "system", "Mars Regional"),
                        hex = ExtractString(body, "hex", "#C47A52"),
                        trafficability = (int)ExtractDouble(body, "trafficability", 3),
                        trafficability_desc = ExtractString(body, "trafficability_desc", "Nominal traverse conditions"),
                        water_ice_potential = ExtractString(body, "water_ice_potential", "Low"),
                        description = ExtractString(body, "description", "")
                    };
                    GeologyUnits.Add(unit);
                }
            }));
        }

        private IEnumerator LoadGeologyStratigraphy(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                Stratigraphy.title = ExtractString(json, "title", "USGS SIM 3292 Chronostratigraphic System of Mars");
                Stratigraphy.authors = ExtractString(json, "authors", "Tanaka et al.");
                Stratigraphy.scale = ExtractString(json, "scale", "1:20,000,000");
                Stratigraphy.reference_ellipsoid = ExtractString(json, "reference_ellipsoid", "IAU2000 Mars (R = 3396190 m)");

                Stratigraphy.epochs.Clear();
                MatchCollection epochMatches = Regex.Matches(json, @"\{\s*""name""\s*:\s*""([^""]+)""\s*,\s*""symbol""\s*:\s*""([^""]+)""(.*?)\}\s*(?=,\s*\{|\s*\])", RegexOptions.Singleline);
                foreach (Match m in epochMatches)
                {
                    string name = m.Groups[1].Value;
                    string symbol = m.Groups[2].Value;
                    string body = m.Groups[3].Value;

                    GeologyEpoch ep = new GeologyEpoch
                    {
                        name = name,
                        symbol = symbol,
                        age_range_ga = ExtractString(body, "age_range_ga", ""),
                        climate = ExtractString(body, "climate", "")
                    };
                    Stratigraphy.epochs.Add(ep);
                }
            }));
        }

        private IEnumerator LoadJezeroOutcrops(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                Outcrops.Clear();
                List<string> objs = ExtractTopLevelJsonObjects(json);
                foreach (string body in objs)
                {
                    JezeroOutcropData item = new JezeroOutcropData
                    {
                        id = ExtractString(body, "id", "jezero_outcrop"),
                        name = ExtractString(body, "name", "Jezero Outcrop"),
                        formation = ExtractString(body, "formation", "Jezero Fm."),
                        rock_type = ExtractString(body, "rock_type", "Basalt"),
                        sol = (int)ExtractDouble(body, "sol", 0),
                        date = ExtractString(body, "date", ""),
                        lat = ExtractDouble(body, "lat", DEFAULT_ORIGIN_LAT),
                        lon = ExtractDouble(body, "lon", DEFAULT_ORIGIN_LON),
                        elev_m = ExtractDouble(body, "elev_m", DEFAULT_ORIGIN_ELEV),
                        usgs_unit = ExtractString(body, "usgs_unit", "HNhu"),
                        crism_mineralogy = ExtractString(body, "crism_mineralogy", ""),
                        geochemistry_wt_pct = ExtractString(body, "geochemistry_wt_pct", ""),
                        thermal_inertia_tiu = (float)ExtractDouble(body, "thermal_inertia_tiu", 320.0),
                        discovery_summary = ExtractString(body, "discovery_summary", ""),
                        source = ExtractString(body, "source", "NASA/JPL-Caltech"),
                        source_url = ExtractString(body, "source_url", "https://mars.nasa.gov/mars2020/"),
                        style = ExtractString(body, "style", "basalt"),
                        image_file = ExtractString(body, "image_file", "perseverance_sample_raw.jpg")
                    };

                    Match posMatch = Regex.Match(body, @"""unity_pos""\s*:\s*\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]");
                    if (posMatch.Success)
                    {
                        float ux = float.Parse(posMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        float uz = float.Parse(posMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                        item.unityPosXZ = new Vector2(ux, uz);
                    }
                    else
                    {
                        Vector3 p = PlanetocentricToUnity(item.lat, item.lon, item.elev_m);
                        item.unityPosXZ = new Vector2(p.x, p.z);
                    }

                    Match scMatch = Regex.Match(body, @"""scale""\s*:\s*\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]");
                    if (scMatch.Success)
                    {
                        item.scale = new Vector3(
                            float.Parse(scMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                            float.Parse(scMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                            float.Parse(scMatch.Groups[3].Value, CultureInfo.InvariantCulture)
                        );
                    }

                    Outcrops.Add(item);
                }
            }));
        }

        private IEnumerator LoadEnvironmentData(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                EnvironmentData.siteName = ExtractString(json, "name", EnvironmentData.siteName);
                EnvironmentData.gravityMs2 = ExtractDouble(json, "gravity_ms2", 3.72076);
                EnvironmentData.solarConstantWm2 = (float)ExtractDouble(json, "solar_constant_mars_wm2", 589.0);
                EnvironmentData.pressureMinPa = (float)ExtractDouble(json, "min", 610.0);
                EnvironmentData.pressureMeanPa = (float)ExtractDouble(json, "annual_mean", 715.0);
                EnvironmentData.pressureMaxPa = (float)ExtractDouble(json, "max", 785.0);
                EnvironmentData.airTempMinC = (float)ExtractDouble(json, "night_min", -83.0);
                EnvironmentData.airTempMeanC = (float)ExtractDouble(json, "diurnal_mean", -56.0);
                EnvironmentData.airTempMaxC = (float)ExtractDouble(json, "day_max", -14.0);
                EnvironmentData.windTypicalMs = (float)ExtractDouble(json, "typical_day", 4.8);
                EnvironmentData.windGustMaxMs = (float)ExtractDouble(json, "dust_devil_gust_max", 22.4);
                EnvironmentData.dustTauNominal = (float)ExtractDouble(json, "nominal", 0.45);
                EnvironmentData.dustTauStorm = (float)ExtractDouble(json, "regional_storm", 1.8);
                EnvironmentData.radGcrMsvPerDay = (float)ExtractDouble(json, "gcr_surface_dose_msv_per_day", 0.67);
                EnvironmentData.radCareerLimitMsv = (float)ExtractDouble(json, "nasa_career_limit_msv", 600.0);
                EnvironmentData.moxieNote = ExtractString(json, "moxie_isru_benchmark", EnvironmentData.moxieNote);

                MatchCollection sepMatches = Regex.Matches(json, @"""sepID""");
                if (sepMatches.Count > 0) EnvironmentData.donkiSepEventCount = sepMatches.Count;
            }));
        }

        private IEnumerator LoadCuriosityMountSharpStops(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                MountSharpStops.Clear();
                Match mArr = Regex.Match(json, @"""waypoints""\s*:\s*(\[.*\])", RegexOptions.Singleline);
                string arrSlice = mArr.Success ? mArr.Groups[1].Value : json;
                List<string> objs = ExtractTopLevelJsonObjects(arrSlice);
                foreach (string body in objs)
                {
                    MountSharpStops.Add(new CuriosityMountSharpStop
                    {
                        stop = (int)ExtractDouble(body, "stop", MountSharpStops.Count + 1),
                        name = ExtractString(body, "name", "Mount Sharp Waypoint"),
                        sol = (int)ExtractDouble(body, "sol", 0),
                        lat = ExtractDouble(body, "lat", -4.6),
                        lon = ExtractDouble(body, "lon", 137.4),
                        elev_m = ExtractDouble(body, "elev_m", -4400.0),
                        unit = ExtractString(body, "unit", "Mount Sharp"),
                        finding = ExtractString(body, "finding", "")
                    });
                }
            }));
        }

        private static List<string> ExtractTopLevelJsonObjects(string json)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(json)) return result;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            int objStart = -1;
            const int QUOTE = 34;
            const int BACKSLASH = 92;
            const int LBRACE = 123;
            const int RBRACE = 125;

            for (int i = 0; i < json.Length; i++)
            {
                int ch = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == BACKSLASH)
                    {
                        escaped = true;
                    }
                    else if (ch == QUOTE)
                    {
                        inString = false;
                    }
                    continue;
                }

                if (ch == QUOTE)
                {
                    inString = true;
                }
                else if (ch == LBRACE)
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (ch == RBRACE)
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        result.Add(json.Substring(objStart, i - objStart + 1));
                        objStart = -1;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Converts planetocentric coordinates (latitude, longitude, elevation) to Unity left-handed Cartesian coordinates
        /// using IAU2000 reference sphere (R = 3,396,190 m).
        /// </summary>
        public static Vector3 PlanetocentricToUnity(double lat, double lon, double alt,
            double originLat = DEFAULT_ORIGIN_LAT, double originLon = DEFAULT_ORIGIN_LON, double originAlt = DEFAULT_ORIGIN_ELEV)
        {
            double dLat = (lat - originLat) * Math.PI / 180.0;
            double dLonDeg = ((lon - originLon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            double dLon = dLonDeg * Math.PI / 180.0;
            double midLat = ((lat + originLat) / 2.0) * Math.PI / 180.0;

            float east = (float)(IAU2000_MARS_RADIUS * Math.Cos(midLat) * dLon);
            float north = (float)(IAU2000_MARS_RADIUS * dLat);
            float up = (float)(alt - originAlt);

            return new Vector3(east, up, north);
        }

        private static string ExtractString(string source, string key, string fallback)
        {
            Match m = Regex.Match(source, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : fallback;
        }

        private static double ExtractDouble(string source, string key, double fallback)
        {
            Match m = Regex.Match(source, $"\"{key}\"\\s*:\\s*(-?[\\d\\.]+)");
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return fallback;
        }
    }
}
