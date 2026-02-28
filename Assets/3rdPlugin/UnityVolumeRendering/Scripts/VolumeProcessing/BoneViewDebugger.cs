using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;
using System.Reflection;

namespace VolumeProcessing
{
    /// <summary>
    /// Debug utility for toggling bone mask isolation on volume renderers
    /// Allows switching between normal volume rendering and bone-only isolation
    /// </summary>
    public class BoneViewDebugger : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField]
        public VolumeRenderedObject volumeRenderer;

        [SerializeField]
        public VolumeDataset boneMaskDataset;

        [SerializeField]
        public UnityVolumeRendering.TransferFunction boneTransferFunction;

        [Header("Bone View Toggle")]
        [Tooltip("Enable to show only bone regions, disable to show full volume")]
        [SerializeField]
        public bool showBoneOnly = false;

        [Header("Bone Rendering Settings")]
        [Tooltip("HU threshold for bone detection (default: 300)")]
        [SerializeField, Range(200f, 600f)]
        public float boneThresholdHU = 300f;

        [Tooltip("Gradient visibility threshold for bone edges")]
        [SerializeField, Range(0.001f, 0.1f)]
        public float gradientThreshold = 0.02f;

        [Header("Debug Options")]
        [Tooltip("Use overlay mode instead of isolate for debugging mask")]
        [SerializeField]
        public bool debugOverlayMode = false;

        private bool previousShowBoneOnly = false;
        private float previousBoneThresholdHU = 300f;
        private float previousGradientThreshold = 0.02f;
        private bool previousDebugOverlayMode = false;

        void OnValidate()
        {
            if (volumeRenderer == null)
            {
                Debug.LogWarning("BoneViewDebugger: VolumeRenderer not assigned!");
                return;
            }

            // Check if settings changed
            bool settingsChanged = showBoneOnly != previousShowBoneOnly ||
                                 boneThresholdHU != previousBoneThresholdHU ||
                                 gradientThreshold != previousGradientThreshold ||
                                 debugOverlayMode != previousDebugOverlayMode;

            if (!settingsChanged)
                return;

            if (showBoneOnly)
            {
                EnableBoneIsolation();
            }
            else
            {
                DisableBoneIsolation();
            }

            // Store current values
            previousShowBoneOnly = showBoneOnly;
            previousBoneThresholdHU = boneThresholdHU;
            previousGradientThreshold = gradientThreshold;
            previousDebugOverlayMode = debugOverlayMode;
        }

        private void EnableBoneIsolation()
        {
            if (boneMaskDataset == null)
            {
                // Generate bone mask if not provided
                if (volumeRenderer.dataset != null)
                {
                    Debug.Log("BoneViewDebugger: Generating bone mask...");
                    boneMaskDataset = BoneMaskExtractor.ExtractBoneMask(volumeRenderer.dataset, boneThresholdHU);
                }
                else
                {
                    Debug.LogError("BoneViewDebugger: No dataset available to generate bone mask!");
                    return;
                }
            }

            if (boneTransferFunction == null)
            {
                // Create bone transfer function if not provided
                Debug.Log("BoneViewDebugger: Creating bone transfer function...");
                if (debugOverlayMode)
                {
                    // For overlay mode, use smoother transition
                    boneTransferFunction = CreateOverlayBoneTransferFunction();
                }
                else
                {
                    // For isolate mode, use sharp threshold to avoid pixelation
                    boneTransferFunction = CreateIsolateBoneTransferFunction();
                }
            }

            // Configure volume renderer for bone isolation
            // ✅ 关键修复：使用 AddSegmentation 而不是 SetOverlayDataset
            // 这样会自动设置 overlayType 为 Segmentation 而不是 Overlay
            volumeRenderer.ClearSegmentations();
            volumeRenderer.AddSegmentation(boneMaskDataset, new List<SegmentationLabel>
            {
                new SegmentationLabel
                {
                    id = 1,
                    name = "Bone",
                    colour = Color.white
                }
            });
            volumeRenderer.SetSecondaryTransferFunction(boneTransferFunction);

            if (debugOverlayMode)
            {
                // Use overlay mode for debugging
                volumeRenderer.SetSegmentationRenderMode(SegmentationRenderMode.OverlayColour);
                Debug.Log("BoneViewDebugger: Bone mask enabled in OVERLAY mode for debugging");
            }
            else
            {
                // Use isolate mode for clean bone-only view
                volumeRenderer.SetSegmentationRenderMode(SegmentationRenderMode.Isolate);
                Debug.Log("BoneViewDebugger: Bone mask enabled in ISOLATE mode");
            }

            // Optimize rendering settings
            volumeRenderer.SetLightingEnabled(true);
            volumeRenderer.SetGradientVisibilityThreshold(gradientThreshold);
            volumeRenderer.SetGradientLightingThreshold(new Vector2(0.01f, 0.05f));

            // Update material
            volumeRenderer.UpdateMaterialProperties();

            // Debug: 验证设置是否正确
            Debug.Log($"BoneViewDebugger: OverlayType = {volumeRenderer.GetOverlayType()}");
            Debug.Log($"BoneViewDebugger: SegmentationRenderMode = {volumeRenderer.GetSegmentationRenderMode()}");
            Debug.Log($"BoneViewDebugger: MULTIVOLUME_ISOLATE = {volumeRenderer.meshRenderer.sharedMaterial.IsKeywordEnabled("MULTIVOLUME_ISOLATE")}");
            
            // Debug: 检查Transfer Function设置
            if (boneTransferFunction != null)
            {
                Debug.Log($"BoneViewDebugger: Bone TF Alpha Points = {boneTransferFunction.alphaControlPoints.Count}");
                foreach (var alphaPoint in boneTransferFunction.alphaControlPoints)
                {
                    Debug.Log($"  Alpha: dataValue={alphaPoint.dataValue}, alpha={alphaPoint.alphaValue}");
                }
            }
        }

        private void DisableBoneIsolation()
        {
            // Clear overlay to return to normal volume rendering
            volumeRenderer.ClearSegmentations();
            Debug.Log("BoneViewDebugger: Bone mask disabled, returned to normal volume rendering");
        }

        /// <summary>
        /// Public method to toggle bone view programmatically
        /// </summary>
        public void ToggleBoneView()
        {
            showBoneOnly = !showBoneOnly;
            OnValidate();
        }

        /// <summary>
        /// Public method to enable bone view
        /// </summary>
        public void EnableBoneView()
        {
            showBoneOnly = true;
            OnValidate();
        }

        /// <summary>
        /// Public method to disable bone view
        /// </summary>
        public void DisableBoneView()
        {
            showBoneOnly = false;
            OnValidate();
        }

        /// <summary>
        /// Regenerate bone mask with current threshold
        /// </summary>
        public void RegenerateBoneMask()
        {
            if (volumeRenderer.dataset != null)
            {
                boneMaskDataset = BoneMaskExtractor.ExtractBoneMask(volumeRenderer.dataset, boneThresholdHU);
                if (showBoneOnly)
                {
                    EnableBoneIsolation();
                }
                Debug.Log($"BoneViewDebugger: Bone mask regenerated with threshold {boneThresholdHU} HU");
            }
        }

        /// <summary>
        /// Creates a transfer function optimized for bone overlay mode with smooth transitions
        /// </summary>
        private UnityVolumeRendering.TransferFunction CreateOverlayBoneTransferFunction()
        {
            UnityVolumeRendering.TransferFunction boneTF = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            // Clear existing points
            boneTF.alphaControlPoints.Clear();
            boneTF.colourControlPoints.Clear();
            
            // Smooth transition for overlay mode
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, 1.0f));
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            boneTF.colourControlPoints.Add(new TFColourControlPoint(1.0f, Color.white));
            
            boneTF.GenerateTexture();
            
            Debug.Log("BoneViewDebugger: Created smooth Transfer Function for Overlay mode");
            return boneTF;
        }

        /// <summary>
        /// Creates a transfer function optimized for bone isolation with sharp thresholds
        /// This prevents pixelation artifacts in Isolate mode
        /// </summary>
        private UnityVolumeRendering.TransferFunction CreateIsolateBoneTransferFunction()
        {
            UnityVolumeRendering.TransferFunction boneTF = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            // Clear existing points
            boneTF.alphaControlPoints.Clear();
            boneTF.colourControlPoints.Clear();
            
            // Sharp threshold for isolate mode: values < 0.5 are transparent, >= 0.5 are fully opaque
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));    // Start fully transparent
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.49f, 0.0f));   // Stay transparent until just before 0.5
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, 1.0f));    // Jump to full opacity at 0.5
            boneTF.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));    // Stay fully opaque
            
            // White color for bones
            boneTF.colourControlPoints.Add(new TFColourControlPoint(0.5f, Color.white));
            boneTF.colourControlPoints.Add(new TFColourControlPoint(1.0f, Color.white));
            
            boneTF.GenerateTexture();
            
            Debug.Log("BoneViewDebugger: Created sharp-threshold Transfer Function for Isolate mode");
            return boneTF;
        }
    }
}
