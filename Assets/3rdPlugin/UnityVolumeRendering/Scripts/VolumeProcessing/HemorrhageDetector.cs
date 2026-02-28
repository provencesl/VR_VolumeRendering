using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;
using System.Threading.Tasks;
using System.Linq;

namespace VolumeProcessing
{
    /// <summary>
    /// Input data structure for intracranial hemorrhage detection
    /// </summary>
    public class HemorrhageDetectionInput
    {
        public float[,,] HU;                  // Raw CT data in HU
        public bool[,,] BoneMask;             // Bone tissue mask
        public bool[,,] IntracranialMask;     // Intracranial space mask
        public Vector3 voxelSpacing;          // Voxel spacing in mm
        public int width, height, depth;      // Volume dimensions

        public HemorrhageDetectionInput(float[,,] hu, bool[,,] boneMask, bool[,,] intracranialMask,
                                      Vector3 spacing, int w, int h, int d)
        {
            HU = hu;
            BoneMask = boneMask;
            IntracranialMask = intracranialMask;
            voxelSpacing = spacing;
            width = w;
            height = h;
            depth = d;
        }
    }

    /// <summary>
    /// Represents a connected region in 3D space
    /// </summary>
    public class ConnectedRegion
    {
        public List<Vector3Int> Voxels;  // All voxels in this region
        public int VoxelCount;           // Number of voxels
        public Vector3 Centroid;         // Center of mass
        public float VolumeML;           // Volume in milliliters
        public float Compactness;        // Shape compactness measure

        public ConnectedRegion()
        {
            Voxels = new List<Vector3Int>();
            VoxelCount = 0;
            Centroid = Vector3.zero;
            VolumeML = 0f;
            Compactness = 0f;
        }

        /// <summary>
        /// Calculate centroid and other properties from voxel list
        /// </summary>
        public void CalculateProperties(Vector3 voxelSpacing)
        {
            if (Voxels.Count == 0) return;

            VoxelCount = Voxels.Count;

            // Calculate centroid
            Vector3 sum = Vector3.zero;
            foreach (Vector3Int voxel in Voxels)
            {
                sum += new Vector3(voxel.x, voxel.y, voxel.z);
            }
            Centroid = sum / VoxelCount;

            // Calculate volume in mL
            float voxelVolume = voxelSpacing.x * voxelSpacing.y * voxelSpacing.z;
            VolumeML = VoxelCount * voxelVolume / 1000f;

            // Calculate compactness (simplified)
            Bounds bounds = CalculateBounds();
            float volume = VoxelCount;
            float surfaceArea = bounds.size.x * bounds.size.y * 2 +
                              bounds.size.x * bounds.size.z * 2 +
                              bounds.size.y * bounds.size.z * 2;
            Compactness = surfaceArea > 0 ? volume / surfaceArea : 0;
        }

        private Bounds CalculateBounds()
        {
            if (Voxels.Count == 0) return new Bounds();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Vector3Int voxel in Voxels)
            {
                min = Vector3.Min(min, voxel);
                max = Vector3.Max(max, voxel);
            }

