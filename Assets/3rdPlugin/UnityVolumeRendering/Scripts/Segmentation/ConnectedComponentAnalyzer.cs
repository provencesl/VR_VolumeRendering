using System.Collections.Generic;
using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for connected component analysis
    /// </summary>
    public static class ConnectedComponentAnalyzer
    {
        /// <summary>
        /// Keep only the largest connected component in the label volume
        /// </summary>
        /// <param name="src">Source label volume</param>
        /// <param name="dataset">Volume dataset</param>
        /// <returns>LabelVolume with only the largest component</returns>
        public static LabelVolume KeepLargest(LabelVolume src, VolumeDataset dataset)
        {
            var result = new LabelVolume(src.width, src.height, src.depth);

            // Simple flood fill to find connected components
            bool[,,] visited = new bool[src.width, src.height, src.depth];
            List<int> componentSizes = new List<int>();
            Dictionary<int, List<int>> components = new Dictionary<int, List<int>>();

            int componentId = 0;

            for (int z = 0; z < src.depth; z++)
            {
                for (int y = 0; y < src.height; y++)
                {
                    for (int x = 0; x < src.width; x++)
                    {
                        if (src.GetMask(x, y, z) == 1 && !visited[x, y, z])
                        {
                            List<int> component = new List<int>();
                            FloodFill(src, visited, x, y, z, component);

                            if (component.Count > 0)
                            {
                                componentSizes.Add(component.Count);
                                components[componentId] = component;
                                componentId++;
                            }
                        }
                    }
                }
            }

            // Find largest component
            if (componentSizes.Count > 0)
            {
                int maxSize = 0;
                int maxIndex = 0;
                for (int i = 0; i < componentSizes.Count; i++)
                {
                    if (componentSizes[i] > maxSize)
                    {
                        maxSize = componentSizes[i];
                        maxIndex = i;
                    }
                }

                // Keep only the largest component
                foreach (int index in components[maxIndex])
                {
                    int x = index % src.width;
                    int y = (index / src.width) % src.height;
                    int z = index / (src.width * src.height);
                    result.SetMask(x, y, z, 1);
                }
            }

            return result;
        }

        /// <summary>
        /// 3D flood fill algorithm
        /// </summary>
        private static void FloodFill(LabelVolume volume, bool[,,] visited, int startX, int startY, int startZ, List<int> component)
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(startX, startY, startZ));
            visited[startX, startY, startZ] = true;

            int[] dx = { -1, 0, 1, 0, 0, 0 };
            int[] dy = { 0, 1, 0, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, -1, 1 };

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                int index = current.x + current.y * volume.width + current.z * volume.width * volume.height;
                component.Add(index);

                for (int i = 0; i < 6; i++) // 6-connected neighbors
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    int nz = current.z + dz[i];

                    if (nx >= 0 && nx < volume.width &&
                        ny >= 0 && ny < volume.height &&
                        nz >= 0 && nz < volume.depth &&
                        !visited[nx, ny, nz] &&
                        volume.GetMask(nx, ny, nz) == 1)
                    {
                        visited[nx, ny, nz] = true;
                        queue.Enqueue(new Vector3Int(nx, ny, nz));
                    }
                }
            }
        }
    }
}
