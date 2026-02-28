using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Medical image post-processing manager
    /// Handles edge enhancement, contrast adjustment, and tone mapping
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MedicalImagePostProcessor : MonoBehaviour
    {
        [SerializeField]
        private PostProcessingSettings settings = new PostProcessingSettings();

        [SerializeField]
        private Material postProcessingMaterial;

        private RenderTexture sourceTexture;
        private RenderTexture destinationTexture;
        private Camera attachedCamera;

        [SerializeField]
        private bool enablePostProcessing = true;

        [SerializeField]
        private string postProcessingShaderName = "VolumeRendering/MedicalImagePostProcessing";

        // Preset selection
        public enum PostProcessingPreset
        {
            Custom,
            Bone,
            Hemorrhage,
            SoftTissue,
            HighContrast,
            Cinematic
        }

        [SerializeField]
        private PostProcessingPreset currentPreset = PostProcessingPreset.Bone;

        private void OnEnable()
        {
            attachedCamera = GetComponent<Camera>();
            if (attachedCamera == null)
            {
                Debug.LogError("MedicalImagePostProcessor must be attached to a Camera");
                enabled = false;
                return;
            }

            // Load post-processing shader
            if (postProcessingMaterial == null)
            {
                Shader ppShader = Shader.Find(postProcessingShaderName);
                if (ppShader != null)
                {
                    postProcessingMaterial = new Material(ppShader);
                }
                else
                {
                    Debug.LogError($"Post-processing shader '{postProcessingShaderName}' not found");
                    enabled = false;
                    return;
                }
            }

            // Apply initial preset
            ApplyPreset(currentPreset);
        }

        private void OnDisable()
        {
            if (sourceTexture != null)
                RenderTexture.ReleaseTemporary(sourceTexture);
            if (destinationTexture != null)
                RenderTexture.ReleaseTemporary(destinationTexture);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!enablePostProcessing || postProcessingMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Update material properties
            UpdateMaterialProperties();

            // Apply post-processing
            Graphics.Blit(source, destination, postProcessingMaterial);
        }

        private void UpdateMaterialProperties()
        {
            if (postProcessingMaterial == null)
                return;

            postProcessingMaterial.SetFloat("_EdgeThreshold", settings.edgeThreshold);
            postProcessingMaterial.SetFloat("_EdgeIntensity", settings.edgeIntensity);
            postProcessingMaterial.SetFloat("_ContrastStrength", settings.contrastStrength);
            postProcessingMaterial.SetFloat("_BrightnessOffset", settings.brightnessOffset);
            postProcessingMaterial.SetFloat("_SaturationBoost", settings.saturationBoost);
            postProcessingMaterial.SetFloat("_TonemapStrength", settings.tonemapStrength);
        }

        /// <summary>
        /// Apply a preset configuration
        /// </summary>
        public void ApplyPreset(PostProcessingPreset preset)
        {
            currentPreset = preset;

            switch (preset)
            {
                case PostProcessingPreset.Bone:
                    settings = PostProcessingSettings.GetBonePreset();
                    break;
                case PostProcessingPreset.Hemorrhage:
                    settings = PostProcessingSettings.GetHemorrhagePreset();
                    break;
                case PostProcessingPreset.SoftTissue:
                    settings = PostProcessingSettings.GetSoftTissuePreset();
                    break;
                case PostProcessingPreset.HighContrast:
                    settings = PostProcessingSettings.GetHighContrastPreset();
                    break;
                case PostProcessingPreset.Cinematic:
                    settings = PostProcessingSettings.GetCinematicPreset();
                    break;
                default:
                    // Custom preset - use current settings
                    break;
            }

            UpdateMaterialProperties();
        }

        /// <summary>
        /// Toggle post-processing on/off
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enablePostProcessing = enabled;
        }

        /// <summary>
        /// Get current settings
        /// </summary>
        public PostProcessingSettings GetSettings()
        {
            return settings;
        }

        /// <summary>
        /// Set custom settings
        /// </summary>
        public void SetSettings(PostProcessingSettings newSettings)
        {
            settings = newSettings;
            currentPreset = PostProcessingPreset.Custom;
            UpdateMaterialProperties();
        }

        // Individual setting adjustments
        public void SetEdgeThreshold(float value)
        {
            settings.edgeThreshold = Mathf.Clamp01(value);
            UpdateMaterialProperties();
        }

        public void SetEdgeIntensity(float value)
        {
            settings.edgeIntensity = Mathf.Clamp(value, 0.0f, 2.0f);
            UpdateMaterialProperties();
        }

        public void SetContrastStrength(float value)
        {
            settings.contrastStrength = Mathf.Clamp(value, 0.0f, 2.0f);
            UpdateMaterialProperties();
        }

        public void SetBrightnessOffset(float value)
        {
            settings.brightnessOffset = Mathf.Clamp(value, -0.5f, 0.5f);
            UpdateMaterialProperties();
        }

        public void SetSaturationBoost(float value)
        {
            settings.saturationBoost = Mathf.Clamp(value, 0.0f, 2.0f);
            UpdateMaterialProperties();
        }

        public void SetTonemapStrength(float value)
        {
            settings.tonemapStrength = Mathf.Clamp(value, 0.0f, 2.0f);
            UpdateMaterialProperties();
        }
    }
}
