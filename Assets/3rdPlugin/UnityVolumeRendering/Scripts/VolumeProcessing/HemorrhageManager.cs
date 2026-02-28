using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;
using UnityVolumeRendering;
using UnityVolumeRendering.Segmentation;

namespace VolumeProcessing
{
    /// <summary>
    /// Comprehensive manager for hemorrhage detection and visualization in CT scans
    /// Supports both intracranial and extracranial hemorrhage detection
    /// </summary>
    public class HemorrhageManager : MonoBehaviour
    {
        [Header("Required References")]
        public VolumeRenderedObject volumeRenderer;
        [Tooltip("Bone mask dataset (required for intracranial hemorrhage detection)")]
        public VolumeDataset boneMaskDataset;

        [Header("Detection Parameters")]
        [Tooltip("Use new 3D Slicer-style segment/label system")]
        public bool useNewSegmentationSystem = true;

        [Tooltip("Body region for hemorrhage detection")]
        public BodyRegion bodyRegion = BodyRegion.Intracranial;

        [Tooltip("HU value scaling factor to compensate for 3D interpolation attenuation")]
        [Range(1.0f, 1.5f)]
        public float huScaleFactor = 1.2f;

        [Tooltip("HU offset for device calibration")]
        public float huOffset = 0f;

        [Tooltip("Minimum HU threshold for hemorrhage detection")]
        [Range(40f, 80f)]
        public float minHUThreshold = 55f;

        [Tooltip("Maximum HU threshold for hemorrhage detection")]
        [Range(80f, 120f)]
        public float maxHUThreshold = 90f;

        [Tooltip("Minimum connected voxel count to consider as hemorrhage")]
        [Range(1, 50)]
        public int minVoxelCount = 10;

        [Header("Visualization Parameters")]
        [Tooltip("Enable/disable hemorrhage visualization")]
        public bool visualizationEnabled = true;

        [Tooltip("Hemorrhage highlight color")]
        public Color hemorrhageColor = new Color(1.0f, 0.0f, 0.0f, 0.8f);

        [Tooltip("Intensity of hemorrhage highlighting")]
        [Range(0.1f, 1.0f)]
        public float hemorrhageIntensity = 0.8f;

        // Internal components
        private HemorrhageRenderer renderer;

        // Results
        private VolumeDataset hemorrhageMaskDataset;
        private bool hasHemorrhage = false;
        private Dictionary<string, float> hemorrhageStatistics;

        /// <summary>
        /// Body regions for different HU thresholds and constraints
        /// </summary>
        public enum BodyRegion
        {
            Intracranial,    // Brain (requires bone mask, spatial constraints)
            Thoracic,        // Chest (no spatial constraints, adjusted HU range)
            Abdominal,       // Abdomen (no spatial constraints, adjusted HU range)
            General         // Generic (no spatial constraints, wide HU range)
        }

        void Awake()
        {
            InitializeComponents();
            Debug.Log("初始化加载");
            // 移除自动检测，改为手动触发以提高性能
            // if (volumeRenderer != null && volumeRenderer.dataset != null)
            // {
            //     DetectAndVisualizeHemorrhage();
            // }
        }

        void OnValidate()
        {
            // Update thresholds based on body region
            UpdateThresholdsForBodyRegion();

            if (Application.isPlaying && volumeRenderer != null)
            {
                UpdateDetectionParameters();
                UpdateVisualizationParameters();
            }
        }

        /// <summary>
        /// Update HU thresholds based on selected body region
        /// </summary>
        private void UpdateThresholdsForBodyRegion()
        {
            switch (bodyRegion)
            {
                case BodyRegion.Intracranial:
                    // Brain hemorrhage: fresh blood 55-90 HU
                    if (minHUThreshold != 55f || maxHUThreshold != 90f)
                    {
                        minHUThreshold = 55f;
                        maxHUThreshold = 90f;
                    }
                    break;
                case BodyRegion.Thoracic:
                    // Chest: higher threshold to avoid soft tissue misdetection
                    // Aortic dissection/aneurysm rupture: 65-120 HU
                    if (minHUThreshold != 65f || maxHUThreshold != 120f)
                    {
                        minHUThreshold = 65f;
                        maxHUThreshold = 120f;
                    }
                    break;
                case BodyRegion.Abdominal:
                    // Abdomen: similar to thoracic but adjusted for organs
                    // Organ hemorrhage: 60-110 HU
                    if (minHUThreshold != 60f || maxHUThreshold != 110f)
                    {
                        minHUThreshold = 60f;
                        maxHUThreshold = 110f;
                    }
                    break;
                case BodyRegion.General:
                    // Generic: wide range for various hemorrhage types
                    if (minHUThreshold != 50f || maxHUThreshold != 130f)
                    {
                        minHUThreshold = 50f;
                        maxHUThreshold = 130f;
                    }
                    break;
            }
        }

        /// <summary>
        /// Initialize detector and renderer components
        /// </summary>
        private void InitializeComponents()
        {
            // Create renderer component if not exists
            renderer = GetComponent<HemorrhageRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<HemorrhageRenderer>();
            }

            // Set references
            renderer.volumeRenderer = volumeRenderer;
        }

        /// <summary>
        /// Perform hemorrhage detection and setup visualization
        /// </summary>
        public void DetectAndVisualizeHemorrhage()
        {
            DetectAndVisualizeHemorrhageAsync(null, null).Wait();
        }

        /// <summary>
        /// Synchronous version for Unity Editor (blocks UI but avoids threading issues)
        /// </summary>
        /// <param name="progressCallback">Progress callback</param>
        //public void DetectAndVisualizeHemorrhageSync(HemorrhageDetector.ProgressCallback progressCallback = null)
        //{
        //    if (volumeRenderer == null || volumeRenderer.dataset == null)
        //    {
        //        Debug.LogError("HemorrhageManager: Volume renderer or dataset not assigned");
        //        return;
        //    }
        //    Debug.Log($"开始{bodyRegion}区域出血检测...");

        //    if (useNewSegmentationSystem)
        //    {
        //        // Use new 3D Slicer-style segment/label system
        //        DetectUsingNewSystem(progressCallback);
        //    }
        //    else
        //    {
        //        // Use legacy detection method
        //        DetectUsingLegacySystem(progressCallback);
        //    }

        //    // Check results
        //    hasHemorrhage = HemorrhageDetector.HasHemorrhage(hemorrhageMaskDataset);
        //    hemorrhageStatistics = HemorrhageDetector.GetHemorrhageStatistics(hemorrhageMaskDataset);

        //    Debug.Log($"{bodyRegion}区域检测完成. 检测到出血: {hasHemorrhage}");
        //    if (hasHemorrhage)
        //    {
        //        Debug.Log($"出血体素: {hemorrhageStatistics["total_voxels"]}, 体积: {hemorrhageStatistics["volume_mm3"]:F2} mm³");
        //    }

