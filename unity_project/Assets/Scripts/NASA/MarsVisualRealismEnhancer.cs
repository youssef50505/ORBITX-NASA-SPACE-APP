using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// MarsVisualRealismEnhancer elevates Unity 3D visual quality across Skybox, visible Sun,
    /// full Mars Globe with Rayleigh/Mie atmospheric rim glow, Stickney-cratered Phobos & Deimos moons,
    /// sleek graded Keplerian orbital wakes, Jezero basalt boulders, and atmospheric dust motes.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class MarsVisualRealismEnhancer : MonoBehaviour
    {
        public static MarsVisualRealismEnhancer Instance { get; private set; }

        [Header("Planetary & Sky References")]
        [SerializeField] private Vector3 marsCenter = new Vector3(0f, -1200f, 0f);
        [SerializeField] private float marsGlobeRadius = 1135f;

        private Material skyboxMat;
        private Material atmoRimMat;
        private Light sunLight;
        private CinemachineCamera orbitalCam;

        private GameObject marsGlobeGo;
        private GameObject spaceSunGo;
        private Transform phobosTr;
        private Transform deimosTr;
        private LineRenderer phobosOrbitLr;
        private LineRenderer deimosOrbitLr;
        private readonly List<OrbitTrackData> orbiterTracks = new List<OrbitTrackData>();
        private Texture2D marsAlbedoTex;
        private Texture2D marsBumpTex;

        private float spaceBlend = 0f;
        private int focusTargetMode = 0; // 0=normal, 1=Sun, 2=Phobos, 3=Deimos
        private bool orbitOverrideActive = false;
        private float simTime = 0f;

        private class OrbitTrackData
        {
            public LineRenderer line;
            public Transform tracker;
            public Color baseColor;
            public Vector3[] points;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("MS_VisualRealismEnhancer");
            Instance = go.AddComponent<MarsVisualRealismEnhancer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            GameObject rigs = GameObject.Find("MarsCameraRigs");
            if (rigs != null && rigs.transform.parent != null)
            {
                rigs.transform.SetParent(null, false);
            }
        }

        private void Start()
        {
            LocateMainSunLight();
            SetupMartianSkyboxAndSpaceSun();
            BuildMarsGlobeAndAtmosphere();
            BuildJezeroHorizonTerrain();
            UpgradeMoonsAndOrbits();
            EnhanceConstellationTrails();
            SpawnJezeroRocksAndDustMotes();
            ConfigureOrbitalCameraFraming();
            Invoke(nameof(LogSceneDiagnostics), 1.0f);
        }

        private void LogSceneDiagnostics()
        {
            Camera cam = Camera.main;
            string camInfo = cam != null
                ? $"pos={cam.transform.position} fwd={cam.transform.forward} far={cam.farClipPlane} fov={cam.fieldOfView}"
                : "NO_MAIN_CAM";
            string hitInfo = "none";
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 5000f))
            {
                hitInfo = $"{hit.collider.name} dist={hit.distance:F2} pt={hit.point}";
            }
            string boundsDump = "";
            foreach (var rn in new[] { "MS_Terrain_Jezero", "MS_Perseverance_Root", "MS_Curiosity_Root", "MS_InSight_Lander_Root", "Phobos", "Deimos", "CM_SurfaceFleetOverview", "CM_OrbitalFleetView" })
            {
                GameObject g = GameObject.Find(rn);
                if (g != null)
                {
                    Renderer rend = g.GetComponentInChildren<Renderer>();
                    string b = rend != null ? $"ctr={rend.bounds.center} ext={rend.bounds.extents}" : "no_rend";
                    boundsDump += $"{rn}@{g.transform.position}({b}); ";
                }
            }
            Debug.Log($"[RealismDiag] {camInfo} | hit={hitInfo} | objs={boundsDump}");
        }

        private void LocateMainSunLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    RenderSettings.sun = l;
                    // Position Martian Sun cleanly in upper-right open sky (~5.5 deg elev, ~16 deg right of cam)
                    sunLight.transform.rotation = Quaternion.LookRotation(new Vector3(0.28f, -0.095f, -0.955f).normalized, Vector3.up);
                    break;
                }
            }
            if (RenderSettings.fog && RenderSettings.fogDensity > 0.0011f)
            {
                RenderSettings.fogDensity = 0.00072f;
            }
        }

        private void SetupMartianSkyboxAndSpaceSun()
        {
            Material loadedSky = Resources.Load<Material>("Materials/M_MartianAtmosphereSkybox");
            if (loadedSky != null)
            {
                skyboxMat = new Material(loadedSky);
            }
            else
            {
                Shader skyShader = Shader.Find("MarsSentinel/MartianAtmosphereSkybox");
                if (skyShader != null) skyboxMat = new Material(skyShader);
            }

            if (skyboxMat != null)
            {
                skyboxMat.SetColor("_ZenithColor", new Color(0.58f, 0.36f, 0.22f, 1f));
                skyboxMat.SetColor("_HorizonColor", new Color(0.86f, 0.57f, 0.37f, 1f));
                skyboxMat.SetColor("_SunHaloColor", new Color(0.55f, 0.78f, 0.98f, 1f));
                skyboxMat.SetColor("_SunDiscColor", new Color(2.1f, 1.78f, 1.32f, 1f));
                skyboxMat.SetFloat("_DustTau", 0.45f);
                skyboxMat.SetFloat("_SpaceBlend", 0f);
                skyboxMat.SetFloat("_StarIntensity", 1.25f);
                RenderSettings.skybox = skyboxMat;
                DynamicGI.UpdateEnvironment();
            }

            Material loadedRim = Resources.Load<Material>("Materials/M_MarsAtmosphereRim");
            if (loadedRim != null)
            {
                atmoRimMat = new Material(loadedRim);
            }
            else
            {
                Shader rimShader = Shader.Find("MarsSentinel/MarsAtmosphereRim");
                if (rimShader != null) atmoRimMat = new Material(rimShader);
            }

            // Visible blazing 3D Solar Photosphere + Corona Beacon in deep sky / space
            spaceSunGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spaceSunGo.name = "MS_Visible_Solar_Photosphere";
            spaceSunGo.transform.SetParent(transform, false);
            spaceSunGo.transform.localScale = Vector3.one * 320f;
            Collider col = spaceSunGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = spaceSunGo.GetComponent<Renderer>();
            if (r != null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null) unlit = Shader.Find("Sprites/Default");
                Material sunMat = new Material(unlit);
                sunMat.SetColor("_BaseColor", new Color(4.5f, 3.9f, 2.8f, 1f));
                sunMat.color = new Color(4.5f, 3.9f, 2.8f, 1f);
                r.material = sunMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            GameObject corona = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            corona.name = "MS_Solar_Corona_Halo";
            corona.transform.SetParent(spaceSunGo.transform, false);
            corona.transform.localScale = Vector3.one * 2.25f;
            Collider cCol = corona.GetComponent<Collider>();
            if (cCol != null) Destroy(cCol);
            Renderer cr = corona.GetComponent<Renderer>();
            if (cr != null && atmoRimMat != null)
            {
                Material cMat = new Material(atmoRimMat);
                cMat.SetColor("_DayRimColor", new Color(1.0f, 0.78f, 0.42f, 0.75f));
                cMat.SetColor("_TwilightRimColor", new Color(1.0f, 0.92f, 0.72f, 0.75f));
                cMat.SetFloat("_Intensity", 1.55f);
                cr.material = cMat;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cr.receiveShadows = false;
            }
        }

        private void GenerateMarsTextures()
        {
            const int w = 512;
            const int h = 256;
            marsAlbedoTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            marsBumpTex = new Texture2D(w, h, TextureFormat.RGBA32, true);

            Color32[] albedoPx = new Color32[w * h];
            Color32[] bumpPx = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float lat = (v - 0.5f) * Mathf.PI;
                float cosLat = Mathf.Max(0.15f, Mathf.Cos(lat));
                float absLat = Mathf.Abs(lat);

                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float lon = (u - 0.5f) * Mathf.PI * 2f;

                    float nx = Mathf.Cos(lat) * Mathf.Cos(lon);
                    float ny = Mathf.Sin(lat);
                    float nz = Mathf.Cos(lat) * Mathf.Sin(lon);

                    float macro = Mathf.PerlinNoise(nx * 2.4f + 4.1f, ny * 2.8f + nz * 1.9f + 2.3f);
                    float meso = Mathf.PerlinNoise(nx * 8.2f + nz * 6.4f + 11.7f, ny * 8.2f + 5.1f);
                    float fine = Mathf.PerlinNoise(nx * 24f + 19f, ny * 24f + nz * 17f + 8f);

                    float syrtis = Mathf.Exp(-(Mathf.Pow((lon - 1.18f) * cosLat, 2f) / 0.18f + Mathf.Pow(lat - 0.16f, 2f) / 0.12f));
                    float valles = Mathf.Exp(-Mathf.Pow((lat + 0.22f) / 0.038f, 2f)) * Mathf.Max(0f, 1f - Mathf.Abs(lon + 1.15f) / 0.55f);
                    float tharsis = Mathf.Exp(-(Mathf.Pow((lon + 1.9f) * cosLat, 2f) / 0.24f + Mathf.Pow(lat - 0.05f, 2f) / 0.16f));

                    float elev = 0.45f + macro * 0.30f + meso * 0.16f + fine * 0.07f + tharsis * 0.20f - valles * 0.38f;
                    float dark = Mathf.Clamp01(syrtis * 0.65f + valles * 0.5f + Mathf.Max(0f, 0.45f - macro) * 0.7f);

                    float r = 0.74f + meso * 0.14f - dark * 0.34f + (fine - 0.5f) * 0.08f;
                    float g = 0.37f + meso * 0.11f - dark * 0.18f + (fine - 0.5f) * 0.05f;
                    float b = 0.21f + meso * 0.07f - dark * 0.09f + (fine - 0.5f) * 0.04f;

                    float polar = 1.29f + (macro - 0.5f) * 0.16f;
                    if (absLat > polar)
                    {
                        float cap = Mathf.Clamp01((absLat - polar) / 0.08f);
                        r = Mathf.Lerp(r, 0.94f, cap);
                        g = Mathf.Lerp(g, 0.91f, cap);
                        b = Mathf.Lerp(b, 0.88f, cap);
                    }

                    int idx = y * w + x;
                    albedoPx[idx] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                    byte bv = (byte)Mathf.Clamp(elev * 255f, 0f, 255f);
                    bumpPx[idx] = new Color32(bv, bv, bv, 255);
                }
            }

            marsAlbedoTex.SetPixels32(albedoPx);
            marsAlbedoTex.Apply(true);
            marsBumpTex.SetPixels32(bumpPx);
            marsBumpTex.Apply(true);
        }

        private void BuildMarsGlobeAndAtmosphere()
        {
            GenerateMarsTextures();

            marsGlobeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marsGlobeGo.name = "MS_Mars_Orbital_Globe";
            marsGlobeGo.transform.position = marsCenter;
            marsGlobeGo.transform.localScale = Vector3.one * (marsGlobeRadius * 2f);
            Collider col = marsGlobeGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = marsGlobeGo.GetComponent<Renderer>();
            if (r != null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                Material marsMat = new Material(lit != null ? lit : Shader.Find("Standard"));
                marsMat.SetTexture("_BaseMap", marsAlbedoTex);
                marsMat.mainTexture = marsAlbedoTex;
                marsMat.SetTexture("_BumpMap", marsBumpTex);
                marsMat.SetFloat("_BumpScale", 0.45f);
                marsMat.SetFloat("_Smoothness", 0.12f);
                marsMat.SetFloat("_Metallic", 0.02f);
                r.material = marsMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            if (atmoRimMat != null)
            {
                GameObject atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                atmo.name = "MS_Mars_Atmosphere_Shell";
                atmo.transform.SetParent(marsGlobeGo.transform, false);
                atmo.transform.localScale = Vector3.one * 1.038f;
                Collider aCol = atmo.GetComponent<Collider>();
                if (aCol != null) Destroy(aCol);

                Renderer ar = atmo.GetComponent<Renderer>();
                if (ar != null)
                {
                    atmoRimMat.SetFloat("_Intensity", 1.35f);
                    ar.material = atmoRimMat;
                    ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    ar.receiveShadows = false;
                }
            }
        }

        private void UpgradeMoonsAndOrbits()
        {
            GameObject phobos = GameObject.Find("Phobos");
            GameObject deimos = GameObject.Find("Deimos");

            if (phobos != null)
            {
                phobosTr = phobos.transform;
                foreach (Renderer pr in phobos.GetComponentsInChildren<Renderer>())
                {
                    if (pr.material != null)
                    {
                        pr.material.EnableKeyword("_EMISSION");
                        pr.material.SetColor("_EmissionColor", new Color(0.75f, 0.68f, 0.60f, 1f));
                    }
                }
                phobosOrbitLr = CreateGradedMoonOrbit("MS_Orbit_Phobos_Trail", 1920f, 1.09f, new Color(0.45f, 0.72f, 1.0f, 1f), 7.5f);
            }

            if (deimos != null)
            {
                deimosTr = deimos.transform;
                foreach (Renderer dr in deimos.GetComponentsInChildren<Renderer>())
                {
                    if (dr.material != null)
                    {
                        dr.material.EnableKeyword("_EMISSION");
                        dr.material.SetColor("_EmissionColor", new Color(0.74f, 0.68f, 0.56f, 1f));
                    }
                }
                deimosOrbitLr = CreateGradedMoonOrbit("MS_Orbit_Deimos_Trail", 2780f, 1.79f, new Color(0.98f, 0.85f, 0.35f, 1f), 7.0f);
            }
        }

        private LineRenderer CreateGradedMoonOrbit(string name, float radius, float inclDeg, Color accent, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            int segs = 180;
            lr.positionCount = segs;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;

            Shader unlit = Shader.Find("Sprites/Default");
            Material mat = new Material(unlit);
            mat.color = Color.white;
            lr.material = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float inc = inclDeg * Mathf.Deg2Rad;
            for (int i = 0; i < segs; i++)
            {
                float th = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 local = new Vector3(
                    radius * Mathf.Cos(th),
                    radius * Mathf.Sin(th) * Mathf.Sin(inc) + 120f * Mathf.Sin(th),
                    radius * Mathf.Sin(th) * Mathf.Cos(inc)
                );
                lr.SetPosition(i, marsCenter + local);
            }

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(accent * 0.35f, 0f),
                    new GradientColorKey(accent, 0.72f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.18f, 0f),
                    new GradientAlphaKey(0.55f, 0.75f),
                    new GradientAlphaKey(0.95f, 1f)
                }
            );
            lr.colorGradient = grad;
            return lr;
        }

        private void EnhanceConstellationTrails()
        {
            string[] craftNames = new[] { "MS_MRO_Root", "MS_Odyssey_Root", "MS_MAVEN_Root", "MS_GlobalSurveyor_Root" };
            List<Transform> crafts = new List<Transform>();
            foreach (var cn in craftNames)
            {
                GameObject cgo = GameObject.Find(cn);
                if (cgo != null) crafts.Add(cgo.transform);
            }

            LineRenderer[] allLrs = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
            foreach (var lr in allLrs)
            {
                if (lr == phobosOrbitLr || lr == deimosOrbitLr) continue;
                if (!lr.loop || lr.positionCount < 20) continue;

                Color baseCol = lr.startColor;
                Shader unlit = Shader.Find("Sprites/Default");
                if (unlit != null && lr.material != null)
                {
                    lr.material = new Material(unlit);
                    lr.material.color = Color.white;
                }
                lr.startWidth = 4.2f;
                lr.endWidth = 4.2f;

                Vector3[] pts = new Vector3[lr.positionCount];
                lr.GetPositions(pts);

                Transform nearestCraft = null;
                float bestDist = float.MaxValue;
                foreach (var ct in crafts)
                {
                    for (int k = 0; k < pts.Length; k += 8)
                    {
                        float dk = Vector3.SqrMagnitude(ct.position - pts[k]);
                        if (dk < bestDist)
                        {
                            bestDist = dk;
                            nearestCraft = ct;
                        }
                    }
                }

                Gradient g = new Gradient();
                g.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(baseCol * 0.4f, 0f),
                        new GradientColorKey(baseCol, 0.7f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.14f, 0f),
                        new GradientAlphaKey(0.55f, 0.7f),
                        new GradientAlphaKey(0.95f, 1f)
                    }
                );
                lr.colorGradient = g;

                orbiterTracks.Add(new OrbitTrackData
                {
                    line = lr,
                    tracker = nearestCraft,
                    baseColor = baseCol,
                    points = pts
                });
            }
        }

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private float SampleHorizonTerrainHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float basinMask = SmoothStep01(24f, 85f, r);
            float rimMask = SmoothStep01(145f, 355f, r);
            float dunes = (Mathf.PerlinNoise(x * 0.018f + 11.2f, z * 0.018f + 4.7f) - 0.5f) * 3.4f;
            float ridges = Mathf.Abs(Mathf.PerlinNoise(x * 0.007f + 27.3f, z * 0.007f + 19.1f) - 0.5f) * 44f;
            float fine = (Mathf.PerlinNoise(x * 0.075f + 3.1f, z * 0.075f + 9.4f) - 0.5f) * 0.65f;
            return -0.06f + (dunes + fine) * basinMask + ridges * rimMask;
        }

        private void BuildJezeroHorizonTerrain()
        {
            GameObject terrainGo = new GameObject("MS_Jezero_Horizon_Terrain");
            terrainGo.transform.SetParent(transform, false);

            const int grid = 96;
            const float size = 980f;
            Mesh mesh = new Mesh { name = "JezeroHorizonMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] verts = new Vector3[(grid + 1) * (grid + 1)];
            Vector2[] uvs = new Vector2[(grid + 1) * (grid + 1)];
            int[] tris = new int[grid * grid * 6];

            for (int z = 0; z <= grid; z++)
            {
                float tz = z / (float)grid;
                float wz = (tz - 0.5f) * size;
                for (int x = 0; x <= grid; x++)
                {
                    float tx = x / (float)grid;
                    float wx = (tx - 0.5f) * size;
                    int idx = z * (grid + 1) + x;
                    verts[idx] = new Vector3(wx, SampleHorizonTerrainHeight(wx, wz), wz);
                    uvs[idx] = new Vector2(tx * 16f, tz * 16f);
                }
            }

            int ti = 0;
            for (int z = 0; z < grid; z++)
            {
                for (int x = 0; x < grid; x++)
                {
                    int i0 = z * (grid + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (grid + 1);
                    int i3 = i2 + 1;
                    tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                    tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter mf = terrainGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = terrainGo.AddComponent<MeshRenderer>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = new Material(lit != null ? lit : Shader.Find("Standard"));

            // Generate dedicated seamless tileable Jezero regolith sand + basalt pebble texture
            const int tw = 256;
            Texture2D regoTex = new Texture2D(tw, tw, TextureFormat.RGBA32, true);
            Texture2D regoBump = new Texture2D(tw, tw, TextureFormat.RGBA32, true);
            Color32[] rc = new Color32[tw * tw];
            Color32[] rb = new Color32[tw * tw];
            for (int py = 0; py < tw; py++)
            {
                for (int px = 0; px < tw; px++)
                {
                    float u = px / (float)tw;
                    float v = py / (float)tw;
                    // Seamless periodic domain wrap
                    float n1 = Mathf.PerlinNoise(Mathf.Cos(u * Mathf.PI * 2f) * 2.1f + 5.2f, Mathf.Sin(v * Mathf.PI * 2f) * 2.1f + 3.4f);
                    float n2 = Mathf.PerlinNoise(Mathf.Sin(u * Mathf.PI * 2f) * 5.8f + 12f, Mathf.Cos(v * Mathf.PI * 2f) * 5.8f + 8f);
                    float ripples = Mathf.Sin((u * 14f + n1 * 1.4f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float tone = n1 * 0.45f + n2 * 0.38f + ripples * 0.17f;
                    float cr = Mathf.Clamp01(0.69f + (tone - 0.5f) * 0.18f);
                    float cg = Mathf.Clamp01(0.39f + (tone - 0.5f) * 0.11f);
                    float cb = Mathf.Clamp01(0.22f + (tone - 0.5f) * 0.07f);
                    int idx = py * tw + px;
                    rc[idx] = new Color(cr, cg, cb, 1f);
                    byte bv = (byte)Mathf.Clamp(tone * 255f, 0f, 255f);
                    rb[idx] = new Color32(bv, bv, bv, 255);
                }
            }
            regoTex.wrapMode = TextureWrapMode.Repeat;
            regoTex.SetPixels32(rc);
            regoTex.Apply(true);
            regoBump.wrapMode = TextureWrapMode.Repeat;
            regoBump.SetPixels32(rb);
            regoBump.Apply(true);

            mat.SetTexture("_BaseMap", regoTex);
            mat.mainTexture = regoTex;
            mat.SetTexture("_BumpMap", regoBump);
            mat.SetFloat("_BumpScale", 0.42f);
            mat.SetColor("_BaseColor", Color.white);
            mat.color = Color.white;
            mat.SetFloat("_Smoothness", 0.06f);
            mr.sharedMaterial = mat;
            mr.receiveShadows = true;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SpawnJezeroRocksAndDustMotes()
        {
            GameObject rocksRoot = new GameObject("MS_Jezero_Basalt_Rocks");
            rocksRoot.transform.SetParent(transform, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material rockMat = new Material(lit != null ? lit : Shader.Find("Standard"));
            rockMat.SetColor("_BaseColor", new Color(0.38f, 0.21f, 0.14f, 1f));
            rockMat.color = new Color(0.38f, 0.21f, 0.14f, 1f);
            rockMat.SetFloat("_Smoothness", 0.08f);

            System.Random rng = new System.Random(2020);
            for (int i = 0; i < 120; i++)
            {
                double ang = rng.NextDouble() * Math.PI * 2.0;
                double dist = 12.0 + Math.Pow(rng.NextDouble(), 0.7) * 95.0;
                float x = (float)(Math.Cos(ang) * dist);
                float z = (float)(Math.Sin(ang) * dist);
                float y = SampleHorizonTerrainHeight(x, z);

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"JezeroRock_{i:000}";
                rock.transform.SetParent(rocksRoot.transform, false);
                float s = 0.22f + (float)Math.Pow(rng.NextDouble(), 2.4) * 1.25f;
                // Embed bottom half solidly in Martian regolith so rocks never hover
                rock.transform.position = new Vector3(x, y + s * 0.08f, z);
                rock.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
                rock.transform.localScale = new Vector3(s * 1.45f, s * 0.55f, s * 1.2f);
                Collider c = rock.GetComponent<Collider>();
                if (c != null) Destroy(c);
                Renderer rr = rock.GetComponent<Renderer>();
                if (rr != null) rr.sharedMaterial = rockMat;
            }

            // Procedural radial glow texture so Martian dust motes never render magenta quads
            Texture2D moteTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int py = 0; py < 32; py++)
            {
                for (int px = 0; px < 32; px++)
                {
                    float dx = (px - 15.5f) / 15.5f;
                    float dy = (py - 15.5f) / 15.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    moteTex.SetPixel(px, py, new Color(1f, 0.85f, 0.68f, a));
                }
            }
            moteTex.Apply();

            GameObject dustGo = new GameObject("MS_Martian_Dust_Motes");
            dustGo.transform.SetParent(transform, false);
            dustGo.transform.position = new Vector3(0f, 3.2f, 0f);
            ParticleSystem ps = dustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 8f;
            main.startSpeed = 0.5f;
            main.startSize = 0.14f;
            main.maxParticles = 180;
            main.startColor = new Color(0.95f, 0.72f, 0.50f, 0.42f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 7f, 50f);

            ParticleSystemRenderer psr = dustGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader pShader = Shader.Find("Sprites/Default");
                if (pShader == null) pShader = Shader.Find("Universal Render Pipeline/Unlit");
                Material pMat = new Material(pShader);
                pMat.mainTexture = moteTex;
                pMat.color = new Color(0.95f, 0.76f, 0.55f, 0.5f);
                psr.sharedMaterial = pMat;
            }
        }

        private void ConfigureOrbitalCameraFraming()
        {
            CinemachineCamera[] cams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c.name == "CM_OrbitalFleetView")
                {
                    orbitalCam = c;
                    c.transform.position = marsCenter + new Vector3(3100f, 2250f, -5200f);
                    c.transform.LookAt(marsCenter + new Vector3(0f, 200f, 0f));
                    var lens = c.Lens;
                    lens.FieldOfView = 52f;
                    lens.FarClipPlane = 35000f;
                    c.Lens = lens;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            if (GetComponent<MarsVisualRealismPolish>() == null)
            {
                gameObject.AddComponent<MarsVisualRealismPolish>();
            }
            simTime += Time.deltaTime;
            HandleCelestialFocusHotkeys();

            if (orbitOverrideActive && orbitalCam != null)
            {
                orbitalCam.Priority.Value = 100;
            }

            bool isOrbitCam = orbitOverrideActive ||
                              (orbitalCam != null && orbitalCam.Priority.Value >= 15) ||
                              (Camera.main != null && Vector3.Distance(Camera.main.transform.position, marsCenter) > 2200f && Camera.main.transform.position.y > 400f);
            float targetSpace = isOrbitCam ? 1f : 0f;
            spaceBlend = Mathf.MoveTowards(spaceBlend, targetSpace, Time.deltaTime * 2.5f);

            // Derive scenic sky Sun direction in open upper-right Martian sky (~8 deg elev) & Orbit frame
            Vector3 surfaceSunDir = new Vector3(-0.02f + Mathf.Sin(simTime * 0.015f) * 0.02f, 0.145f, 0.989f).normalized;
            Vector3 orbitSunDir = new Vector3(-0.28f, -0.04f, 0.96f).normalized;
            Vector3 sunDir = Vector3.Slerp(surfaceSunDir, orbitSunDir, spaceBlend);

            if (skyboxMat != null)
            {
                skyboxMat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                skyboxMat.SetFloat("_SpaceBlend", spaceBlend);
            }

            if (atmoRimMat != null)
            {
                atmoRimMat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            }

            if (spaceSunGo != null && Camera.main != null)
            {
                spaceSunGo.SetActive(spaceBlend > 0.05f);
                spaceSunGo.transform.position = Camera.main.transform.position + sunDir * 14000f;
                spaceSunGo.transform.localScale = Vector3.one * 460f;
            }

            if (marsGlobeGo != null)
            {
                marsGlobeGo.SetActive(spaceBlend > 0.02f);
                if (marsGlobeGo.activeSelf)
                {
                    marsGlobeGo.transform.Rotate(Vector3.up, Time.deltaTime * 0.35f, Space.World);
                }
            }

            // Smoothly scale & frame Phobos & Deimos for both Surface sky transit and Orbital system view
            if (phobosTr != null)
            {
                if (spaceBlend > 0.35f)
                {
                    float th = simTime * 0.14f + 0.5f;
                    float inc = 1.09f * Mathf.Deg2Rad;
                    phobosTr.position = marsCenter + new Vector3(1920f * Mathf.Cos(th), 1920f * Mathf.Sin(th) * Mathf.Sin(inc) + 120f * Mathf.Sin(th), 1920f * Mathf.Sin(th) * Mathf.Cos(inc));
                    phobosTr.localScale = Vector3.one * 4.2f;
                }
                else
                {
                    // Place Phobos in clear upper center-left Jezero sky (~6.7 deg elev, ~30% from left)
                    float drift = simTime * 0.04f;
                    phobosTr.position = new Vector3(-390f + Mathf.Sin(drift) * 28f, 108f + Mathf.Cos(drift * 0.7f) * 5f, 900f);
                    phobosTr.localScale = Vector3.one * 1.15f;
                    phobosTr.Rotate(Vector3.up, Time.deltaTime * 6f, Space.Self);
                }
            }

            if (deimosTr != null)
            {
                if (spaceBlend > 0.35f)
                {
                    float th = simTime * 0.06f + 2.3f;
                    float inc = 1.79f * Mathf.Deg2Rad;
                    deimosTr.position = marsCenter + new Vector3(2780f * Mathf.Cos(th), 2780f * Mathf.Sin(th) * Mathf.Sin(inc) + 120f * Mathf.Sin(th), 2780f * Mathf.Sin(th) * Mathf.Cos(inc));
                    deimosTr.localScale = Vector3.one * 5.0f;
                }
                else
                {
                    // Place Deimos in clear upper center Jezero sky (~7.2 deg elev, ~42% from left)
                    float drift = simTime * 0.025f;
                    deimosTr.position = new Vector3(-240f + Mathf.Cos(drift) * 22f, 122f + Mathf.Sin(drift * 0.6f) * 4f, 940f);
                    deimosTr.localScale = Vector3.one * 1.25f;
                    deimosTr.Rotate(Vector3.up, Time.deltaTime * 4f, Space.Self);
                }
            }

            if (phobosOrbitLr != null) phobosOrbitLr.enabled = (spaceBlend > 0.25f);
            if (deimosOrbitLr != null) deimosOrbitLr.enabled = (spaceBlend > 0.25f);

            if (focusTargetMode != 0 && Camera.main != null)
            {
                ApplyFocusOverride();
            }
        }

        public void ClearCameraOverrides()
        {
            focusTargetMode = 0;
            orbitOverrideActive = false;
            if (orbitalCam != null)
            {
                orbitalCam.Priority.Value = 5;
            }
        }

        private void HandleCelestialFocusHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if ((kb.oKey.wasPressedThisFrame || kb.digit5Key.wasPressedThisFrame) && orbitalCam != null)
            {
                orbitOverrideActive = kb.digit5Key.wasPressedThisFrame ? true : !orbitOverrideActive;
                orbitalCam.Priority.Value = orbitOverrideActive ? 100 : 5;
                focusTargetMode = 0;
            }
            else if (kb.digit6Key.wasPressedThisFrame || kb.uKey.wasPressedThisFrame)
            {
                focusTargetMode = (focusTargetMode == 1) ? 0 : 1;
            }
            else if (kb.digit7Key.wasPressedThisFrame)
            {
                focusTargetMode = (focusTargetMode == 2) ? 0 : 2;
            }
            else if (kb.digit8Key.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)
            {
                focusTargetMode = (focusTargetMode == 3) ? 0 : 3;
            }
            else if (kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame ||
                     kb.digit3Key.wasPressedThisFrame || kb.digit4Key.wasPressedThisFrame)
            {
                focusTargetMode = 0;
                orbitOverrideActive = false;
                if (orbitalCam != null)
                {
                    orbitalCam.Priority.Value = 5;
                }
            }
        }

        private void ApplyFocusOverride()
        {
            if (Camera.main == null) return;
            Transform camT = Camera.main.transform;
            if (focusTargetMode == 1 && spaceSunGo != null)
            {
                camT.LookAt(spaceSunGo.transform.position);
            }
            else if (focusTargetMode == 2 && phobosTr != null)
            {
                camT.LookAt(phobosTr.position);
            }
            else if (focusTargetMode == 3 && deimosTr != null)
            {
                camT.LookAt(deimosTr.position);
            }
        }
    }
}
