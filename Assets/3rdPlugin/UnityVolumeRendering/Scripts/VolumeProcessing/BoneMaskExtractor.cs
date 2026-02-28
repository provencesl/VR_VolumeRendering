using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;

namespace VolumeProcessing
{
    public static class BoneMaskExtractor
    {
        /// <summary>
        /// Creates a transfer function optimized for bone visualization
        /// </summary>
        /// <returns>TransferFunction for bone rendering</returns>
        public static UnityVolumeRendering.TransferFunction CreateBoneTransferFunction()
        {
            UnityVolumeRendering.TransferFunction boneTF = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, 1.0f));
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            boneTF.colourControlPoints.Add(new TFColourControlPoint(1.0f, Color.white));
            boneTF.GenerateTexture();
            return boneTF;
        }

        /// <summary>
        /// Extracts bone mask dataset from volume dataset using HU threshold and connected component analysis
        /// </summary>
        /// <param name="dataset">Original volume dataset containing HU values</param>
        /// <param name="boneThresholdHU">HU threshold for bone detection (default: 300f)</param>
        /// <returns>VolumeDataset containing the bone mask (1.0f for bone, 0.0f for background)</returns>
        public static VolumeDataset ExtractBoneMask(VolumeDataset dataset, float boneThresholdHU = 300f)
        {
            int width = dataset.dimX;
            int height = dataset.dimY;
            int depth = dataset.dimZ;

            // Step 1: HU threshold → initial bone candidates
            bool[,,] boneCandidate = CreateBoneCandidates(dataset.data, width, height, depth, boneThresholdHU);

            // Step 2: 3D connected component analysis
            var (labels, componentSizes) = PerformConnectedComponentAnalysis(boneCandidate, width, height, depth);

            // Step 3: Find the largest connected component (main bone body)
            int maxLabel = FindLargestComponent(componentSizes);

            // Step 4: Generate final bone mask data
            float[] maskData = GenerateBoneMaskData(labels, width, height, depth, maxLabel);

            // Step 5: Create VolumeDataset
            VolumeDataset boneMaskDataset = ScriptableObject.CreateInstance<VolumeDataset>();
            boneMaskDataset.data = maskData;
            boneMaskDataset.dimX = width;
            boneMaskDataset.dimY = height;
            boneMaskDataset.dimZ = depth;
            boneMaskDataset.scale = dataset.scale;
            boneMaskDataset.RecalculateBounds();
            boneMaskDataset.RecreateDataTexture();
            boneMaskDataset.GetDataTexture().filterMode = FilterMode.Point;

            return boneMaskDataset;
        }

        /// <summary>
        /// Applies bone isolation to a volume rendered object by extracting bone mask and creating appropriate transfer function
        /// </summary>
        /// <param name="volumeRenderedObject">Volume rendered object to apply bone isolation to</param>
        /// <param name="dataset">Original volume dataset containing HU values</param>
        /// <param name="boneThresholdHU">HU threshold for bone detection (default: 300f)</param>
        public static async void ApplyBoneIsolation(VolumeRenderedObject volumeRenderedObject, VolumeDataset dataset, float boneThresholdHU = 300f)
        {
            // Step 1: Extract bone mask
            VolumeDataset boneMaskDataset = ExtractBoneMask(dataset, boneThresholdHU);

            // Step 2: Create bone transfer function
            UnityVolumeRendering.TransferFunction boneTF = CreateBoneTransferFunction();

            // Step 3: Apply bone mask and transfer function to the volume rendered object
            if (volumeRenderedObject != null)
            {
                // Set the volume dataset to the bone mask
                volumeRenderedObject.dataset = boneMaskDataset;
                
                // Apply the bone transfer function
                volumeRenderedObject.transferFunction = boneTF;
                
                // Force re-rendering
                // volumeRenderedObject.Render();
            }
        }

        private static bool[,,] CreateBoneCandidates(float[] volumeData, int width, int height, int depth, float boneThresholdHU)
        {
            bool[,,] boneCandidate = new bool[width, height, depth];

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * (width * height);
                        if (volumeData[index] >= boneThresholdHU)
                            boneCandidate[x, y, z] = true;
                    }
                }
            }

            return boneCandidate;
        }

        private static (int[,,], Dictionary<int, int>) PerformConnectedComponentAnalysis(bool[,,] boneCandidate, int width, int height, int depth)
        {
            int[,,] labels = new int[width, height, depth];
            Dictionary<int, int> componentSizes = new Dictionary<int, int>();

            int currentLabel = 1;
            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!boneCandidate[x, y, z] || labels[x, y, z] != 0)
                            continue;

                        int count = 0;
                        Queue<Vector3Int> queue = new Queue<Vector3Int>();
                        queue.Enqueue(new Vector3Int(x, y, z));
                        labels[x, y, z] = currentLabel;

                        while (queue.Count > 0)
                        {
                            var p = queue.Dequeue();
                            count++;

                            for (int i = 0; i < 6; i++)
                            {
                                int nx = p.x + dx[i];
                                int ny = p.y + dy[i];
                                int nz = p.z + dz[i];

                                if (nx < 0 || ny < 0 || nz < 0 ||
                                    nx >= width || ny >= height || nz >= depth)
                                    continue;

                                if (boneCandidate[nx, ny, nz] && labels[nx, ny, nz] == 0)
                                {
                                    labels[nx, ny, nz] = currentLabel;
                                    queue.Enqueue(new Vector3Int(nx, ny, nz));
                                }
                            }
                        }

                        componentSizes[currentLabel] = count;
                        currentLabel++;
                    }
                }
            }

            return (labels, componentSizes);
        }

        private static int FindLargestComponent(Dictionary<int, int> componentSizes)
        {
            int maxLabel = -1;
            int maxSize = 0;

            foreach (var kv in componentSizes)
            {
                if (kv.Value > maxSize)
                {
                    maxSize = kv.Value;
                    maxLabel = kv.Key;
                }
            }

            return maxLabel;
        }

        private static float[] GenerateBoneMaskData(int[,,] labels, int width, int height, int depth, int maxLabel)
        {
            float[] maskData = new float[width * height * depth];
            int idx = 0;

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        maskData[idx++] = (labels[x, y, z] == maxLabel) ? 1.0f : 0.0f;
                    }
                }
            }

            return maskData;
        }
    }
}
