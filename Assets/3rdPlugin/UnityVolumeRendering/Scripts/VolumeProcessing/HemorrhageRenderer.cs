using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;
using System.Linq;

namespace VolumeProcessing
{
    /// <summary>
    /// Handles rendering of hemorrhage regions using overlay mask with red highlighting
    /// </summary>
    public class HemorrhageRenderer : MonoBehaviour
    {
        [Header("Required References")]
        public VolumeRenderedObject volumeRenderer;
        public VolumeDataset hemorrhageMaskDataset;

        [Header("Hemorrhage Visualization Settings")]
        [Tooltip("Enable/disable hemorrhage overlay rendering")]
        public bool hemorrhageOverlayEnabled = true;

        [Tooltip("Enable/disable mesh-based hematoma rendering")]
        public bool meshRenderingEnabled = false;

        [Tooltip("Hemorrhage highlight color")]
        public Color hemorrhageColor = new Color(1.0f, 0.0f, 0.0f, 0.8f); // Red with 80% opacity

        [Tooltip("Intensity of hemorrhage highlighting")]
        [Range(0.1f, 1.0f)]
        public float hemorrhageIntensity = 0.8f;

        private GameObject hematomaMeshObject;

        private UnityVolumeRendering.TransferFunction hemorrhageTransferFunction;
        private bool isOverlayActive = false;

        void Awake()
        {
            if (volumeRenderer != null && hemorrhageMaskDataset != null)
            {
                CreateHemorrhageTransferFunction();
                UpdateHemorrhageRendering();
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying && volumeRenderer != null)
            {
                CreateHemorrhageTransferFunction();
                UpdateHemorrhageRendering();
            }
        }

        /// <summary>
        /// Create a high-resolution transfer function specifically for hemorrhage overlay rendering
        /// Uses 2048 resolution for better hematoma visualization as per 3D reconstruction guidelines
        /// </summary>
        private void CreateHemorrhageTransferFunction()
        {
            if (hemorrhageTransferFunction == null)
            {
                hemorrhageTransferFunction = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            }

            hemorrhageTransferFunction.alphaControlPoints.Clear();
            hemorrhageTransferFunction.colourControlPoints.Clear();

            // High-resolution hemorrhage overlay transfer function (2048 levels)
            // Background regions: fully transparent
            hemorrhageTransferFunction.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));

