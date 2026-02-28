using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Simplified post-processing for medical imaging
    /// Focuses on subtle enhancement rather than aggressive effects
    /// </summary>
    [System.Serializable]
    public class SimplifiedPostProcessingSettings
    {
        [Header("Subtle Enhancement")]
        [Range(0.0f, 1.0f)]
        public float edgeEnhance = 0.3f;  // Very subtle edge enhancement
        
        [Range(0.8f, 1.2f)]
        public float contrast = 1.0f;  // Minimal contrast change
        
        [Range(-0.1f, 0.1f)]
        public float brightness = 0.0f;  // Very subtle brightness

        [Header("Clarity")]
        [Range(0.0f, 1.0f)]
        public float clarity = 0.2f;  // Mid-tone contrast
        
        [Range(0.0f, 0.5f)]
        public float sharpness = 0.1f;  // Subtle sharpening

        // Medical-specific presets
        public static SimplifiedPostProcessingSettings GetBonePreset()
        {
            return new SimplifiedPostProcessingSettings
            {
                edgeEnhance = 0.4f,
                contrast = 1.05f,
                brightness = 0.02f,
                clarity = 0.25f,
                sharpness = 0.15f
            };
        }

        public static SimplifiedPostProcessingSettings GetHemorrhagePreset()
        {
            return new SimplifiedPostProcessingSettings
            {
                edgeEnhance = 0.35f,
                contrast = 1.03f,
                brightness = 0.0f,
                clarity = 0.2f,
                sharpness = 0.1f
            };
        }

        public static SimplifiedPostProcessingSettings GetMinimalPreset()
        {
            return new SimplifiedPostProcessingSettings
            {
                edgeEnhance = 0.1f,
                contrast = 1.0f,
                brightness = 0.0f,
                clarity = 0.1f,
                sharpness = 0.05f
            };
        }

        public static SimplifiedPostProcessingSettings GetNormalPreset()
        {
            return new SimplifiedPostProcessingSettings
            {
                edgeEnhance = 0.25f,
                contrast = 1.02f,
                brightness = 0.01f,
                clarity = 0.15f,
                sharpness = 0.08f
            };
        }
    }

    /// <summary>
    /// Simplified post-processor for medical imaging
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SimplifiedPostProcessor : MonoBehaviour
    {
        [SerializeField]
        private SimplifiedPostProcessingSettings settings = new SimplifiedPostProcessingSettings();

        [SerializeField]
        private Material postProcessingMaterial;

        private Camera attachedCamera;

        [SerializeField]
        private bool enablePostProcessing = true;

        [SerializeField]
        private string postProcessingShaderName = "VolumeRendering/SimplifiedMedicalPostProcessing";

        public enum PostProcessingPreset
        {
            Minimal,
            Normal,
            Bone,
            Hemorrhage
        }

        [SerializeField]
        private PostProcessingPreset currentPreset = PostProcessingPreset.Normal;

        private void OnEnable()
        {
            attachedCamera = GetComponent<Camera>();
            if (attachedCamera == null)
            {
                Debug.LogError("SimplifiedPostProcessor must be attached to a Camera");
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
                    Debug.LogWarning($"Post-processing shader '{postProcessingShaderName}' not found. Post-processing disabled.");
                    enabled = false;
                    return;
                }
            }

            ApplyPreset(currentPreset);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!enablePostProcessing || postProcessingMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            UpdateMaterialProperties();
            Graphics.Blit(source, destination, postProcessingMaterial);
        }

        private void UpdateMaterialProperties()
        {
            if (postProcessingMaterial == null)
                return;

            postProcessingMaterial.SetFloat("_EdgeEnhance", settings.edgeEnhance);
            postProcessingMaterial.SetFloat("_Contrast", settings.contrast);
            postProcessingMaterial.SetFloat("_Brightness", settings.brightness);
            postProcessingMaterial.SetFloat("_Clarity", settings.clarity);
            postProcessingMaterial.SetFloat("_Sharpness", settings.sharpness);
        }

        public void ApplyPreset(PostProcessingPreset preset)
        {
            currentPreset = preset;

            switch (preset)
            {
                case PostProcessingPreset.Minimal:
                    settings = SimplifiedPostProcessingSettings.GetMinimalPreset();
                    break;
                case PostProcessingPreset.Normal:
                    settings = SimplifiedPostProcessingSettings.GetNormalPreset();
                    break;
                case PostProcessingPreset.Bone:
                    settings = SimplifiedPostProcessingSettings.GetBonePreset();
                    break;
                case PostProcessingPreset.Hemorrhage:
                    settings = SimplifiedPostProcessingSettings.GetHemorrhagePreset();
                    break;
            }

            UpdateMaterialProperties();
        }

        public void SetEnabled(bool enabled)
        {
            enablePostProcessing = enabled;
        }

        public SimplifiedPostProcessingSettings GetSettings()
        {
            return settings;
        }

        public void SetSettings(SimplifiedPostProcessingSettings newSettings)
        {
            settings = newSettings;
            currentPreset = PostProcessingPreset.Normal;
            UpdateMaterialProperties();
        }
    }
}
