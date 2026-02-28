using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;

namespace VolumeProcessing
{
    /// <summary>
    /// Enhanced bone rendering with optimized transparency and default TF compatibility
    /// Fixed transparency issues while maintaining edge clarity
    /// </summary>
    public class FixedBoneRenderer : MonoBehaviour
    {
        [Header("Required References")]
        public VolumeRenderedObject volumeRenderer;
        
        [Header("Rendering Modes")]
        public BoneRenderMode renderMode = BoneRenderMode.DefaultCompatible;
        
        [Header("Bone Detection Settings")]
        [Tooltip("HU threshold for bone detection")]
        [Range(200f, 600f)]
        public float boneThresholdHU = 300f;
        
        [Header("Transparency Fix")]
        [Tooltip("Minimum opacity for bones to avoid transparency")]
        [Range(0.1f, 1.0f)]
        public float minBoneOpacity = 0.7f;
        
        [Tooltip("Opacity boost factor for thin bones")]
        [Range(1.0f, 3.0f)]
        public float opacityBoost = 1.5f;
        
        [Header("Edge Enhancement")]
        [Tooltip("Enable gradient lighting for better edge definition")]
        public bool enableGradientLighting = true;
        
        [Tooltip("Gradient visibility threshold")]
        [Range(0.001f, 0.05f)]
        public float gradientThreshold = 0.02f;

        private UnityVolumeRendering.TransferFunction optimizedBoneTF;
        private UnityVolumeRendering.TransferFunction defaultCompatibleTF;

        void Start()
        {
            if (volumeRenderer != null && volumeRenderer.dataset != null)
            {
                SetupTransferFunctions();
                ApplyRenderMode();
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying && volumeRenderer != null)
            {
                SetupTransferFunctions();
                ApplyRenderMode();
            }
        }

        private void SetupTransferFunctions()
        {
            // 1. Optimized bone transfer function
            optimizedBoneTF = CreateOptimizedBoneTF();
            
            // 2. Default compatible transfer function
            defaultCompatibleTF = CreateDefaultCompatibleTF();
        }

