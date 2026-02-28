using UnityEngine;
using System.Collections.Generic;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Medical HU (Hounsfield Unit) range definitions and management
    /// Used for precise tissue isolation and visualization
    /// </summary>
    public static class MedicalHURange
    {
        // Standard HU ranges for CT imaging
        public const float HU_AIR = -1000f;
        public const float HU_LUNG = -500f;
        public const float HU_FAT = -100f;
        public const float HU_WATER = 0f;
        public const float HU_SOFT_TISSUE = 40f;
        public const float HU_BLOOD_ACUTE = 60f;
        public const float HU_BLOOD_SUBACUTE = 50f;
        public const float HU_BONE_TRABECULAR = 400f;
        public const float HU_BONE_CORTICAL = 1000f;
        public const float HU_METAL = 3000f;

        [System.Serializable]
        public struct HURangePreset
        {
            public string name;
            public float minHU;
            public float maxHU;
            public Color color;
            public float opacity;
            public string description;

            public HURangePreset(string name, float minHU, float maxHU, Color color, float opacity, string description)
            {
                this.name = name;
                this.minHU = minHU;
                this.maxHU = maxHU;
                this.color = color;
                this.opacity = opacity;
                this.description = description;
            }
        }

        // Tissue-specific HU ranges
        public static readonly HURangePreset[] TISSUE_PRESETS = new HURangePreset[]
        {
            new HURangePreset(
                "Bone (Cortical)",
                800f, 3000f,
                new Color(1.0f, 1.0f, 1.0f, 1.0f),
                0.95f,
                "Dense cortical bone - ideal for fracture detection"
            ),
            new HURangePreset(
                "Bone (Trabecular)",
                200f, 800f,
                new Color(0.9f, 0.9f, 0.9f, 1.0f),
                0.85f,
                "Trabecular bone - internal bone structure"
            ),
            new HURangePreset(
                "Hemorrhage (Acute)",
                30f, 100f,
                new Color(1.0f, 0.2f, 0.1f, 1.0f),
                0.85f,
                "Fresh blood - bright red appearance"
            ),
            new HURangePreset(
                "Hemorrhage (Subacute)",
                40f, 80f,
                new Color(1.0f, 0.5f, 0.1f, 1.0f),
                0.75f,
                "Older blood - orange appearance"
            ),
            new HURangePreset(
                "Soft Tissue",
                -10f, 100f,
                new Color(0.6f, 0.5f, 0.5f, 1.0f),
                0.7f,
                "Muscle, organs, and soft tissue"
            ),
            new HURangePreset(
                "Fat",
                -100f, -50f,
                new Color(0.8f, 0.7f, 0.6f, 1.0f),
                0.6f,
                "Adipose tissue"
            ),
            new HURangePreset(
                "Lung",
                -500f, -100f,
                new Color(0.4f, 0.4f, 0.4f, 1.0f),
                0.5f,
                "Lung tissue"
            ),
            new HURangePreset(
                "Air",
                -1000f, -500f,
                new Color(0.0f, 0.0f, 0.0f, 0.0f),
                0.0f,
                "Air - typically invisible"
            )
        };

        /// <summary>
        /// Convert HU value to normalized [0,1] range
        /// Assumes typical CT data range of -1000 to 3000 HU
        /// </summary>
        public static float HUToNormalized(float huValue)
        {
            const float MIN_HU = -1000f;
            const float MAX_HU = 3000f;
            return Mathf.Clamp01((huValue - MIN_HU) / (MAX_HU - MIN_HU));
        }

        /// <summary>
        /// Convert normalized [0,1] value back to HU
        /// </summary>
        public static float NormalizedToHU(float normalizedValue)
        {
            const float MIN_HU = -1000f;
            const float MAX_HU = 3000f;
            return Mathf.Lerp(MIN_HU, MAX_HU, normalizedValue);
        }

        /// <summary>
        /// Get preset by name
        /// </summary>
        public static HURangePreset GetPresetByName(string name)
        {
            foreach (var preset in TISSUE_PRESETS)
            {
                if (preset.name == name)
                    return preset;
            }
            return TISSUE_PRESETS[0];
        }

        /// <summary>
        /// Get all preset names
        /// </summary>
        public static string[] GetAllPresetNames()
        {
            string[] names = new string[TISSUE_PRESETS.Length];
            for (int i = 0; i < TISSUE_PRESETS.Length; i++)
            {
                names[i] = TISSUE_PRESETS[i].name;
            }
            return names;
        }
    }

    /// <summary>
    /// HU Range layer for tissue isolation
    /// Similar to Photoshop layers but for medical imaging
    /// Extended with density-priority support for medical-grade rendering
    /// </summary>
    [System.Serializable]
    public class HURangeLayer
    {
        public string layerName = "New Layer";
        public float minHU = 0f;
        public float maxHU = 100f;
        public Color color = Color.white;
        public float opacity = 1.0f;
        public bool visible = true;
        public bool locked = false;

        // Density-priority system fields
        public DensityPrioritySystem.TissueType tissueType = DensityPrioritySystem.TissueType.SoftTissue;
        public int densityPriority = 3;  // 0-5, higher = rendered first
        public float densityWeightFactor = 1.0f;  // How much this tissue suppresses lower priority

        public HURangeLayer() { }

        public HURangeLayer(string name, float minHU, float maxHU, Color color, float opacity)
        {
            this.layerName = name;
            this.minHU = minHU;
            this.maxHU = maxHU;
            this.color = color;
            this.opacity = opacity;
            this.visible = true;
            this.locked = false;
            this.tissueType = DensityPrioritySystem.TissueType.SoftTissue;
            this.densityPriority = 3;
            this.densityWeightFactor = 1.0f;
        }

        public HURangeLayer(MedicalHURange.HURangePreset preset)
        {
            this.layerName = preset.name;
            this.minHU = preset.minHU;
            this.maxHU = preset.maxHU;
            this.color = preset.color;
            this.opacity = preset.opacity;
            this.visible = true;
            this.locked = false;
            this.tissueType = DensityPrioritySystem.TissueType.SoftTissue;
            this.densityPriority = 3;
            this.densityWeightFactor = 1.0f;
        }

        /// <summary>
        /// Apply tissue type preset with priority settings
        /// </summary>
        public void ApplyTissueTypePreset(DensityPrioritySystem.TissueType type)
        {
            this.tissueType = type;
            
            // Set priority and weight based on tissue type
            switch (type)
            {
                case DensityPrioritySystem.TissueType.Bone:
                    this.densityPriority = 5;
                    this.densityWeightFactor = 1.5f;
                    break;
                case DensityPrioritySystem.TissueType.Vessel:
                    this.densityPriority = 4;
                    this.densityWeightFactor = 1.3f;
                    break;
                case DensityPrioritySystem.TissueType.SoftTissue:
                    this.densityPriority = 3;
                    this.densityWeightFactor = 1.0f;
                    break;
                case DensityPrioritySystem.TissueType.Fat:
                    this.densityPriority = 2;
                    this.densityWeightFactor = 0.8f;
                    break;
                case DensityPrioritySystem.TissueType.Lung:
                    this.densityPriority = 1;
                    this.densityWeightFactor = 0.6f;
                    break;
                case DensityPrioritySystem.TissueType.Air:
                    this.densityPriority = 0;
                    this.densityWeightFactor = 0.0f;
                    break;
            }
        }
    }

    /// <summary>
    /// HU Range layer manager
    /// Manages multiple tissue isolation layers with density-priority rendering
    /// </summary>
    public class HURangeLayerManager : MonoBehaviour
    {
        [SerializeField]
        private List<HURangeLayer> layers = new List<HURangeLayer>();

        [SerializeField]
        private int activeLayerIndex = 0;

        [SerializeField]
        private VolumeRenderedObject volumeObject;

        [SerializeField]
        private DensityPrioritySystem densityPrioritySystem;

        [SerializeField]
        private DensityPrioritySystem.PresetMode currentPresetMode = DensityPrioritySystem.PresetMode.FullAnatomy;

        [SerializeField]
        private float densityPriorityStrength = 1.0f;

        private void OnEnable()
        {
            if (volumeObject == null)
                volumeObject = GetComponent<VolumeRenderedObject>();

            // Initialize or find density priority system
            if (densityPrioritySystem == null)
            {
                densityPrioritySystem = Resources.Load<DensityPrioritySystem>("DensityPrioritySystem");
                if (densityPrioritySystem == null)
                {
                    // Create default instance if not found
                    densityPrioritySystem = ScriptableObject.CreateInstance<DensityPrioritySystem>();
                }
            }
        }

        /// <summary>
        /// Add a new HU range layer
        /// </summary>
        public void AddLayer(HURangeLayer layer)
        {
            layers.Add(layer);
            UpdateTransferFunction();
        }

        /// <summary>
        /// Remove layer by index
        /// </summary>
        public void RemoveLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
            {
                layers.RemoveAt(index);
                if (activeLayerIndex >= layers.Count)
                    activeLayerIndex = Mathf.Max(0, layers.Count - 1);
                UpdateTransferFunction();
            }
        }

        /// <summary>
        /// Get layer by index
        /// </summary>
        public HURangeLayer GetLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
                return layers[index];
            return null;
        }

        /// <summary>
        /// Get all layers
        /// </summary>
        public List<HURangeLayer> GetAllLayers()
        {
            return new List<HURangeLayer>(layers);
        }

        /// <summary>
        /// Set active layer
        /// </summary>
        public void SetActiveLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
            {
                activeLayerIndex = index;
            }
        }

        /// <summary>
        /// Get active layer
        /// </summary>
        public HURangeLayer GetActiveLayer()
        {
            if (activeLayerIndex >= 0 && activeLayerIndex < layers.Count)
                return layers[activeLayerIndex];
            return null;
        }

        /// <summary>
        /// Toggle layer visibility
        /// </summary>
        public void ToggleLayerVisibility(int index)
        {
            if (index >= 0 && index < layers.Count)
            {
                layers[index].visible = !layers[index].visible;
                UpdateTransferFunction();
            }
        }

        /// <summary>
        /// Update transfer function based on visible layers
        /// </summary>
        private void UpdateTransferFunction()
        {
            if (volumeObject == null)
                return;

            // Create a 1D transfer function from visible layers
            TransferFunction tf = ScriptableObject.CreateInstance<TransferFunction>();

            // Add control points for each visible layer
            foreach (var layer in layers)
            {
                if (!layer.visible)
                    continue;

                // Add color control points at range boundaries
                float minNorm = MedicalHURange.HUToNormalized(layer.minHU);
                float maxNorm = MedicalHURange.HUToNormalized(layer.maxHU);

                tf.AddControlPoint(new TFColourControlPoint(minNorm, layer.color));
                tf.AddControlPoint(new TFColourControlPoint(maxNorm, layer.color));

                // Add alpha control points
                tf.AddControlPoint(new TFAlphaControlPoint(minNorm, layer.opacity));
                tf.AddControlPoint(new TFAlphaControlPoint(maxNorm, layer.opacity));
            }

            tf.GenerateTexture();
            volumeObject.SetTransferFunctionAsync(tf);
        }

        /// <summary>
        /// Clear all layers
        /// </summary>
        public void ClearAllLayers()
        {
            layers.Clear();
            activeLayerIndex = 0;
        }

        /// <summary>
        /// Create preset configuration
        /// </summary>
        public void LoadPreset(string presetName)
        {
            ClearAllLayers();
            var preset = MedicalHURange.GetPresetByName(presetName);
            AddLayer(new HURangeLayer(preset));
        }

        /// <summary>
        /// Create bone + hemorrhage visualization
        /// </summary>
        public void LoadBoneHemorrhagePreset()
        {
            ClearAllLayers();
            AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2])); // Hemorrhage
            AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0])); // Cortical bone
        }

        /// <summary>
        /// Load density-priority preset mode
        /// </summary>
        public void LoadDensityPriorityPreset(DensityPrioritySystem.PresetMode presetMode)
        {
            if (densityPrioritySystem == null)
                return;

            currentPresetMode = presetMode;
            densityPrioritySystem.SetPresetMode(presetMode);

            ClearAllLayers();

            var presetConfig = densityPrioritySystem.GetPresetConfiguration(presetMode);
            if (presetConfig == null)
                return;

            // Load layers based on visible tissues in preset
            foreach (var tissueType in presetConfig.visibleTissues)
            {
                HURangeLayer layer = CreateLayerForTissueType(tissueType);
                if (layer != null)
                {
                    AddLayer(layer);
                }
            }
        }

        /// <summary>
        /// Create a HURangeLayer configured for a specific tissue type
        /// </summary>
        private HURangeLayer CreateLayerForTissueType(DensityPrioritySystem.TissueType tissueType)
        {
            HURangeLayer layer = null;

            switch (tissueType)
            {
                case DensityPrioritySystem.TissueType.Bone:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]); // Cortical bone
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Bone);
                    break;

                case DensityPrioritySystem.TissueType.Vessel:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2]); // Hemorrhage acute
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Vessel);
                    break;

                case DensityPrioritySystem.TissueType.SoftTissue:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4]); // Soft tissue
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.SoftTissue);
                    break;

                case DensityPrioritySystem.TissueType.Fat:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[5]); // Fat
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Fat);
                    break;

                case DensityPrioritySystem.TissueType.Lung:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[6]); // Lung
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Lung);
                    break;

                case DensityPrioritySystem.TissueType.Air:
                    layer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[7]); // Air
                    layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Air);
                    break;
            }

            return layer;
        }

        /// <summary>
        /// Set density priority strength (0 = disabled, 1 = full effect)
        /// </summary>
        public void SetDensityPriorityStrength(float strength)
        {
            densityPriorityStrength = Mathf.Clamp01(strength);
            if (densityPrioritySystem != null)
                densityPrioritySystem.SetDensityPriorityStrength(densityPriorityStrength);
        }

        /// <summary>
        /// Get density priority strength
        /// </summary>
        public float GetDensityPriorityStrength()
        {
            return densityPriorityStrength;
        }

        /// <summary>
        /// Get current preset mode
        /// </summary>
        public DensityPrioritySystem.PresetMode GetCurrentPresetMode()
        {
            return currentPresetMode;
        }

        /// <summary>
        /// Get density priority system
        /// </summary>
        public DensityPrioritySystem GetDensityPrioritySystem()
        {
            return densityPrioritySystem;
        }
    }
}
