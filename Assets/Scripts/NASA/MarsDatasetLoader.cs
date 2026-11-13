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
        public List<GeologyUnit> GeologyUnits { get; private set; } = new List<GeologyUnit>();
        public GeologyStratigraphy Stratigraphy { get; private set; } = new GeologyStratigraphy();

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
            yield return StartCoroutine(LoadGeologyUnits(Path.Combine(basePath, "geology", "usgs_sim3292_units.json")));
            yield return StartCoroutine(LoadGeologyStratigraphy(Path.Combine(basePath, "geology", "usgs_sim3292_stratigraphy.json")));

            isLoaded = true;
            Debug.Log($"[MarsDatasetLoader] All datasets loaded successfully! " +
                      $"Routes: Safest({RoutesData.safest.unityPositions.Count}pts), " +
                      $"Shortest({RoutesData.shortest.unityPositions.Count}pts), " +
                      $"Science({RoutesData.science.unityPositions.Count}pts). " +
                      $"Sample Tubes: {SampleTubes.Count}, Geology Units: {GeologyUnits.Count}.");

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
                        lon = lon,
                        lat = lat,
                        elev = elev,
                        unityPosition = PlanetocentricToUnity(lat, lon, elev, DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV)
                    };
                    SampleTubes.Add(tube);
                }
            }));
        }

        private IEnumerator LoadPerseveranceTraverse(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                PerseveranceTraverses.Clear();
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

                    MatchCollection coordMatches = Regex.Matches(coordsStr, @"\[\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*\]");
                    foreach (Match cm in coordMatches)
                    {
                        double lon = double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
                        double lat = double.Parse(cm.Groups[2].Value, CultureInfo.InvariantCulture);
                        double elev = double.Parse(cm.Groups[3].Value, CultureInfo.InvariantCulture);
                        leg.points.Add(PlanetocentricToUnity(lat, lon, elev, DEFAULT_ORIGIN_LAT, DEFAULT_ORIGIN_LON, DEFAULT_ORIGIN_ELEV));
                    }

                    if (leg.points.Count > 0)
                    {
                        PerseveranceTraverses.Add(leg);
                    }
                }
            }));
        }

        private IEnumerator LoadGeologyUnits(string path)
        {
            yield return StartCoroutine(ReadTextFile(path, (json) =>
            {
                if (string.IsNullOrEmpty(json)) return;

                GeologyUnits.Clear();
                MatchCollection unitMatches = Regex.Matches(json, @"\{\s*""symbol""\s*:\s*""([^""]+)""(.*?)\}");
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
                MatchCollection epochMatches = Regex.Matches(json, @"\{\s*""name""\s*:\s*""([^""]+)""\s*,\s*""symbol""\s*:\s*""([^""]+)""(.*?)\}\s*(?=,\s*\{|\s*\])");
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

        /// <summary>
        /// Converts planetocentric coordinates (latitude, longitude, elevation) to Unity left-handed Cartesian coordinates
        /// using IAU2000 reference sphere (R = 3,396,190 m).
        /// </summary>
        public static Vector3 PlanetocentricToUnity(double lat, double lon, double alt,
            double originLat = DEFAULT_ORIGIN_LAT, double originLon = DEFAULT_ORIGIN_LON, double originAlt = DEFAULT_ORIGIN_ELEV)
        {
            double dLat = (lat - originLat) * Math.PI / 180.0;
            double dLonDeg = (lon - originLon + 180.0) % 360.0 - 180.0;
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
