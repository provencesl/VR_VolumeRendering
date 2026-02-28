using UnityEngine;
using UnityVolumeRendering;

/// <summary>
/// Example script showing how to use HU Range isolation and simplified post-processing
/// for medical imaging visualization
/// </summary>
public class MedicalImagingExample : MonoBehaviour
{
    [SerializeField]
    private VolumeRenderedObject volumeObject;

    [SerializeField]
    private Camera mainCamera;

    private HURangeLayerManager layerManager;
    private SimplifiedPostProcessor postProcessor;

    private void Start()
    {
        if (volumeObject == null)
            volumeObject = GetComponent<VolumeRenderedObject>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Setup layer manager
        layerManager = volumeObject.GetComponent<HURangeLayerManager>();
        if (layerManager == null)
            layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();

        // Setup post processor
        postProcessor = mainCamera.GetComponent<SimplifiedPostProcessor>();
        if (postProcessor == null)
            postProcessor = mainCamera.gameObject.AddComponent<SimplifiedPostProcessor>();
    }

    /// <summary>
    /// Example 1: Display only bone (hide all other organs)
    /// </summary>
    public void ShowBoneOnly()
    {
        Debug.Log("Displaying bone only...");

        // Clear all layers
        layerManager.ClearAllLayers();

        // Add cortical bone layer (HU 800-3000)
        var boneLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]);
        layerManager.AddLayer(boneLayer);

        // Apply bone-optimized post-processing
        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Bone);
    }

    /// <summary>
    /// Example 2: Display only hemorrhage (bleeding)
    /// </summary>
    public void ShowHemorrhageOnly()
    {
        Debug.Log("Displaying hemorrhage only...");

        layerManager.ClearAllLayers();

        // Add acute hemorrhage layer (HU 30-100)
        var hemorrhageLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2]);
        layerManager.AddLayer(hemorrhageLayer);

        // Apply hemorrhage-optimized post-processing
        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Hemorrhage);
    }

    /// <summary>
    /// Example 3: Display bone + hemorrhage (trauma assessment)
    /// </summary>
    public void ShowBoneAndHemorrhage()
    {
        Debug.Log("Displaying bone and hemorrhage...");

        layerManager.LoadBoneHemorrhagePreset();
        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
    }

    /// <summary>
    /// Example 4: Display soft tissue only
    /// </summary>
    public void ShowSoftTissueOnly()
    {
        Debug.Log("Displaying soft tissue only...");

        layerManager.ClearAllLayers();

        // Add soft tissue layer (HU -10 to 100)
        var softTissueLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4]);
        layerManager.AddLayer(softTissueLayer);

        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
    }

    /// <summary>
    /// Example 5: Display trabecular bone (internal bone structure)
    /// </summary>
    public void ShowTrabecularBoneOnly()
    {
        Debug.Log("Displaying trabecular bone only...");

        layerManager.ClearAllLayers();

        // Add trabecular bone layer (HU 200-800)
        var trabecularLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[1]);
        layerManager.AddLayer(trabecularLayer);

        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
    }

    /// <summary>
    /// Example 6: Custom HU range isolation
    /// Useful for specific diagnostic purposes
    /// </summary>
    public void ShowCustomHURange(float minHU, float maxHU, Color color, string layerName)
    {
        Debug.Log($"Displaying custom HU range: {minHU} - {maxHU}");

        layerManager.ClearAllLayers();

        var customLayer = new HURangeLayer(
            layerName,
            minHU,
            maxHU,
            color,
            0.85f
        );
        layerManager.AddLayer(customLayer);

        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
    }

    /// <summary>
    /// Example 7: Multi-layer visualization
    /// Show multiple tissues simultaneously
    /// </summary>
    public void ShowMultipleLayers()
    {
        Debug.Log("Displaying multiple tissue layers...");

        layerManager.ClearAllLayers();

        // Add multiple layers
        layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0])); // Cortical bone
        layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[1])); // Trabecular bone
        layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2])); // Hemorrhage
        layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4])); // Soft tissue

        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Minimal);
    }

    /// <summary>
    /// Example 8: Fracture detection workflow
    /// Optimize for fracture line visibility
    /// </summary>
    public void ShowFractureDetection()
    {
        Debug.Log("Optimizing for fracture detection...");

        layerManager.ClearAllLayers();

        // Cortical bone (main structure)
        var corticalBone = new HURangeLayer(
            "Cortical Bone",
            800f, 3000f,
            Color.white,
            0.95f
        );
        layerManager.AddLayer(corticalBone);

        // Trabecular bone (internal detail)
        var trabecularBone = new HURangeLayer(
            "Trabecular Bone",
            200f, 800f,
            new Color(0.8f, 0.8f, 0.8f),
            0.8f
        );
        layerManager.AddLayer(trabecularBone);

        // Apply bone-optimized post-processing with slightly higher edge enhancement
        postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Bone);
    }

    /// <summary>
    /// Example 9: Disable post-processing for clinical accuracy
    /// </summary>
    public void DisablePostProcessing()
    {
        Debug.Log("Disabling post-processing for maximum clinical accuracy...");

        postProcessor.SetEnabled(false);
    }

    /// <summary>
    /// Example 10: Reset to default bone visualization
    /// </summary>
    public void ResetToDefault()
    {
        Debug.Log("Resetting to default bone visualization...");

        ShowBoneOnly();
    }

    /// <summary>
    /// Helper: Print all available HU presets
    /// </summary>
    public void PrintAvailablePresets()
    {
        Debug.Log("=== Available HU Range Presets ===");
        var presets = MedicalHURange.TISSUE_PRESETS;
        for (int i = 0; i < presets.Length; i++)
        {
            var preset = presets[i];
            Debug.Log($"{i}: {preset.name} (HU {preset.minHU:F0} - {preset.maxHU:F0})");
            Debug.Log($"   Description: {preset.description}");
        }
    }

    /// <summary>
    /// Helper: Get HU value from normalized value
    /// </summary>
    public float GetHUFromNormalized(float normalizedValue)
    {
        return MedicalHURange.NormalizedToHU(normalizedValue);
    }

    /// <summary>
    /// Helper: Get normalized value from HU
    /// </summary>
    public float GetNormalizedFromHU(float huValue)
    {
        return MedicalHURange.HUToNormalized(huValue);
    }
}
