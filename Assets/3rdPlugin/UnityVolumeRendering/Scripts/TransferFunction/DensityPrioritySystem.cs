using UnityEngine;
using System.Collections.Generic;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Medical-grade density-priority volume rendering system
    /// Implements tissue priority-based raymarching for accurate medical visualization
    /// Higher density tissues suppress lower density contributions during rendering
    /// </summary>
    public class DensityPrioritySystem : ScriptableObject
    {
        /// <summary>
        /// Tissue type enumeration for priority-based rendering
        /// Priority order (highest to lowest): Bone > Vessel > SoftTissue > Fat > Lung > Air
        /// </summary>
        public enum TissueType
        {
            Bone = 5,           // Cortical and trabecular bone
            Vessel = 4,         // Blood vessels and hemorrhage
            SoftTissue = 3,     // Organs, muscle, connective tissue
            Fat = 2,            // Adipose tissue
            Lung = 1,           // Lung parenchyma
            Air = 0             // Air (typically invisible)
        }

        /// <summary>
        /// Preset rendering modes for common clinical workflows
        /// </summary>
        public enum PresetMode
        {
            PureBone,           // Bone only - fracture detection
            BoneAndVessels,     // Bone + hemorrhage/vessels - trauma assessment
            BoneAndOrgans,      // Bone + soft tissue - anatomical context
            FullAnatomy         // All tissues - comprehensive visualization
        }

        [System.Serializable]
        public class TissuePriority
        {
            public TissueType tissueType;
            public int priority;                    // 0-5, higher = rendered first (suppresses lower)
            public float densityWeightFactor;       // Multiplier for density-based opacity suppression
            public bool suppressLowerPriority;      // Whether this tissue suppresses lower priority tissues
            public float opacityBoost;              // Opacity multiplier for this tissue

            public TissuePriority(TissueType type, int prio, float densityWeight, bool suppress, float opacityBoost)
            {
                this.tissueType = type;
                this.priority = prio;
                this.densityWeightFactor = densityWeight;
                this.suppressLowerPriority = suppress;
                this.opacityBoost = opacityBoost;
            }
        }

        [System.Serializable]
        public class PresetConfiguration
        {
            public PresetMode mode;
            public string modeName;
            public List<TissueType> visibleTissues = new List<TissueType>();
            public string description;

            public PresetConfiguration(PresetMode mode, string name, string desc)
            {
                this.mode = mode;
                this.modeName = name;
                this.description = desc;
            }
        }

        // Tissue priority definitions
        [SerializeField]
        private List<TissuePriority> tissuePriorities = new List<TissuePriority>();

        // Preset configurations
        [SerializeField]
        private List<PresetConfiguration> presetConfigurations = new List<PresetConfiguration>();

        // Current active preset
        [SerializeField]
        private PresetMode currentPreset = PresetMode.FullAnatomy;

        // Global density priority blending factor (0 = no priority, 1 = full priority)
        [SerializeField]
        private float densityPriorityStrength = 1.0f;

        private void OnEnable()
        {
            InitializeDefaults();
        }

        /// <summary>
        /// Initialize default tissue priorities and presets
        /// </summary>
        private void InitializeDefaults()
        {
            if (tissuePriorities.Count == 0)
            {
                // Define medical-grade tissue priorities
                // Priority order: Bone (5) > Vessel (4) > SoftTissue (3) > Fat (2) > Lung (1) > Air (0)
                tissuePriorities.Add(new TissuePriority(TissueType.Bone, 5, 1.5f, true, 1.2f));
                tissuePriorities.Add(new TissuePriority(TissueType.Vessel, 4, 1.3f, true, 1.1f));
                tissuePriorities.Add(new TissuePriority(TissueType.SoftTissue, 3, 1.0f, true, 1.0f));
                tissuePriorities.Add(new TissuePriority(TissueType.Fat, 2, 0.8f, false, 0.9f));
                tissuePriorities.Add(new TissuePriority(TissueType.Lung, 1, 0.6f, false, 0.7f));
                tissuePriorities.Add(new TissuePriority(TissueType.Air, 0, 0.0f, false, 0.0f));
            }

            if (presetConfigurations.Count == 0)
            {
                // Pure Bone: Fracture detection
                PresetConfiguration pureBone = new PresetConfiguration(
                    PresetMode.PureBone, "Pure Bone",
                    "Bone only - ideal for fracture detection and orthopedic assessment"
                );
                pureBone.visibleTissues.Add(TissueType.Bone);
                presetConfigurations.Add(pureBone);

                // Bone + Vessels: Trauma assessment
                PresetConfiguration boneVessels = new PresetConfiguration(
                    PresetMode.BoneAndVessels, "Bone + Vessels",
                    "Bone and hemorrhage/vessels - for trauma and vascular assessment"
                );
                boneVessels.visibleTissues.Add(TissueType.Bone);
                boneVessels.visibleTissues.Add(TissueType.Vessel);
                presetConfigurations.Add(boneVessels);

                // Bone + Organs: Anatomical context
                PresetConfiguration boneOrgans = new PresetConfiguration(
                    PresetMode.BoneAndOrgans, "Bone + Organs",
                    "Bone and soft tissue - for anatomical context and surgical planning"
                );
                boneOrgans.visibleTissues.Add(TissueType.Bone);
                boneOrgans.visibleTissues.Add(TissueType.SoftTissue);
                presetConfigurations.Add(boneOrgans);

                // Full Anatomy: Comprehensive visualization
                PresetConfiguration fullAnatomy = new PresetConfiguration(
                    PresetMode.FullAnatomy, "Full Anatomy",
                    "All tissues - comprehensive anatomical visualization"
                );
                fullAnatomy.visibleTissues.Add(TissueType.Bone);
                fullAnatomy.visibleTissues.Add(TissueType.Vessel);
                fullAnatomy.visibleTissues.Add(TissueType.SoftTissue);
                fullAnatomy.visibleTissues.Add(TissueType.Fat);
                fullAnatomy.visibleTissues.Add(TissueType.Lung);
                presetConfigurations.Add(fullAnatomy);
            }
        }

        /// <summary>
        /// Get tissue priority by type
        /// </summary>
        public TissuePriority GetTissuePriority(TissueType type)
        {
            foreach (var priority in tissuePriorities)
            {
                if (priority.tissueType == type)
                    return priority;
            }
            return null;
        }

        /// <summary>
        /// Get priority value for a tissue type (higher = rendered first)
        /// </summary>
        public int GetPriorityValue(TissueType type)
        {
            var priority = GetTissuePriority(type);
            return priority != null ? priority.priority : 0;
        }

        /// <summary>
        /// Calculate opacity suppression factor based on density and priority
        /// Higher density tissues suppress lower density contributions
        /// </summary>
        public float CalculateOpacitySuppression(float density, TissueType tissueType, float accumulatedAlpha)
        {
            var priority = GetTissuePriority(tissueType);
            if (priority == null || !priority.suppressLowerPriority)
                return 1.0f;

            // Suppress lower priority contributions based on density and accumulated alpha
            float suppressionFactor = 1.0f - (accumulatedAlpha * priority.densityWeightFactor * density);
            return Mathf.Max(0.0f, suppressionFactor);
        }

        /// <summary>
        /// Get density weight factor for opacity calculation
        /// </summary>
        public float GetDensityWeightFactor(TissueType type)
        {
            var priority = GetTissuePriority(type);
            return priority != null ? priority.densityWeightFactor : 1.0f;
        }

        /// <summary>
        /// Get opacity boost for a tissue type
        /// </summary>
        public float GetOpacityBoost(TissueType type)
        {
            var priority = GetTissuePriority(type);
            return priority != null ? priority.opacityBoost : 1.0f;
        }

        /// <summary>
        /// Check if tissue type is visible in current preset
        /// </summary>
        public bool IsTissueVisible(TissueType type)
        {
            var preset = GetPresetConfiguration(currentPreset);
            if (preset == null)
                return true;
            return preset.visibleTissues.Contains(type);
        }

        /// <summary>
        /// Get preset configuration by mode
        /// </summary>
        public PresetConfiguration GetPresetConfiguration(PresetMode mode)
        {
            foreach (var preset in presetConfigurations)
            {
                if (preset.mode == mode)
                    return preset;
            }
            return null;
        }

        /// <summary>
        /// Set active preset mode
        /// </summary>
        public void SetPresetMode(PresetMode mode)
        {
            currentPreset = mode;
        }

        /// <summary>
        /// Get current preset mode
        /// </summary>
        public PresetMode GetCurrentPreset()
        {
            return currentPreset;
        }

        /// <summary>
        /// Set density priority strength (0 = disabled, 1 = full effect)
        /// </summary>
        public void SetDensityPriorityStrength(float strength)
        {
            densityPriorityStrength = Mathf.Clamp01(strength);
        }

        /// <summary>
        /// Get density priority strength
        /// </summary>
        public float GetDensityPriorityStrength()
        {
            return densityPriorityStrength;
        }

        /// <summary>
        /// Get all preset configurations
        /// </summary>
        public List<PresetConfiguration> GetAllPresets()
        {
            return new List<PresetConfiguration>(presetConfigurations);
        }

        /// <summary>
        /// Get all tissue priorities
        /// </summary>
        public List<TissuePriority> GetAllTissuePriorities()
        {
            return new List<TissuePriority>(tissuePriorities);
        }
    }
}