            return new Bounds((min + max) / 2f, max - min);
        }
    }

    /// <summary>
    /// 3D Connected Component Analysis
    /// </summary>
    public static class ConnectedComponent3D
    {
        /// <summary>
        /// Extract connected regions from a 3D binary mask using 26-connectivity
        /// </summary>
        public static List<ConnectedRegion> Extract(bool[,,] mask, int width, int height, int depth, Vector3 voxelSpacing)
        {
            List<ConnectedRegion> regions = new List<ConnectedRegion>();
            bool[,,] visited = new bool[width, height, depth];

            int[] dx = { -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1 };
            int[] dz = { -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[x, y, z] && !visited[x, y, z])
                        {
                            ConnectedRegion region = new ConnectedRegion();
                            Queue<Vector3Int> queue = new Queue<Vector3Int>();
                            queue.Enqueue(new Vector3Int(x, y, z));
                            visited[x, y, z] = true;

                            while (queue.Count > 0)
                            {
                                Vector3Int current = queue.Dequeue();
                                region.Voxels.Add(current);

                                // Check 26-connected neighbors
                                for (int i = 0; i < 27; i++)
                                {
                                    int nx = current.x + dx[i];
                                    int ny = current.y + dy[i];
                                    int nz = current.z + dz[i];

                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                                    {
                                        if (mask[nx, ny, nz] && !visited[nx, ny, nz])
                                        {
                                            visited[nx, ny, nz] = true;
                                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                                        }
                                    }
                                }
                            }

                            region.CalculateProperties(voxelSpacing);
                            regions.Add(region);
                        }
                    }
                }
            }

            return regions;
        }
    }

    /// <summary>
    /// Builds candidate hemorrhage voxels based on HU thresholds and spatial constraints
    /// </summary>
    public static class HemorrhageCandidateBuilder
    {
        /// <summary>
        /// Check if a voxel is a potential hemorrhage candidate
        /// Implements AND chain logic - ALL constraints must be satisfied
        /// </summary>
        public static bool IsHemorrhageCandidate(int x, int y, int z, HemorrhageDetectionInput input)
        {
            // Constraint 1: HU density (50-90 HU for acute hemorrhage)
            float hu = input.HU[x, y, z];
            if (hu < 50 || hu > 90) return false;

            // Constraint 2: Spatial legality (must be within intracranial space)
            if (!input.IntracranialMask[x, y, z]) return false;

            // Constraint 3: Non-bone tissue (aggressive bone exclusion)
            if (input.BoneMask[x, y, z]) return false;

            // Constraint 4: Local context check (avoid isolated high-density voxels)
            // Check if this voxel has neighboring candidates (reduces noise)
            int neighborCount = CountHemorrhageNeighbors(x, y, z, input, 1);
            if (neighborCount < 2) return false; // Must have at least 2 neighbors in 3x3x3

            return true;
        }

        /// <summary>
        /// Count neighboring voxels that could be hemorrhage candidates
        /// </summary>
        private static int CountHemorrhageNeighbors(int x, int y, int z, HemorrhageDetectionInput input, int radius)
        {
            int count = 0;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue; // Skip center voxel

                        int nx = x + dx, ny = y + dy, nz = z + dz;
                        if (nx >= 0 && nx < input.width && ny >= 0 && ny < input.height && nz >= 0 && nz < input.depth)
                        {
                            float nhu = input.HU[nx, ny, nz];
                            // Simple HU check for neighbors (relaxed constraints)
                            if (nhu >= 45 && nhu <= 95 &&
                                input.IntracranialMask[nx, ny, nz] &&
                                !input.BoneMask[nx, ny, nz]) // Bone exclusion
                            {
                                count++;
                            }
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Build candidate mask from input data
        /// </summary>
        public static bool[,,] BuildCandidates(HemorrhageDetectionInput input)
        {
            bool[,,] candidates = new bool[input.width, input.height, input.depth];

            for (int z = 0; z < input.depth; z++)
            {
                for (int y = 0; y < input.height; y++)
                {
                    for (int x = 0; x < input.width; x++)
                    {
                        candidates[x, y, z] = IsHemorrhageCandidate(x, y, z, input);
                    }
                }
            }

            return candidates;
        }
    }

    /// <summary>
    /// Analyzes connected regions to identify true hemorrhage areas
    /// </summary>
    public static class HemorrhageRegionAnalyzer
    {
        /// <summary>
        /// Analyze regions and filter out non-hemorrhage areas
        /// </summary>
        public static List<ConnectedRegion> AnalyzeRegions(List<ConnectedRegion> regions, HemorrhageDetectionInput input)
        {
            List<ConnectedRegion> validHemorrhages = new List<ConnectedRegion>();

            foreach (ConnectedRegion region in regions)
            {
                // Volume threshold (0.5 mL clinical minimum)
                if (region.VolumeML < 0.5f)
                    continue;

                // Distance to skull filter (remove skull-adjacent artifacts)
                float minDistanceToSkull = CalculateMinDistanceToSkull(region, input);
                if (minDistanceToSkull < 2.0f) // 2mm threshold
                    continue;

                // Morphological rationality (compactness)
                if (region.Compactness < 0.1f) // Too irregular
                    continue;

                validHemorrhages.Add(region);
            }

            return validHemorrhages;
        }

        /// <summary>
        /// Calculate minimum distance from region to bone/skull
        /// </summary>
        private static float CalculateMinDistanceToSkull(ConnectedRegion region, HemorrhageDetectionInput input)
        {
            float minDistance = float.MaxValue;

            foreach (Vector3Int voxel in region.Voxels)
            {
                // Simple distance calculation - find closest bone voxel
                // In practice, this could be optimized with distance transforms
                float distance = CalculateDistanceToNearestBone(voxel, input);
                minDistance = Mathf.Min(minDistance, distance);
            }

            return minDistance;
        }

        /// <summary>
        /// Calculate distance to nearest bone voxel (simplified implementation)
        /// </summary>
        private static float CalculateDistanceToNearestBone(Vector3Int voxel, HemorrhageDetectionInput input)
        {
            // Simple search in expanding spheres - not optimal but works
            const int maxSearchRadius = 10; // 10 voxels ~ 2mm at typical resolution

            for (int radius = 1; radius <= maxSearchRadius; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            // Only check surface of sphere
                            float distSq = dx * dx + dy * dy + dz * dz;
                            if (distSq >= (radius - 0.5f) * (radius - 0.5f) && distSq <= (radius + 0.5f) * (radius + 0.5f))
                            {
                                int nx = voxel.x + dx;
                                int ny = voxel.y + dy;
                                int nz = voxel.z + dz;

                                if (nx >= 0 && nx < input.width && ny >= 0 && ny < input.height && nz >= 0 && nz < input.depth)
                                {
                                    if (input.BoneMask[nx, ny, nz])
                                    {
                                        // Return distance in mm
                                        return radius * Mathf.Max(input.voxelSpacing.x, input.voxelSpacing.y, input.voxelSpacing.z);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // No bone found within search radius
            return maxSearchRadius * Mathf.Max(input.voxelSpacing.x, input.voxelSpacing.y, input.voxelSpacing.z);
        }
    }

    /// <summary>
    /// Improved intracranial hemorrhage detector following medical requirements
    /// Engineering flow: CT → Brain Parenchyma Mask → Candidates → 3D CCA → Select Best Component → Morphological Repair → Mesh
    /// </summary>
    public static class ImprovedHemorrhageDetector
    {
        /// <summary>
        /// Detect intracranial hemorrhage using the complete engineering pipeline
        /// </summary>
        public static bool[,,] DetectIntracranialHemorrhage(HemorrhageDetectionInput input, HemorrhageDetector.ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "开始工程级颅内出血检测...");

            // Step 1: Build candidate voxels (HU constraints)
            progressCallback?.Invoke(0.1f, "步骤1: HU约束筛选候选体素...");
            bool[,,] candidates = HemorrhageCandidateBuilder.BuildCandidates(input);

            // Step 2: True 3D connected component analysis (26-connectivity)
            progressCallback?.Invoke(0.2f, "步骤2: 3D连通域分析(26连通)...");
            var (labels, componentSizes) = Perform3DConnectedComponentAnalysis(candidates, input.width, input.height, input.depth);

            // Step 3: Select the best hematoma component (largest valid one)
            progressCallback?.Invoke(0.4f, "步骤3: 选择最佳血肿组件(最大有效连通块)...");
            int bestLabel = SelectBestHematomaComponent(labels, componentSizes, input);

            // Step 4: Generate mask for only the selected component
            progressCallback?.Invoke(0.6f, "步骤4: 生成选中组件的mask...");
            bool[,,] hematomaMask = GenerateHematomaMask(labels, bestLabel, input.width, input.height, input.depth);

            // Step 5: Morphological repair (closing operation to fill gaps)
            progressCallback?.Invoke(0.8f, "步骤5: 形态学修复(闭运算填充空隙)...");
            bool[,,] repairedMask = ApplyMorphologicalRepair(hematomaMask, input.width, input.height, input.depth);

            progressCallback?.Invoke(1.0f, "工程级出血检测完成");
            return repairedMask;
        }

        /// <summary>
        /// Perform true 3D connected component analysis (26-connectivity)
        /// </summary>
        private static (int[,,], Dictionary<int, int>) Perform3DConnectedComponentAnalysis(bool[,,] mask, int width, int height, int depth)
        {
            int[,,] labels = new int[width, height, depth];
            Dictionary<int, int> componentSizes = new Dictionary<int, int>();
            int currentLabel = 1;

            // 26-connectivity directions
            int[] dx = { -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1 };
            int[] dz = { -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[x, y, z] && labels[x, y, z] == 0)
                        {
                            // Start new component with flood fill
                            int componentSize = FloodFill3D(labels, mask, x, y, z, currentLabel, width, height, depth, dx, dy, dz);
                            componentSizes[currentLabel] = componentSize;
                            currentLabel++;
                        }
                    }
                }
            }

            return (labels, componentSizes);
        }

        /// <summary>
        /// Flood fill for 3D connected component labeling (26-connectivity)
        /// </summary>
        private static int FloodFill3D(int[,,] labels, bool[,,] mask, int startX, int startY, int startZ, int label,
                                      int width, int height, int depth, int[] dx, int[] dy, int[] dz)
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(startX, startY, startZ));
            labels[startX, startY, startZ] = label;
            int componentSize = 0;

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                componentSize++;

                // Check all 26 neighbors
                for (int i = 0; i < 27; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    int nz = current.z + dz[i];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                    {
                        if (mask[nx, ny, nz] && labels[nx, ny, nz] == 0)
                        {
                            labels[nx, ny, nz] = label;
                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                        }
                    }
                }
            }

            return componentSize;
        }

        /// <summary>
        /// Select the best hematoma component: largest valid connected component
        /// Core logic: Hematoma = intracranial · non-bone · largest connected high-density block
        /// </summary>
        private static int SelectBestHematomaComponent(int[,,] labels, Dictionary<int, int> componentSizes, HemorrhageDetectionInput input)
        {
            const int MIN_VOXEL_COUNT = 50; // Minimum 50 voxels for valid hematoma
            int bestLabel = -1;
            int maxSize = 0;

            foreach (var kvp in componentSizes)
            {
                int label = kvp.Key;
                int size = kvp.Value;

                // Must meet minimum size requirement
                if (size < MIN_VOXEL_COUNT)
                    continue;

                // Additional validation: component should be reasonably positioned (not too peripheral)
                if (IsValidHematomaPosition(labels, label, input))
                {
                    if (size > maxSize)
                    {
                        maxSize = size;
                        bestLabel = label;
                    }
                }
            }

            return bestLabel;
        }

        /// <summary>
        /// Validate if a component is in a valid hematoma position
        /// </summary>
        private static bool IsValidHematomaPosition(int[,,] labels, int targetLabel, HemorrhageDetectionInput input)
        {
            // Check if component centroid is within brain parenchyma region
            Vector3 centroid = CalculateComponentCentroid(labels, targetLabel, input.width, input.height, input.depth);

            // Should be in central 70% of volume (reasonable brain region)
            float marginX = input.width * 0.15f;
            float marginY = input.height * 0.15f;
            float marginZ = input.depth * 0.15f;

            return centroid.x >= marginX && centroid.x < input.width - marginX &&
                   centroid.y >= marginY && centroid.y < input.height - marginY &&
                   centroid.z >= marginZ && centroid.z < input.depth - marginZ;
        }

        /// <summary>
        /// Calculate centroid of a labeled component
        /// </summary>
        private static Vector3 CalculateComponentCentroid(int[,,] labels, int targetLabel, int width, int height, int depth)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (labels[x, y, z] == targetLabel)
                        {
                            sum += new Vector3(x, y, z);
                            count++;
                        }
                    }
                }
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// Generate mask for only the selected hematoma component
        /// </summary>
        private static bool[,,] GenerateHematomaMask(int[,,] labels, int bestLabel, int width, int height, int depth)
        {
            bool[,,] hematomaMask = new bool[width, height, depth];

            if (bestLabel <= 0) return hematomaMask; // No valid hematoma found

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        hematomaMask[x, y, z] = (labels[x, y, z] == bestLabel);
                    }
                }
            }

            return hematomaMask;
        }

        /// <summary>
        /// Apply morphological repair to create closed, solid hematoma mask
        /// Uses 3D closing operation (dilation + erosion) to fill gaps and create solid entity
        /// </summary>
        private static bool[,,] ApplyMorphologicalRepair(bool[,,] mask, int width, int height, int depth)
        {
            // 3D closing: dilation followed by erosion to fill small gaps
            bool[,,] dilated = MorphologicalDilation3D(mask, width, height, depth);
            bool[,,] closed = MorphologicalErosion3D(dilated, width, height, depth);

            return closed;
        }

        /// <summary>
        /// 3D morphological dilation (grow regions)
        /// </summary>
        private static bool[,,] MorphologicalDilation3D(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] dilated = new bool[width, height, depth];

            for (int z = 1; z < depth - 1; z++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Check 3x3x3 neighborhood
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (mask[x + dx, y + dy, z + dz])
                                    {
                                        dilated[x, y, z] = true;
                                        goto nextVoxel;
                                    }
                                }
                            }
                        }
                        nextVoxel:;
                    }
                }
            }

            return dilated;
        }

        /// <summary>
        /// 3D morphological erosion (shrink regions)
        /// </summary>
        private static bool[,,] MorphologicalErosion3D(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] eroded = new bool[width, height, depth];

            for (int z = 1; z < depth - 1; z++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Check if all neighbors in 3x3x3 are true
                        bool allNeighborsTrue = true;
                        for (int dz = -1; dz <= 1 && allNeighborsTrue; dz++)
                        {
                            for (int dy = -1; dy <= 1 && allNeighborsTrue; dy++)
                            {
                                for (int dx = -1; dx <= 1 && allNeighborsTrue; dx++)
                                {
                                    if (!mask[x + dx, y + dy, z + dz])
                                    {
                                        allNeighborsTrue = false;
                                    }
                                }
                            }
                        }
                        eroded[x, y, z] = allNeighborsTrue;
                    }
                }
            }

            return eroded;
        }

        /// <summary>
        /// Build 3D mask from valid hemorrhage regions
        /// </summary>
        private static bool[,,] BuildHemorrhageMask(List<ConnectedRegion> regions, int width, int height, int depth)
        {
            bool[,,] mask = new bool[width, height, depth];

            foreach (ConnectedRegion region in regions)
            {
                foreach (Vector3Int voxel in region.Voxels)
                {
                    mask[voxel.x, voxel.y, voxel.z] = true;
                }
            }

            return mask;
        }

        /// <summary>
        /// Convert mask to float array for volume dataset
        /// </summary>
        public static float[] MaskToFloatArray(bool[,,] mask, int width, int height, int depth)
        {
            float[] data = new float[width * height * depth];
            int idx = 0;

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        data[idx++] = mask[x, y, z] ? 1.0f : 0.0f;
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Bleeding types based on timing and HU characteristics
    /// </summary>
    public enum BleedingType
    {
        Acute,      // 50-90 HU, hyperdense
        Subacute,   // 35-65 HU, isodense
        Chronic     // 20-45 HU, hypodense
    }

    /// <summary>
    /// Legacy hemorrhage detector - kept for backward compatibility
    /// Detects hemorrhage regions in CT brain scans using HU thresholds and morphological constraints
    /// </summary>
    public static class HemorrhageDetector
    {
        public delegate void ProgressCallback(float progress, string status);

        // Constants for optimization
        private const int MAX_ITERATIONS_FLOOD_FILL = 1000000;
        private const float BONE_THRESHOLD_CONSERVATIVE = 0.3f;
        private const float BONE_THRESHOLD_AGGRESSIVE = 0.2f;
        private const int MIN_INTRACRANIAL_VOXELS = 50;
        private const int MAX_REASONABLE_COMPONENT_SIZE = 10000;
        /// <summary>
        /// Detects hemorrhage regions in the volume dataset
        /// </summary>
        /// <param name="dataset">CT volume dataset with HU values</param>
        /// <param name="boneMaskDataset">Pre-computed bone mask dataset</param>
        /// <param name="minHU">Minimum HU threshold for hemorrhage detection (default: 55)</param>
        /// <param name="maxHU">Maximum HU threshold for hemorrhage detection (default: 90)</param>
        /// <param name="minVoxelCount">Minimum connected voxel count to consider as hemorrhage (default: 10)</param>
        /// <returns>VolumeDataset containing hemorrhage mask (1.0f for hemorrhage, 0.0f for background)</returns>
        public static VolumeDataset DetectHemorrhage(VolumeDataset dataset, VolumeDataset boneMaskDataset,
                                                   float minHU = 55f, float maxHU = 90f, int minVoxelCount = 10)
        {
            return DetectHemorrhageAsync(dataset, boneMaskDataset, minHU, maxHU, minVoxelCount, null, null).Result;
        }

        /// <summary>
        /// Fast hemorrhage detection without complex morphological operations
        /// </summary>
        /// <param name="dataset">CT volume dataset with HU values</param>
        /// <param name="minHU">Minimum HU threshold for hemorrhage detection</param>
        /// <param name="maxHU">Maximum HU threshold for hemorrhage detection</param>
        /// <param name="minVoxelCount">Minimum connected voxel count to consider as hemorrhage</param>
        /// <returns>VolumeDataset containing hemorrhage mask</returns>
        public static VolumeDataset DetectHemorrhageFast(VolumeDataset dataset, float minHU = 55f, float maxHU = 90f, int minVoxelCount = 10)
        {
            return DetectHemorrhageFastAsync(dataset, minHU, maxHU, minVoxelCount, null, null).Result;
        }

        /// <summary>
        /// Performs fast hemorrhage detection computation (can run on background thread)
        /// </summary>
        /// <param name="volumeData">CT volume data array</param>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxelCount">Minimum voxel count</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>Computed mask data array</returns>
        public static float[] ComputeFastHemorrhageMask(
            float[] volumeData,
            int width,
            int height,
            int depth,
            float minHU = 55f,
            float maxHU = 90f,
            int minVoxelCount = 10,
            ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "开始快速检测...");

            // Step 1: Simple HU threshold filtering with basic spatial constraints
            progressCallback?.Invoke(0.2f, "执行HU阈值和空间过滤...");
            bool[,,] hemorrhageMask = CreateFastHemorrhageMask(volumeData, width, height, depth, minHU, maxHU);

            // Step 2: Simple connected component filtering (optional, can be skipped for speed)
            progressCallback?.Invoke(0.6f, "执行简单连通组件分析...");
            if (minVoxelCount > 1)
            {
                hemorrhageMask = FilterSmallComponents(hemorrhageMask, width, height, depth, minVoxelCount);
            }

            // Step 3: Generate mask data
            progressCallback?.Invoke(0.9f, "生成检测结果...");
            float[] maskData = GenerateHemorrhageMaskData(hemorrhageMask, width, height, depth);

            progressCallback?.Invoke(1.0f, "计算完成");
            return maskData;
        }

        /// <summary>
        /// Performs fast hemorrhage detection for extracranial regions (chest, abdomen, etc.)
        /// No spatial constraints applied - detects hemorrhage anywhere in the volume
        /// </summary>
        /// <param name="volumeData">CT volume data array</param>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxelCount">Minimum voxel count</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>Computed mask data array</returns>
        public static float[] ComputeFastExtracranialHemorrhageMask(
            float[] volumeData,
            int width,
            int height,
            int depth,
            float minHU = 55f,
            float maxHU = 90f,
            int minVoxelCount = 10,
            ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "开始快速体腔外检测...");

            // Step 1: HU threshold filtering without spatial constraints
            progressCallback?.Invoke(0.2f, "执行HU阈值过滤...");
            bool[,,] hemorrhageMask = CreateExtracranialHemorrhageMask(volumeData, width, height, depth, minHU, maxHU);

            // Step 2: Simple connected component filtering (optional, can be skipped for speed)
            progressCallback?.Invoke(0.6f, "执行简单连通组件分析...");
            if (minVoxelCount > 1)
            {
                hemorrhageMask = FilterSmallComponents(hemorrhageMask, width, height, depth, minVoxelCount);
            }

            // Step 3: Generate mask data
            progressCallback?.Invoke(0.9f, "生成检测结果...");
            float[] maskData = GenerateHemorrhageMaskData(hemorrhageMask, width, height, depth);

            progressCallback?.Invoke(1.0f, "体腔外检测完成");
            return maskData;
        }

        /// <summary>
        /// Performs complete intracranial hemorrhage detection with AND chain logic
        /// Implements all required constraints: HU density, intracranial space, bone exclusion, morphology, and context
        /// </summary>
        /// <param name="volumeData">CT volume data array</param>
        /// <param name="boneMaskData">Bone mask data array</param>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxelCount">Minimum voxel count for connected components</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>Computed mask data array</returns>
        public static float[] ComputeCompleteIntracranialHemorrhageMask(
            float[] volumeData,
            float[] boneMaskData,
            int width,
            int height,
            int depth,
            float minHU = 50f,
            float maxHU = 90f,
            int minVoxelCount = 50,
            ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "开始完整颅内出血检测...");

            // Step 1: HU threshold filtering (create potential hemorrhage candidates)
            progressCallback?.Invoke(0.1f, "步骤1: HU阈值筛选 (50-90 HU)...");
            bool[,,] potentialHemorrhage = CreatePotentialHemorrhageMask(volumeData, width, height, depth, minHU, maxHU);

            // Step 2: Create intracranial cavity mask from bone mask
            progressCallback?.Invoke(0.3f, "步骤2: 创建颅腔mask...");
            bool[,,] intracranialMask = CreateIntracranialMask(boneMaskData, width, height, depth);

            // Step 3: Apply intracranial constraint (must be inside cranial cavity)
            progressCallback?.Invoke(0.4f, "步骤3: 应用颅腔约束...");
            bool[,,] intracranialHemorrhage = ApplyIntracranialConstraint(potentialHemorrhage, intracranialMask, width, height, depth);

            // Step 4: Apply bone exclusion (must not be bone)
            progressCallback?.Invoke(0.5f, "步骤4: 排除骨骼区域...");
            bool[,,] boneExcludedHemorrhage = ApplyBoneExclusion(intracranialHemorrhage, boneMaskData, width, height, depth);

            // Step 5: Connected component analysis and morphological filtering
            progressCallback?.Invoke(0.6f, "步骤5: 连通域分析和形态学过滤...");
            var (labels, componentSizes) = PerformConnectedComponentAnalysis(boneExcludedHemorrhage, width, height, depth);

            // Filter by minimum voxel count (50-200 voxels for intracranial hemorrhage)
            int intracranialMinVoxels = Mathf.Max(minVoxelCount, 50); // At least 50 voxels for intracranial
            bool[,,] sizeFiltered = FilterByVoxelCount(labels, componentSizes, width, height, depth, intracranialMinVoxels);

            // Step 6: Context-based filtering (clinical reasonableness)
            progressCallback?.Invoke(0.8f, "步骤6: 上下文判断和临床合理性过滤...");
            bool[,,] contextFiltered = ApplyContextFiltering(sizeFiltered, width, height, depth, componentSizes);

            // Step 7: Generate final mask data
            progressCallback?.Invoke(0.9f, "步骤7: 生成最终检测结果...");
            float[] maskData = GenerateHemorrhageMaskData(contextFiltered, width, height, depth);

            progressCallback?.Invoke(1.0f, "颅内出血检测完成");
            return maskData;
        }

        /// <summary>
        /// Creates VolumeDataset from computed mask data (must be called on main thread)
        /// </summary>
        /// <param name="maskData">Computed mask data</param>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <param name="scale">Volume scale</param>
        /// <returns>VolumeDataset with hemorrhage mask</returns>
        public static VolumeDataset CreateHemorrhageMaskDataset(float[] maskData, int width, int height, int depth, Vector3 scale)
        {
            VolumeDataset hemorrhageMaskDataset = ScriptableObject.CreateInstance<VolumeDataset>();
            hemorrhageMaskDataset.data = maskData;
            hemorrhageMaskDataset.dimX = width;
            hemorrhageMaskDataset.dimY = height;
            hemorrhageMaskDataset.dimZ = depth;
            hemorrhageMaskDataset.scale = scale;
            hemorrhageMaskDataset.RecalculateBounds();
            hemorrhageMaskDataset.RecreateDataTexture();
            hemorrhageMaskDataset.GetDataTexture().filterMode = FilterMode.Point;
            return hemorrhageMaskDataset;
        }

        /// <summary>
        /// Asynchronously performs fast hemorrhage detection
        /// </summary>
        /// <param name="dataset">CT volume dataset with HU values</param>
        /// <param name="minHU">Minimum HU threshold for hemorrhage detection</param>
        /// <param name="maxHU">Maximum HU threshold for hemorrhage detection</param>
        /// <param name="minVoxelCount">Minimum connected voxel count to consider as hemorrhage</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task containing VolumeDataset with hemorrhage mask</returns>
        public static async System.Threading.Tasks.Task<VolumeDataset> DetectHemorrhageFastAsync(
            VolumeDataset dataset,
            float minHU = 55f,
            float maxHU = 90f,
            int minVoxelCount = 10,
            ProgressCallback progressCallback = null,
            System.Threading.CancellationToken? cancellationToken = null)
        {
            // Perform computation on background thread
            float[] maskData = await System.Threading.Tasks.Task.Run(() =>
                ComputeFastHemorrhageMask(dataset.data, dataset.dimX, dataset.dimY, dataset.dimZ, minHU, maxHU, minVoxelCount, progressCallback)
            );

            // Create Unity objects on main thread
            return CreateHemorrhageMaskDataset(maskData, dataset.dimX, dataset.dimY, dataset.dimZ, dataset.scale);
        }

        /// <summary>
        /// Asynchronously performs fast hemorrhage detection for extracranial regions (chest, abdomen, etc.)
        /// No spatial constraints applied - detects hemorrhage anywhere in the volume
        /// </summary>
        /// <param name="dataset">CT volume dataset with HU values</param>
        /// <param name="bodyRegion">Body region type for logging purposes</param>
        /// <param name="minHU">Minimum HU threshold for hemorrhage detection</param>
        /// <param name="maxHU">Maximum HU threshold for hemorrhage detection</param>
        /// <param name="minVoxelCount">Minimum connected voxel count to consider as hemorrhage</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task containing VolumeDataset with hemorrhage mask</returns>
        public static async System.Threading.Tasks.Task<VolumeDataset> DetectHemorrhageFastExtracranialAsync(
            VolumeDataset dataset,
            HemorrhageManager.BodyRegion bodyRegion,
            float minHU = 55f,
            float maxHU = 90f,
            int minVoxelCount = 10,
            ProgressCallback progressCallback = null,
            System.Threading.CancellationToken? cancellationToken = null)
        {
            // Perform computation on background thread
            float[] maskData = await System.Threading.Tasks.Task.Run(() =>
                ComputeFastExtracranialHemorrhageMask(dataset.data, dataset.dimX, dataset.dimY, dataset.dimZ, minHU, maxHU, minVoxelCount, progressCallback)
            );

            // Create Unity objects on main thread
            return CreateHemorrhageMaskDataset(maskData, dataset.dimX, dataset.dimY, dataset.dimZ, dataset.scale);
        }

        /// <summary>
        /// Asynchronously detects hemorrhage regions in the volume dataset with progress callback
        /// Implements complete intracranial hemorrhage detection with AND chain logic
        /// </summary>
        /// <param name="dataset">CT volume dataset with HU values</param>
        /// <param name="boneMaskDataset">Pre-computed bone mask dataset (required for intracranial)</param>
        /// <param name="minHU">Minimum HU threshold for hemorrhage detection (default: 55)</param>
        /// <param name="maxHU">Maximum HU threshold for hemorrhage detection (default: 90)</param>
        /// <param name="minVoxelCount">Minimum connected voxel count to consider as hemorrhage (default: 10)</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task containing VolumeDataset with hemorrhage mask (1.0f for hemorrhage, 0.0f for background)</returns>
        public static async System.Threading.Tasks.Task<VolumeDataset> DetectHemorrhageAsync(
            VolumeDataset dataset,
            VolumeDataset boneMaskDataset,
            float minHU = 55f,
            float maxHU = 90f,
            int minVoxelCount = 10,
            ProgressCallback progressCallback = null,
            System.Threading.CancellationToken? cancellationToken = null)
        {
            // Validate input parameters
            if (dataset == null)
            {
                throw new System.ArgumentNullException(nameof(dataset), "CT volume dataset cannot be null");
            }
            if (boneMaskDataset == null)
            {
                throw new System.ArgumentNullException(nameof(boneMaskDataset), "Bone mask dataset cannot be null");
            }
            if (dataset.data == null)
            {
                throw new System.ArgumentNullException(nameof(dataset.data), "CT volume dataset data cannot be null");
            }
            if (boneMaskDataset.data == null)
            {
                throw new System.ArgumentNullException(nameof(boneMaskDataset.data), "Bone mask dataset data cannot be null");
            }

            // Perform complete intracranial hemorrhage detection on background thread
            float[] maskData = await System.Threading.Tasks.Task.Run(() =>
                ComputeCompleteIntracranialHemorrhageMask(dataset.data, boneMaskDataset.data,
                                                         dataset.dimX, dataset.dimY, dataset.dimZ,
                                                         minHU, maxHU, minVoxelCount, progressCallback)
            );

            // Create Unity objects on main thread
            return CreateHemorrhageMaskDataset(maskData, dataset.dimX, dataset.dimY, dataset.dimZ, dataset.scale);
        }

        /// <summary>
        /// Fast hemorrhage mask creation with basic spatial constraints
        /// </summary>
        private static bool[,,] CreateFastHemorrhageMask(float[] volumeData, int width, int height, int depth,
                                                        float minHU, float maxHU)
        {
            bool[,,] hemorrhageMask = new bool[width, height, depth];

            // Add basic spatial constraints (focus on central region where brain typically is)
            int marginX = width / 6;  // Skip outer 1/6 of width
            int marginY = height / 6; // Skip outer 1/6 of height
            int marginZ = depth / 6;  // Skip outer 1/6 of depth

            for (int z = marginZ; z < depth - marginZ; z++)
            {
                for (int y = marginY; y < height - marginY; y++)
                {
                    for (int x = marginX; x < width - marginX; x++)
                    {
                        int index = x + y * width + z * (width * height);
                        float huValue = volumeData[index];
                        if (huValue >= minHU && huValue <= maxHU)
                        {
                            hemorrhageMask[x, y, z] = true;
                        }
                    }
                }
            }

            return hemorrhageMask;
        }

        /// <summary>
        /// Fast hemorrhage mask creation for extracranial regions (chest, abdomen, etc.)
        /// No spatial constraints applied - detects hemorrhage anywhere in the volume
        /// </summary>
        private static bool[,,] CreateExtracranialHemorrhageMask(float[] volumeData, int width, int height, int depth,
                                                               float minHU, float maxHU)
        {
            bool[,,] hemorrhageMask = new bool[width, height, depth];

            // No spatial constraints - check entire volume
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * (width * height);
                        float huValue = volumeData[index];
                        if (huValue >= minHU && huValue <= maxHU)
                        {
                            hemorrhageMask[x, y, z] = true;
                        }
                    }
                }
            }

            return hemorrhageMask;
        }

        /// <summary>
        /// Simple filtering of small connected components
        /// </summary>
        private static bool[,,] FilterSmallComponents(bool[,,] mask, int width, int height, int depth, int minVoxelCount)
        {
            // For speed, use a simplified approach: count neighbors
            bool[,,] filtered = new bool[width, height, depth];

            for (int z = 1; z < depth - 1; z++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (mask[x, y, z])
                        {
                            // Count immediate neighbors (26-connected)
                            int neighborCount = 0;
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        if (dx == 0 && dy == 0 && dz == 0) continue;
                                        if (mask[x + dx, y + dy, z + dz]) neighborCount++;
                                    }
                                }
                            }

                            // Keep if connected to at least minVoxelCount-1 other voxels
                            if (neighborCount >= minVoxelCount - 1)
                            {
                                filtered[x, y, z] = true;
                            }
                        }
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// Step 1: Create initial hemorrhage candidates based on HU thresholds
        /// </summary>
        private static bool[,,] CreatePotentialHemorrhageMask(float[] volumeData, int width, int height, int depth,
                                                            float minHU, float maxHU)
        {
            bool[,,] potentialHemorrhage = new bool[width, height, depth];

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * (width * height);
                        float huValue = volumeData[index];
                        if (huValue >= minHU && huValue <= maxHU)
                        {
                            potentialHemorrhage[x, y, z] = true;
                        }
                    }
                }
            }

            return potentialHemorrhage;
        }

        /// <summary>
        /// Step 2: Create intracranial cavity mask from bone mask
        /// The intracranial space is the region inside the skull bones
        /// </summary>
        private static bool[,,] CreateIntracranialMask(float[] boneMaskData, int width, int height, int depth)
        {
            Debug.Log($"[CreateIntracranialMask] 开始创建颅内掩膜，数据尺寸: {width}x{height}x{depth}");

            // Analyze bone mask quality first
            int totalVoxels = width * height * depth;
            int boneVoxels = 0;
            int lowBoneVoxels = 0;
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            for (int i = 0; i < boneMaskData.Length; i++)
            {
                float val = boneMaskData[i];
                minValue = Mathf.Min(minValue, val);
                maxValue = Mathf.Max(maxValue, val);
                if (val > 0.5f) boneVoxels++;
                if (val > 0.2f) lowBoneVoxels++;
            }

            float bonePercentage = (float)boneVoxels / totalVoxels * 100f;
            Debug.Log($"[CreateIntracranialMask] 骨骼mask分析: 骨体素={boneVoxels}, 低阈值骨体素={lowBoneVoxels}, 占比={bonePercentage:F1}%");

            bool[,,] intracranialMask = new bool[width, height, depth];

            if (bonePercentage < 1.0f)
            {
                // Bone mask seems incomplete or missing - fall back to spatial constraints
                Debug.LogWarning("[CreateIntracranialMask] 骨骼mask质量不足，使用空间约束备选方案");

                // Use central region as intracranial space (typical brain location)
                int marginX = width / 4;  // Central 50% of width
                int marginY = height / 4; // Central 50% of height
                int marginZ = depth / 4;  // Central 50% of depth

                for (int z = marginZ; z < depth - marginZ; z++)
                {
                    for (int y = marginY; y < height - marginY; y++)
                    {
                        for (int x = marginX; x < width - marginX; x++)
                        {
                            intracranialMask[x, y, z] = true;
                        }
                    }
                }
            }
            else
            {
                // Bone mask appears valid - use it to define intracranial space
                // Intracranial space = regions that are NOT definitively bone
                float boneThreshold = 0.3f; // Conservative threshold

                for (int z = 0; z < depth; z++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * (width * height);
                            float boneValue = boneMaskData[index];

                            // If not definitively bone, consider it potentially intracranial
                            intracranialMask[x, y, z] = (boneValue < boneThreshold);
                        }
                    }
                }
            }

            // Analyze final result
            int intracranialVoxels = 0;
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (intracranialMask[x, y, z]) intracranialVoxels++;
                    }
                }
            }

            Debug.Log($"[CreateIntracranialMask] 最终颅内空间体素: {intracranialVoxels}/{totalVoxels} ({(float)intracranialVoxels / totalVoxels * 100:F1}%)");

            return intracranialMask;
        }

        /// <summary>
        /// Simplified morphological filling to identify intracranial space
        /// In a real implementation, this would use proper morphological operations
        /// </summary>
        private static bool[,,] FillInternalCavity(bool[,,] boneMask, int width, int height, int depth)
        {
            Debug.Log($"[FillInternalCavity] 开始洪水填充，尺寸: {width}x{height}x{depth}");

            bool[,,] filled = new bool[width, height, depth];

            // Find seed points (assuming brain is roughly centered)
            int centerX = width / 2;
            int centerY = height / 2;
            int centerZ = depth / 2;

            // Use flood fill from center, but stop at bone boundaries
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            // Count valid seed points
            int validSeeds = 0;
            // Start from multiple seed points to ensure filling
            for (int offsetX = -10; offsetX <= 10; offsetX += 5)
            {
                for (int offsetY = -10; offsetY <= 10; offsetY += 5)
                {
                    for (int offsetZ = -10; offsetZ <= 10; offsetZ += 5)
                    {
                        int sx = Mathf.Clamp(centerX + offsetX, 0, width - 1);
                        int sy = Mathf.Clamp(centerY + offsetY, 0, height - 1);
                        int sz = Mathf.Clamp(centerZ + offsetZ, 0, depth - 1);

                        Vector3Int seed = new Vector3Int(sx, sy, sz);
                        if (!visited.Contains(seed) && !boneMask[sx, sy, sz])
                        {
                            queue.Enqueue(seed);
                            visited.Add(seed);
                            validSeeds++;
                        }
                    }
                }
            }

            Debug.Log($"[FillInternalCavity] 找到 {validSeeds} 个有效种子点");

            if (validSeeds == 0)
            {
                Debug.LogWarning("[FillInternalCavity] 没有找到有效的种子点！使用备选方案：所有非骨骼区域作为颅内空间");
                // Fallback: treat all non-bone regions as intracranial space
                for (int z = 0; z < depth; z++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            filled[x, y, z] = !boneMask[x, y, z];
                        }
                    }
                }
                return filled;
            }

            int[] dx = { 1, -1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, 1, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, 1, -1 };

            // Add safety limits
            int maxIterations = width * height * depth / 4; // Limit to 25% of total voxels
            int iterations = 0;
            int filledVoxels = 0;

            while (queue.Count > 0 && iterations < maxIterations)
            {
                Vector3Int current = queue.Dequeue();
                filled[current.x, current.y, current.z] = true;
                filledVoxels++;

                // Debug progress every 10000 iterations
                if (iterations % 10000 == 0 && iterations > 0)
                {
                    Debug.Log($"[FillInternalCavity] 处理进度: 迭代={iterations}, 队列大小={queue.Count}, 已填充={filledVoxels}");
                }

                for (int i = 0; i < 6; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    int nz = current.z + dz[i];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                    {
                        Vector3Int neighbor = new Vector3Int(nx, ny, nz);
                        if (!visited.Contains(neighbor) && !boneMask[nx, ny, nz])
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                iterations++;
            }

            Debug.Log($"[FillInternalCavity] 填充完成: 总迭代={iterations}, 已填充体素={filledVoxels}, 最终队列大小={queue.Count}");

            if (iterations >= maxIterations)
            {
                Debug.LogWarning($"[FillInternalCavity] 达到最大迭代限制 ({maxIterations})，可能存在问题");
            }

            return filled;
        }

        /// <summary>
        /// Step 3: Apply intracranial constraint to potential hemorrhage regions
        /// </summary>
        private static bool[,,] ApplyIntracranialConstraint(bool[,,] potentialHemorrhage, bool[,,] intracranialMask,
                                                         int width, int height, int depth)
        {
            bool[,,] constrained = new bool[width, height, depth];

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Hemorrhage must be both potential hemorrhage AND inside intracranial space
                        constrained[x, y, z] = potentialHemorrhage[x, y, z] && intracranialMask[x, y, z];
                    }
                }
            }

            return constrained;
        }

        /// <summary>
        /// Step 4: Apply bone exclusion constraint
        /// Ensures hemorrhage voxels are not classified as bone
        /// Bone mask should be more aggressive than hemorrhage threshold
        /// </summary>
        private static bool[,,] ApplyBoneExclusion(bool[,,] intracranialHemorrhage, float[] boneMaskData,
                                                 int width, int height, int depth)
        {
            bool[,,] boneExcluded = new bool[width, height, depth];

            // Use a more aggressive bone threshold (e.g., 0.3 instead of 0.5)
            // This ensures that borderline bone voxels are excluded from hemorrhage detection
            float boneThreshold = 0.3f;

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * (width * height);
                        float boneValue = boneMaskData[index];

                        // Hemorrhage must be intracranial AND not bone
                        boneExcluded[x, y, z] = intracranialHemorrhage[x, y, z] && (boneValue < boneThreshold);
                    }
                }
            }

            return boneExcluded;
        }

        /// <summary>
        /// Step 6: Apply context-based filtering for clinical reasonableness
        /// Filters out hemorrhage candidates that are clinically unlikely
        /// </summary>
        private static bool[,,] ApplyContextFiltering(bool[,,] sizeFiltered, int width, int height, int depth,
                                                    Dictionary<int, int> componentSizes)
        {
            bool[,,] contextFiltered = new bool[width, height, depth];

            // Find all remaining components and their centroids
            Dictionary<int, List<Vector3Int>> componentVoxels = new Dictionary<int, List<Vector3Int>>();
            Dictionary<int, Vector3> centroids = new Dictionary<int, Vector3>();

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (sizeFiltered[x, y, z])
                        {
                            // This voxel belongs to a component, but we need to identify which one
                            // For simplicity, we'll create a temporary label-based approach
                            int tempLabel = x + y * width + z * (width * height); // Unique temp label
                            if (!componentVoxels.ContainsKey(tempLabel))
                            {
                                componentVoxels[tempLabel] = new List<Vector3Int>();
                            }
                            componentVoxels[tempLabel].Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            // For this simplified implementation, we'll apply basic clinical rules:
            // 1. Keep components that are reasonably sized (not too large for single hemorrhage)
            // 2. Prefer components in central brain regions
            // 3. Avoid extremely scattered distributions

            int maxReasonableSize = 10000; // Maximum reasonable hemorrhage size in voxels
            Vector3 brainCenter = new Vector3(width / 2f, height / 2f, depth / 2f);

            foreach (var kvp in componentVoxels)
            {
                int componentId = kvp.Key;
                List<Vector3Int> voxels = kvp.Value;

                if (voxels.Count > maxReasonableSize)
                {
                    Debug.Log($"[ContextFiltering] 跳过过大组件: {voxels.Count} 体素");
                    continue; // Skip unreasonably large components
                }

                // Calculate centroid
                Vector3 centroid = Vector3.zero;
                foreach (Vector3Int voxel in voxels)
                {
                    centroid += new Vector3(voxel.x, voxel.y, voxel.z);
                }
                centroid /= voxels.Count;

                // Check if component is reasonably positioned (not too peripheral)
                float distanceFromCenter = Vector3.Distance(centroid, brainCenter);
                float maxReasonableDistance = Mathf.Min(width, height, depth) / 3f;

                if (distanceFromCenter > maxReasonableDistance)
                {
                    Debug.Log($"[ContextFiltering] 跳过位置异常组件: 距离中心 {distanceFromCenter:F1}, 最大允许 {maxReasonableDistance:F1}");
                    continue; // Skip components too far from brain center
                }

                // Component passed all filters - keep it
                foreach (Vector3Int voxel in voxels)
                {
                    contextFiltered[voxel.x, voxel.y, voxel.z] = true;
                }
            }

            Debug.Log($"[ContextFiltering] 上下文过滤完成: 保留了 {componentVoxels.Count} 个组件");
            return contextFiltered;
        }

        /// <summary>
        /// Step 4: Perform 3D connected component analysis
        /// </summary>
        private static (int[,,], Dictionary<int, int>) PerformConnectedComponentAnalysis(bool[,,] mask,
                                                                                       int width, int height, int depth)
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
                        if (!mask[x, y, z] || labels[x, y, z] != 0)
                            continue;

                        int count = 0;
                        Queue<Vector3Int> queue = new Queue<Vector3Int>();
                        queue.Enqueue(new Vector3Int(x, y, z));
                        labels[x, y, z] = currentLabel;

                        while (queue.Count > 0)
                        {
                            Vector3Int p = queue.Dequeue();
                            count++;

                            for (int i = 0; i < 6; i++)
                            {
                                int nx = p.x + dx[i];
                                int ny = p.y + dy[i];
                                int nz = p.z + dz[i];

                                if (nx < 0 || ny < 0 || nz < 0 ||
                                    nx >= width || ny >= height || nz >= depth)
                                    continue;

                                if (mask[nx, ny, nz] && labels[nx, ny, nz] == 0)
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

        /// <summary>
        /// Step 5: Filter components by minimum voxel count
        /// </summary>
        private static bool[,,] FilterByVoxelCount(int[,,] labels, Dictionary<int, int> componentSizes,
                                                 int width, int height, int depth, int minVoxelCount)
        {
            bool[,,] filtered = new bool[width, height, depth];

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int label = labels[x, y, z];
                        if (label != 0 && componentSizes.ContainsKey(label) && componentSizes[label] >= minVoxelCount)
                        {
                            filtered[x, y, z] = true;
                        }
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// Step 6: Generate final hemorrhage mask data array
        /// </summary>
        private static float[] GenerateHemorrhageMaskData(bool[,,] hemorrhageMask, int width, int height, int depth)
        {
            float[] maskData = new float[width * height * depth];
            int idx = 0;

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        maskData[idx++] = hemorrhageMask[x, y, z] ? 1.0f : 0.0f;
                    }
                }
            }

            return maskData;
        }

        /// <summary>
        /// Check if hemorrhage is present in the dataset
        /// </summary>
        /// <param name="hemorrhageMaskDataset">Hemorrhage mask dataset</param>
        /// <returns>True if any hemorrhage voxels are detected</returns>
        public static bool HasHemorrhage(VolumeDataset hemorrhageMaskDataset)
        {
            foreach (float value in hemorrhageMaskDataset.data)
            {
                if (value > 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get hemorrhage statistics
        /// </summary>
        /// <param name="hemorrhageMaskDataset">Hemorrhage mask dataset</param>
        /// <returns>Dictionary with statistics (total_voxels, volume_mm3, etc.)</returns>
        public static Dictionary<string, float> GetHemorrhageStatistics(VolumeDataset hemorrhageMaskDataset)
        {
            int totalVoxels = 0;
            float voxelVolume = hemorrhageMaskDataset.scale.x * hemorrhageMaskDataset.scale.y * hemorrhageMaskDataset.scale.z;

            foreach (float value in hemorrhageMaskDataset.data)
            {
                if (value > 0.5f)
                    totalVoxels++;
            }

            return new Dictionary<string, float>
            {
                { "total_voxels", totalVoxels },
                { "volume_mm3", totalVoxels * voxelVolume }
            };
        }

        /// <summary>
        /// Get optimal HU thresholds for different bleeding types
        /// </summary>
        /// <param name="bleedingType">Type of bleeding to detect</param>
        /// <returns>Tuple of (minHU, maxHU) thresholds</returns>
        public static (float minHU, float maxHU) GetHUThresholdsForBleedingType(BleedingType bleedingType)
        {
            switch (bleedingType)
            {
                case BleedingType.Acute:
                    return (50f, 90f);  // Hyperdense acute hemorrhage
                case BleedingType.Subacute:
                    return (35f, 65f);  // Isodense subacute hemorrhage
                case BleedingType.Chronic:
                    return (20f, 45f);  // Hypodense chronic hemorrhage
                default:
                    return (50f, 90f);  // Default to acute
            }
        }

        /// <summary>
        /// Validate detection parameters for clinical reasonableness
        /// </summary>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxelCount">Minimum voxel count</param>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <returns>True if parameters are valid</returns>
        public static bool ValidateParameters(float minHU, float maxHU, int minVoxelCount,
                                            int width, int height, int depth)
        {
            // Check HU range validity
            if (minHU >= maxHU)
            {
                Debug.LogError($"[ParameterValidation] 无效HU范围: minHU({minHU}) >= maxHU({maxHU})");
                return false;
            }

            // Check HU range is clinically reasonable
            if (minHU < 0 || maxHU > 150)
            {
                Debug.LogWarning($"[ParameterValidation] HU范围可能不合理: [{minHU}, {maxHU}]，超出典型出血范围");
            }

            // Check voxel count
            if (minVoxelCount < 1)
            {
                Debug.LogError($"[ParameterValidation] 最小体素数无效: {minVoxelCount}");
                return false;
            }

            // Check dimensions
            if (width <= 0 || height <= 0 || depth <= 0)
            {
                Debug.LogError($"[ParameterValidation] 无效数据尺寸: {width}x{height}x{depth}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Optimized intracranial mask creation using morphological operations
        /// </summary>
        private static bool[,,] CreateOptimizedIntracranialMask(float[] boneMaskData, int width, int height, int depth)
        {
            Debug.Log($"[CreateOptimizedIntracranialMask] 开始优化颅内掩膜创建，尺寸: {width}x{height}x{depth}");

            // Analyze bone mask quality
            int totalVoxels = width * height * depth;
            int boneVoxels = 0;

            // Parallel analysis for better performance
            Parallel.For(0, boneMaskData.Length, i =>
            {
                if (boneMaskData[i] > BONE_THRESHOLD_CONSERVATIVE)
                    System.Threading.Interlocked.Increment(ref boneVoxels);
            });

            float bonePercentage = (float)boneVoxels / totalVoxels * 100f;
            Debug.Log($"[CreateOptimizedIntracranialMask] 骨骼占比: {bonePercentage:F2}%");

            bool[,,] intracranialMask = new bool[width, height, depth];

            if (bonePercentage < 0.5f)
            {
                // Bone mask quality is poor - use spatial fallback
                Debug.LogWarning("[CreateOptimizedIntracranialMask] 骨骼mask质量不足，使用空间约束");
                int marginX = width / 5;
                int marginY = height / 5;
                int marginZ = depth / 5;

                Parallel.For(marginZ, depth - marginZ, z =>
                {
                    for (int y = marginY; y < height - marginY; y++)
                    {
                        for (int x = marginX; x < width - marginX; x++)
                        {
                            intracranialMask[x, y, z] = true;
                        }
                    }
                });
            }
            else
            {
                // Use morphological approach for better intracranial space definition
                // Step 1: Create initial bone mask
                bool[,,] boneMask = new bool[width, height, depth];
                Parallel.For(0, depth, z =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * width * height;
                            boneMask[x, y, z] = boneMaskData[index] > BONE_THRESHOLD_AGGRESSIVE;
                        }
                    }
                });

                // Step 2: Morphological closing to fill small gaps in skull
                bool[,,] closedBoneMask = MorphologicalClosing(boneMask, width, height, depth);

                // Step 3: Find intracranial space using optimized flood fill
                intracranialMask = FindIntracranialSpaceOptimized(closedBoneMask, width, height, depth);
            }

            // Validate result
            int intracranialCount = 0;
            Parallel.For(0, depth, z =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (intracranialMask[x, y, z])
                            System.Threading.Interlocked.Increment(ref intracranialCount);
                    }
                }
            });

            Debug.Log($"[CreateOptimizedIntracranialMask] 颅内空间体素: {intracranialCount}/{totalVoxels} ({(float)intracranialCount / totalVoxels * 100:F1}%)");

            return intracranialMask;
        }

        /// <summary>
        /// Morphological closing operation (dilation followed by erosion)
        /// </summary>
        private static bool[,,] MorphologicalClosing(bool[,,] mask, int width, int height, int depth)
        {
            // Simple 3x3x3 structuring element
            bool[,,] dilated = MorphologicalDilation(mask, width, height, depth);
            return MorphologicalErosion(dilated, width, height, depth);
        }

        /// <summary>
        /// Morphological dilation
        /// </summary>
        private static bool[,,] MorphologicalDilation(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] dilated = new bool[width, height, depth];

            Parallel.For(1, depth - 1, z =>
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Check 3x3x3 neighborhood
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (mask[x + dx, y + dy, z + dz])
                                    {
                                        dilated[x, y, z] = true;
                                        goto nextVoxel;
                                    }
                                }
                            }
                        }
                        nextVoxel:;
                    }
                }
            });

            return dilated;
        }

        /// <summary>
        /// Morphological erosion
        /// </summary>
        private static bool[,,] MorphologicalErosion(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] eroded = new bool[width, height, depth];

            Parallel.For(1, depth - 1, z =>
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Check if all neighbors in 3x3x3 neighborhood are true
                        bool allNeighborsTrue = true;
                        for (int dz = -1; dz <= 1 && allNeighborsTrue; dz++)
                        {
                            for (int dy = -1; dy <= 1 && allNeighborsTrue; dy++)
                            {
                                for (int dx = -1; dx <= 1 && allNeighborsTrue; dx++)
                                {
                                    if (!mask[x + dx, y + dy, z + dz])
                                    {
                                        allNeighborsTrue = false;
                                    }
                                }
                            }
                        }
                        eroded[x, y, z] = allNeighborsTrue;
                    }
                }
            });

            return eroded;
        }

        /// <summary>
        /// Optimized intracranial space finding using multiple seed points and early termination
        /// </summary>
        private static bool[,,] FindIntracranialSpaceOptimized(bool[,,] boneMask, int width, int height, int depth)
        {
            bool[,,] intracranialSpace = new bool[width, height, depth];
            bool[,,] visited = new bool[width, height, depth];

            // Use multiple seed points in central region
            List<Vector3Int> seedPoints = GenerateSeedPoints(width, height, depth);

            foreach (Vector3Int seed in seedPoints)
            {
                if (!visited[seed.x, seed.y, seed.z] && !boneMask[seed.x, seed.y, seed.z])
                {
                    FloodFillOptimized(intracranialSpace, visited, boneMask, seed, width, height, depth);
                }
            }

            return intracranialSpace;
        }

        /// <summary>
        /// Generate optimized seed points for flood fill
        /// </summary>
        private static List<Vector3Int> GenerateSeedPoints(int width, int height, int depth)
        {
            List<Vector3Int> seeds = new List<Vector3Int>();
            int centerX = width / 2;
            int centerY = height / 2;
            int centerZ = depth / 2;

            // Add center point
            seeds.Add(new Vector3Int(centerX, centerY, centerZ));

            // Add points in a 3x3x3 grid around center
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = Mathf.Clamp(centerX + dx * 5, 0, width - 1);
                        int y = Mathf.Clamp(centerY + dy * 5, 0, height - 1);
                        int z = Mathf.Clamp(centerZ + dz * 5, 0, depth - 1);
                        seeds.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            return seeds;
        }

        /// <summary>
        /// Optimized flood fill with early termination and iteration limits
        /// </summary>
        private static void FloodFillOptimized(bool[,,] result, bool[,,] visited, bool[,,] boneMask,
                                             Vector3Int start, int width, int height, int depth)
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            visited[start.x, start.y, start.z] = true;
            result[start.x, start.y, start.z] = true;

            int iterations = 0;
            int[] directions = { 1, -1, 0, 0, 0, 0, 0, 0, 1, -1, 1, -1, 1, -1, 1, -1, 1, -1 };
            int dirIndex = 0;

            while (queue.Count > 0 && iterations < MAX_ITERATIONS_FLOOD_FILL)
            {
                Vector3Int current = queue.Dequeue();

                // Check 6-connected neighbors
                for (int i = 0; i < 6; i++)
                {
                    int nx = current.x + directions[dirIndex++];
                    int ny = current.y + directions[dirIndex++];
                    int nz = current.z + directions[dirIndex++];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                    {
                        if (!visited[nx, ny, nz] && !boneMask[nx, ny, nz])
                        {
                            visited[nx, ny, nz] = true;
                            result[nx, ny, nz] = true;
                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                        }
                    }
                }

                dirIndex = 0;
                iterations++;
            }

            if (iterations >= MAX_ITERATIONS_FLOOD_FILL)
            {
                Debug.LogWarning($"[FloodFillOptimized] 达到最大迭代限制，可能存在不完整填充");
            }
        }

        /// <summary>
        /// Enhanced context filtering with advanced clinical rules
        /// </summary>
        private static bool[,,] ApplyEnhancedContextFiltering(bool[,,] sizeFiltered, int width, int height, int depth,
                                                            Dictionary<int, int> componentSizes)
        {
            bool[,,] contextFiltered = new bool[width, height, depth];
            Vector3 brainCenter = new Vector3(width / 2f, height / 2f, depth / 2f);

            // Group voxels by component
            Dictionary<int, List<Vector3Int>> components = new Dictionary<int, List<Vector3Int>>();
            int[,,] labels = new int[width, height, depth];

            // Simple labeling for component identification
            int currentLabel = 1;
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (sizeFiltered[x, y, z] && labels[x, y, z] == 0)
                        {
                            // Start new component
                            FloodFillLabel(labels, sizeFiltered, x, y, z, currentLabel, width, height, depth);
                            components[currentLabel] = new List<Vector3Int>();
                            currentLabel++;
                        }
                    }
                }
            }

            // Populate component voxel lists
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int label = labels[x, y, z];
                        if (label > 0 && components.ContainsKey(label))
                        {
                            components[label].Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            // Apply clinical filtering rules
            foreach (var kvp in components)
            {
                int componentId = kvp.Key;
                List<Vector3Int> voxels = kvp.Value;
                int voxelCount = voxels.Count;

                // Rule 1: Size limits
                if (voxelCount < MIN_INTRACRANIAL_VOXELS || voxelCount > MAX_REASONABLE_COMPONENT_SIZE)
                {
                    Debug.Log($"[EnhancedContextFiltering] 跳过异常大小组件 {componentId}: {voxelCount} 体素");
                    continue;
                }

                // Rule 2: Calculate shape features
                Vector3 centroid = CalculateCentroid(voxels);
                float distanceFromCenter = Vector3.Distance(centroid, brainCenter);
                float maxReasonableDistance = Mathf.Min(width, height, depth) / 3f;

                // Rule 3: Position constraints (avoid peripheral regions)
                if (distanceFromCenter > maxReasonableDistance)
                {
                    Debug.Log($"[EnhancedContextFiltering] 跳过位置异常组件 {componentId}: 距离中心 {distanceFromCenter:F1}");
                    continue;
                }

                // Rule 4: Shape constraints (avoid extremely elongated or irregular shapes)
                Bounds componentBounds = CalculateBounds(voxels);
                float compactness = CalculateCompactness(voxels, componentBounds);

                if (compactness < 0.1f) // Too irregular
                {
                    Debug.Log($"[EnhancedContextFiltering] 跳过不规则形状组件 {componentId}: 紧密度 {compactness:F3}");
                    continue;
                }

                // Rule 5: Clinical location preferences
                if (!IsInPreferredBrainRegion(centroid, width, height, depth))
                {
                    Debug.Log($"[EnhancedContextFiltering] 跳过非优先脑区组件 {componentId}: 位置 {centroid}");
                    continue;
                }

                // Component passed all filters
                foreach (Vector3Int voxel in voxels)
                {
                    contextFiltered[voxel.x, voxel.y, voxel.z] = true;
                }
            }

            Debug.Log($"[EnhancedContextFiltering] 保留了 {components.Count} 个符合临床规则的组件");
            return contextFiltered;
        }

        /// <summary>
        /// Simple flood fill for component labeling
        /// </summary>
        private static void FloodFillLabel(int[,,] labels, bool[,,] mask, int startX, int startY, int startZ,
                                         int label, int width, int height, int depth)
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(startX, startY, startZ));
            labels[startX, startY, startZ] = label;

            int[] directions = { 1, -1, 0, 0, 0, 0, 0, 0, 1, -1, 1, -1, 1, -1, 1, -1, 1, -1 };

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                int dirIndex = 0;

                for (int i = 0; i < 6; i++)
                {
                    int nx = current.x + directions[dirIndex++];
                    int ny = current.y + directions[dirIndex++];
                    int nz = current.z + directions[dirIndex++];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                    {
                        if (mask[nx, ny, nz] && labels[nx, ny, nz] == 0)
                        {
                            labels[nx, ny, nz] = label;
                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculate centroid of voxel list
        /// </summary>
        private static Vector3 CalculateCentroid(List<Vector3Int> voxels)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3Int voxel in voxels)
            {
                sum += new Vector3(voxel.x, voxel.y, voxel.z);
            }
            return sum / voxels.Count;
        }

        /// <summary>
        /// Calculate bounding box of voxel list
        /// </summary>
        private static Bounds CalculateBounds(List<Vector3Int> voxels)
        {
            if (voxels.Count == 0) return new Bounds();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Vector3Int voxel in voxels)
            {
                min = Vector3.Min(min, voxel);
                max = Vector3.Max(max, voxel);
            }

            return new Bounds((min + max) / 2f, max - min);
        }

        /// <summary>
        /// Calculate compactness (volume / surface area ratio)
        /// </summary>
        private static float CalculateCompactness(List<Vector3Int> voxels, Bounds bounds)
        {
            float volume = voxels.Count;
            float surfaceArea = bounds.size.x * bounds.size.y * 2 +
                              bounds.size.x * bounds.size.z * 2 +
                              bounds.size.y * bounds.size.z * 2;
            return surfaceArea > 0 ? volume / surfaceArea : 0;
        }

        /// <summary>
        /// Check if position is in preferred brain regions for hemorrhage
        /// </summary>
        private static bool IsInPreferredBrainRegion(Vector3 position, int width, int height, int depth)
        {
            // Define preferred regions (central 60% of each dimension)
            float marginX = width * 0.2f;
            float marginY = height * 0.2f;
            float marginZ = depth * 0.2f;

            return position.x >= marginX && position.x < width - marginX &&
                   position.y >= marginY && position.y < height - marginY &&
                   position.z >= marginZ && position.z < depth - marginZ;
        }
    }
}