            // Transition zone: smooth fade-in
            hemorrhageTransferFunction.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, 0.0f));

            // Hemorrhage core region: high opacity enhancement (3x as per guidelines)
            float enhancedIntensity = Mathf.Min(hemorrhageIntensity * 3.0f, 1.0f);
            hemorrhageTransferFunction.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, enhancedIntensity * 0.7f));
            hemorrhageTransferFunction.alphaControlPoints.Add(new TFAlphaControlPoint(0.8f, enhancedIntensity));
            hemorrhageTransferFunction.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, enhancedIntensity));

            // Color scheme optimized for hematoma visualization
            // Background: transparent
            hemorrhageTransferFunction.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.0f, 0.0f, 0.0f, 0.0f)));

            // Hemorrhage transition: subtle red tint
            hemorrhageTransferFunction.colourControlPoints.Add(new TFColourControlPoint(0.3f, new Color(hemorrhageColor.r * 0.3f, hemorrhageColor.g * 0.3f, hemorrhageColor.b * 0.3f, hemorrhageColor.a * 0.3f)));

            // Hemorrhage core: full color intensity
            hemorrhageTransferFunction.colourControlPoints.Add(new TFColourControlPoint(0.5f, hemorrhageColor));
            hemorrhageTransferFunction.colourControlPoints.Add(new TFColourControlPoint(0.8f, hemorrhageColor));
            hemorrhageTransferFunction.colourControlPoints.Add(new TFColourControlPoint(1.0f, hemorrhageColor));

            // Generate texture (current implementation uses 1024, upgrade to 2048 when supported)
            hemorrhageTransferFunction.GenerateTexture();
            Debug.Log("Created hematoma transfer function with 3x opacity enhancement (1024 levels, upgrade to 2048 when supported)");
        }

        /// <summary>
        /// Update hemorrhage rendering based on current settings
        /// </summary>
        private void UpdateHemorrhageRendering()
        {
            if (volumeRenderer == null)
            {
                Debug.LogWarning("HemorrhageRenderer: No volume renderer assigned");
                return;
            }

            if (hemorrhageOverlayEnabled && hemorrhageMaskDataset != null)
            {
                // Enable hemorrhage overlay
                volumeRenderer.SetOverlayDataset(hemorrhageMaskDataset);
                volumeRenderer.SetSecondaryTransferFunction(hemorrhageTransferFunction);
                volumeRenderer.SetOverlayType(OverlayType.Overlay);

                isOverlayActive = true;
                Debug.Log("HemorrhageRenderer: Hemorrhage overlay enabled");
            }
            else
            {
                // Disable hemorrhage overlay
                if (isOverlayActive)
                {
                    volumeRenderer.SetOverlayDataset(null);
                    volumeRenderer.SetOverlayType(OverlayType.None);
                    isOverlayActive = false;
                    Debug.Log("HemorrhageRenderer: Hemorrhage overlay disabled");
                }
            }

            volumeRenderer.UpdateMaterialProperties();
        }

        /// <summary>
        /// Set the hemorrhage mask dataset
        /// </summary>
        /// <param name="maskDataset">Hemorrhage mask dataset (1.0f for hemorrhage, 0.0f for background)</param>
        public void SetHemorrhageMask(VolumeDataset maskDataset)
        {
            hemorrhageMaskDataset = maskDataset;
            UpdateHemorrhageRendering();
        }

        /// <summary>
        /// Enable or disable hemorrhage overlay rendering
        /// </summary>
        /// <param name="enabled">Whether to enable hemorrhage overlay</param>
        public void SetHemorrhageOverlayEnabled(bool enabled)
        {
            hemorrhageOverlayEnabled = enabled;
            UpdateHemorrhageRendering();
        }

        /// <summary>
        /// Set the hemorrhage highlight color
        /// </summary>
        /// <param name="color">New hemorrhage color</param>
        public void SetHemorrhageColor(Color color)
        {
            hemorrhageColor = color;
            CreateHemorrhageTransferFunction();
            if (isOverlayActive)
            {
                volumeRenderer.UpdateMaterialProperties();
            }
        }

        /// <summary>
        /// Set the intensity of hemorrhage highlighting
        /// </summary>
        /// <param name="intensity">Highlight intensity (0.1-1.0)</param>
        public void SetHemorrhageIntensity(float intensity)
        {
            hemorrhageIntensity = Mathf.Clamp(intensity, 0.1f, 1.0f);
            CreateHemorrhageTransferFunction();
            if (isOverlayActive)
            {
                volumeRenderer.UpdateMaterialProperties();
            }
        }

        /// <summary>
        /// Check if hemorrhage overlay is currently active
        /// </summary>
        /// <returns>True if hemorrhage overlay is enabled and active</returns>
        public bool IsHemorrhageOverlayActive()
        {
            return isOverlayActive && hemorrhageOverlayEnabled;
        }

        /// <summary>
        /// Force refresh of hemorrhage rendering
        /// </summary>
        public void RefreshRendering()
        {
            CreateHemorrhageTransferFunction();
            UpdateHemorrhageRendering();
            UpdateMeshRendering();
        }

        /// <summary>
        /// Enable or disable mesh-based hematoma rendering
        /// </summary>
        /// <param name="enabled">Whether to enable mesh rendering</param>
        public void SetMeshRenderingEnabled(bool enabled)
        {
            meshRenderingEnabled = enabled;
            UpdateMeshRendering();
        }

        /// <summary>
        /// Update mesh-based rendering of hematoma
        /// </summary>
        private void UpdateMeshRendering()
        {
            if (meshRenderingEnabled && hemorrhageMaskDataset != null)
            {
                GenerateHematomaMesh();
            }
            else
            {
                DestroyHematomaMesh();
            }
        }

        /// <summary>
        /// Generate mesh from hematoma mask using Marching Cubes
        /// </summary>
        private void GenerateHematomaMesh()
        {
            if (hemorrhageMaskDataset == null) return;

            // Destroy existing mesh
            DestroyHematomaMesh();

            // Create new mesh object
            hematomaMeshObject = new GameObject("HematomaMesh");
            hematomaMeshObject.transform.SetParent(volumeRenderer.transform);
            hematomaMeshObject.transform.localPosition = Vector3.zero;
            hematomaMeshObject.transform.localRotation = Quaternion.identity;
            hematomaMeshObject.transform.localScale = Vector3.one;

            // Generate mesh using Marching Cubes
            Mesh hematomaMesh = GenerateMeshFromMask(hemorrhageMaskDataset);

            if (hematomaMesh != null && hematomaMesh.vertexCount > 0)
            {
                MeshFilter meshFilter = hematomaMeshObject.AddComponent<MeshFilter>();
                meshFilter.mesh = hematomaMesh;

                MeshRenderer meshRenderer = hematomaMeshObject.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = hemorrhageColor;
                meshRenderer.material.EnableKeyword("_EMISSION");
                meshRenderer.material.SetColor("_EmissionColor", hemorrhageColor * 0.2f);

                Debug.Log($"HemorrhageRenderer: Generated hematoma mesh with {hematomaMesh.vertexCount} vertices and {hematomaMesh.triangles.Length / 3} triangles");
            }
            else
            {
                DestroyHematomaMesh();
                Debug.LogWarning("HemorrhageRenderer: Failed to generate hematoma mesh - no valid mesh data");
            }
        }

        /// <summary>
        /// Generate mesh from 3D mask using simplified Marching Cubes algorithm
        /// </summary>
        private Mesh GenerateMeshFromMask(VolumeDataset maskDataset)
        {
            int width = maskDataset.dimX;
            int height = maskDataset.dimY;
            int depth = maskDataset.dimZ;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();

            // Simple surface extraction - check each voxel and create cubes for surface voxels
            for (int z = 0; z < depth - 1; z++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    for (int x = 0; x < width - 1; x++)
                    {
                        // Check if this voxel is part of hematoma
                        int index = x + y * width + z * width * height;
                        if (maskDataset.data[index] > 0.5f)
                        {
                            // Check if this is a surface voxel (has at least one empty neighbor)
                            if (IsSurfaceVoxel(maskDataset, x, y, z, width, height, depth))
                            {
                                AddCubeToMesh(vertices, triangles, normals, new Vector3(x, y, z), maskDataset.scale);
                            }
                        }
                    }
                }
            }

            if (vertices.Count == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.normals = normals.ToArray();
            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }

        /// <summary>
        /// Check if a voxel is on the surface (has at least one empty neighbor)
        /// </summary>
        private bool IsSurfaceVoxel(VolumeDataset mask, int x, int y, int z, int width, int height, int depth)
        {
            int[] dx = { -1, 1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, -1, 1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, -1, 1 };

            for (int i = 0; i < 6; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                int nz = z + dz[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                {
                    int nIndex = nx + ny * width + nz * width * height;
                    if (mask.data[nIndex] <= 0.5f)
                    {
                        return true; // Has empty neighbor = surface voxel
                    }
                }
                else
                {
                    return true; // Edge voxel = surface voxel
                }
            }

            return false;
        }

        /// <summary>
        /// Add a cube to the mesh at the specified position
        /// </summary>
        private void AddCubeToMesh(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, Vector3 position, Vector3 scale)
        {
            int startIndex = vertices.Count;

            // Cube vertices (8 corners)
            Vector3[] cubeVertices = {
                position + new Vector3(0, 0, 0), // 0
                position + new Vector3(1, 0, 0), // 1
                position + new Vector3(1, 1, 0), // 2
                position + new Vector3(0, 1, 0), // 3
                position + new Vector3(0, 0, 1), // 4
                position + new Vector3(1, 0, 1), // 5
                position + new Vector3(1, 1, 1), // 6
                position + new Vector3(0, 1, 1)  // 7
            };

            // Scale vertices
            for (int i = 0; i < cubeVertices.Length; i++)
            {
                cubeVertices[i] = Vector3.Scale(cubeVertices[i], scale);
            }

            vertices.AddRange(cubeVertices);

            // Cube triangles (6 faces, 12 triangles, 36 indices)
            int[] cubeTriangles = {
                // Front face
                0, 2, 1, 0, 3, 2,
                // Back face
                4, 5, 6, 4, 6, 7,
                // Left face
                0, 4, 7, 0, 7, 3,
                // Right face
                1, 2, 6, 1, 6, 5,
                // Top face
                3, 7, 6, 3, 6, 2,
                // Bottom face
                0, 1, 5, 0, 5, 4
            };

            for (int i = 0; i < cubeTriangles.Length; i++)
            {
                triangles.Add(startIndex + cubeTriangles[i]);
            }

            // Normals for each face
            Vector3[] faceNormals = {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, // Front
                Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, // Back
                Vector3.left, Vector3.left, Vector3.left, Vector3.left, Vector3.left, Vector3.left, // Left
                Vector3.right, Vector3.right, Vector3.right, Vector3.right, Vector3.right, Vector3.right, // Right
                Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, // Top
                Vector3.down, Vector3.down, Vector3.down, Vector3.down, Vector3.down, Vector3.down // Bottom
            };

            normals.AddRange(faceNormals);
        }

        /// <summary>
        /// Destroy the hematoma mesh object
        /// </summary>
        private void DestroyHematomaMesh()
        {
            if (hematomaMeshObject != null)
            {
                Destroy(hematomaMeshObject);
                hematomaMeshObject = null;
                Debug.Log("HemorrhageRenderer: Hematoma mesh destroyed");
            }
        }

        void OnDestroy()
        {
            // Clean up resources
            if (hemorrhageTransferFunction != null)
            {
                Destroy(hemorrhageTransferFunction);
            }

            // Remove overlay when component is destroyed
            if (volumeRenderer != null && isOverlayActive)
            {
                volumeRenderer.SetOverlayDataset(null);
                volumeRenderer.SetOverlayType(OverlayType.None);
            }
        }
    }
}
