using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// MarsTerrainManager loads local Jezero Crater 3D topography and assigns
    /// the custom MartianSandSlopeBlend URP PBR material for slope-aware shading.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MarsTerrainManager : MonoBehaviour
    {
        [Header("Terrain Topography Configuration")]
        [SerializeField] private string stlRelativePath = "NASA_Data/elevation_dtm/sample_jezero_3d.stl";
        [SerializeField] private Vector3 terrainScale = new Vector3(0.05f, 0.05f, 0.05f);

        [Header("Material Settings")]
        [SerializeField] private Shader slopeShader;
        [SerializeField] private Color sandColor = new Color(0.761f, 0.439f, 0.239f, 1.0f); // #C2703D
        [SerializeField] private Color rockColor = new Color(0.420f, 0.220f, 0.141f, 1.0f); // #6B3824
        [Range(0f, 1f)] [SerializeField] private float slopeThreshold = 0.85f;
        [Range(0.01f, 0.5f)] [SerializeField] private float slopeBlendWidth = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float rippleStrength = 0.40f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material terrainMaterial;

        public Material TerrainMaterial => terrainMaterial;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            InitializeTerrain();
        }

        public void InitializeTerrain()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            // Setup Material
            SetupMaterial();

            // Setup Mesh if not already assigned
            if (meshFilter.sharedMesh == null)
            {
                Mesh terrainMesh = LoadSTLOrGenerateFallback();
                if (terrainMesh != null)
                {
                    meshFilter.sharedMesh = terrainMesh;
                }
            }

            transform.localScale = terrainScale;

            // Ensure deterministic non-convex static MeshCollider for accurate 3D PhysX grounding & raycasts
            if (meshFilter.sharedMesh != null)
            {
                MeshCollider mc = GetComponent<MeshCollider>();
                if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;
                mc.sharedMesh = meshFilter.sharedMesh;
            }
        }

        private void SetupMaterial()
        {
            if (slopeShader == null)
            {
                slopeShader = Shader.Find("MarsSentinel/MartianSandSlopeBlend");
                if (slopeShader == null)
                {
                    slopeShader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }

            if (slopeShader != null)
            {
                terrainMaterial = new Material(slopeShader);
                terrainMaterial.name = "M_Martian_Sand_Slope_Blend";
                terrainMaterial.SetColor("_SandColor", sandColor);
                terrainMaterial.SetColor("_RockColor", rockColor);
                terrainMaterial.SetFloat("_SlopeThreshold", slopeThreshold);
                terrainMaterial.SetFloat("_SlopeBlendWidth", slopeBlendWidth);
                terrainMaterial.SetFloat("_RippleStrength", rippleStrength);

                meshRenderer.sharedMaterial = terrainMaterial;
            }
        }

        private Mesh LoadSTLOrGenerateFallback()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Application.isPlaying)
            {
                StartCoroutine(LoadSTLWebGLCoroutine());
                return null;
            }