        private UnityVolumeRendering.TransferFunction CreateOptimizedBoneTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 修复透明问题：更积极的Alpha曲线
            // 0-0.2: 开始显现
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.2f, minBoneOpacity * 0.3f));
            
            // 0.2-0.5: 骨骼区域，显著提高透明度
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.2f, minBoneOpacity * 0.3f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.35f, minBoneOpacity * 0.8f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, minBoneOpacity * 1.0f));
            
            // 0.5-1.0: 高密度骨骼，完全不透明
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, minBoneOpacity * 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.7f, minBoneOpacity * 1.0f * opacityBoost));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, minBoneOpacity * 1.0f * opacityBoost));
            
            // 白色骨骼
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.8f, 0.8f, 0.8f, 0.0f)));
            // tf.colourControlPoints.Add(new TFAlphaControlPoint(0.2f, new Color(0.9f, 0.9f, 0.9f, minBoneOpacity * 0.3f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.5f, new Color(1.0f, 1.0f, 1.0f, minBoneOpacity * 1.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, minBoneOpacity * 1.0f * opacityBoost)));
            
            tf.GenerateTexture();
            return tf;
        }

        private UnityVolumeRendering.TransferFunction CreateDefaultCompatibleTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 基于默认TF但增强骨骼部分
            // 保持低密度组织的可见性
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.1f, 0.1f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.2f, 0.2f));
            
            // 软组织区域
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, 0.3f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.4f, 0.4f));
            
            // 骨骼开始区域
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.45f, 0.6f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.6f, 0.8f));
            
            // 骨骼主体
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.7f, 0.9f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.85f, 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 渐变色彩
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.6f, 0.6f, 0.8f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.2f, new Color(0.7f, 0.7f, 0.8f, 0.2f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.4f, new Color(0.8f, 0.8f, 0.9f, 0.4f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.6f, new Color(0.9f, 0.9f, 0.95f, 0.8f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.8f, new Color(1.0f, 1.0f, 1.0f, 0.9f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        private void ApplyRenderMode()
        {
            if (volumeRenderer == null) return;

            switch (renderMode)
            {
                case BoneRenderMode.DefaultCompatible:
                    EnableDefaultCompatibleMode();
                    break;
                    
                case BoneRenderMode.OptimizedBone:
                    EnableOptimizedBoneMode();
                    break;
                    
                case BoneRenderMode.HighContrast:
                    EnableHighContrastMode();
                    break;
            }
        }

        private void EnableDefaultCompatibleMode()
        {
            volumeRenderer.ClearSegmentations();
            volumeRenderer.SetTransferFunction(defaultCompatibleTF);
            
            if (enableGradientLighting)
            {
                volumeRenderer.SetLightingEnabled(true);
                volumeRenderer.SetGradientVisibilityThreshold(gradientThreshold);
                volumeRenderer.SetGradientLightingThreshold(new Vector2(gradientThreshold * 0.5f, gradientThreshold * 2f));
            }
            
            volumeRenderer.UpdateMaterialProperties();
            Debug.Log("FixedBoneRenderer: Default-compatible mode enabled with fixed transparency");
        }

        private void EnableOptimizedBoneMode()
        {
            volumeRenderer.ClearSegmentations();
            volumeRenderer.SetTransferFunction(optimizedBoneTF);
            
            if (enableGradientLighting)
            {
                volumeRenderer.SetLightingEnabled(true);
                volumeRenderer.SetGradientVisibilityThreshold(gradientThreshold * 0.8f);
                volumeRenderer.SetGradientLightingThreshold(new Vector2(gradientThreshold * 0.3f, gradientThreshold * 1.5f));
            }
            
            volumeRenderer.UpdateMaterialProperties();
            Debug.Log("FixedBoneRenderer: Optimized bone mode enabled with enhanced opacity");
        }

        private void EnableHighContrastMode()
        {
            var highContrastTF = CreateHighContrastTF();
            
            volumeRenderer.ClearSegmentations();
            volumeRenderer.SetTransferFunction(highContrastTF);
            
            volumeRenderer.SetLightingEnabled(true);
            volumeRenderer.SetGradientVisibilityThreshold(gradientThreshold * 0.5f);
            volumeRenderer.SetGradientLightingThreshold(new Vector2(gradientThreshold * 0.2f, gradientThreshold * 1.0f));
            
            volumeRenderer.UpdateMaterialProperties();
            Debug.Log("FixedBoneRenderer: High contrast mode enabled");
        }

        private UnityVolumeRendering.TransferFunction CreateHighContrastTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 高对比度模式：更激进的骨骼增强
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.25f, 0.0f));
            
            // 骨骼区域急剧增强
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.4f, 0.8f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.6f, 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 白色骨骼
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.9f, 0.9f, 0.9f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.4f, new Color(0.95f, 0.95f, 0.95f, 0.8f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.6f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        public void SetRenderMode(BoneRenderMode mode)
        {
            renderMode = mode;
            ApplyRenderMode();
        }

        public void FixTransparency()
        {
            // 紧急修复透明问题
            minBoneOpacity = Mathf.Max(minBoneOpacity, 0.7f);
            opacityBoost = Mathf.Max(opacityBoost, 1.2f);
            
            SetupTransferFunctions();
            ApplyRenderMode();
            
            Debug.Log($"FixedBoneRenderer: Transparency fixed - MinOpacity: {minBoneOpacity}, Boost: {opacityBoost}");
        }

        void OnDestroy()
        {
            if (optimizedBoneTF != null) Destroy(optimizedBoneTF);
            if (defaultCompatibleTF != null) Destroy(defaultCompatibleTF);
        }
    }

    // public enum BoneRenderMode
    // {
    //     DefaultCompatible,    // 与默认TF兼容，推荐
    //     OptimizedBone,       // 优化骨骼显示
    //     HighContrast        // 高对比度模式
    // }
}