        //    // Setup visualization
        //    renderer.SetHemorrhageMask(hemorrhageMaskDataset);
        //    renderer.SetHemorrhageOverlayEnabled(visualizationEnabled);
        //    renderer.SetHemorrhageColor(hemorrhageColor);
        //    renderer.SetHemorrhageIntensity(hemorrhageIntensity);
        //}

        /// <summary>
        /// Detect using new 3D Slicer-style segment/label system
        /// </summary>
        /// <param name="progressCallback">Progress callback</param>
        private void DetectUsingNewSystem(HemorrhageDetector.ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "Using new segmentation system...");

            // Create VolumeDerivationManager and build segment
            VolumeDerivationManager derivationManager = new VolumeDerivationManager();
            derivationManager.BuildFromCT(volumeRenderer.dataset);

            // Get the hemorrhage segment
            Segment hemorrhageSegment = derivationManager.hemorrhageSegment;
            if (hemorrhageSegment != null)
            {
                // Convert Segment mask data to VolumeDataset for compatibility with renderer
                hemorrhageMaskDataset = ConvertSegmentToVolumeDataset(hemorrhageSegment, volumeRenderer.dataset);

                // Set up mesh rendering if segment has mesh
                if (hemorrhageSegment.mesh != null)
                {
                    SetupHemorrhageMeshObject(hemorrhageSegment.mesh);
                }

                progressCallback?.Invoke(1.0f, "New segmentation system detection complete");
            }
            else
            {
                Debug.LogWarning("HemorrhageManager: No hemorrhage segment found in new system");
                hemorrhageMaskDataset = null;
            }
        }

        /// <summary>
        /// Detect using legacy system for backward compatibility
        /// </summary>
        /// <param name="progressCallback">Progress callback</param>
        private void DetectUsingLegacySystem(HemorrhageDetector.ProgressCallback progressCallback = null)
        {
            // Choose detection method based on body region
            if (bodyRegion == BodyRegion.Intracranial)
            {
                // Use improved intracranial detection if bone mask is available
                if (boneMaskDataset != null)
                {
                    hemorrhageMaskDataset = DetectIntracranialHemorrhageSync(progressCallback);
                }
                else
                {
                    Debug.LogWarning("HemorrhageManager: Bone mask dataset not assigned. Attempting automatic bone mask generation...");

                    // Try to auto-generate bone mask
                    try
                    {
                        boneMaskDataset = GenerateBoneMask(volumeRenderer.dataset);
                        Debug.Log("boneMaskDataset:" + boneMaskDataset);

                        Debug.Log("Successfully auto-generated bone mask for intracranial detection");

                        // Now proceed with intracranial detection using generated bone mask
                        hemorrhageMaskDataset = DetectIntracranialHemorrhageSync(progressCallback);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to auto-generate bone mask: {ex.Message}. Falling back to basic detection.");
                        // Fallback to fast detection without spatial constraints
                        hemorrhageMaskDataset = HemorrhageDetector.DetectHemorrhage(
                            volumeRenderer.dataset,
                            null, // No bone mask
                            minHUThreshold,
                            maxHUThreshold,
                            minVoxelCount);
                    }
                }
            }
            else
            {
                // Use fast detection without spatial constraints for other regions
                hemorrhageMaskDataset = HemorrhageDetector.DetectHemorrhageFast(
                    volumeRenderer.dataset,
                    minHUThreshold,
                    maxHUThreshold,
                    minVoxelCount);
            }
        }

        /// <summary>
        /// Synchronous intracranial hemorrhage detection for editor
        /// </summary>
        private VolumeDataset DetectIntracranialHemorrhageSync(HemorrhageDetector.ProgressCallback progressCallback = null)
        {
            progressCallback?.Invoke(0.0f, "准备改进版颅内出血检测...");

            // Create 3D arrays from volume data
            VolumeDataset dataset = volumeRenderer.dataset;
            int width = dataset.dimX;
            int height = dataset.dimY;
            int depth = dataset.dimZ;

            progressCallback?.Invoke(0.1f, "转换数据格式...");

            // Convert 1D arrays to 3D arrays
            float[,,] huData = new float[width, height, depth];
            bool[,,] boneMask = new bool[width, height, depth];
            bool[,,] intracranialMask = new bool[width, height, depth];

            // Fill HU data
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * width * height;
                        huData[x, y, z] = dataset.data[index];
                    }
                }
            }

            // Fill bone mask
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * width * height;
                        boneMask[x, y, z] = boneMaskDataset.data[index] > 0.5f;
                    }
                }
            }

            // Create intracranial mask based on HU values (as per 3D Slicer approach)
            // Intracranial space: HU between -100 and 100 (brain tissue and CSF)
            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * width * height;
                        float huValue = dataset.data[index];
                        // Basic intracranial mask: HU between -100 and 100 (brain tissue, CSF, blood)
                        intracranialMask[x, y, z] = (huValue > -100f && huValue < 100f);
                    }
                }
            }

            // Create input structure
            HemorrhageDetectionInput input = new HemorrhageDetectionInput(
                huData, boneMask, intracranialMask, dataset.scale, width, height, depth);

            progressCallback?.Invoke(0.2f, "开始检测流程...");

            // Run improved detection
            bool[,,] hemorrhageMask = ImprovedHemorrhageDetector.DetectIntracranialHemorrhage(input, progressCallback);

            progressCallback?.Invoke(0.95f, "创建结果数据集...");

            // Convert back to float array
            float[] maskData = ImprovedHemorrhageDetector.MaskToFloatArray(hemorrhageMask, width, height, depth);

            // Create VolumeDataset
            return HemorrhageDetector.CreateHemorrhageMaskDataset(maskData, width, height, depth, dataset.scale);
        }

        /// <summary>
        /// Asynchronously perform hemorrhage detection and setup visualization
        /// </summary>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        public async System.Threading.Tasks.Task DetectAndVisualizeHemorrhageAsync(
            HemorrhageDetector.ProgressCallback progressCallback = null,
            System.Threading.CancellationToken? cancellationToken = null)
        {
            if (volumeRenderer == null || volumeRenderer.dataset == null)
            {
                Debug.LogError("HemorrhageManager: Volume renderer or dataset not assigned");
                return;
            }
            Debug.Log($"开始{bodyRegion}区域出血检测...");

            // Choose detection method based on body region
            if (bodyRegion == BodyRegion.Intracranial)
            {
                // Use improved intracranial detection if bone mask is available
                if (boneMaskDataset != null)
                {
                    hemorrhageMaskDataset = await DetectIntracranialHemorrhageImprovedAsync(
                        progressCallback, cancellationToken);
                }
                else
                {
                    Debug.LogWarning("HemorrhageManager: Bone mask dataset not assigned. Attempting automatic bone mask generation...");

                    // Try to auto-generate bone mask
                    try
                    {
                        boneMaskDataset = GenerateBoneMask(volumeRenderer.dataset);
                        Debug.Log("boneMaskDataset:" + boneMaskDataset);
                        Debug.Log("Successfully auto-generated bone mask for intracranial detection");

                        // Now proceed with intracranial detection using generated bone mask
                        hemorrhageMaskDataset = await DetectIntracranialHemorrhageImprovedAsync(
                            progressCallback, cancellationToken);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to auto-generate bone mask: {ex.Message}. Falling back to basic detection.");
                        // Fallback to fast detection without spatial constraints
                        hemorrhageMaskDataset = await HemorrhageDetector.DetectHemorrhageFastExtracranialAsync(
                            volumeRenderer.dataset,
                            bodyRegion,
                            minHUThreshold,
                            maxHUThreshold,
                            minVoxelCount,
                            progressCallback,
                            cancellationToken
                        );
                    }
                }
            }
            else
            {
                // Use fast detection without spatial constraints for other regions
                hemorrhageMaskDataset = await HemorrhageDetector.DetectHemorrhageFastExtracranialAsync(
                    volumeRenderer.dataset,
                    bodyRegion,
                    minHUThreshold,
                    maxHUThreshold,
                    minVoxelCount,
                    progressCallback,
                    cancellationToken
                );
            }

            // Check results
            hasHemorrhage = HemorrhageDetector.HasHemorrhage(hemorrhageMaskDataset);
            hemorrhageStatistics = HemorrhageDetector.GetHemorrhageStatistics(hemorrhageMaskDataset);

            Debug.Log($"{bodyRegion}区域检测完成. 检测到出血: {hasHemorrhage}");
            if (hasHemorrhage)
            {
                Debug.Log($"出血体素: {hemorrhageStatistics["total_voxels"]}, 体积: {hemorrhageStatistics["volume_mm3"]:F2} mm³");
            }

            // Setup visualization
            renderer.SetHemorrhageMask(hemorrhageMaskDataset);
            renderer.SetHemorrhageOverlayEnabled(visualizationEnabled);
            renderer.SetHemorrhageColor(hemorrhageColor);
            renderer.SetHemorrhageIntensity(hemorrhageIntensity);
        }

        /// <summary>
        /// Perform improved intracranial hemorrhage detection using modular architecture
        /// </summary>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        private async System.Threading.Tasks.Task<VolumeDataset> DetectIntracranialHemorrhageImprovedAsync(
            HemorrhageDetector.ProgressCallback progressCallback = null,
            System.Threading.CancellationToken? cancellationToken = null)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    progressCallback?.Invoke(0.0f, "准备改进版颅内出血检测...");

                    // Create 3D arrays from volume data
                    VolumeDataset dataset = volumeRenderer.dataset;
                    int width = dataset.dimX;
                    int height = dataset.dimY;
                    int depth = dataset.dimZ;

                    progressCallback?.Invoke(0.1f, "转换数据格式...");

                    // Convert 1D arrays to 3D arrays
                    float[,,] huData = new float[width, height, depth];
                    bool[,,] boneMask = new bool[width, height, depth];
                    bool[,,] intracranialMask = new bool[width, height, depth];

                    // Fill HU data
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = x + y * width + z * width * height;
                                huData[x, y, z] = dataset.data[index];
                            }
                        }
                    }

                    // Fill bone mask
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = x + y * width + z * width * height;
                                boneMask[x, y, z] = boneMaskDataset.data[index] > 0.5f;
                            }
                        }
                    }

                    // Create intracranial mask based on HU values (as per 3D Slicer approach)
                    // Intracranial space: HU between -100 and 100 (brain tissue and CSF)
                    for (int z = 0; z < depth; z++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int index = x + y * width + z * width * height;
                                float huValue = dataset.data[index];
                                // Basic intracranial mask: HU between -100 and 100 (brain tissue, CSF, blood)
                                intracranialMask[x, y, z] = (huValue > -100f && huValue < 100f);
                            }
                        }
                    }

                    // Create input structure
                    HemorrhageDetectionInput input = new HemorrhageDetectionInput(
                        huData, boneMask, intracranialMask, dataset.scale, width, height, depth);

                    progressCallback?.Invoke(0.2f, "开始检测流程...");

                    // Run improved detection
                    bool[,,] hemorrhageMask = ImprovedHemorrhageDetector.DetectIntracranialHemorrhage(input, progressCallback);

                    progressCallback?.Invoke(0.95f, "创建结果数据集...");

                    // Convert back to float array
                    float[] maskData = ImprovedHemorrhageDetector.MaskToFloatArray(hemorrhageMask, width, height, depth);

                    // Create VolumeDataset
                    return HemorrhageDetector.CreateHemorrhageMaskDataset(maskData, width, height, depth, dataset.scale);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"改进版颅内出血检测失败: {ex.Message}");
                    // Fallback to legacy method
                    return HemorrhageDetector.DetectHemorrhageAsync(
                        volumeRenderer.dataset,
                        boneMaskDataset,
                        minHUThreshold,
                        maxHUThreshold,
                        minVoxelCount,
                        progressCallback,
                        cancellationToken).Result;
                }
            });
        }

        /// <summary>
        /// Update detection parameters and re-run detection if needed
        /// </summary>
        private void UpdateDetectionParameters()
        {
            if (hemorrhageMaskDataset != null && Application.isPlaying)
            {
                DetectAndVisualizeHemorrhage();
            }
        }

        /// <summary>
        /// Update visualization parameters
        /// </summary>
        private void UpdateVisualizationParameters()
        {
            if (renderer != null)
            {
                renderer.SetHemorrhageOverlayEnabled(visualizationEnabled);
                renderer.SetHemorrhageColor(hemorrhageColor);
                renderer.SetHemorrhageIntensity(hemorrhageIntensity);
            }
        }

        /// <summary>
        /// Check if hemorrhage is present
        /// </summary>
        /// <returns>True if hemorrhage is detected</returns>
        public bool HasHemorrhage()
        {
            return hasHemorrhage;
        }

        /// <summary>
        /// Get hemorrhage statistics
        /// </summary>
        /// <returns>Dictionary containing statistics like total_voxels and volume_mm3</returns>
        public Dictionary<string, float> GetHemorrhageStatistics()
        {
            return hemorrhageStatistics;
        }

        /// <summary>
        /// Set detection parameters
        /// </summary>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxels">Minimum voxel count</param>
        public void SetDetectionParameters(float minHU, float maxHU, int minVoxels)
        {
            minHUThreshold = minHU;
            maxHUThreshold = maxHU;
            minVoxelCount = minVoxels;

            if (Application.isPlaying)
            {
                DetectAndVisualizeHemorrhage();
            }
        }

        /// <summary>
        /// Set body region for detection
        /// </summary>
        /// <param name="region">Body region</param>
        public void SetBodyRegion(BodyRegion region)
        {
            bodyRegion = region;
            UpdateThresholdsForBodyRegion();

            if (Application.isPlaying)
            {
                DetectAndVisualizeHemorrhage();
            }
        }

        /// <summary>
        /// Set visualization parameters
        /// </summary>
        /// <param name="enabled">Enable/disable visualization</param>
        /// <param name="color">Hemorrhage color</param>
        /// <param name="intensity">Highlight intensity</param>
        public void SetVisualizationParameters(bool enabled, Color color, float intensity)
        {
            visualizationEnabled = enabled;
            hemorrhageColor = color;
            hemorrhageIntensity = intensity;

            UpdateVisualizationParameters();
        }

        /// <summary>
        /// Enable mesh-based hematoma rendering instead of volume overlay
        /// </summary>
        /// <param name="enableMesh">Whether to use mesh rendering</param>
        public void EnableMeshRendering(bool enableMesh)
        {
            if (renderer != null)
            {
                renderer.SetMeshRenderingEnabled(enableMesh);
                renderer.SetHemorrhageOverlayEnabled(!enableMesh); // Disable overlay when mesh is enabled
            }
        }

        /// <summary>
        /// Force re-detection of hemorrhage
        /// </summary>
        public void RedetectHemorrhage()
        {
            DetectAndVisualizeHemorrhage();
        }

        /// <summary>
        /// Toggle hemorrhage visualization on/off
        /// </summary>
        public void ToggleVisualization()
        {
            visualizationEnabled = !visualizationEnabled;
            UpdateVisualizationParameters();
        }

        /// <summary>
        /// Get the hemorrhage mask dataset for external use
        /// </summary>
        /// <returns>Hemorrhage mask dataset</returns>
        public VolumeDataset GetHemorrhageMask()
        {
            return hemorrhageMaskDataset;
        }

        /// <summary>
        /// Pure computation method for hemorrhage detection (no Unity API calls)
        /// Returns raw byte array mask for threading safety
        /// </summary>
        /// <param name="originalDataset">Original CT volume dataset</param>
        /// <param name="boneMaskDataset">Bone mask dataset (required for intracranial)</param>
        /// <param name="minHU">Minimum HU threshold</param>
        /// <param name="maxHU">Maximum HU threshold</param>
        /// <param name="minVoxelCount">Minimum connected voxel count</param>
        /// <returns>Byte array mask (0=non-hemorrhage, 1=hemorrhage)</returns>
        public byte[] DetectHemorrhageRaw(
            VolumeDataset originalDataset,
            VolumeDataset boneMaskDataset,
            float minHU = 50f,
            float maxHU = 90f,
            int minVoxelCount = 100)
        {
            // Apply volume spacing considerations for anisotropic voxels
            // CT scans typically have anisotropic spacing (e.g., 0.5mm × 0.5mm × 3mm)
            Vector3 volumeSpacing = originalDataset.scale;
            Debug.Log($"体素间距: X={volumeSpacing.x:F3}mm, Y={volumeSpacing.y:F3}mm, Z={volumeSpacing.z:F3}mm (各向异性系数: {volumeSpacing.z / volumeSpacing.x:F2})");

            // Adjust minimum voxel count based on anisotropic spacing
            // For thick slices (Z > 1mm), reduce minimum count proportionally
            float anisotropyFactor = volumeSpacing.z / Mathf.Min(volumeSpacing.x, volumeSpacing.y);
            if (anisotropyFactor > 2.0f)
            {
                int adjustedMinVoxels = Mathf.RoundToInt(minVoxelCount / anisotropyFactor);
                Debug.Log($"各向异性体素调整: 最小体素数 {minVoxelCount} -> {adjustedMinVoxels} (各向异性系数: {anisotropyFactor:F2})");
                minVoxelCount = Mathf.Max(adjustedMinVoxels, 5); // Minimum 5 voxels
            }

            // Apply HU scaling correction for 3D reconstruction
            minHU *= huScaleFactor;
            maxHU *= huScaleFactor;

            // Apply HU offset for device calibration
            minHU += huOffset;
            maxHU += huOffset;

            Debug.Log($"应用HU校正 - 缩放因子: {huScaleFactor}, 偏移: {huOffset}, 校正后范围: [{minHU:F1}, {maxHU:F1}] HU");

            if (originalDataset == null)
                throw new ArgumentNullException(nameof(originalDataset));

            int width = originalDataset.dimX;
            int height = originalDataset.dimY;
            int depth = originalDataset.dimZ;
            int totalVoxels = width * height * depth;

            // Validate HU range first
            float minHUValue = float.MaxValue;
            float maxHUValue = float.MinValue;
            for (int i = 0; i < totalVoxels; i++)
            {
                float val = originalDataset.data[i];
                minHUValue = Mathf.Min(minHUValue, val);
                maxHUValue = Mathf.Max(maxHUValue, val);
            }
            Debug.Log($"HU range validation: {minHUValue:F1} ~ {maxHUValue:F1} HU");

            if (maxHUValue < 100)
            {
                Debug.LogWarning("HU values appear to be non-standard (normalized or windowed). Detection may not work correctly.");
            }

            byte[] mask = new byte[totalVoxels];

            if (bodyRegion == BodyRegion.Intracranial)
            {
                // Intracranial detection requires bone mask
                if (boneMaskDataset == null)
                    throw new ArgumentNullException(nameof(boneMaskDataset), "Bone mask dataset is required for intracranial hemorrhage detection");

                Debug.Log("开始颅内出血检测 - 使用正确的封闭骨腔提取方法");

                // Step 1: Create bone mask with conservative threshold (as per optimization guidelines)
                bool[,,] boneMask = new bool[width, height, depth];
                for (int z = 0; z < depth; z++)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * width * height;
                            boneMask[x, y, z] = boneMaskDataset.data[index] > 0.3f; // Conservative threshold as per optimization docs
                        }

                // Step 2: Flood fill from boundaries to find outside air (only through true air, not intracranial air)
                bool[,,] outsideAir = FloodFillFromBoundary(boneMask, originalDataset.data, width, height, depth);

                // Step 3: Create proper intracranial mask = non-bone AND non-outside-air AND not padding
                bool[,,] intracranialMask = new bool[width, height, depth];
                for (int z = 0; z < depth; z++)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * width * height;
                            float huValue = originalDataset.data[index];

                            // Filter out padding voxels (typically HU < -2000)
                            bool isPadding = huValue < -2000f;

                            intracranialMask[x, y, z] = !boneMask[x, y, z] &&
                                                       !outsideAir[x, y, z] &&
                                                       !isPadding;
                        }

                // Step 4: Keep only the largest connected component (main brain cavity)
                intracranialMask = KeepLargestConnectedComponent(intracranialMask, width, height, depth);

                // Step 5: Create blood candidates within intracranial space
                bool[,,] bloodCandidates = new bool[width, height, depth];
                int candidateCount = 0;
                for (int z = 0; z < depth; z++)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * width * height;
                            float huValue = originalDataset.data[index];
                            bloodCandidates[x, y, z] = intracranialMask[x, y, z] &&
                                                       (huValue >= minHU && huValue <= maxHU);
                            if (bloodCandidates[x, y, z]) candidateCount++;
                        }

                Debug.Log($"血肿候选体素: {candidateCount}");

                // Step 6: Apply connected component analysis and select valid hemorrhages
                var (labels, componentSizes) = PerformConnectedComponentAnalysis(bloodCandidates, width, height, depth);

                // Debug: Log component information
                Debug.Log($"找到 {componentSizes.Count} 个连通域:");
                foreach (var kvp in componentSizes)
                {
                    int label = kvp.Key;
                    int size = kvp.Value;
                    // Debug.Log($"连通域 {label}: {size} 体素");
                }

                // Step 7: Filter and create final mask
                bool[,,] finalMask = FilterHemorrhageComponents(labels, componentSizes, width, height, depth, minVoxelCount);

                // Step 8: Apply morphological processing (closing operation) as per 3D reconstruction guidelines
                finalMask = ApplyMorphologicalClosing(finalMask, width, height, depth);

                // Convert 3D mask to 1D byte array
                for (int z = 0; z < depth; z++)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int index = x + y * width + z * width * height;
                            mask[index] = (byte)(finalMask[x, y, z] ? 1 : 0);
                        }
            }
            else
            {
                // Extracranial detection - simpler HU thresholding
                for (int i = 0; i < totalVoxels; i++)
                {
                    float huValue = originalDataset.data[i];
                    mask[i] = (byte)((huValue >= minHU && huValue <= maxHU) ? 1 : 0);
                }

                // Apply connected component filtering if needed
                if (minVoxelCount > 1)
                {
                    mask = ApplyConnectedComponentFilter(mask, width, height, depth, minVoxelCount);
                }
            }

            return mask;
        }

        /// <summary>
        /// Flood fill from volume boundaries to identify outside air (only through true air regions)
        /// </summary>
        private bool[,,] FloodFillFromBoundary(bool[,,] boneMask, float[] huData, int width, int height, int depth)
        {
            bool[,,] outsideAir = new bool[width, height, depth];
            bool[,,] visited = new bool[width, height, depth];
            Queue<Vector3Int> queue = new Queue<Vector3Int>();

            // Start from all 6 boundary faces - but only enqueue true air voxels
            // Front and back faces (z = 0 and z = depth-1)
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Front face (z = 0)
                    int frontIndex = x + y * width + 0 * width * height;
                    bool isTrueAirFront = !boneMask[x, y, 0] && huData[frontIndex] < -500f; // True air threshold
                    if (isTrueAirFront && !visited[x, y, 0])
                    {
                        queue.Enqueue(new Vector3Int(x, y, 0));
                        visited[x, y, 0] = true;
                    }

                    // Back face (z = depth-1)
                    int backIndex = x + y * width + (depth-1) * width * height;
                    bool isTrueAirBack = !boneMask[x, y, depth-1] && huData[backIndex] < -500f;
                    if (isTrueAirBack && !visited[x, y, depth-1])
                    {
                        queue.Enqueue(new Vector3Int(x, y, depth-1));
                        visited[x, y, depth-1] = true;
                    }
                }

            // Left and right faces (x = 0 and x = width-1)
            for (int z = 0; z < depth; z++)
                for (int y = 0; y < height; y++)
                {
                    // Left face (x = 0)
                    int leftIndex = 0 + y * width + z * width * height;
                    bool isTrueAirLeft = !boneMask[0, y, z] && huData[leftIndex] < -500f;
                    if (isTrueAirLeft && !visited[0, y, z])
                    {
                        queue.Enqueue(new Vector3Int(0, y, z));
                        visited[0, y, z] = true;
                    }

                    // Right face (x = width-1)
                    int rightIndex = (width-1) + y * width + z * width * height;
                    bool isTrueAirRight = !boneMask[width-1, y, z] && huData[rightIndex] < -500f;
                    if (isTrueAirRight && !visited[width-1, y, z])
                    {
                        queue.Enqueue(new Vector3Int(width-1, y, z));
                        visited[width-1, y, z] = true;
                    }
                }

            // Top and bottom faces (y = 0 and y = height-1)
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    // Top face (y = 0)
                    int topIndex = x + 0 * width + z * width * height;
                    bool isTrueAirTop = !boneMask[x, 0, z] && huData[topIndex] < -500f;
                    if (isTrueAirTop && !visited[x, 0, z])
                    {
                        queue.Enqueue(new Vector3Int(x, 0, z));
                        visited[x, 0, z] = true;
                    }

                    // Bottom face (y = height-1)
                    int bottomIndex = x + (height-1) * width + z * width * height;
                    bool isTrueAirBottom = !boneMask[x, height-1, z] && huData[bottomIndex] < -500f;
                    if (isTrueAirBottom && !visited[x, height-1, z])
                    {
                        queue.Enqueue(new Vector3Int(x, height-1, z));
                        visited[x, height-1, z] = true;
                    }
                }

            // Flood fill through true air regions only (HU < -500)
            int[] dx = { -1, 1, 0, 0, 0, 0 };
            int[] dy = { 0, 0, -1, 1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, -1, 1 };

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                outsideAir[current.x, current.y, current.z] = true;

                for (int d = 0; d < 6; d++)
                {
                    int nx = current.x + dx[d];
                    int ny = current.y + dy[d];
                    int nz = current.z + dz[d];

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                    {
                        int nIndex = nx + ny * width + nz * width * height;
                        // Only flood through regions that are NOT bone AND are true air (HU < -500)
                        bool isTrueAirNeighbor = !boneMask[nx, ny, nz] && huData[nIndex] < -500f;

                        if (isTrueAirNeighbor && !visited[nx, ny, nz])
                        {
                            visited[nx, ny, nz] = true;
                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                        }
                    }
                }
            }

            return outsideAir;
        }

        /// <summary>
        /// Keep only the largest connected component in the mask
        /// </summary>
        private bool[,,] KeepLargestConnectedComponent(bool[,,] mask, int width, int height, int depth)
        {
            var (labels, componentSizes) = PerformConnectedComponentAnalysis(mask, width, height, depth);

            if (componentSizes.Count == 0)
                return new bool[width, height, depth]; // Empty mask

            // Find largest component
            int largestLabel = -1;
            int maxSize = 0;
            foreach (var kvp in componentSizes)
            {
                if (kvp.Value > maxSize)
                {
                    maxSize = kvp.Value;
                    largestLabel = kvp.Key;
                }
            }

            // Create new mask with only the largest component
            bool[,,] filteredMask = new bool[width, height, depth];
            for (int z = 0; z < depth; z++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (labels[x, y, z] == largestLabel)
                        {
                            filteredMask[x, y, z] = true;
                        }
                    }

            Debug.Log($"保留最大连通域: {maxSize} 体素 (共 {componentSizes.Count} 个连通域)");
            return filteredMask;
        }

        /// <summary>
        /// Perform 3D connected component analysis
        /// </summary>
        private (int[,,], Dictionary<int, int>) PerformConnectedComponentAnalysis(bool[,,] mask, int width, int height, int depth)
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
                        if (mask[x, y, z] && labels[x, y, z] == 0)
                        {
                            int size = FloodFillLabel(labels, mask, x, y, z, currentLabel, width, height, depth, dx, dy, dz);
                            componentSizes[currentLabel] = size;
                            currentLabel++;
                        }
                    }
                }
            }

            return (labels, componentSizes);
        }

        /// <summary>
        /// Flood fill for component labeling
        /// </summary>
        private int FloodFillLabel(int[,,] labels, bool[,,] mask, int startX, int startY, int startZ, int label,
                                  int width, int height, int depth, int[] dx, int[] dy, int[] dz)
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(startX, startY, startZ));
            labels[startX, startY, startZ] = label;
            int size = 0;

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                size++;

                for (int i = 0; i < 6; i++)
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

            return size;
        }

        /// <summary>
        /// Filter hemorrhage components based on size and other criteria
        /// </summary>
        private bool[,,] FilterHemorrhageComponents(int[,,] labels, Dictionary<int, int> componentSizes,
                                                   int width, int height, int depth, int minVoxelCount)
        {
            bool[,,] filteredMask = new bool[width, height, depth];

            foreach (var kvp in componentSizes)
            {
                int label = kvp.Key;
                int size = kvp.Value;

                // Basic size filter
                if (size >= minVoxelCount)
                {
                    // Mark all voxels of this component
                    for (int z = 0; z < depth; z++)
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                            {
                                if (labels[x, y, z] == label)
                                {
                                    filteredMask[x, y, z] = true;
                                }
                            }
                }
            }

            return filteredMask;
        }

        /// <summary>
        /// Apply morphological closing operation (dilation followed by erosion) to fill small holes
        /// as per 3D reconstruction guidelines for hematoma segmentation
        /// </summary>
        private bool[,,] ApplyMorphologicalClosing(bool[,,] mask, int width, int height, int depth)
        {
            Debug.Log("Applying morphological closing operation to fill small holes in hematoma mask");

            // Step 1: Dilation (expand regions)
            bool[,,] dilatedMask = ApplyMorphologicalDilation(mask, width, height, depth);

            // Step 2: Erosion (shrink back to original size, filling holes)
            bool[,,] closedMask = ApplyMorphologicalErosion(dilatedMask, width, height, depth);

            int originalVoxels = CountVoxels(mask);
            int closedVoxels = CountVoxels(closedMask);
            Debug.Log($"Morphological closing: {originalVoxels} -> {closedVoxels} voxels (filled {closedVoxels - originalVoxels} holes)");

            return closedMask;
        }

        /// <summary>
        /// Apply morphological dilation (expand regions by 1 voxel in all directions)
        /// </summary>
        private bool[,,] ApplyMorphologicalDilation(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] dilatedMask = new bool[width, height, depth];

            int[] dx = { -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1 };
            int[] dz = { -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int z = 0; z < depth; z++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (mask[x, y, z])
                        {
                            // Mark the center voxel
                            dilatedMask[x, y, z] = true;

                            // Mark all 26 neighbors (if within bounds)
                            for (int d = 0; d < 27; d++)
                            {
                                int nx = x + dx[d];
                                int ny = y + dy[d];
                                int nz = z + dz[d];

                                if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                                {
                                    dilatedMask[nx, ny, nz] = true;
                                }
                            }
                        }
                    }

            return dilatedMask;
        }

        /// <summary>
        /// Apply morphological erosion (shrink regions by 1 voxel in all directions)
        /// </summary>
        private bool[,,] ApplyMorphologicalErosion(bool[,,] mask, int width, int height, int depth)
        {
            bool[,,] erodedMask = new bool[width, height, depth];

            int[] dx = { -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1, -1, -1, -1, 0, 0, 0, 1, 1, 1 };
            int[] dz = { -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int z = 0; z < depth; z++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        // Check if all 27 voxels in the 3x3x3 neighborhood are true
                        bool allNeighborsTrue = true;

                        for (int d = 0; d < 27; d++)
                        {
                            int nx = x + dx[d];
                            int ny = y + dy[d];
                            int nz = z + dz[d];

                            if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                            {
                                if (!mask[nx, ny, nz])
                                {
                                    allNeighborsTrue = false;
                                    break;
                                }
                            }
                        }

                        if (allNeighborsTrue)
                        {
                            erodedMask[x, y, z] = true;
                        }
                    }

            return erodedMask;
        }

        /// <summary>
        /// Count the number of true voxels in a 3D mask
        /// </summary>
        private int CountVoxels(bool[,,] mask)
        {
            int count = 0;
            for (int z = 0; z < mask.GetLength(2); z++)
                for (int y = 0; y < mask.GetLength(1); y++)
                    for (int x = 0; x < mask.GetLength(0); x++)
                    {
                        if (mask[x, y, z]) count++;
                    }
            return count;
        }

        /// <summary>
        /// Create VolumeDataset and set up visualization from raw mask data
        /// Must be called from main thread
        /// </summary>
        /// <param name="hemorrhageMask">Raw byte array mask from DetectHemorrhageRaw</param>
        public void VisualizeHemorrhageFromMask(byte[] hemorrhageMask)
        {
            if (hemorrhageMask == null)
                throw new ArgumentNullException(nameof(hemorrhageMask));

            Debug.Log("开始从mask数据创建可视化...");

            try
            {
                VolumeDataset dataset = volumeRenderer.dataset;
                int width = dataset.dimX;
                int height = dataset.dimY;
                int depth = dataset.dimZ;

                Debug.Log($"处理数据集尺寸: {width}x{height}x{depth}, 数据量: {hemorrhageMask.Length}");

                // Convert byte mask to float array for VolumeDataset
                float[] maskData = new float[hemorrhageMask.Length];
                int hemorrhageVoxelCount = 0;
                for (int i = 0; i < hemorrhageMask.Length; i++)
                {
                    maskData[i] = hemorrhageMask[i];
                    if (hemorrhageMask[i] > 0) hemorrhageVoxelCount++;
                }

                Debug.Log($"出血体素计数: {hemorrhageVoxelCount}");

                // Create VolumeDataset - this is a potentially expensive operation
                Debug.Log("创建VolumeDataset...");
                hemorrhageMaskDataset = HemorrhageDetector.CreateHemorrhageMaskDataset(
                    maskData, width, height, depth, dataset.scale);

                // Check results
                hasHemorrhage = HemorrhageDetector.HasHemorrhage(hemorrhageMaskDataset);
                hemorrhageStatistics = HemorrhageDetector.GetHemorrhageStatistics(hemorrhageMaskDataset);

                Debug.Log($"检测结果: 有出血={hasHemorrhage}, 体素数={hemorrhageStatistics["total_voxels"]}, 体积={hemorrhageStatistics["volume_mm3"]:F2} mm³");

                // Defer heavy rendering operations to avoid blocking the UI
                Debug.Log("延迟设置可视化...");
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () => {
                    try {
                        Debug.Log("开始设置渲染器...");
                        renderer.SetHemorrhageMask(hemorrhageMaskDataset);
                        renderer.SetHemorrhageOverlayEnabled(visualizationEnabled);
                        renderer.SetHemorrhageColor(hemorrhageColor);
                        renderer.SetHemorrhageIntensity(hemorrhageIntensity);
                        Debug.Log("可视化设置完成");
                    } catch (System.Exception ex) {
                        Debug.LogError($"可视化设置失败: {ex.Message}");
                    }
                };
                #else
                // In runtime, set immediately
                renderer.SetHemorrhageMask(hemorrhageMaskDataset);
                renderer.SetHemorrhageOverlayEnabled(visualizationEnabled);
                renderer.SetHemorrhageColor(hemorrhageColor);
                renderer.SetHemorrhageIntensity(hemorrhageIntensity);
                #endif

                Debug.Log("VisualizeHemorrhageFromMask完成");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"VisualizeHemorrhageFromMask失败: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Generate bone mask from CT dataset using HU thresholding
        /// Always returns a valid VolumeDataset, even in error cases (returns empty mask)
        /// </summary>
        /// <param name="originalDataset">Original CT dataset</param>
        /// <returns>Bone mask VolumeDataset (never null)</returns>
        public VolumeDataset GenerateBoneMask(VolumeDataset originalDataset)
        {
            Debug.Log($"生成骨骼掩码中... originalDataset: {(originalDataset != null ? "有效" : "null")}");

            // Handle null input - return empty dataset
            if (originalDataset == null)
            {
                Debug.LogError("GenerateBoneMask: 输入数据集为null，返回空骨骼遮罩");
                return CreateEmptyBoneMask();
            }

            int width = originalDataset.dimX;
            int height = originalDataset.dimY;
            int depth = originalDataset.dimZ;
            int totalVoxels = width * height * depth;

            Debug.Log($"数据集尺寸: {width}x{height}x{depth}, 总体素数: {totalVoxels}");

            // Handle invalid dimensions
            if (totalVoxels == 0 || width <= 0 || height <= 0 || depth <= 0)
            {
                Debug.LogError($"GenerateBoneMask: 无效数据集尺寸 {width}x{height}x{depth}，返回空骨骼遮罩");
                return CreateEmptyBoneMask();
            }

            // Handle invalid data
            if (originalDataset.data == null || originalDataset.data.Length == 0)
            {
                Debug.LogError("GenerateBoneMask: 数据集数据为空，返回空骨骼遮罩");
                return CreateEmptyBoneMask();
            }

            try
            {
                // Bone typically has HU > 200
                float[] boneMaskData = new float[totalVoxels];
                int boneVoxelCount = 0;

                // Safe data processing with bounds checking
                int dataLength = Mathf.Min(originalDataset.data.Length, totalVoxels);
                for (int i = 0; i < dataLength; i++)
                {
                    float huValue = originalDataset.data[i];
                    bool isBone = huValue > 200f;
                    boneMaskData[i] = isBone ? 1.0f : 0.0f;
                    if (isBone) boneVoxelCount++;
                }

                float bonePercentage = totalVoxels > 0 ? (float)boneVoxelCount / totalVoxels * 100f : 0f;
                Debug.Log($"骨骼体素统计: {boneVoxelCount}/{totalVoxels} ({bonePercentage:F2}%)");

                // Create VolumeDataset - this should never fail now
                //VolumeDataset boneMaskDataset = HemorrhageDetector.CreateHemorrhageMaskDataset(
                //    boneMaskData, width, height, depth, originalDataset.scale);
                boneMaskDataset = HemorrhageDetector.CreateHemorrhageMaskDataset(
                   boneMaskData, width, height, depth, originalDataset.scale);
                // Additional safety check
                if (boneMaskDataset == null)
                {
                    Debug.LogError("CreateHemorrhageMaskDataset 返回了 null，使用空遮罩作为后备");
                    return CreateEmptyBoneMask();
                }

                Debug.Log($"骨骼遮罩生成成功: {boneMaskDataset.dimX}x{boneMaskDataset.dimY}x{boneMaskDataset.dimZ}");
                //赋值
           
                return boneMaskDataset;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"生成骨骼遮罩时发生异常: {ex.Message}\n{ex.StackTrace}");
                Debug.Log("返回空骨骼遮罩作为后备方案");
                return CreateEmptyBoneMask();
            }
        }

        /// <summary>
        /// Create an empty bone mask dataset for fallback cases
        /// Guaranteed to never return null
        /// </summary>
        /// <returns>Empty VolumeDataset with all zeros (never null)</returns>
        private VolumeDataset CreateEmptyBoneMask()
        {
            try
            {
                // Create a minimal 1x1x1 empty mask
                float[] emptyData = new float[] { 0.0f };
                VolumeDataset emptyMask = HemorrhageDetector.CreateHemorrhageMaskDataset(
                    emptyData, 1, 1, 1, Vector3.one);

                if (emptyMask != null)
                {
                    Debug.Log("创建了空的骨骼遮罩数据集作为后备");
                    return emptyMask;
                }
                else
                {
                    Debug.LogError("CreateHemorrhageMaskDataset 返回了 null，创建静态空数据集");
                    return CreateStaticEmptyDataset();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"创建空骨骼遮罩失败: {ex.Message}，使用静态空数据集");
                return CreateStaticEmptyDataset();
            }
        }

        /// <summary>
        /// Create a static empty dataset that is guaranteed to work
        /// This is the absolute fallback that should never fail
        /// </summary>
        /// <returns>Static empty VolumeDataset</returns>
        private VolumeDataset CreateStaticEmptyDataset()
        {
            // Create a minimal ScriptableObject directly
            VolumeDataset staticEmpty = ScriptableObject.CreateInstance<VolumeDataset>();

            // Set minimal valid properties
            staticEmpty.data = new float[] { 0.0f };
            staticEmpty.dimX = 1;
            staticEmpty.dimY = 1;
            staticEmpty.dimZ = 1;
            staticEmpty.scale = Vector3.one;
            staticEmpty.datasetName = "EmptyBoneMask";

            // Initialize texture and bounds
            try
            {
                staticEmpty.RecalculateBounds();
                staticEmpty.GetDataTexture(); // This might fail but we'll catch it
            }
            catch
            {
                // If texture creation fails, we'll still return the dataset
                // The caller should handle missing texture gracefully
            }

            Debug.LogWarning("使用了静态空数据集 - 这表示系统遇到了严重问题");
            return staticEmpty;
        }

        /// <summary>
        /// Apply connected component filtering to remove small isolated regions
        /// </summary>
        private byte[] ApplyConnectedComponentFilter(byte[] mask, int width, int height, int depth, int minVoxelCount)
        {
            // Simple flood fill implementation for connected components
            // This is a basic implementation - could be optimized with Union-Find or more sophisticated algorithms

            byte[] filteredMask = new byte[mask.Length];
            bool[,,] visited = new bool[width, height, depth];

            int[] dx = { -1, 0, 1, 0, 0, 0 };
            int[] dy = { 0, 1, 0, -1, 0, 0 };
            int[] dz = { 0, 0, 0, 0, -1, 1 };

            for (int z = 0; z < depth; z++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = x + y * width + z * width * height;
                        if (mask[index] == 1 && !visited[x, y, z])
                        {
                            // Find connected component
                            List<Vector3Int> component = new List<Vector3Int>();
                            Queue<Vector3Int> queue = new Queue<Vector3Int>();
                            queue.Enqueue(new Vector3Int(x, y, z));
                            visited[x, y, z] = true;

                            while (queue.Count > 0)
                            {
                                Vector3Int current = queue.Dequeue();
                                component.Add(current);

                                // Check 6-connected neighbors
                                for (int d = 0; d < 6; d++)
                                {
                                    int nx = current.x + dx[d];
                                    int ny = current.y + dy[d];
                                    int nz = current.z + dz[d];

                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && nz >= 0 && nz < depth)
                                    {
                                        int nIndex = nx + ny * width + nz * width * height;
                                        if (mask[nIndex] == 1 && !visited[nx, ny, nz])
                                        {
                                            visited[nx, ny, nz] = true;
                                            queue.Enqueue(new Vector3Int(nx, ny, nz));
                                        }
                                    }
                                }
                            }

                            // Keep component if large enough
                            if (component.Count >= minVoxelCount)
                            {
                                foreach (Vector3Int voxel in component)
                                {
                                    int vIndex = voxel.x + voxel.y * width + voxel.z * width * height;
                                    filteredMask[vIndex] = 1;
                                }
                            }
                        }
                    }
                }
            }

            return filteredMask;
        }

        /// <summary>
        /// Convert LabelVolume to VolumeDataset for compatibility
        /// </summary>
        /// <param name="label">Label volume to convert</param>
        /// <param name="referenceDataset">Reference dataset for dimensions and scale</param>
        /// <returns>VolumeDataset with mask data</returns>
        private VolumeDataset ConvertLabelVolumeToVolumeDataset(UnityVolumeRendering.LabelVolume label, VolumeDataset referenceDataset)
        {
            float[] maskData = new float[label.width * label.height * label.depth];
            for (int i = 0; i < maskData.Length; i++)
            {
                maskData[i] = label.mask[i];
            }

            return HemorrhageDetector.CreateHemorrhageMaskDataset(maskData, label.width, label.height, label.depth, referenceDataset.scale);
        }

        /// <summary>
        /// Convert Segment mask data to VolumeDataset for compatibility with renderer
        /// </summary>
        /// <param name="segment">Segment containing mask data</param>
        /// <param name="referenceDataset">Reference dataset for scale</param>
        /// <returns>VolumeDataset with mask data</returns>
        private VolumeDataset ConvertSegmentToVolumeDataset(UnityVolumeRendering.Segmentation.Segment segment, VolumeDataset referenceDataset)
        {
            byte[] maskData = segment.GetDataCopy();
            int width = segment.GetDimensionX();
            int height = segment.GetDimensionY();
            int depth = segment.GetDimensionZ();

            float[] floatMaskData = new float[maskData.Length];
            for (int i = 0; i < maskData.Length; i++)
            {
                floatMaskData[i] = maskData[i];
            }

            return HemorrhageDetector.CreateHemorrhageMaskDataset(floatMaskData, width, height, depth, referenceDataset.scale);
        }

        /// <summary>
        /// Set up hemorrhage mesh object in scene hierarchy
        /// </summary>
        /// <param name="mesh">Hemorrhage mesh</param>
        private void SetupHemorrhageMeshObject(Mesh mesh)
        {
            // Create or update hemorrhage segment game object
            GameObject hemorrhageSegment = GameObject.Find("Hemorrhage_Segment");
            if (hemorrhageSegment == null)
            {
                hemorrhageSegment = new GameObject("Hemorrhage_Segment");
                hemorrhageSegment.transform.SetParent(volumeRenderer.transform);
                hemorrhageSegment.transform.localPosition = Vector3.zero;
                hemorrhageSegment.transform.localRotation = Quaternion.identity;
                hemorrhageSegment.transform.localScale = Vector3.one;
            }

            MeshFilter meshFilter = hemorrhageSegment.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = hemorrhageSegment.AddComponent<MeshFilter>();
            }
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = hemorrhageSegment.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = hemorrhageSegment.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));
            }

            meshRenderer.material.color = new Color(hemorrhageColor.r, hemorrhageColor.g, hemorrhageColor.b, 0.8f);
            meshRenderer.material.EnableKeyword("_EMISSION");
            meshRenderer.material.SetColor("_EmissionColor", hemorrhageColor * 0.2f);
        }

        /// <summary>
        /// Export hemorrhage detection results to debug log
        /// </summary>
        public void LogDetectionResults()
        {
            Debug.Log("=== Hemorrhage Detection Results ===");
            Debug.Log($"Body Region: {bodyRegion}");
            Debug.Log($"Hemorrhage Present: {hasHemorrhage}");
            if (hasHemorrhage && hemorrhageStatistics != null)
            {
                Debug.Log($"Total Voxels: {hemorrhageStatistics["total_voxels"]}");
                Debug.Log($"Volume: {hemorrhageStatistics["volume_mm3"]} mm³");
            }
            Debug.Log($"Detection Parameters: HU [{minHUThreshold}-{maxHUThreshold}], Min Voxels: {minVoxelCount}");
            Debug.Log($"Visualization: {(visualizationEnabled ? "Enabled" : "Disabled")}, Color: {hemorrhageColor}, Intensity: {hemorrhageIntensity}");
            Debug.Log("===================================");
        }

        void OnDestroy()
        {
            // Cleanup will be handled by individual components
        }
    }
}
