using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Settings for medical image post-processing
    /// </summary>
    [System.Serializable]
    public class PostProcessingSettings
    {
        [Header("Edge Enhancement")]
        [Range(0.0f, 1.0f)]
        public float edgeThreshold = 0.3f;
        
        [Range(0.0f, 2.0f)]
        public float edgeIntensity = 1.0f;

        [Header("Contrast & Brightness")]
        [Range(0.0f, 2.0f)]
        public float contrastStrength = 1.2f;
        
        [Range(-0.5f, 0.5f)]
        public float brightnessOffset = 0.0f;

        [Header("Color Enhancement")]
        [Range(0.0f, 2.0f)]
        public float saturationBoost = 1.2f;

        [Header("Tone Mapping (HDR Effect)")]
        [Range(0.0f, 2.0f)]
        public float tonemapStrength = 1.0f;

        // Presets for different imaging modes
        public static PostProcessingSettings GetBonePreset()
        {
            return new PostProcessingSettings
            {
                edgeThreshold = 0.25f,
                edgeIntensity = 1.5f,
                contrastStrength = 1.4f,
                brightnessOffset = 0.05f,
                saturationBoost = 1.0f,
                tonemapStrength = 1.1f
            };
        }

        public static PostProcessingSettings GetHemorrhagePreset()
        {
            return new PostProcessingSettings
            {
                edgeThreshold = 0.2f,
                edgeIntensity = 1.2f,
                contrastStrength = 1.3f,
                brightnessOffset = 0.0f,
                saturationBoost = 1.5f,  // Boost red colors
                tonemapStrength = 1.2f
            };
        }

        public static PostProcessingSettings GetSoftTissuePreset()
        {
            return new PostProcessingSettings
            {
                edgeThreshold = 0.35f,
                edgeIntensity = 0.8f,
                contrastStrength = 1.1f,
                brightnessOffset = 0.02f,
                saturationBoost = 1.1f,
                tonemapStrength = 0.9f
            };
        }

        public static PostProcessingSettings GetHighContrastPreset()
        {
            return new PostProcessingSettings
            {
                edgeThreshold = 0.15f,
                edgeIntensity = 2.0f,
                contrastStrength = 1.6f,
                brightnessOffset = 0.1f,
                saturationBoost = 0.8f,
                tonemapStrength = 1.3f
            };
        }

        public static PostProcessingSettings GetCinematicPreset()
        {
            return new PostProcessingSettings
            {
                edgeThreshold = 0.28f,
                edgeIntensity = 1.3f,
                contrastStrength = 1.25f,
                brightnessOffset = 0.03f,
                saturationBoost = 1.3f,
                tonemapStrength = 1.15f
            };
        }
    }
}
