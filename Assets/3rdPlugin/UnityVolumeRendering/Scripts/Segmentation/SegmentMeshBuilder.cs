using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for building meshes from label volumes using Marching Cubes
    /// </summary>
    public static class SegmentMeshBuilder
    {
        /// <summary>
        /// Build mesh from label volume using Marching Cubes algorithm
        /// </summary>
        /// <param name="label">Label volume containing the mask</param>
        /// <param name="dataset">Volume dataset for scale information</param>
        /// <returns>Generated mesh</returns>
        public static Mesh BuildMesh(LabelVolume label, VolumeDataset dataset)
        {
            return GenerateMeshFromLabelVolume(label, dataset.scale);
        }

        /// <summary>
        /// Generate mesh from label volume using simplified Marching Cubes
        /// </summary>
        /// <param name="label">Label volume</param>
        /// <param name="scale">Voxel scale</param>
        /// <returns>Generated mesh</returns>
        private static Mesh GenerateMeshFromLabelVolume(LabelVolume label, Vector3 scale)
        {
            int width = label.width;
            int height = label.height;
            int depth = label.depth;

            System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
            System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();

            // Simple surface extraction - check each voxel and create cubes for surface voxels
            for (int z = 0; z < depth - 1; z++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    for (int x = 0; x < width - 1; x++)
                    {
                        // Check if this voxel is part of the segment
                        if (label.GetMask(x, y, z) == 1)
                        {
                            // Check if this is a surface voxel (has at least one empty neighbor)
                            if (IsSurfaceVoxel(label, x, y, z, width, height, depth))
                            {
                                AddCubeToMesh(vertices, triangles, normals, new Vector3(x, y, z), scale);
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
        private static bool IsSurfaceVoxel(LabelVolume label, int x, int y, int z, int width, int height, int depth)
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
                    if (label.GetMask(nx, ny, nz) == 0)
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
        private static void AddCubeToMesh(System.Collections.Generic.List<Vector3> vertices,
                                         System.Collections.Generic.List<int> triangles,
                                         System.Collections.Generic.List<Vector3> normals,
                                         Vector3 position, Vector3 scale)
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
    }
}
