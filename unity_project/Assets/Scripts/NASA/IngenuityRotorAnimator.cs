using System;
using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// IngenuityRotorAnimator controls the coaxial counter-rotating dual rotor blades
    /// of the Ingenuity Mars Helicopter at Wright Brothers Field, Jezero Crater.
    /// Provides continuous non-snapping rotation and flight readiness hover sway.
    /// </summary>
    public class IngenuityRotorAnimator : MonoBehaviour
    {
        [Header("Rotor Kinetics")]
        [SerializeField] private float rotorSpeedDegPerSec = 1080.0f; // 180 RPM
        [SerializeField] private bool isSpinning = true;

        [Header("Hover Dynamics")]
        [SerializeField] private bool enableHoverSway = true;
        [SerializeField] private float hoverAltitudeOffset = 0.08f;
        [SerializeField] private float hoverSwayAmplitude = 0.04f;
        [SerializeField] private float hoverSwayFrequency = 2.4f;

        [Header("Rotor Blade Transforms")]
        [SerializeField] private Transform upperRotor;
        [SerializeField] private Transform lowerRotor;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float currentAngle = 0f;

        public float RotorSpeedDegPerSec
        {
            get => rotorSpeedDegPerSec;
            set => rotorSpeedDegPerSec = value;
        }

        public bool IsSpinning
        {
            get => isSpinning;
            set => isSpinning = value;
        }

        public Transform UpperRotor => upperRotor;
        public Transform LowerRotor => lowerRotor;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;

            LocateOrCreateRotors();
        }

        private void Start()
        {
            if (upperRotor == null || lowerRotor == null)
            {
                LocateOrCreateRotors();
            }
        }

        public void LocateOrCreateRotors()
        {
            // 1. Search for existing rotor nodes in imported GLB hierarchy
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var t in children)
            {
                string n = t.name.ToLowerInvariant();
                if (upperRotor == null && (n.Contains("rotors_01") || n.Contains("upper_rotor") || n.Contains("rotor_upper") || n.Contains("top_rotor")))
                {
                    upperRotor = t;
                }
                else if (lowerRotor == null && (n.Contains("rotors_02") || n.Contains("lower_rotor") || n.Contains("rotor_lower") || n.Contains("bottom_rotor")))
                {
                    lowerRotor = t;
                }
            }

            // 2. If rotors are not found (e.g. GLB without extracted children), build procedural coaxial blades
            if (upperRotor == null)
            {
                GameObject upperGo = new GameObject("MS_Ingenuity_UpperRotor");
                upperGo.transform.SetParent(transform, false);
                upperGo.transform.localPosition = new Vector3(0f, 0.94f, 0f);
                CreateProceduralBladeMesh(upperGo);
                upperRotor = upperGo.transform;
            }

            if (lowerRotor == null)
            {
                GameObject lowerGo = new GameObject("MS_Ingenuity_LowerRotor");
                lowerGo.transform.SetParent(transform, false);
                lowerGo.transform.localPosition = new Vector3(0f, 0.76f, 0f);
                CreateProceduralBladeMesh(lowerGo);
                lowerRotor = lowerGo.transform;
            }
        }

        private void CreateProceduralBladeMesh(GameObject parent)
        {
            // Create carbon-fiber twin blade assembly
            GameObject bladeMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeMesh.name = "BladeMesh";
            bladeMesh.transform.SetParent(parent.transform, false);
            bladeMesh.transform.localPosition = Vector3.zero;
            bladeMesh.transform.localScale = new Vector3(1.35f, 0.014f, 0.085f);

            var col = bladeMesh.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            var rend = bladeMesh.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");
                if (standardShader != null)
                {
                    Material mat = new Material(standardShader);
                    mat.color = new Color(0.12f, 0.16f, 0.22f, 1.0f); // Dark carbon composite
                    mat.SetFloat("_Smoothness", 0.6f);
                    rend.sharedMaterial = mat;
                }
            }
        }

        private void Update()
        {
            if (!isSpinning) return;

            float dt = Time.deltaTime;
            float step = rotorSpeedDegPerSec * dt;

            // Advance continuous rotation
            currentAngle = (currentAngle + step) % 360f;

            // Counter-rotation: Upper rotor rotates clockwise (+), Lower rotor rotates counter-clockwise (-)
            if (upperRotor != null)
            {
                upperRotor.Rotate(Vector3.up, step, Space.Self);
            }

            if (lowerRotor != null)
            {
                lowerRotor.Rotate(Vector3.up, -step, Space.Self);
            }

            // Flight readiness hover sway
            if (enableHoverSway)
            {
                float t = Time.time;
                float yOffset = hoverAltitudeOffset + Mathf.Sin(t * hoverSwayFrequency) * hoverSwayAmplitude;
                transform.localPosition = basePosition + new Vector3(0f, yOffset, 0f);

                float pitchSway = Mathf.Cos(t * hoverSwayFrequency * 0.9f) * 0.8f;
                float rollSway = Mathf.Sin(t * hoverSwayFrequency * 1.1f) * 0.8f;
                transform.localRotation = baseRotation * Quaternion.Euler(pitchSway, 0f, rollSway);
            }
        }
    }
}
