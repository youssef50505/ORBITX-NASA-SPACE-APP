using System.Collections.Generic;
using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Builds and populates interactive base facilities, official NASA GLB infrastructure mounts,
    /// high-detail Agriculture Hydroponics Dome, First Aid / Medical & Radiation Bay,
    /// Mars EDL Lifting-Body Aircraft/Lander, Mars Ascent Rocket Pad,
    /// 12 USGS/NASA Jezero stratified geological outcrops, and NASA fleet metadata targets.
    /// </summary>
    public class MarsBaseFacilitiesBuilder : MonoBehaviour
    {
        public static MarsBaseFacilitiesBuilder Instance { get; private set; }

        public static readonly Vector2 POS_AGRICULTURE = new Vector2(-25.0f, -4.0f);
        public static readonly Vector2 POS_MEDICAL_BAY = new Vector2(-10.0f, 24.0f);
        public static readonly Vector2 POS_HABITAT_HDU = new Vector2(-24.0f, 18.0f);
        public static readonly Vector2 POS_EDL_AIRCRAFT = new Vector2(32.0f, -18.0f);
        public static readonly Vector2 POS_ROCKET_PAD = new Vector2(36.0f, 24.0f);

        private Transform facilitiesRoot;
        private Transform outcropsRoot;
        private bool baseBuilt = false;
        private bool outcropsBuilt = false;

        private readonly Dictionary<string, Transform> quickTargets = new Dictionary<string, Transform>();

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
            BuildAllFacilitiesAndTargets();

            if (MarsDatasetLoader.Instance != null)
            {
                if (MarsDatasetLoader.Instance.IsLoaded)
                {
                    BuildOutcropsFromDatasets();
                }
                else
                {
                    MarsDatasetLoader.Instance.OnDataLoaded += HandleDatasetsLoaded;
                }
            }
        }

        private void OnDestroy()
        {
            if (MarsDatasetLoader.Instance != null)
            {
                MarsDatasetLoader.Instance.OnDataLoaded -= HandleDatasetsLoaded;
            }
        }

        private void HandleDatasetsLoaded()
        {
            BuildOutcropsFromDatasets();
        }

        public Transform GetQuickTarget(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            quickTargets.TryGetValue(key, out Transform t);
            return t;
        }

        public bool EvaluateInteriorShelter(Vector3 worldPos, out string zoneName)
        {
            float dAgri = Vector2.Distance(new Vector2(worldPos.x, worldPos.z), POS_AGRICULTURE);
            if (dAgri < 5.6f)
            {
                zoneName = "Closed-Loop Agriculture & Hydroponics Dome";
                return true;
            }

            Vector2 dMed = new Vector2(worldPos.x - POS_MEDICAL_BAY.x, worldPos.z - POS_MEDICAL_BAY.y);
            if (Mathf.Abs(dMed.x) < 4.2f && Mathf.Abs(dMed.y) < 3.2f)
            {
                zoneName = "First Aid · Medical & Radiation Triage Bay";
                return true;
            }

            float dHab = Vector2.Distance(new Vector2(worldPos.x, worldPos.z), POS_HABITAT_HDU);
            if (dHab < 5.2f)
            {
                zoneName = "NASA Habitat Demonstration Unit (HDU)";
                return true;
            }

            zoneName = "Jezero Crater EVA Surface";
            return false;
        }

        public void BuildAllFacilitiesAndTargets()
        {
            if (baseBuilt) return;
            baseBuilt = true;

            GameObject rootGo = GameObject.Find("MS_Base_Facilities");
            if (rootGo == null) rootGo = new GameObject("MS_Base_Facilities");
            facilitiesRoot = rootGo.transform;

            BuildAgricultureHydroponicsDome();
            BuildMedicalRadiationBay();
            BuildHabitatPadAndWrapHdu();
            BuildEdlLiftingBodyLander();
            BuildMarsAscentRocketPad();
            AttachHeroFacilityGlbUpgrades();
            AttachFleetInspectables();
        }

        /// <summary>
        /// Phase 1.2: Replaces procedural primitive meshes on AgriDome, MedBay, EDL Lander,
        /// and Launch Pad with Blender-authored PBR Hero GLBs while preserving all shelter zones,
        /// walkways, quickTargets, and MarsInspectableTarget scientific metadata.
        /// </summary>
        private void AttachHeroFacilityGlbUpgrades()
        {
            MountHeroFacilityGlb("MS_Hero_AgriDome", "MS_Agriculture_Lab", POS_AGRICULTURE, 1.12f);
            MountHeroFacilityGlb("MS_Hero_MedBay", "MS_Medical_Radiation_Bay", POS_MEDICAL_BAY, 1.0f);
            MountHeroFacilityGlb("MS_Hero_EDL_Lander", "MS_EDL_LiftingBody_Lander", POS_EDL_AIRCRAFT, 1.05f);
            MountHeroFacilityGlb("MS_Hero_LaunchPad", "MS_Mars_Ascent_Launch_Pad", POS_ROCKET_PAD, 1.0f);
        }

        private void MountHeroFacilityGlb(string heroGlbNodeName, string legacyRootName, Vector2 xz, float scale)
        {
            GameObject heroNode = GameObject.Find(heroGlbNodeName);
            if (heroNode == null || heroNode.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                return;
            }

            float y = MarsVisualRealismPolish.SampleTerrainElevation(xz.x, xz.y);
            GameObject legacyRoot = GameObject.Find(legacyRootName);
            if (legacyRoot != null)
            {
                // Hide boxy primitive child renderers (keep inspectable targets & walkable floor colliders intact)
                foreach (Renderer r in legacyRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.IsChildOf(heroNode.transform)) continue;
                    string rn = r.gameObject.name;
                    if (rn.Contains("Inspect") || rn.Contains("Pylon") || rn.Contains("Rocket") || rn.Contains("Ares") || rn.Contains("Launcher"))
                        continue;
                    r.enabled = false;
                }
                heroNode.transform.SetParent(legacyRoot.transform, false);
                heroNode.transform.localPosition = Vector3.zero;
            }
            else
            {
                heroNode.transform.position = new Vector3(xz.x, y, xz.y);
            }

            heroNode.transform.localScale = Vector3.one * scale;
            // Ground bottom cleanly to local Y = 0
            Renderer[] rends = heroNode.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float targetY = legacyRoot != null ? legacyRoot.transform.position.y : y;
                heroNode.transform.position += new Vector3(0f, targetY - b.min.y, 0f);
            }
        }

        public void BuildOutcropsFromDatasets()
        {
            if (outcropsBuilt) return;
            var loader = MarsDatasetLoader.Instance;
            if (loader == null || loader.Outcrops == null || loader.Outcrops.Count == 0) return;

            outcropsBuilt = true;
            GameObject rootGo = GameObject.Find("MS_Jezero_Rock_Outcrops");
            if (rootGo == null) rootGo = new GameObject("MS_Jezero_Rock_Outcrops");
            outcropsRoot = rootGo.transform;

            foreach (var outcrop in loader.Outcrops)
            {
                CreateGeologicalOutcrop(outcrop);
            }
        }

        // -----------------------------------------------------------------
        // 1. CLOSED-LOOP AGRICULTURE & HYDROPONICS RESEARCH DOME
        // -----------------------------------------------------------------
        private void BuildAgricultureHydroponicsDome()
        {
            GameObject existing = GameObject.Find("MS_Agriculture_Lab");
            if (existing != null) Destroy(existing);

            float y = MarsVisualRealismPolish.SampleTerrainElevation(POS_AGRICULTURE.x, POS_AGRICULTURE.y);
            GameObject root = new GameObject("MS_Agriculture_Lab");
            root.transform.SetParent(facilitiesRoot, false);
            root.transform.position = new Vector3(POS_AGRICULTURE.x, y, POS_AGRICULTURE.y);
            quickTargets["Agri"] = root.transform;

            Material regolithBermMat = CreatePbrMat("Agri_Berm", new Color(0.46f, 0.25f, 0.16f), 0.0f, 0.12f);
            Material floorMat = CreatePbrMat("Agri_Floor", new Color(0.24f, 0.27f, 0.30f), 0.6f, 0.45f);
            Material frameMat = CreatePbrMat("Agri_Frame", new Color(0.86f, 0.88f, 0.91f), 0.72f, 0.62f);
            Material accentTeal = CreatePbrMat("Agri_Teal", new Color(0.10f, 0.62f, 0.56f), 0.25f, 0.55f);
            Material glassMat = CreateGlassMat("Agri_Glass", new Color(0.68f, 0.92f, 0.98f, 0.08f));
            Material trayMat = CreatePbrMat("Agri_Tray", new Color(0.75f, 0.78f, 0.82f), 0.65f, 0.55f);
            Material stemMat = CreatePbrMat("Agri_Stem", new Color(0.22f, 0.46f, 0.18f), 0.0f, 0.30f);
            Material leafBright = CreatePbrMat("Agri_LeafA", new Color(0.20f, 0.70f, 0.24f), 0.0f, 0.45f);
            Material leafDeep = CreatePbrMat("Agri_LeafB", new Color(0.14f, 0.54f, 0.21f), 0.0f, 0.40f);
            Material tomatoMat = CreatePbrMat("Agri_Fruit", new Color(0.90f, 0.18f, 0.14f), 0.05f, 0.65f);
            Material parLedMat = CreateEmissiveMat("Agri_PAR_LED", new Color(0.95f, 0.42f, 0.86f), new Color(1.0f, 0.52f, 0.90f) * 1.8f);
            Material pipeBlueMat = CreatePbrMat("Agri_WaterPipe", new Color(0.16f, 0.52f, 0.85f), 0.35f, 0.60f);
            Material pipeOrangeMat = CreatePbrMat("Agri_O2Pipe", new Color(0.88f, 0.50f, 0.12f), 0.35f, 0.60f);
            Material solarMat = CreatePbrMat("Agri_SolarCell", new Color(0.08f, 0.14f, 0.30f), 0.75f, 0.82f);
            Material screenMat = CreateEmissiveMat("Agri_ScreenGlow", new Color(0.12f, 0.78f, 0.95f), new Color(0.15f, 0.85f, 1.0f) * 1.3f);

            // Walkable floor deck + radiation shielding collar (with open doorway on +X East side!)
            CreatePart("Foundation_Pad", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(13.2f, 0.12f, 13.2f), regolithBermMat, true);
            CreatePart("Interior_Deck", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(11.6f, 0.06f, 11.6f), floorMat, true);
            CreatePart("Center_Walkway", PrimitiveType.Cube, root.transform, new Vector3(0.8f, 0.17f, 0f), Vector3.zero, new Vector3(11.4f, 0.03f, 2.1f), accentTeal, false);

            // Perimeter concrete pressure ring leaving East door open (angles around -20..+20 deg skipped for airlock)
            for (int seg = 0; seg < 16; seg++)
            {
                float deg = seg * 22.5f;
                if (deg < 23f || deg > 337f) continue; // Open walkable entrance facing East!
                float rad = deg * Mathf.Deg2Rad;
                Vector3 wp = new Vector3(Mathf.Cos(rad) * 5.80f, 0.62f, Mathf.Sin(rad) * 5.80f);
                Quaternion rot = Quaternion.Euler(0f, -deg, 0f);
                CreatePart($"PerimeterWall_{seg}", PrimitiveType.Cube, root.transform, wp, rot.eulerAngles, new Vector3(0.42f, 1.05f, 2.38f), frameMat, true);
            }

            // Smooth Continuous Extruded Geodesic Meridian Arches (zero jagged stick overlaps)
            Mesh ribArchMesh = BuildMeridianArchMesh(5.82f, 4.52f, 0.065f, 20);
            for (int r = 0; r < 6; r++)
            {
                GameObject arch = new GameObject($"DomeArch_{r}");
                arch.transform.SetParent(root.transform, false);
                arch.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                arch.transform.localRotation = Quaternion.Euler(0f, r * 30f, 0f);
                arch.AddComponent<MeshFilter>().sharedMesh = ribArchMesh;
                MeshRenderer amr = arch.AddComponent<MeshRenderer>();
                amr.sharedMaterial = frameMat;
            }
            // Horizontal geodesic latitude collar rings at two elevations
            for (int ring = 0; ring < 2; ring++)
            {
                float ringRad = (ring == 0 ? 32f : 58f) * Mathf.Deg2Rad;
                float ringR = 5.82f * Mathf.Cos(ringRad);
                float ringY = 0.95f + 4.52f * Mathf.Sin(ringRad);
                for (int s = 0; s < 24; s++)
                {
                    float a = s * 15f * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Cos(a) * ringR, ringY, Mathf.Sin(a) * ringR);
                    Quaternion rRot = Quaternion.Euler(0f, -s * 15f, 0f);
                    CreatePart($"DomeCollar_{ring}_{s}", PrimitiveType.Cube, root.transform, pos, rRot.eulerAngles, new Vector3(0.09f, 0.09f, 2f * ringR * Mathf.Sin(7.5f * Mathf.Deg2Rad) + 0.05f), frameMat, false);
                }
            }
            // Top skylight ventilation cupola cap
            CreatePart("DomeCrownRing", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 5.42f, 0f), Vector3.zero, new Vector3(1.65f, 0.12f, 1.65f), accentTeal, false);

            // Translucent protective glass cupola (no collider so player walks freely inside!)
            CreatePart("Cupola_Glass", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(11.6f, 9.0f, 11.6f), glassMat, false);

            // Pressurized East Airlock Vestibule (+X open entry)
            CreatePart("Airlock_Ramp", PrimitiveType.Cube, root.transform, new Vector3(7.4f, 0.08f, 0f), new Vector3(0f, 0f, -4f), new Vector3(2.8f, 0.10f, 2.8f), floorMat, true);
            CreatePart("Airlock_Frame_N", PrimitiveType.Cube, root.transform, new Vector3(6.2f, 1.45f, 1.35f), Vector3.zero, new Vector3(2.4f, 2.8f, 0.35f), frameMat, true);
            CreatePart("Airlock_Frame_S", PrimitiveType.Cube, root.transform, new Vector3(6.2f, 1.45f, -1.35f), Vector3.zero, new Vector3(2.4f, 2.8f, 0.35f), frameMat, true);
            CreatePart("Airlock_Roof", PrimitiveType.Cube, root.transform, new Vector3(6.2f, 2.90f, 0f), Vector3.zero, new Vector3(2.6f, 0.35f, 3.05f), frameMat, true);
            CreatePart("Airlock_StatusLED", PrimitiveType.Cube, root.transform, new Vector3(7.42f, 2.75f, 0f), Vector3.zero, new Vector3(0.12f, 0.24f, 2.1f), accentTeal, false);

            // Multi-tier Botanical Hydroponic Bays (North and South rows)
            for (int side = -1; side <= 1; side += 2)
            {
                float zBase = side * 2.55f;
                for (int rack = 0; rack < 2; rack++)
                {
                    float xCenter = -1.9f + rack * 3.2f;
                    // Vertical stainless rack uprights
                    for (int leg = -1; leg <= 1; leg += 2)
                    {
                        CreatePart($"RackPost_{side}_{rack}_{leg}", PrimitiveType.Cube, root.transform,
                            new Vector3(xCenter + leg * 1.15f, 1.25f, zBase), Vector3.zero, new Vector3(0.08f, 2.3f, 0.9f), frameMat, true);
                    }

                    // 2 Grow Tiers with nutrient troughs, PAR magenta LEDs & botanical multi-leaf plants
                    for (int tier = 0; tier < 2; tier++)
                    {
                        float ty = 0.65f + tier * 1.05f;
                        CreatePart($"HydroTrough_{side}_{rack}_{tier}", PrimitiveType.Cube, root.transform,
                            new Vector3(xCenter, ty, zBase), Vector3.zero, new Vector3(2.35f, 0.12f, 0.88f), trayMat, false);

                        CreatePart($"PAR_Light_{side}_{rack}_{tier}", PrimitiveType.Cube, root.transform,
                            new Vector3(xCenter, ty + 0.66f, zBase), Vector3.zero, new Vector3(2.1f, 0.05f, 0.22f), parLedMat, false);

                        // 5 Detailed lush botanical plants per trough (multi-frond canopy visible through dome)
                        for (int p = 0; p < 5; p++)
                        {
                            float px = xCenter - 0.85f + p * 0.42f;
                            float pz = zBase + ((p % 2 == 0) ? 0.14f : -0.14f);
                            CreatePart($"PlantStem_{side}_{rack}_{tier}_{p}", PrimitiveType.Cylinder, root.transform,
                                new Vector3(px, ty + 0.24f, pz), Vector3.zero, new Vector3(0.04f, 0.20f, 0.04f), stemMat, false);

                            for (int lf = 0; lf < 6; lf++)
                            {
                                float lAngle = lf * 60f + p * 23f;
                                Material lMat = (lf % 2 == 0) ? leafBright : leafDeep;
                                Quaternion lRot = Quaternion.Euler(28f + (lf % 2) * 14f, lAngle, 0f);
                                Vector3 lOff = lRot * new Vector3(0f, 0.03f, 0.15f);
                                CreatePart($"Leaf_{side}_{rack}_{tier}_{p}_{lf}", PrimitiveType.Sphere, root.transform,
                                    new Vector3(px, ty + 0.34f, pz) + lOff, lRot.eulerAngles, new Vector3(0.13f, 0.035f, 0.25f), lMat, false);
                            }

                            // Cherry tomato / pepper fruits on alternating racks
                            if (rack == 1 && (p % 2 == 0))
                            {
                                CreatePart($"Fruit_{side}_{rack}_{tier}_{p}", PrimitiveType.Sphere, root.transform,
                                    new Vector3(px + 0.05f, ty + 0.38f, pz - 0.04f), Vector3.zero, Vector3.one * 0.09f, tomatoMat, false);
                            }
                        }
                    }
                }
            }

            // Closed-Loop ISRU Tanks (H2O / O2 / Nutrient Recycler) at West End (-X)
            CreatePart("ISRU_WaterTank", PrimitiveType.Cylinder, root.transform, new Vector3(-4.2f, 1.35f, 1.2f), Vector3.zero, new Vector3(0.95f, 1.2f, 0.95f), pipeBlueMat, true);
            CreatePart("ISRU_MOXIE_O2Tank", PrimitiveType.Cylinder, root.transform, new Vector3(-4.2f, 1.35f, -1.2f), Vector3.zero, new Vector3(0.95f, 1.2f, 0.95f), pipeOrangeMat, true);
            CreatePart("ISRU_PipeBus", PrimitiveType.Cylinder, root.transform, new Vector3(-4.2f, 2.3f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.14f, 1.4f, 0.14f), frameMat, false);

            // Exterior Solar Tracking Wings
            for (int w = -1; w <= 1; w += 2)
            {
                Vector3 baseW = new Vector3(-2.5f, 0.6f, w * 7.8f);
                CreatePart($"SolarMast_{w}", PrimitiveType.Cylinder, root.transform, baseW, Vector3.zero, new Vector3(0.22f, 0.65f, 0.22f), frameMat, true);
                CreatePart($"SolarWing_{w}", PrimitiveType.Cube, root.transform, baseW + new Vector3(0f, 0.72f, 0f), new Vector3(24f * w, 0f, 0f), new Vector3(4.6f, 0.08f, 2.0f), solarMat, false);
            }

            // Interior magenta/warm botanical light + warm canopy spotlight for crisp visibility through glass
            GameObject lightGo = new GameObject("Agri_GrowLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            Light pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1.0f, 0.90f, 0.96f);
            pl.intensity = 42f;
            pl.range = 18f;

            // NASA Attribution & Data Plaque facing directly East (+X) toward arriving explorer + overhead gantry header
            CreateSignagePlaque(root.transform, new Vector3(7.52f, 3.12f, 0f), Vector3.zero,
                "ORBITX HYDROPONICS\nNASA MOXIE / ISRU LAB", accentTeal);

            // Sleek angled botanical telemetry console kiosk beside airlock (replacing boxy cube pylon)
            GameObject insp = CreatePart("Agri_InspectPylon", PrimitiveType.Cylinder, root.transform,
                new Vector3(6.95f, 0.52f, -2.05f), Vector3.zero, new Vector3(0.24f, 0.52f, 0.24f), frameMat, true);
            CreatePart("Agri_InspectHead", PrimitiveType.Cube, root.transform,
                new Vector3(6.95f, 1.08f, -2.05f), new Vector3(0f, 0f, -26f), new Vector3(0.46f, 0.07f, 0.64f), accentTeal, false);
            CreatePart("Agri_InspectScreen", PrimitiveType.Cube, root.transform,
                new Vector3(6.99f, 1.12f, -2.05f), new Vector3(0f, 0f, -26f), new Vector3(0.38f, 0.02f, 0.54f), screenMat, false);

            var target = insp.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "facility_agriculture_dome",
                title: "Closed-Loop Hydroponics & Bioregenerative Dome",
                category: "Base Facility · ECLSS & ISRU",
                subtitle: "Walkable Pressurized Mars Greenhouse (11.6m Dome)",
                coords: "Jezero Base West · 18.4446°N, 77.4503°E · Walk Inside via East Airlock",
                geology: "Anchored onto sintered Jezero crater basalt pad w/ regolith radiation collar.",
                mineralogy: "ISRU Hydroponics: dwarf wheat, leafy greens, cherry tomato & nitrogen-fixing legumes fed by perchlorate-scrubbed Mars ice.",
                environment: "Interior: 21.5°C, 70 kPa N2/O2, 660/450nm PAR LEDs | Coupled to NASA MOXIE 12 g/hr O2 ISRU.",
                summary: "Realistic Mars closed-loop food production dome integrating water reclamation, perchlorate filtration, and high-efficiency hydroponic racks. Enter via the East airlock!",
                sourceUrl: "https://science.nasa.gov/mission/mars-2020-perseverance/moxie/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.EnterFacility,
                actionHint: "Inspect Hydroponics Telemetry",
                interactionRadius: 9.0f
            );
        }

        // -----------------------------------------------------------------
        // 2. FIRST AID / MEDICAL & RADIATION TRIAGE BAY
        // -----------------------------------------------------------------
        private void BuildMedicalRadiationBay()
        {
            GameObject existing = GameObject.Find("MS_Medical_Bay");
            if (existing != null) Destroy(existing);

            float y = MarsVisualRealismPolish.SampleTerrainElevation(POS_MEDICAL_BAY.x, POS_MEDICAL_BAY.y);
            GameObject root = new GameObject("MS_Medical_Bay");
            root.transform.SetParent(facilitiesRoot, false);
            root.transform.position = new Vector3(POS_MEDICAL_BAY.x, y, POS_MEDICAL_BAY.y);
            quickTargets["Medical"] = root.transform;

            Material hullWhite = CreatePbrMat("Med_Hull", new Color(0.92f, 0.94f, 0.96f), 0.30f, 0.55f);
            Material floorMat = CreatePbrMat("Med_Floor", new Color(0.76f, 0.80f, 0.84f), 0.15f, 0.65f);
            Material shieldSkirt = CreatePbrMat("Med_RegolithShield", new Color(0.45f, 0.25f, 0.17f), 0.0f, 0.15f);
            Material redCrossMat = CreateEmissiveMat("Med_RedCross", new Color(0.88f, 0.12f, 0.12f), new Color(0.95f, 0.15f, 0.15f) * 0.9f);
            Material consoleMat = CreateEmissiveMat("Med_Monitor", new Color(0.12f, 0.78f, 0.95f), new Color(0.15f, 0.85f, 1.0f) * 1.4f);
            Material mattressMat = CreatePbrMat("Med_BedPad", new Color(0.20f, 0.45f, 0.70f), 0.0f, 0.35f);
            Material o2TankMat = CreatePbrMat("Med_O2Tank", new Color(0.16f, 0.68f, 0.36f), 0.45f, 0.60f);
            Material darkTrim = CreatePbrMat("Med_Trim", new Color(0.18f, 0.22f, 0.26f), 0.65f, 0.45f);

            Material medGlass = CreateGlassMat("Med_WindowGlass", new Color(0.65f, 0.90f, 0.98f, 0.14f));
            Material whiteMedDisc = CreatePbrMat("Med_WhiteCircle", new Color(0.96f, 0.97f, 0.99f), 0.1f, 0.5f);

            // Walkable floor + regolith radiation shielding perimeter berm
            CreatePart("Med_Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(9.0f, 0.16f, 7.0f), floorMat, true);
            CreatePart("Med_Roof", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.55f, 0f), Vector3.zero, new Vector3(9.5f, 0.28f, 7.5f), hullWhite, true);
            CreatePart("Med_RadShieldRoof", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.75f, 0f), Vector3.zero, new Vector3(8.6f, 0.18f, 6.6f), shieldSkirt, false);
            CreatePart("Med_EntryCanopy", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.35f, -3.95f), new Vector3(8f, 0f, 0f), new Vector3(3.4f, 0.14f, 1.15f), darkTrim, false);

            // Corner Structural Buttress Pillars + External Thermal Radiator Louvers
            CreatePart("Med_Col_NW", PrimitiveType.Cube, root.transform, new Vector3(-4.45f, 1.80f, 3.45f), Vector3.zero, new Vector3(0.42f, 3.55f, 0.42f), darkTrim, false);
            CreatePart("Med_Col_NE", PrimitiveType.Cube, root.transform, new Vector3(4.45f, 1.80f, 3.45f), Vector3.zero, new Vector3(0.42f, 3.55f, 0.42f), darkTrim, false);
            CreatePart("Med_Col_SW", PrimitiveType.Cube, root.transform, new Vector3(-4.45f, 1.80f, -3.45f), Vector3.zero, new Vector3(0.42f, 3.55f, 0.42f), darkTrim, false);
            CreatePart("Med_Col_SE", PrimitiveType.Cube, root.transform, new Vector3(4.45f, 1.80f, -3.45f), Vector3.zero, new Vector3(0.42f, 3.55f, 0.42f), darkTrim, false);
            CreatePart("Med_Radiator_Unit", PrimitiveType.Cube, root.transform, new Vector3(4.65f, 2.20f, 0f), Vector3.zero, new Vector3(0.28f, 1.6f, 3.6f), darkTrim, false);

            // Walls: North, West, East solid; South has wide 2.6m airlock door + TWO recessed glass viewports looking inside!
            CreatePart("Med_Wall_N", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.80f, 3.4f), Vector3.zero, new Vector3(9.0f, 3.4f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_W", PrimitiveType.Cube, root.transform, new Vector3(-4.4f, 1.80f, 0f), Vector3.zero, new Vector3(0.24f, 3.4f, 7.0f), hullWhite, true);
            CreatePart("Med_Wall_E", PrimitiveType.Cube, root.transform, new Vector3(4.4f, 1.80f, 0f), Vector3.zero, new Vector3(0.24f, 3.4f, 7.0f), hullWhite, true);

            // South Wall segments around two 1.7m observation windows (sill 0..0.95m, lintel 2.10..3.5m, piers flanking door & corners)
            CreatePart("Med_Wall_S_SillL", PrimitiveType.Cube, root.transform, new Vector3(-2.9f, 0.50f, -3.4f), Vector3.zero, new Vector3(3.2f, 0.92f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_SillR", PrimitiveType.Cube, root.transform, new Vector3(2.9f, 0.50f, -3.4f), Vector3.zero, new Vector3(3.2f, 0.92f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_TopL", PrimitiveType.Cube, root.transform, new Vector3(-2.9f, 2.75f, -3.4f), Vector3.zero, new Vector3(3.2f, 1.45f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_TopR", PrimitiveType.Cube, root.transform, new Vector3(2.9f, 2.75f, -3.4f), Vector3.zero, new Vector3(3.2f, 1.45f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_PierL", PrimitiveType.Cube, root.transform, new Vector3(-1.55f, 1.50f, -3.4f), Vector3.zero, new Vector3(0.55f, 1.12f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_PierR", PrimitiveType.Cube, root.transform, new Vector3(1.55f, 1.50f, -3.4f), Vector3.zero, new Vector3(0.55f, 1.12f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_CornerL", PrimitiveType.Cube, root.transform, new Vector3(-4.15f, 1.50f, -3.4f), Vector3.zero, new Vector3(0.65f, 1.12f, 0.24f), hullWhite, true);
            CreatePart("Med_Wall_S_CornerR", PrimitiveType.Cube, root.transform, new Vector3(4.15f, 1.50f, -3.4f), Vector3.zero, new Vector3(0.65f, 1.12f, 0.24f), hullWhite, true);

            // Recessed Clear Pressurized Glass Windows + Dark Aluminum Frames
            CreatePart("Med_ViewportGlass_L", PrimitiveType.Cube, root.transform, new Vector3(-2.85f, 1.50f, -3.4f), Vector3.zero, new Vector3(2.05f, 1.05f, 0.06f), medGlass, false);
            CreatePart("Med_ViewportGlass_R", PrimitiveType.Cube, root.transform, new Vector3(2.85f, 1.50f, -3.4f), Vector3.zero, new Vector3(2.05f, 1.05f, 0.06f), medGlass, false);
            CreatePart("Med_Door_Header", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.10f, -3.4f), Vector3.zero, new Vector3(2.6f, 0.72f, 0.28f), darkTrim, true);
            CreatePart("Med_EntryRamp", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.04f, -4.3f), new Vector3(4f, 0f, 0f), new Vector3(2.8f, 0.08f, 1.8f), darkTrim, true);

            // Framed White Medallion Backboards + Unmistakable Red Cross emblems on front facade + roof
            CreatePart("Med_CrossDiscL", PrimitiveType.Cylinder, root.transform, new Vector3(-2.85f, 2.68f, -3.54f), new Vector3(90f, 0f, 0f), new Vector3(1.18f, 0.03f, 1.18f), whiteMedDisc, false);
            CreatePart("Med_CrossDiscR", PrimitiveType.Cylinder, root.transform, new Vector3(2.85f, 2.68f, -3.54f), new Vector3(90f, 0f, 0f), new Vector3(1.18f, 0.03f, 1.18f), whiteMedDisc, false);
            CreatePart("Med_CrossFrontL_H", PrimitiveType.Cube, root.transform, new Vector3(-2.85f, 2.68f, -3.58f), Vector3.zero, new Vector3(0.86f, 0.26f, 0.05f), redCrossMat, false);
            CreatePart("Med_CrossFrontL_V", PrimitiveType.Cube, root.transform, new Vector3(-2.85f, 2.68f, -3.58f), Vector3.zero, new Vector3(0.26f, 0.86f, 0.05f), redCrossMat, false);
            CreatePart("Med_CrossFrontR_H", PrimitiveType.Cube, root.transform, new Vector3(2.85f, 2.68f, -3.58f), Vector3.zero, new Vector3(0.86f, 0.26f, 0.05f), redCrossMat, false);
            CreatePart("Med_CrossFrontR_V", PrimitiveType.Cube, root.transform, new Vector3(2.85f, 2.68f, -3.58f), Vector3.zero, new Vector3(0.26f, 0.86f, 0.05f), redCrossMat, false);
            CreatePart("Med_CrossRoof_H", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.86f, 0f), Vector3.zero, new Vector3(2.4f, 0.05f, 0.72f), redCrossMat, false);
            CreatePart("Med_CrossRoof_V", PrimitiveType.Cube, root.transform, new Vector3(0f, 3.86f, 0f), Vector3.zero, new Vector3(0.72f, 0.05f, 2.4f), redCrossMat, false);

            // Front overhead Medical Nameplate facing South (-Z)
            CreateSignagePlaque(root.transform, new Vector3(0f, 3.16f, -3.58f), new Vector3(0f, 90f, 0f),
                "FIRST AID · MEDICAL BAY\nNASA RAD TRIAGE SHELTER", redCrossMat);

            // Interior Clinical & Radiation Triage Equipment
            // 1. Hyperbaric Medical Exam Bed
            CreatePart("Med_BedFrame", PrimitiveType.Cube, root.transform, new Vector3(-2.3f, 0.55f, 1.1f), Vector3.zero, new Vector3(1.15f, 0.52f, 2.25f), darkTrim, true);
            CreatePart("Med_BedMattress", PrimitiveType.Cube, root.transform, new Vector3(-2.3f, 0.88f, 1.1f), Vector3.zero, new Vector3(1.05f, 0.16f, 2.15f), mattressMat, false);
            CreatePart("Med_Pillow", PrimitiveType.Cube, root.transform, new Vector3(-2.3f, 1.00f, 1.90f), Vector3.zero, new Vector3(0.72f, 0.12f, 0.38f), hullWhite, false);
            // 2. Radiation Dosimetry & Biometrics Console
            CreatePart("Med_ConsoleDesk", PrimitiveType.Cube, root.transform, new Vector3(1.8f, 0.58f, 2.45f), Vector3.zero, new Vector3(2.4f, 1.0f, 0.75f), darkTrim, true);
            CreatePart("Med_VitalsScreen", PrimitiveType.Cube, root.transform, new Vector3(1.8f, 1.55f, 2.75f), new Vector3(-12f, 0f, 0f), new Vector3(1.8f, 0.92f, 0.08f), consoleMat, false);
            // 3. Medical O2 / N2 Gas Cylinders
            CreatePart("Med_O2_Cyl1", PrimitiveType.Cylinder, root.transform, new Vector3(3.6f, 1.05f, 2.4f), Vector3.zero, new Vector3(0.40f, 0.90f, 0.40f), o2TankMat, true);
            CreatePart("Med_O2_Cyl2", PrimitiveType.Cylinder, root.transform, new Vector3(3.6f, 1.05f, 1.5f), Vector3.zero, new Vector3(0.40f, 0.90f, 0.40f), o2TankMat, true);
            // 4. Perchlorate Dust Decontamination Portal inside right aisle
            CreatePart("Med_DeconBooth", PrimitiveType.Cube, root.transform, new Vector3(2.8f, 1.45f, -1.3f), Vector3.zero, new Vector3(1.6f, 2.6f, 0.18f), darkTrim, false);

            // Interior Clinical Light
            GameObject lightGo = new GameObject("Med_InteriorLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            Light pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.88f, 0.96f, 1.0f);
            pl.intensity = 22f;
            pl.range = 14f;

            // Compact biometric scan pedestal beside South entry door
            GameObject insp = CreatePart("Med_TriagePylon", PrimitiveType.Cylinder, root.transform,
                new Vector3(1.55f, 0.58f, -4.05f), Vector3.zero, new Vector3(0.28f, 0.55f, 0.28f), darkTrim, true);
            CreatePart("Med_TriageCap", PrimitiveType.Cube, root.transform,
                new Vector3(1.55f, 1.16f, -4.05f), new Vector3(-22f, 0f, 0f), new Vector3(0.32f, 0.05f, 0.28f), consoleMat, false);

            var target = insp.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "facility_medical_triage_bay",
                title: "First Aid · Medical & Radiation Triage Bay",
                category: "Base Facility · Crew Health & RAD Shielding",
                subtitle: "Pressurized Storm Shelter, Perchlorate Decon & Medical Suite",
                coords: "Jezero Base North · 18.4450°N, 77.4506°E · Walk Inside via South Airlock",
                geology: "Shielded with 0.5m compacted Jezero basalt regolith berm (attenuates secondary neutrons & SEP flare protons).",
                mineralogy: "Airlock HEPA + electrostatic dust mitigation blocks toxic Martian Ca-perchlorates (~0.5 wt%) from entering clean clinical bay.",
                environment: "Baseline GCR: 0.67 mSv/day (244.6 mSv/yr) | NASA Career Limit: 600 mSv | SEP Storm Shelter Mode Ready",
                summary: "Crew medical triage suite featuring hyperbaric treatment bed, perchlorate decontamination archway, O2 resuscitation cylinders, and live Curiosity RAD / DONKI SEP radiation shield monitoring.",
                sourceUrl: "https://science.nasa.gov/mission/msl-curiosity/science-instruments/rad/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.EnterFacility,
                actionHint: "Inspect Medical & RAD Shield Telemetry",
                interactionRadius: 9.0f
            );
        }

        // -----------------------------------------------------------------
        // 3. NASA HABITAT DEMONSTRATION UNIT (HDU) PAD & WRAPPER
        // -----------------------------------------------------------------
        private void BuildHabitatPadAndWrapHdu()
        {
            GameObject existingPad = GameObject.Find("MS_Habitat_Complex");
            if (existingPad != null) Destroy(existingPad);

            float y = MarsVisualRealismPolish.SampleTerrainElevation(POS_HABITAT_HDU.x, POS_HABITAT_HDU.y);
            GameObject root = new GameObject("MS_Habitat_Complex");
            root.transform.SetParent(facilitiesRoot, false);
            root.transform.position = new Vector3(POS_HABITAT_HDU.x, y, POS_HABITAT_HDU.y);
            quickTargets["Habitat"] = root.transform;

            Material padMat = CreatePbrMat("Hab_Pad", new Color(0.32f, 0.33f, 0.36f), 0.35f, 0.35f);
            Material accentAmber = CreatePbrMat("Hab_Amber", new Color(0.88f, 0.62f, 0.18f), 0.4f, 0.55f);
            Material whiteComms = CreatePbrMat("Hab_White", new Color(0.86f, 0.88f, 0.91f), 0.4f, 0.5f);

            CreatePart("Hab_RegolithPad", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(11.0f, 0.16f, 11.0f), padMat, true);

            // Re-seat unified imported NASA Habitat Demonstration Unit GLBs cleanly onto this pad
            bool foundGlb = false;
            GameObject hduCombined = GameObject.Find("MS_NASA_HDU_Combined");
            if (hduCombined != null && hduCombined.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                foundGlb = true;
                hduCombined.transform.SetParent(root.transform, true);
                NormalizeAndSeatObject(hduCombined, new Vector3(0f, 0.16f, 0f), 4.8f);
                EnsureBoxCollider(hduCombined);
                LiftPitchBlackGlbMaterials(hduCombined);
            }
            else
            {
                string[] hduNames = { "MS_NASA_HDU_Part1", "MS_NASA_HDU_Part2" };
                foreach (string hName in hduNames)
                {
                    GameObject hduGo = GameObject.Find(hName);
                    if (hduGo != null)
                    {
                        foundGlb = true;
                        hduGo.transform.SetParent(root.transform, true);
                        NormalizeAndSeatObject(hduGo, new Vector3(0f, 0.16f, 0f), 4.8f);
                        EnsureBoxCollider(hduGo);
                        LiftPitchBlackGlbMaterials(hduGo);
                    }
                }
            }

            GameObject mark3 = GameObject.Find("MS_NASA_MarkIII_Spacesuit");
            if (mark3 != null)
            {
                LiftPitchBlackGlbMaterials(mark3);
                // Seat circular white-rimmed EVA Showcase Plinth directly under Mark III Spacesuit
                CreatePart("MarkIII_ShowcasePlinth", PrimitiveType.Cylinder, root.transform,
                    new Vector3(3.8f, 0.14f, -6.6f), Vector3.zero, new Vector3(1.45f, 0.14f, 1.45f), whiteComms, true);
                CreatePart("MarkIII_PlinthRing", PrimitiveType.Cylinder, root.transform,
                    new Vector3(3.8f, 0.27f, -6.6f), Vector3.zero, new Vector3(1.38f, 0.03f, 1.38f), accentAmber, false);
            }

            if (!foundGlb)
            {
                // Solid engineered backup cylindrical HDU core if GLBs haven't been baked into scene yet
                CreatePart("Hab_LowerShell", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(5.6f, 1.35f, 5.6f), whiteComms, true);
                CreatePart("Hab_UpperLoft", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3.4f, 0f), Vector3.zero, new Vector3(4.8f, 0.75f, 4.8f), accentAmber, true);
            }

            // Front work-lights so NASA HDU facade & Mark III EVA Suit stay crisp in oblique afternoon sun
            CreateHduFloodlight(root.transform, "HDU_WorkLight_FrontR", new Vector3(5.2f, 3.4f, -6.2f), 28f, 18f);
            CreateHduFloodlight(root.transform, "HDU_WorkLight_FrontL", new Vector3(-4.6f, 3.4f, -5.8f), 22f, 16f);

            // Deep Space Network Comm Dish Mast + NASA Attribution Plaque
            CreatePart("Hab_CommsMast", PrimitiveType.Cylinder, root.transform, new Vector3(4.2f, 2.4f, -2.5f), Vector3.zero, new Vector3(0.18f, 2.3f, 0.18f), whiteComms, true);
            CreatePart("Hab_Dish", PrimitiveType.Cylinder, root.transform, new Vector3(4.2f, 4.6f, -2.5f), new Vector3(48f, 35f, 0f), new Vector3(1.6f, 0.08f, 1.6f), whiteComms, false);
            CreateSignagePlaque(root.transform, new Vector3(2.4f, 3.65f, -5.1f), new Vector3(0f, 58f, 0f),
                "NASA HABITAT UNIT (HDU)\nORBITX CREW QUARTERS", accentAmber);

            // Sleek NASA Telemetry Lectern replacing the old tall yellow box pylon
            Material lecternScreen = CreateEmissiveMat("HDU_ConsoleScreen", new Color(0.12f, 0.78f, 0.95f), new Color(0.15f, 0.85f, 1.0f) * 1.4f);
            GameObject pylon = CreatePart("Hab_SciencePylon", PrimitiveType.Cylinder, root.transform,
                new Vector3(5.8f, 0.56f, -5.2f), Vector3.zero, new Vector3(0.24f, 0.54f, 0.24f), whiteComms, true);
            CreatePart("Hab_LecternHead", PrimitiveType.Cube, root.transform,
                new Vector3(5.8f, 1.12f, -5.2f), new Vector3(0f, 52f, -24f), new Vector3(0.46f, 0.08f, 0.68f), accentAmber, false);
            CreatePart("Hab_LecternScreen", PrimitiveType.Cube, root.transform,
                new Vector3(5.82f, 1.16f, -5.22f), new Vector3(0f, 52f, -24f), new Vector3(0.38f, 0.03f, 0.58f), lecternScreen, false);

            var target = pylon.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "facility_nasa_hdu",
                title: "NASA Habitat Demonstration Unit (HDU)",
                category: "Official NASA 3D Architecture",
                subtitle: "Deep Space Habitat · Pressurized Crew Living & Science Lab",
                coords: "Jezero Base NW · 18.4449°N, 77.4503°E",
                geology: "Seated on compacted Jezero HNhu unit pad (bearing capacity >250 kPa).",
                mineralogy: "Official NASA-3D-Resources HDU model integrated with ORBITX survival suite.",
                environment: "Pressurized 56 kPa / 34% O2 exploration atmosphere · DSN Ka-band telemetry relay.",
                summary: "Official NASA Habitat Demonstration Unit 3D asset configured as crew command quarters, EVA suitport airlock, and geological sample preparation lab.",
                sourceUrl: "https://github.com/nasa/NASA-3D-Resources",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.EnterFacility,
                actionHint: "Inspect NASA HDU Specs",
                interactionRadius: 10.0f
            );
        }

        // -----------------------------------------------------------------
        // 4. MARS EDL LIFTING-BODY AIRCRAFT / AEROSHELL DESCENT MODULE
        // -----------------------------------------------------------------
        private void BuildEdlLiftingBodyLander()
        {
            GameObject existing = GameObject.Find("MS_EDL_Landing_Module");
            if (existing != null) Destroy(existing);

            float y = MarsVisualRealismPolish.SampleTerrainElevation(POS_EDL_AIRCRAFT.x, POS_EDL_AIRCRAFT.y);
            GameObject root = new GameObject("MS_EDL_Landing_Module");
            root.transform.SetParent(facilitiesRoot, false);
            root.transform.position = new Vector3(POS_EDL_AIRCRAFT.x, y, POS_EDL_AIRCRAFT.y);
            quickTargets["EDL"] = root.transform;

            Material thermalTile = CreatePbrMat("EDL_ThermalShield", new Color(0.18f, 0.16f, 0.15f), 0.25f, 0.22f);
            Material aeroShellWhite = CreatePbrMat("EDL_ShellWhite", new Color(0.89f, 0.90f, 0.92f), 0.45f, 0.58f);
            Material goldFoil = CreatePbrMat("EDL_MLI_Gold", new Color(0.86f, 0.65f, 0.16f), 0.85f, 0.72f);
            Material thrusterAlloy = CreatePbrMat("EDL_Thruster", new Color(0.32f, 0.34f, 0.38f), 0.85f, 0.65f);
            Material aeroOrange = CreatePbrMat("EDL_NASA_Orange", new Color(0.88f, 0.34f, 0.12f), 0.35f, 0.50f);
            Material scorchMat = CreatePbrMat("EDL_BlastScorch", new Color(0.24f, 0.15f, 0.12f), 0.0f, 0.10f);
            Material beaconGlow = CreateEmissiveMat("EDL_BeaconGlow", new Color(1.0f, 0.55f, 0.15f), new Color(0.85f, 0.45f, 0.12f));

            // Blast scorch footprint on regolith
            CreatePart("EDL_ScorchFootprint", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(9.5f, 0.04f, 9.5f), scorchMat, false);

            // 4 Deployed shock-absorbing landing legs & wide footpads
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 foot = new Vector3(Mathf.Cos(ang) * 3.1f, 0.08f, Mathf.Sin(ang) * 3.1f);
                Vector3 hip = new Vector3(Mathf.Cos(ang) * 1.9f, 1.45f, Mathf.Sin(ang) * 1.9f);
                CreatePart($"EDL_Pad_{i}", PrimitiveType.Cylinder, root.transform, foot, Vector3.zero, new Vector3(0.88f, 0.08f, 0.88f), thrusterAlloy, true);

                Vector3 mid = (foot + hip) * 0.5f;
                Vector3 dir = (hip - foot).normalized;
                float len = Vector3.Distance(foot, hip);
                Quaternion legRot = Quaternion.FromToRotation(Vector3.up, dir);
                CreatePart($"EDL_Strut_{i}", PrimitiveType.Cylinder, root.transform, mid, legRot.eulerAngles, new Vector3(0.14f, len * 0.5f, 0.14f), thrusterAlloy, false);
            }

            // Sculpted Procedural Lifting-Body Aeroshell & PICA-X Heatshield Belly (single unified BoxCollider avoids PhysX QuickHull errors)
            GameObject bellyShield = new GameObject("EDL_HeatShieldBelly");
            bellyShield.transform.SetParent(root.transform, false);
            bellyShield.transform.localPosition = new Vector3(0f, 1.24f, 0f);
            bellyShield.AddComponent<MeshFilter>().sharedMesh = BuildLiftingBodyMesh(3.4f, 0.54f, 6.2f, true);
            bellyShield.AddComponent<MeshRenderer>().sharedMaterial = thermalTile;

            GameObject dorsalShell = new GameObject("EDL_LiftingHull");
            dorsalShell.transform.SetParent(root.transform, false);
            dorsalShell.transform.localPosition = new Vector3(0f, 1.50f, 0f);
            dorsalShell.AddComponent<MeshFilter>().sharedMesh = BuildLiftingBodyMesh(3.35f, 1.38f, 6.0f, false);
            dorsalShell.AddComponent<MeshRenderer>().sharedMaterial = aeroShellWhite;

            BoxCollider edlCol = root.AddComponent<BoxCollider>();
            edlCol.center = new Vector3(0f, 1.65f, 0f);
            edlCol.size = new Vector3(4.6f, 2.3f, 6.2f);

            // Sculpted Swept Delta Winglets + Twin Canted Aero Tail Fins + Flush Dorsal Payload Spine & Canopy
            Material visorGlass = CreateGlassMat("EDL_CanopyGlass", new Color(0.18f, 0.35f, 0.52f, 0.72f));
            Mesh deltaWingMesh = BuildSweptFinMesh(2.05f, 0.75f, 1.35f, 0.11f, -0.85f);
            Mesh tailFinMesh = BuildSweptFinMesh(1.35f, 0.52f, 1.05f, 0.09f, -0.62f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wing = new GameObject($"EDL_DeltaWing_{(side < 0 ? "L" : "R")}");
                wing.transform.SetParent(root.transform, false);
                wing.transform.localPosition = new Vector3(side * 1.42f, 1.48f, -0.45f);
                wing.transform.localRotation = Quaternion.Euler(0f, 0f, side < 0 ? 84f : -84f);
                wing.transform.localScale = Vector3.one;
                wing.AddComponent<MeshFilter>().sharedMesh = deltaWingMesh;
                wing.AddComponent<MeshRenderer>().sharedMaterial = aeroOrange;

                GameObject fin = new GameObject($"EDL_VFin_{(side < 0 ? "L" : "R")}");
                fin.transform.SetParent(root.transform, false);
                fin.transform.localPosition = new Vector3(side * 0.85f, 1.88f, -1.55f);
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, side < 0 ? 22f : -22f);
                fin.AddComponent<MeshFilter>().sharedMesh = tailFinMesh;
                fin.AddComponent<MeshRenderer>().sharedMaterial = aeroOrange;
            }

            CreatePart("EDL_SensorCanopy", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.02f, 1.08f), new Vector3(14f, 0f, 0f), new Vector3(0.88f, 0.24f, 1.05f), visorGlass, false);

            // Flush Recessed Gold MLI Payload Bay + 8 Supersonic Retro-Thruster Pods
            CreatePart("EDL_GoldPayloadBay", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.06f, -0.42f), Vector3.zero, new Vector3(1.32f, 0.34f, 1.85f), goldFoil, false);
            for (int e = 0; e < 4; e++)
            {
                float ex = (e % 2 == 0) ? -1.42f : 1.42f;
                float ez = (e < 2) ? 1.15f : -1.15f;
                CreatePart($"EDL_EnginePod_{e}A", PrimitiveType.Cylinder, root.transform, new Vector3(ex, 1.32f, ez - 0.18f), new Vector3(0f, 0f, ex < 0 ? 14f : -14f), new Vector3(0.28f, 0.42f, 0.28f), thrusterAlloy, false);
                CreatePart($"EDL_EnginePod_{e}B", PrimitiveType.Cylinder, root.transform, new Vector3(ex, 1.32f, ez + 0.18f), new Vector3(0f, 0f, ex < 0 ? 14f : -14f), new Vector3(0.28f, 0.42f, 0.28f), thrusterAlloy, false);
            }

            Material edlNasaMat = CreateNasaBadgeMaterial();
            if (edlNasaMat != null)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    b.name = $"EDL_NASABadge_{(s < 0 ? "L" : "R")}";
                    b.transform.SetParent(root.transform, false);
                    Destroy(b.GetComponent<Collider>());
                    b.transform.localPosition = new Vector3(s * 1.42f, 1.68f, 0.72f);
                    b.transform.localRotation = Quaternion.Euler(-22f, s < 0 ? 90f : -90f, 0f);
                    b.transform.localScale = Vector3.one * 0.48f;
                    b.GetComponent<Renderer>().sharedMaterial = edlNasaMat;
                }
            }

            CreatePart("EDL_StrobeBeacon", PrimitiveType.Sphere, root.transform, new Vector3(0f, 2.28f, -1.85f), Vector3.zero, Vector3.one * 0.22f, beaconGlow, false);

            var target = root.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "vehicle_edl_aircraft_lander",
                title: "Mars EDL Lifting-Body Entry Aircraft & Descent Stage",
                category: "Atmospheric Entry, Descent & Landing (EDL)",
                subtitle: "Hypersonic Lifting Aeroshell (L/D 0.24) + Supersonic Retropropulsion",
                coords: "Jezero Landing Ellipse East · 18.4445°N, 77.4514°E",
                geology: "Supersonic retropropulsion plume scoured top 4 cm of loose ferric dust exposing HNhu bedrock.",
                mineralogy: "PICA-X carbon phenolic ablative heatshield rated for peak 220 W/cm^2 atmospheric deceleration.",
                environment: "Entry Speed: Mach 24.5 (5.5 km/s) -> Thin CO2 Scale Height 11.1 km -> Touchdown <0.75 m/s",
                summary: "Mars Entry, Descent & Landing lifting-body aircraft capable of guided cross-range aerodynamic steering through Mars's 715 Pa CO2 atmosphere followed by powered retro-thruster touchdown.",
                sourceUrl: "https://science.nasa.gov/mission/mars-2020-perseverance/entry-descent-and-landing/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.InspectOnly,
                actionHint: "Inspect EDL Flight & Aero Data",
                interactionRadius: 9.5f
            );
        }

        // -----------------------------------------------------------------
        // 5. MARS ASCENT ROCKET & MOBILE LAUNCHER PAD
        // -----------------------------------------------------------------
        private void BuildMarsAscentRocketPad()
        {
            GameObject existing = GameObject.Find("MS_Mars_Ascent_Rocket_Pad");
            if (existing != null) Destroy(existing);

            float y = MarsVisualRealismPolish.SampleTerrainElevation(POS_ROCKET_PAD.x, POS_ROCKET_PAD.y);
            GameObject root = new GameObject("MS_Mars_Ascent_Rocket_Pad");
            root.transform.SetParent(facilitiesRoot, false);
            root.transform.position = new Vector3(POS_ROCKET_PAD.x, y, POS_ROCKET_PAD.y);
            quickTargets["Rocket"] = root.transform;

            Material concreteMat = CreatePbrMat("Pad_Concrete", new Color(0.36f, 0.37f, 0.40f), 0.15f, 0.30f);
            Material hazardYellow = CreatePbrMat("Pad_HazardYellow", new Color(0.92f, 0.72f, 0.14f), 0.3f, 0.45f);
            Material steelMat = CreatePbrMat("Pad_Steel", new Color(0.55f, 0.58f, 0.62f), 0.80f, 0.55f);
            Material whiteHull = CreatePbrMat("MAV_Hull", new Color(0.93f, 0.94f, 0.96f), 0.35f, 0.65f);
            Material darkNozzle = CreatePbrMat("MAV_Nozzle", new Color(0.18f, 0.19f, 0.22f), 0.88f, 0.50f);
            Material cryoBlue = CreatePbrMat("Pad_CryoLOX", new Color(0.22f, 0.58f, 0.88f), 0.50f, 0.68f);
            Material cryoAmber = CreatePbrMat("Pad_CryoCH4", new Color(0.86f, 0.44f, 0.16f), 0.50f, 0.68f);

            // Launch Pad Apron + Blast Trench Deflector
            CreatePart("Pad_Apron", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(13.5f, 0.30f, 13.5f), concreteMat, true);
            CreatePart("Pad_FlameTable", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(5.5f, 0.85f, 5.5f), steelMat, true);
            CreatePart("Pad_CautionStrip", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.31f, -6.5f), Vector3.zero, new Vector3(13.5f, 0.04f, 0.45f), hazardYellow, false);

            // Seat imported official NASA Launcher & Rocket GLBs if present in scene
            bool hasRocketGlb = false;
            GameObject launcherGlb = GameObject.Find("MS_NASA_MobileLauncher");
            if (launcherGlb != null)
            {
                launcherGlb.transform.SetParent(root.transform, true);
                NormalizeAndSeatObject(launcherGlb, new Vector3(-2.6f, 0.30f, 0f), 14.5f);
                EnsureBoxCollider(launcherGlb);
            }

            GameObject saturnGlb = GameObject.Find("MS_NASA_Rocket_SaturnV");
            if (saturnGlb != null)
            {
                hasRocketGlb = true;
                saturnGlb.transform.SetParent(root.transform, true);
                NormalizeAndSeatObject(saturnGlb, new Vector3(0f, 1.08f, 0f), 16.5f);
                EnsureBoxCollider(saturnGlb);
            }

            GameObject aresGlb = GameObject.Find("MS_NASA_Rocket_Ares1");
            if (aresGlb != null)
            {
                hasRocketGlb = true;
                CreatePart("Ares_FlamePedestal", PrimitiveType.Cube, root.transform, new Vector3(3.6f, 0.65f, 0f), Vector3.zero, new Vector3(2.2f, 0.85f, 2.2f), steelMat, true);
                aresGlb.transform.SetParent(root.transform, true);
                NormalizeAndSeatObject(aresGlb, new Vector3(3.6f, 1.08f, 0f), 12.0f);
                EnsureBoxCollider(aresGlb);
            }

            if (!hasRocketGlb)
            {
                // Clean multi-stage Mars Ascent Vehicle (MAV) + Strongback Umbilical Tower
                CreatePart("MAV_EngineBell", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.55f, 0f), Vector3.zero, new Vector3(1.1f, 0.55f, 1.1f), darkNozzle, true);
                CreatePart("MAV_Stage1", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 5.2f, 0f), Vector3.zero, new Vector3(1.8f, 3.3f, 1.8f), whiteHull, true);
                CreatePart("MAV_Interstage", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 8.9f, 0f), Vector3.zero, new Vector3(1.75f, 0.55f, 1.75f), darkNozzle, true);
                CreatePart("MAV_Stage2", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 11.2f, 0f), Vector3.zero, new Vector3(1.55f, 1.9f, 1.55f), whiteHull, true);
                CreatePart("MAV_Fairing", PrimitiveType.Capsule, root.transform, new Vector3(0f, 14.0f, 0f), Vector3.zero, new Vector3(1.55f, 1.4f, 1.55f), whiteHull, true);
                CreatePart("Umbilical_Tower", PrimitiveType.Cube, root.transform, new Vector3(-2.6f, 7.6f, 0f), Vector3.zero, new Vector3(1.4f, 14.5f, 1.4f), steelMat, true);
                CreatePart("Umbilical_Arm", PrimitiveType.Cube, root.transform, new Vector3(-1.35f, 11.8f, 0f), Vector3.zero, new Vector3(1.9f, 0.25f, 0.35f), hazardYellow, false);
            }

            // Cryogenic ISRU LOX + LCH4 Propellant Spheres on Steel Support Cradles + Propellant Manifolds
            CreatePart("Cryo_LOX_Sphere", PrimitiveType.Sphere, root.transform, new Vector3(5.2f, 1.85f, -3.5f), Vector3.zero, Vector3.one * 2.2f, cryoBlue, true);
            CreatePart("Cryo_CH4_Sphere", PrimitiveType.Sphere, root.transform, new Vector3(5.2f, 1.85f, 3.5f), Vector3.zero, Vector3.one * 2.2f, cryoAmber, true);
            for (int tSide = -1; tSide <= 1; tSide += 2)
            {
                float tz = tSide * 3.5f;
                for (int leg = 0; leg < 4; leg++)
                {
                    float la = (45f + leg * 90f) * Mathf.Deg2Rad;
                    CreatePart($"CryoLeg_{tSide}_{leg}", PrimitiveType.Cylinder, root.transform,
                        new Vector3(5.2f + Mathf.Cos(la) * 0.85f, 0.85f, tz + Mathf.Sin(la) * 0.85f), Vector3.zero, new Vector3(0.14f, 0.72f, 0.14f), steelMat, false);
                }
                // Insulated cryo feed line from storage sphere to flame launch table
                CreatePart($"CryoPipe_{tSide}", PrimitiveType.Cylinder, root.transform,
                    new Vector3(3.4f, 0.48f, tz * 0.65f), new Vector3(0f, tSide * 26f, 90f), new Vector3(0.18f, 2.2f, 0.18f), steelMat, false);
            }
            // Perimeter Blast Safety Bollards & Launch Control Signage Plaque
            Vector2[] bollardOffsets = { new Vector2(-6.4f, -6.4f), new Vector2(6.4f, -6.4f), new Vector2(-6.4f, 6.4f), new Vector2(6.4f, 6.4f) };
            for (int bIdx = 0; bIdx < bollardOffsets.Length; bIdx++)
            {
                CreatePart($"Pad_Bollard_{bIdx}", PrimitiveType.Cylinder, root.transform,
                    new Vector3(bollardOffsets[bIdx].x, 0.72f, bollardOffsets[bIdx].y), Vector3.zero, new Vector3(0.22f, 0.42f, 0.22f), hazardYellow, true);
            }
            CreateSignagePlaque(root.transform, new Vector3(-5.2f, 1.45f, -6.2f), new Vector3(0f, 45f, 0f),
                "NASA · ORBITX LAUNCH PAD\nISRU LOX / CH4 MAV COMPLEX", hazardYellow);

            GameObject beacon = CreatePart("Pad_BeaconLight", PrimitiveType.Sphere, root.transform, new Vector3(-2.6f, 15.6f, 0f), Vector3.zero, Vector3.one * 0.4f, hazardYellow, false);
            Light padLight = root.AddComponent<Light>();
            padLight.type = LightType.Point;
            padLight.color = new Color(1.0f, 0.85f, 0.58f);
            padLight.intensity = 18f;
            padLight.range = 24f;

            var target = root.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "facility_mars_ascent_rocket_pad",
                title: "Mars Ascent & Transit Launch Complex (ISRU Propellant Pad)",
                category: "Official NASA Launch Hardware · Orbital Return",
                subtitle: "Low-Mars Orbit Rendezvous (Delta-v ~4.1 km/s) · LOX/CH4 ISRU",
                coords: "Jezero Launch Sector NE · 18.4451°N, 77.4515°E",
                geology: "Reinforced launch apron built over flat basaltic plain 36m NE of Octavia E. Butler Landing.",
                mineralogy: "Sabatier & MOXIE ISRU synthesis: atmospheric CO2 + subsurface water ice -> liquid oxygen & methane rocket fuel.",
                environment: "Surface Gravity: 3.72 m/s^2 (0.38g) | Surface Pressure: 715 Pa (minimal aerodynamic drag loss on ascent).",
                summary: "Mars Ascent Vehicle & Mobile Launcher launch complex powered by NASA/JPL official launch vehicle models, cryogenic ISRU LOX/LCH4 propellant farm, and blast deflector pad.",
                sourceUrl: "https://science.nasa.gov/mission/mars-sample-return/",
                imageFile: "perseverance_sample_raw.jpg",
                actionType: MarsInspectActionType.InspectOnly,
                actionHint: "Inspect Ascent Delta-V & Propellant Specs",
                interactionRadius: 13.0f
            );
        }

        // -----------------------------------------------------------------
        // 6. 12 NAMED JEZERO GEOLOGICAL OUTCROPS & ROCK FORMATIONS
        // -----------------------------------------------------------------
        private void CreateGeologicalOutcrop(JezeroOutcropData data)
        {
            if (data == null) return;
            float x = data.unityPosXZ.x;
            float z = data.unityPosXZ.y;
            float y = MarsVisualRealismPolish.SampleTerrainElevation(x, z);

            GameObject rockRoot = new GameObject($"Outcrop_{data.id}");
            rockRoot.transform.SetParent(outcropsRoot, false);
            rockRoot.transform.position = new Vector3(x, y, z);

            Vector3 sc = data.scale.magnitude > 0.1f ? data.scale : new Vector3(2.2f, 1.0f, 1.8f);
            Color baseCol = GetOutcropStyleColor(data.style);
            Material primaryMat = CreatePbrMat("Rock_" + data.id, baseCol, 0.0f, 0.18f);
            Material veinMat = CreatePbrMat("Vein_" + data.id, new Color(0.88f, 0.87f, 0.82f), 0.0f, 0.30f);
            Material darkSpotMat = CreatePbrMat("Spot_" + data.id, new Color(0.14f, 0.12f, 0.11f), 0.0f, 0.25f);

            // Sculpted Stratified Geological Outcrop Bedrock Mesh + Stratified Banded Texture (deterministic layered Perlin ridges)
            int seed = Mathf.Abs(data.id.GetHashCode());
            GameObject rockMeshGo = new GameObject("RockMass_Sculpted");
            rockMeshGo.transform.SetParent(rockRoot.transform, false);
            rockMeshGo.AddComponent<MeshFilter>().sharedMesh = BuildStratifiedRockMesh(sc, seed, data.style);
            Material rockMat = CreatePbrMat("RockSculpt_" + data.id, Color.white, 0.0f, 0.18f);
            Texture2D rockTex = CreateStratifiedRockTexture(baseCol, seed, data.style);
            if (rockMat.HasProperty("_BaseMap")) rockMat.SetTexture("_BaseMap", rockTex);
            if (rockMat.HasProperty("_MainTex")) rockMat.SetTexture("_MainTex", rockTex);
            rockMeshGo.AddComponent<MeshRenderer>().sharedMaterial = rockMat;

            // Diagnostic mineral signatures (Cheyava sulfate veins + dark redox spots, or crystal veins)
            if (data.style == "cheyava" || data.style == "carbonate")
            {
                CreatePart("MineralVein_A", PrimitiveType.Cube, rockRoot.transform,
                    new Vector3(0f, sc.y * 0.72f, 0f), new Vector3(8f, 35f, 0f), new Vector3(sc.x * 0.62f, 0.05f, 0.07f), veinMat, false);
                CreatePart("MineralVein_B", PrimitiveType.Cube, rockRoot.transform,
                    new Vector3(0.12f, sc.y * 0.75f, -0.08f), new Vector3(-6f, 115f, 0f), new Vector3(sc.x * 0.56f, 0.05f, 0.06f), veinMat, false);

                if (data.style == "cheyava")
                {
                    for (int sp = 0; sp < 4; sp++)
                    {
                        float sx = -0.26f + sp * 0.17f;
                        CreatePart($"RedoxSpot_{sp}", PrimitiveType.Cylinder, rockRoot.transform,
                            new Vector3(sx, sc.y * 0.78f, (sp % 2 == 0) ? 0.11f : -0.10f), Vector3.zero, new Vector3(0.10f, 0.04f, 0.10f), darkSpotMat, false);
                    }
                }
            }

            // Main unified SphereCollider for clean raycast clicking & proximity prompt
            SphereCollider scCol = rockRoot.AddComponent<SphereCollider>();
            scCol.center = new Vector3(0f, sc.y * 0.5f, 0f);
            scCol.radius = Mathf.Max(sc.x, sc.z) * 0.75f;

            var target = rockRoot.AddComponent<MarsInspectableTarget>();
            target.Configure(
                id: "outcrop_" + data.id,
                title: data.name,
                category: $"Jezero Outcrop · Sol {data.sol} ({data.date})",
                subtitle: $"{data.formation} · {data.rock_type}",
                coords: $"{data.lat:F5}°N, {data.lon:F5}°E · Elev {data.elev_m:F1} m MOLA · USGS Unit {data.usgs_unit}",
                geology: $"Formation: {data.formation}\nThermal Inertia: {data.thermal_inertia_tiu:F0} TIU (J m^-2 K^-1 s^-1/2)",
                mineralogy: $"CRISM/SHERLOC: {data.crism_mineralogy}\nPIXL Chemistry: {data.geochemistry_wt_pct}",
                environment: $"Source: {data.source}",
                summary: data.discovery_summary,
                sourceUrl: data.source_url,
                imageFile: data.image_file,
                actionType: MarsInspectActionType.InspectOnly,
                actionHint: $"Inspect {data.name}",
                interactionRadius: 7.5f
            );
        }

        private static Color GetOutcropStyleColor(string style)
        {
            switch (style)
            {
                case "cheyava": return new Color(0.62f, 0.30f, 0.22f);
                case "sedimentary": return new Color(0.64f, 0.44f, 0.30f);
                case "delta": return new Color(0.58f, 0.42f, 0.29f);
                case "carbonate": return new Color(0.72f, 0.60f, 0.44f);
                case "olivine": return new Color(0.36f, 0.38f, 0.25f);
                case "serpentinite": return new Color(0.28f, 0.33f, 0.27f);
                case "metamorphic": return new Color(0.40f, 0.35f, 0.31f);
                default: return new Color(0.38f, 0.29f, 0.25f);
            }
        }

        // -----------------------------------------------------------------
        // 7. EXISTING NASA FLEET TARGET REGISTRATION
        // -----------------------------------------------------------------
        private void AttachFleetInspectables()
        {
            RegisterFleetTarget("MS_Perseverance_Root", "fleet_perseverance",
                "NASA Perseverance Rover (Mars 2020)", "Active Planetary Rover · Jezero Crater",
                "MOXIE · PIXL · SHERLOC · Mastcam-Z · SuperCam · RIMFAX",
                "18.4447°N, 77.4508°E · Jezero Crater Delta & Margin",
                "USGS HNhu / HNu Basalt & Deltaic Lacustrine Units",
                "Coring 43 titanium sample tubes; detected olivine cumulates, hydrated sulfates, and organic compounds at Wildcat Ridge & Cheyava Falls.",
                "Surface Ops: Sols 0–1600+ · MMRTG 110 W Electric · Dust tau ~0.45",
                "Perseverance investigates Jezero Crater's ancient lake-delta system for astrobiology, caches core samples, and proved O2 production from Martian CO2 via MOXIE.",
                "https://mars.nasa.gov/mars2020/", "wildcat_ridge_sol509.jpg", new Vector3(2.8f, 2.2f, 3.2f));

            RegisterFleetTarget("MS_Ingenuity_Root", "fleet_ingenuity",
                "NASA Ingenuity Mars Helicopter (Ginny)", "First Powered Aircraft on Another Planet",
                "72 Flights · Dual Counter-Rotating 1.2m Carbon Blades (2537 rpm)",
                "Wright Brothers Field -> Neretva Vallis · Jezero Crater",
                "Reconnaissance aerial scout mapping delta scarps & treacherous dune fields.",
                "Atmospheric flight in ~1% Earth density CO2 atmosphere (~715 Pa).",
                "Reynolds number ~10^4 · Solar Li-Ion battery · Survived 72 sorties over 17.0 km.",
                "Originally slated for 5 technology-demonstration flights, Ingenuity operated for nearly 3 years as Perseverance's aerial scout.",
                "https://science.nasa.gov/mission/mars-2020-perseverance/ingenuity-mars-helicopter/", "perseverance_sample_raw.jpg", new Vector3(1.8f, 1.2f, 1.8f));

            RegisterFleetTarget("MS_Curiosity_Root", "fleet_curiosity",
                "NASA Curiosity Rover (MSL)", "Nuclear Mars Science Laboratory · Gale Crater",
                "ChemCam · SAM · CheMin · RAD · MAHLI · REMS Weather",
                "4.5895°S, 137.4417°E · Mount Sharp (Aeolis Mons), Gale Crater",
                "Sheepbed Mudstone -> Murray Fm -> Vera Rubin Ridge -> Gediz Vallis Sulfur",
                "Discovered habitable neutral-pH ancient lakes, clay phyllosilicates, methane plumes, and pure elemental sulfur crystals (2024).",
                "RAD Surface GCR Dose: 0.67 mSv/day · Elevation Climb: +800 m up Mount Sharp",
                "Curiosity's RAD instrument provided the authoritative Martian surface radiation benchmark used directly in ORBITX crew triage planning.",
                "https://science.nasa.gov/resource/curiositys-proposed-path-up-mount-sharp/", "perseverance_sample_raw.jpg", new Vector3(2.8f, 2.2f, 3.2f));

            RegisterFleetTarget("MS_Opportunity_Root", "fleet_opportunity",
                "NASA Opportunity Rover (MER-B)", "Marathon Mars Explorer (2004–2018)",
                "Pancam · Mini-TES · Mössbauer · APXS · Rock Abrasion Tool",
                "1.9462°S, 354.4734°E · Meridiani Planum & Endeavour Crater",
                "Burns Formation Sulfate Sandstones & Hematite 'Blueberries'",
                "Discovered acidic ancient groundwater concretions and smectite clays along Endeavour rim.",
                "45.16 km traversed over 5,111 sols before planet-encircling 2018 dust storm.",
                "Proved sustained surface water chemistry on early Mars across 14+ Earth years of solar-powered operations.",
                "https://science.nasa.gov/mission/mer-opportunity/", "perseverance_sample_raw.jpg", new Vector3(2.0f, 1.6f, 2.0f));

            RegisterFleetTarget("MS_InSight_Lander_Root", "fleet_insight",
                "NASA InSight Geophysical Lander", "Seismic & Atmospheric Station · Elysium Planitia",
                "SEIS Seismometer · HP3 Heat Probe · TWINS Meteorology",
                "4.5024°N, 135.6234°E · Elysium Planitia (2655m elevation -MOLA)",
                "Volcanic regolith & regolith crust/mantle/liquid core seismic tomography.",
                "Measured Mars crustal thickness (39-72 km) and 1,300+ marsquakes.",
                "Live Sol 675-681 Pressure: ~748 Pa · Mean Temp: -62.5°C · WNW 7.5 m/s Winds",
                "InSight's REMS/TWINS pressure, wind, and temperature telemetry feeds ORBITX's real-time meteorological telemetry display.",
                "https://science.nasa.gov/mission/insight/", "perseverance_sample_raw.jpg", new Vector3(3.2f, 1.8f, 3.2f));

            RegisterFleetTarget("MS_Viking_Lander_Root", "fleet_viking",
                "NASA Viking Lander 1 (Historic 1976 Baseline)", "First Sustained US Surface Mission · Chryse Planitia",
                "GC-MS · XRF Spectrometer · Meteorology · Stereo Cameras",
                "22.48°N, 312.05°E · Chryse Planitia",
                "Ferrihydrite-rich basaltic soil & reactive surface oxidants.",
                "SiO2 ~44%, Fe2O3 ~18%, SO3 ~7%, Cl ~0.7% (first in-situ Mars geochemistry).",
                "Baseline barometric tide & wind dataset (1976–1982).",
                "Established foundational Martian surface soil composition, sky brightness, and pressure cycling still compared with Perseverance MEDA today.",
                "https://science.nasa.gov/mission/viking-1/", "perseverance_sample_raw.jpg", new Vector3(2.8f, 1.8f, 2.8f));

            RegisterFleetTarget("MS_MRO_Root", "fleet_mro",
                "NASA Mars Reconnaissance Orbiter (MRO)", "High-Resolution Orbital Mapper (2006–Present)",
                "HiRISE (0.25 m/px) · CTX (6 m/px) · CRISM Spectrometer · SHARAD Radar",
                "Sun-Synchronous Orbit (255 x 320 km)",
                "Provides JEZ_hirise_soc_006_DTM_1m.tif & ESP_045994_1985_MRGB terrain maps used in this project!",
                "CRISM infrared spectra mapped Jezero's delta clays, carbonates & olivine unit.",
                "SHARAD radar probes subsurface water-ice sheets for ISRU.",
                "MRO's HiRISE & CTX elevation models form the literal 3D terrain mesh under your boots.",
                "https://science.nasa.gov/mission/mro/", "perseverance_sample_raw.jpg", new Vector3(6.0f, 5.0f, 6.0f));

            RegisterFleetTarget("MS_Odyssey_Root", "fleet_odyssey",
                "NASA 2001 Mars Odyssey", "Thermal & Subsurface Water-Ice Orbiter",
                "THEMIS Thermal IR · GRS Gamma Ray Spectrometer · MARIE Radiation",
                "~400 km Sun-Synchronous Orbit",
                "Maps global elemental H2O ice distribution & nighttime thermal inertia (TIU).",
                "THEMIS 100m thermal inertia reveals consolidated bedrock vs loose sand traps.",
                "Longest continuously operating spacecraft around another planet.",
                "Odyssey's GRS hydrogen & THEMIS thermal inertia data guide rover trafficability and ISRU ice prospecting.",
                "https://science.nasa.gov/mission/odyssey/", "perseverance_sample_raw.jpg", new Vector3(6.0f, 5.0f, 6.0f));

            RegisterFleetTarget("MS_MAVEN_Root", "fleet_maven",
                "NASA MAVEN Orbiter", "Upper Atmosphere & Solar Wind Loss Mission",
                "SEP Solar Energetic Particles · MAG Magnetometer · NGIMS Ion Mass Spec",
                "Elliptical Science Orbit (150 x 4500 km)",
                "Measures solar wind sputtering that stripped Mars's early dense wet atmosphere.",
                "Real-time space weather & SEP proton warning correlated with NASA DONKI.",
                "Essential communications relay + solar storm radiation early-warning node.",
                "MAVEN monitors solar energetic particle storms to warn Jezero ground crews when to shelter in the Medical & Radiation Bay.",
                "https://science.nasa.gov/mission/maven/", "perseverance_sample_raw.jpg", new Vector3(6.0f, 5.0f, 6.0f));

            RegisterFleetTarget("MS_MGS_Root", "fleet_mgs",
                "NASA Mars Global Surveyor (MGS)", "MOLA Global Laser Altimeter Baseline",
                "MOLA Laser Altimeter · TES Thermal Emission · MOC Camera",
                "Historic 378 km Polar Mapping Orbit (1997–2006)",
                "Built Mars's reference MOLA geoid/ellipsoid elevation system (IAU2000 R = 3396.19 km).",
                "TES mapped global basalt, hematite, and surface dust thermal inertia.",
                "Global topographic baseline integrated into NASA Mars Trek WMTS layers.",
                "All Jezero elevation values (-2569.9 m MOLA) trace directly back to MGS MOLA global laser altimetry.",
                "https://science.nasa.gov/mission/mars-global-surveyor/", "perseverance_sample_raw.jpg", new Vector3(6.0f, 5.0f, 6.0f));
        }

        private void RegisterFleetTarget(
            string goName, string id, string title, string category, string subtitle,
            string coords, string geology, string mineralogy, string env, string summary, string url, string img, Vector3 fallbackColliderSize)
        {
            GameObject go = GameObject.Find(goName);
            if (go == null) return;

            Collider col = go.GetComponentInChildren<Collider>();
            if (col == null)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, fallbackColliderSize.y * 0.5f, 0f);
                bc.size = fallbackColliderSize;
            }

            var target = go.GetComponent<MarsInspectableTarget>() ?? go.AddComponent<MarsInspectableTarget>();
            target.Configure(id, title, category, subtitle, coords, geology, mineralogy, env, summary, url, img,
                MarsInspectActionType.InspectOnly, "Inspect NASA Spacecraft", 10.0f);
        }

        // -----------------------------------------------------------------
        // HELPERS
        // -----------------------------------------------------------------
        private static void EnsureBoxCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(4f, 4f, 4f);
                bc.center = new Vector3(0f, 2f, 0f);
                return;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 lossy = go.transform.lossyScale;
            box.size = new Vector3(
                b.size.x / Mathf.Max(0.01f, Mathf.Abs(lossy.x)),
                b.size.y / Mathf.Max(0.01f, Mathf.Abs(lossy.y)),
                b.size.z / Mathf.Max(0.01f, Mathf.Abs(lossy.z)));
        }

        private static void CreateHduFloodlight(Transform parent, string name, Vector3 localPos, float intensity, float range)
        {
            GameObject lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPos;
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.94f, 0.86f);
            l.intensity = intensity;
            l.range = range;
        }

        private static void LiftPitchBlackGlbMaterials(GameObject rootGo)
        {
            if (rootGo == null) return;
            foreach (Renderer r in rootGo.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string colProp = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null);
                    if (colProp != null)
                    {
                        Color c = m.GetColor(colProp);
                        float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        if (lum < 0.22f)
                        {
                            m.SetColor(colProp, new Color(
                                Mathf.Max(c.r, 0.34f),
                                Mathf.Max(c.g, 0.36f),
                                Mathf.Max(c.b, 0.40f),
                                c.a));
                        }
                    }
                    if (m.HasProperty("_Metallic"))
                    {
                        m.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), 0.38f));
                    }
                    if (m.HasProperty("metallicFactor"))
                    {
                        m.SetFloat("metallicFactor", Mathf.Min(m.GetFloat("metallicFactor"), 0.38f));
                    }
                    if (m.HasProperty("baseColorFactor"))
                    {
                        Color c2 = m.GetColor("baseColorFactor");
                        float lum2 = 0.2126f * c2.r + 0.7152f * c2.g + 0.0722f * c2.b;
                        if (lum2 < 0.22f)
                        {
                            m.SetColor("baseColorFactor", new Color(
                                Mathf.Max(c2.r, 0.34f),
                                Mathf.Max(c2.g, 0.36f),
                                Mathf.Max(c2.b, 0.40f),
                                c2.a));
                        }
                    }
                }
            }
        }

        public static void NormalizeAndSeatObject(GameObject go, Vector3 localTargetPos, float desiredHeight)
        {
            if (go == null) return;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                go.transform.localPosition = localTargetPos;
                return;
            }

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = Mathf.Max(0.05f, b.size.y);
            float factor = desiredHeight / h;
            go.transform.localScale = go.transform.localScale * factor;

            // Recompute bounds after scale to seat min.y onto localTargetPos.y cleanly
            Bounds scaledB = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) scaledB.Encapsulate(rends[i].bounds);

            Vector3 worldDesired = go.transform.parent != null
                ? go.transform.parent.TransformPoint(localTargetPos)
                : localTargetPos;
            Vector3 offset = new Vector3(
                worldDesired.x - scaledB.center.x,
                worldDesired.y - scaledB.min.y,
                worldDesired.z - scaledB.center.z);
            go.transform.position += offset;
        }

        private static GameObject CreatePart(
            string name, PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale,
            Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            else if (type == PrimitiveType.Cylinder)
            {
                // Replace Unity's rounded CapsuleCollider on cylinders with flat Box/convex MeshCollider
                Collider cap = go.GetComponent<Collider>();
                if (cap != null) Destroy(cap);
                if (localScale.y < 0.35f)
                {
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    box.center = Vector3.zero;
                    box.size = new Vector3(1f, 2f, 1f);
                }
                else
                {
                    MeshCollider mc = go.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
            }

            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static void CreateSignagePlaque(Transform parent, Vector3 localPos, Vector3 localEuler, string label, Material accent)
        {
            GameObject holder = new GameObject("NASA_ORBITX_Plaque");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(localEuler);

            Material plaqueBg = CreatePbrMat("Plaque_DarkSlate", new Color(0.08f, 0.11f, 0.16f), 0.25f, 0.55f);
            CreatePart("PlaqueBoard", PrimitiveType.Cube, holder.transform,
                Vector3.zero, Vector3.zero, new Vector3(0.08f, 0.86f, 2.96f), plaqueBg, false);
            CreatePart("PlaqueFrameTop", PrimitiveType.Cube, holder.transform,
                new Vector3(0.01f, 0.45f, 0f), Vector3.zero, new Vector3(0.10f, 0.06f, 3.02f), accent, false);
            CreatePart("PlaqueFrameBot", PrimitiveType.Cube, holder.transform,
                new Vector3(0.01f, -0.45f, 0f), Vector3.zero, new Vector3(0.10f, 0.06f, 3.02f), accent, false);

            Material nasaBadgeMat = CreateNasaBadgeMaterial();
            if (nasaBadgeMat != null)
            {
                GameObject emblem = GameObject.CreatePrimitive(PrimitiveType.Quad);
                emblem.name = "Plaque_NASABadge";
                emblem.transform.SetParent(holder.transform, false);
                Collider ec = emblem.GetComponent<Collider>();
                if (ec != null) Destroy(ec);
                emblem.transform.localPosition = new Vector3(0.06f, 0f, -1.04f);
                emblem.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                emblem.transform.localScale = new Vector3(0.64f, 0.64f, 0.64f);
                emblem.GetComponent<Renderer>().sharedMaterial = nasaBadgeMat;
            }

            GameObject textGo = new GameObject("PlaqueText");
            textGo.transform.SetParent(holder.transform, false);
            textGo.transform.localPosition = new Vector3(0.06f, 0f, 0.30f);
            textGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            textGo.transform.localScale = Vector3.one * 0.033f;
            TextMesh tm = textGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.fontSize = 44;
            tm.lineSpacing = 1.05f;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
        }

        private static Material cachedNasaBadgeMaterial;

        public static Material CreateNasaBadgeMaterial()
        {
            if (cachedNasaBadgeMaterial != null) return cachedNasaBadgeMaterial;
            const int S = 192;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[S * S];
            Color navy = new Color(0.043f, 0.239f, 0.569f, 1f);
            Color white = new Color(0.97f, 0.98f, 1.0f, 1f);
            Color nasaRed = new Color(0.98f, 0.22f, 0.13f, 1f);
            for (int y = 0; y < S; y++)
            {
                float ny = (y / (float)(S - 1)) * 2f - 1f;
                for (int x = 0; x < S; x++)
                {
                    float nx = (x / (float)(S - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (r <= 0.96f)
                    {
                        c = (r >= 0.89f) ? white : navy;
                        // Subtle star field inside deep space disc
                        int h = ((x * 73856093) ^ (y * 19349663)) & 0x3FF;
                        if (r < 0.85f && h < 5) c = white;
                        // White elliptical orbital ring
                        float ox = nx * 0.82f + ny * 0.57f;
                        float oy = -nx * 0.57f + ny * 0.82f;
                        float orbitDist = Mathf.Abs(Mathf.Sqrt((ox / 0.80f) * (ox / 0.80f) + (oy / 0.28f) * (oy / 0.28f)) - 1.0f);
                        if (orbitDist < 0.075f && r < 0.89f) c = white;
                        // Iconic red supersonic chevron V-wing
                        float leg1 = Mathf.Abs(ny - (nx * 1.45f + 0.25f));
                        float leg2 = Mathf.Abs(ny - (-nx * 1.15f + 0.12f));
                        if ((nx >= -0.68f && nx <= 0.12f && ny >= -0.58f && ny <= 0.62f && leg1 < 0.11f) ||
                            (nx >= -0.05f && nx <= 0.72f && ny >= -0.42f && ny <= 0.56f && leg2 < 0.11f))
                        {
                            c = nasaRed;
                        }
                        // Crisp horizontal NASA center bar highlight
                        if (Mathf.Abs(ny + 0.04f) < 0.14f && Mathf.Abs(nx) < 0.66f)
                        {
                            // Keep chevron prominent over center stripe
                            if (Mathf.Abs(ny + 0.04f) > 0.08f) c = white;
                        }
                    }
                    px[y * S + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(sh) { name = "NASA_ORBITX_Emblem_Mat" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1.0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1.0f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.35f);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_EmissionMap"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(0.28f, 0.28f, 0.28f));
            }
            cachedNasaBadgeMaterial = m;
            return m;
        }

        private static Mesh BuildMeridianArchMesh(float radiusX, float heightY, float tubeRadius, int archSteps)
        {
            const int sides = 6;
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int i = 0; i <= archSteps; i++)
            {
                float rad = (i / (float)archSteps) * Mathf.PI;
                Vector3 center = new Vector3(Mathf.Cos(rad) * radiusX, Mathf.Sin(rad) * heightY, 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(rad) * radiusX, Mathf.Cos(rad) * heightY, 0f).normalized;
                Vector3 right = Vector3.forward;
                Vector3 normal = Vector3.Cross(right, tangent).normalized;
                for (int s = 0; s < sides; s++)
                {
                    float a = (s / (float)sides) * Mathf.PI * 2f;
                    verts.Add(center + (right * Mathf.Cos(a) + normal * Mathf.Sin(a)) * tubeRadius);
                }
            }
            for (int i = 0; i < archSteps; i++)
            {
                int rowA = i * sides;
                int rowB = (i + 1) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int sn = (s + 1) % sides;
                    tris.Add(rowA + s); tris.Add(rowB + s); tris.Add(rowB + sn);
                    tris.Add(rowA + s); tris.Add(rowB + sn); tris.Add(rowA + sn);
                }
            }
            Mesh mesh = new Mesh { name = "Dome_ContinuousMeridianArch" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLiftingBodyMesh(float widthX, float heightY, float lengthZ, bool isBellyShield)
        {
            const int zSlices = 14;
            const int ringVerts = 16;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            for (int iz = 0; iz <= zSlices; iz++)
            {
                float tz = iz / (float)zSlices; // 0 aft -> 1 nose
                // Keep outer rings cleanly separated from aftCap/noseCap so no coplanar zero-area triangles produce Bloom NaNs
                float z = Mathf.Lerp(-0.46f * lengthZ, 0.46f * lengthZ, tz);
                float taper = Mathf.Pow(Mathf.Sin(Mathf.Lerp(0.14f, 0.86f, tz) * Mathf.PI), 0.52f) * (0.68f + 0.32f * (1f - tz));
                float rx = Mathf.Max(0.28f, widthX * 0.5f * Mathf.Max(0.20f, taper));
                float ry = Mathf.Max(0.14f, heightY * 0.5f * Mathf.Max(0.26f, Mathf.Sin(Mathf.Lerp(0.12f, 0.88f, tz) * Mathf.PI)));
                for (int ir = 0; ir < ringVerts; ir++)
                {
                    float a = (ir / (float)ringVerts) * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * rx;
                    float rawY = Mathf.Sin(a) * ry;
                    float y = isBellyShield
                        ? (rawY < 0f ? rawY * 0.9f : rawY * 0.35f)
                        : (rawY >= 0f ? rawY * 1.15f : rawY * 0.35f);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(ir / (float)ringVerts, tz));
                }
            }
            for (int iz = 0; iz < zSlices; iz++)
            {
                int rA = iz * ringVerts;
                int rB = (iz + 1) * ringVerts;
                for (int ir = 0; ir < ringVerts; ir++)
                {
                    int inx = (ir + 1) % ringVerts;
                    tris.Add(rA + ir); tris.Add(rA + inx); tris.Add(rB + inx);
                    tris.Add(rA + ir); tris.Add(rB + inx); tris.Add(rB + ir);
                }
            }
            // Cap aft bulkhead & nose cone cleanly
            int aftCap = verts.Count;
            verts.Add(new Vector3(0f, 0f, -0.5f * lengthZ));
            uvs.Add(new Vector2(0.5f, 0f));
            int noseCap = verts.Count;
            verts.Add(new Vector3(0f, 0f, 0.5f * lengthZ));
            uvs.Add(new Vector2(0.5f, 1f));
            int lastRing = zSlices * ringVerts;
            for (int ir = 0; ir < ringVerts; ir++)
            {
                int inx = (ir + 1) % ringVerts;
                tris.Add(aftCap); tris.Add(inx); tris.Add(ir);
                tris.Add(noseCap); tris.Add(lastRing + ir); tris.Add(lastRing + inx);
            }
            Mesh m = new Mesh { name = isBellyShield ? "EDL_BellyHeatshield" : "EDL_LiftingAeroshell" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            Vector3[] norms = m.normals;
            for (int i = 0; i < norms.Length; i++)
            {
                if (norms[i].sqrMagnitude < 1e-5f || float.IsNaN(norms[i].x)) norms[i] = Vector3.up;
            }
            m.normals = norms;
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildSweptFinMesh(float rootChord, float tipChord, float span, float thickness, float sweep)
        {
            float ht = thickness * 0.5f;
            float rFront = rootChord * 0.5f;
            float rBack = -rootChord * 0.5f;
            float tFront = tipChord * 0.5f + sweep;
            float tBack = -tipChord * 0.5f + sweep;

            Vector3[] baseVerts =
            {
                new Vector3(-ht, 0f, rFront),  // 0: root front-left
                new Vector3( ht, 0f, rFront),  // 1: root front-right
                new Vector3( ht, 0f, rBack),   // 2: root back-right
                new Vector3(-ht, 0f, rBack),   // 3: root back-left
                new Vector3(-ht * 0.5f, span, tFront), // 4: tip front-left
                new Vector3( ht * 0.5f, span, tFront), // 5: tip front-right
                new Vector3( ht * 0.5f, span, tBack),  // 6: tip back-right
                new Vector3(-ht * 0.5f, span, tBack)   // 7: tip back-left
            };
            int[] idx =
            {
                0,4,5, 0,5,1, // leading edge
                1,5,6, 1,6,2, // right face
                2,6,7, 2,7,3, // trailing edge
                3,7,4, 3,4,0, // left face
                4,7,6, 4,6,5, // top cap
                0,1,2, 0,2,3  // root cap
            };
            Vector3[] verts = new Vector3[idx.Length];
            Vector2[] uvs = new Vector2[idx.Length];
            int[] tris = new int[idx.Length];
            for (int i = 0; i < idx.Length; i++)
            {
                verts[i] = baseVerts[idx[i]];
                uvs[i] = new Vector2((verts[i].z + rootChord) / (rootChord * 2f), verts[i].y / Mathf.Max(0.1f, span));
                tris[i] = i;
            }
            Mesh m = new Mesh { name = "SweptFinMesh" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Texture2D CreateStratifiedRockTexture(Color baseColor, int seed, string style)
        {
            const int W = 64, H = 64;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            Color[] px = new Color[W * H];
            bool layered = style == "sedimentary" || style == "delta" || style == "cheyava";
            float ox = (seed % 53) * 0.41f;
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float strata = layered ? (0.84f + 0.24f * Mathf.Sin(v * 22f) + 0.10f * Mathf.Cos(v * 47f)) : (0.90f + 0.14f * v);
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    float grain = Mathf.PerlinNoise(u * 12f + ox, v * 10f + ox);
                    float darkCrack = (layered && Mathf.Abs(Mathf.Sin(v * 18f + grain * 0.8f)) < 0.08f) ? 0.68f : 1.0f;
                    float mul = Mathf.Clamp(strata * (0.82f + grain * 0.34f) * darkCrack, 0.45f, 1.35f);
                    px[y * W + x] = new Color(
                        Mathf.Clamp01(baseColor.r * mul),
                        Mathf.Clamp01(baseColor.g * mul),
                        Mathf.Clamp01(baseColor.b * mul), 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Mesh BuildStratifiedRockMesh(Vector3 scale, int seed, string style)
        {
            const int rings = 10;
            const int sectors = 16;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            float ox = (seed % 97) * 0.37f;
            float oz = ((seed / 97) % 89) * 0.41f;
            bool layered = style == "sedimentary" || style == "delta" || style == "cheyava";
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings; // 0 wide grounded base -> 1 mesa summit
                float ringR = Mathf.Pow(Mathf.Max(0.06f, 1.0f - v * 0.76f), 0.55f);
                float baseH = -0.22f + v * 1.04f; // sunk -0.22*scale.y into regolith up to +0.82*scale.y
                for (int s = 0; s < sectors; s++)
                {
                    float theta = (s / (float)sectors) * Mathf.PI * 2f;
                    float dx = Mathf.Cos(theta) * ringR;
                    float dz = Mathf.Sin(theta) * ringR;
                    float n1 = Mathf.PerlinNoise(dx * 2.1f + ox, dz * 2.1f + oz);
                    float n2 = Mathf.PerlinNoise(dx * 4.5f + oz + 3.1f, v * 5.2f + ox);
                    float terrace = layered
                        ? (Mathf.Floor(Mathf.Max(0f, baseH) * 5.0f) / 5.0f * 0.30f + baseH * 0.70f)
                        : baseH;
                    float radJitter = 0.78f + n1 * 0.44f + (n2 - 0.5f) * 0.16f;
                    float y = (r == 0)
                        ? -0.22f * scale.y
                        : Mathf.Max(-0.08f * scale.y, (terrace + (n2 - 0.5f) * 0.14f) * scale.y);
                    verts.Add(new Vector3(dx * radJitter * scale.x * 0.56f, y, dz * radJitter * scale.z * 0.56f));
                    uvs.Add(new Vector2(s / (float)sectors, v));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                int rowA = r * sectors;
                int rowB = (r + 1) * sectors;
                for (int s = 0; s < sectors; s++)
                {
                    int sn = (s + 1) % sectors;
                    // Grounded tapering dome: Cross(rowB+s - rowA+s, rowB+sn - rowA+s) points +outward & +upward
                    tris.Add(rowA + s); tris.Add(rowB + s); tris.Add(rowB + sn);
                    tris.Add(rowA + s); tris.Add(rowB + sn); tris.Add(rowA + sn);
                }
            }
            // Cap rugged plateau apex outward & upward
            int apexIdx = verts.Count;
            verts.Add(new Vector3(0f, scale.y * 0.88f, 0f));
            uvs.Add(new Vector2(0.5f, 1.0f));
            int topRow = rings * sectors;
            for (int s = 0; s < sectors; s++)
            {
                tris.Add(topRow + s); tris.Add(apexIdx); tris.Add(topRow + (s + 1) % sectors);
            }
            Mesh mesh = new Mesh { name = "Stratified_Mars_Outcrop" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreatePbrMat(string name, Color col, float metallic, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Material CreateEmissiveMat(string name, Color baseCol, Color emissionCol)
        {
            Material m = CreatePbrMat(name, baseCol, 0.2f, 0.7f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissionCol);
            return m;
        }

        private static Material CreateGlassMat(string name, Color col)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1.0f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0.0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0.0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }
    }
}