#endif
            string fullPath = Path.Combine(Application.streamingAssetsPath, stlRelativePath);
            if (File.Exists(fullPath))
            {
                try
                {
                    return ParseBinarySTL(fullPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MarsTerrainManager] Failed to parse STL at {fullPath}: {ex.Message}. Generating procedural fallback.");
                }
            }

            return GenerateProceduralCraterMesh();
        }

        private IEnumerator LoadSTLWebGLCoroutine()
        {
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + stlRelativePath.TrimStart('/');
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler != null && req.downloadHandler.data != null)
                {
                    try
                    {
                        Mesh stlMesh = ParseBinarySTL(req.downloadHandler.data);
                        if (stlMesh != null && meshFilter != null)
                        {
                            meshFilter.sharedMesh = stlMesh;
                            yield break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MarsTerrainManager] WebGL STL parse failed: {ex.Message}");
                    }
                }
            }

            if (meshFilter != null && meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = GenerateProceduralCraterMesh();
            }
        }

        public static Mesh ParseBinarySTL(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return ParseBinarySTL(bytes);
        }

        public static Mesh ParseBinarySTL(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 84) throw new InvalidDataException("STL file is too small to contain valid header and count.");

            uint triangleCount = BitConverter.ToUInt32(bytes, 80);
            if (bytes.Length < 84 + triangleCount * 50)
                throw new InvalidDataException("STL file size does not match triangle count.");

            // In Unity, 16-bit index buffers support up to 65535 vertices.
            // 33,780 triangles * 3 = 101,340 vertices, requiring IndexFormat.UInt32
            Mesh mesh = new Mesh();
            mesh.name = "Jezero_DTM_Topography";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] vertices = new Vector3[triangleCount * 3];
            Vector3[] normals = new Vector3[triangleCount * 3];
            int[] triangles = new int[triangleCount * 3];

            int offset = 84;
            bool requiresNormalRecalc = false;
            for (int i = 0; i < triangleCount; i++)
            {
                float nx = BitConverter.ToSingle(bytes, offset);
                float ny = BitConverter.ToSingle(bytes, offset + 4);
                float nz = BitConverter.ToSingle(bytes, offset + 8);
                // Swap Y and Z from Blender/Mars Z-up right-handed to Unity Y-up left-handed
                Vector3 normal = new Vector3(nx, nz, ny);
                float sqrMag = normal.sqrMagnitude;
                if (sqrMag < 0.001f || float.IsNaN(sqrMag))
                {
                    requiresNormalRecalc = true;
                    normal = Vector3.up;
                }
                else
                {
                    normal /= Mathf.Sqrt(sqrMag);
                }

                int baseIdx = i * 3;
                for (int v = 0; v < 3; v++)
                {
                    float vx = BitConverter.ToSingle(bytes, offset + 12 + v * 12);
                    float vy = BitConverter.ToSingle(bytes, offset + 16 + v * 12);
                    float vz = BitConverter.ToSingle(bytes, offset + 20 + v * 12);

                    int vertIdx = baseIdx + v;
                    vertices[vertIdx] = new Vector3(vx, vz, vy);
                    normals[vertIdx] = normal;
                }

                // Swapping Y and Z flips handedness (-1 determinant): reverse winding (0, 2, 1) so triangles face +Y
                triangles[baseIdx] = baseIdx;
                triangles[baseIdx + 1] = baseIdx + 2;
                triangles[baseIdx + 2] = baseIdx + 1;

                offset += 50; // 12 (normal) + 36 (3 vertices) + 2 (attribute byte count)
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            if (requiresNormalRecalc)
            {
                mesh.RecalculateNormals();
            }
            else
            {
                mesh.normals = normals;
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        private Mesh GenerateProceduralCraterMesh()
        {
            int resolution = 120;
            float size = 200f;
            Mesh mesh = new Mesh();
            mesh.name = "Jezero_Crater_Basin_Fallback";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int vertCount = (resolution + 1) * (resolution + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[resolution * resolution * 6];

            float step = size / resolution;
            int vIdx = 0;
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float px = (x - resolution / 2f) * step;
                    float pz = (z - resolution / 2f) * step;
                    float dist = Mathf.Sqrt(px * px + pz * pz);

                    // Crater rim and basin profile
                    float py = Mathf.Sin(dist * 0.08f) * 4.0f + Mathf.PerlinNoise(px * 0.04f, pz * 0.04f) * 6.0f;
                    if (dist > 60f) py += (dist - 60f) * 0.25f; // Crater outer rim

                    vertices[vIdx] = new Vector3(px, py, pz);
                    uvs[vIdx] = new Vector2((float)x / resolution, (float)z / resolution);
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int row1 = z * (resolution + 1) + x;
                    int row2 = (z + 1) * (resolution + 1) + x;

                    triangles[tIdx++] = row1;
                    triangles[tIdx++] = row2;
                    triangles[tIdx++] = row1 + 1;

                    triangles[tIdx++] = row1 + 1;
                    triangles[tIdx++] = row2;
                    triangles[tIdx++] = row2 + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
