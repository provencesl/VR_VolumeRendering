using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;

namespace VolumeProcessing
{
    /// <summary>
    /// Enhanced bone rendering with multiple visualization modes
    /// Provides better balance between pure bone isolation and edge clarity
    /// </summary>
    public class EnhancedBoneRenderer : MonoBehaviour
    {
        [Header("Required References")]
        public VolumeRenderedObject volumeRenderer;
        public VolumeDataset boneMaskDataset;
        
        [Header("Rendering Modes")]
        [Tooltip("Choose bone visualization mode")]
        public BoneRenderMode renderMode = BoneRenderMode.EdgeEnhanced;
        
        [Header("Bone Enhancement Settings")]
        [Tooltip("HU threshold for bone detection")]
        [Range(200f, 600f)]
        public float boneThresholdHU = 300f;
        
        [Tooltip("Edge enhancement factor")]
        [Range(0.5f, 3.0f)]
        public float edgeEnhancement = 1.5f;
        
        [Tooltip("Preserve soft tissue opacity around bones")]
        [Range(0.0f, 0.5f)]
        public float softTissuePreservation = 0.2f;
        
        [Tooltip("Edge thickness for soft tissue preservation")]
        [Range(1f, 10f)]
        public int edgeThickness = 3;

        private UnityVolumeRendering.TransferFunction enhancedBoneTF;
        private UnityVolumeRendering.TransferFunction edgeEnhancedTF;
        private UnityVolumeRendering.TransferFunction hybridTF;

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
            // 1. Basic bone transfer function
            enhancedBoneTF = CreateEnhancedBoneTF();
            
            // 2. Edge-enhanced transfer function
            edgeEnhancedTF = CreateEdgeEnhancedTF();
            
            // 3. Hybrid mode (bone + soft tissue edges)
            hybridTF = CreateHybridTF();
        }

        private UnityVolumeRendering.TransferFunction CreateEnhancedBoneTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            // 更精确的骨骼 Alpha 曲线
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 0-0.3: 透明
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, 0.0f));
            
            // 0.3-0.7: 骨骼开始出现
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, 0.1f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.5f, 0.8f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.7f, 1.0f));
            
            // 0.7-1.0: 高密度骨骼
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.7f, 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 白色骨骼
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(1.0f, 1.0f, 1.0f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.3f, new Color(1.0f, 1.0f, 1.0f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.5f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        private UnityVolumeRendering.TransferFunction CreateEdgeEnhancedTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 边缘增强模式：强调骨骼边界
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.25f, 0.0f));
            
            // 骨骼边缘区域
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.25f, 0.3f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.4f, 0.9f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.6f, 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 渐变色彩从浅灰到白色
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.8f, 0.8f, 0.8f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.25f, new Color(0.8f, 0.8f, 0.8f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.4f, new Color(0.9f, 0.9f, 0.9f, 0.3f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.6f, new Color(1.0f, 1.0f, 1.0f, 0.9f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        private UnityVolumeRendering.TransferFunction CreateHybridTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 混合模式：保留骨骼周围的软组织作为对比
            // 0-0.2: 软组织
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, softTissuePreservation * 0.5f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.1f, softTissuePreservation * 0.8f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.2f, softTissuePreservation));
            
            // 0.2-0.4: 软组织到骨骼的过渡
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.2f, softTissuePreservation));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, softTissuePreservation * 1.2f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.4f, 0.6f));
            
            // 0.4-1.0: 骨骼主体
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.4f, 0.6f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.6f, 1.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 色彩：软组织到骨骼的渐变
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.6f, 0.6f, 0.6f, softTissuePreservation * 0.5f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.2f, new Color(0.7f, 0.7f, 0.7f, softTissuePreservation)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.4f, new Color(0.8f, 0.8f, 0.8f, 0.6f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.6f, new Color(0.9f, 0.9f, 0.9f, 1.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        private void ApplyRenderMode()
        {
            if (volumeRenderer == null) return;

            switch (renderMode)
            {
                case BoneRenderMode.PureIsolation:
                    EnablePureBoneIsolation();
                    break;
                    
                case BoneRenderMode.EdgeEnhanced:
                    EnableEdgeEnhancedMode();
                    break;
                    
                case BoneRenderMode.Hybrid:
                    EnableHybridMode();
                    break;
                    
                case BoneRenderMode.GradientBased:
                    EnableGradientBasedMode();
                    break;
            }
        }

        private void EnablePureBoneIsolation()
        {
            if (boneMaskDataset == null)
            {
                // 确保有数据集
                if (volumeRenderer.dataset != null)
                {
                    boneMaskDataset = VolumeProcessing.BoneMaskExtractor.ExtractBoneMask(volumeRenderer.dataset, boneThresholdHU);
                }
                else
                {
                    Debug.LogWarning("EnhancedBoneRenderer: No dataset available for bone mask generation");
                    return;
                }
            }
            
            volumeRenderer.SetOverlayDataset(boneMaskDataset);
            volumeRenderer.SetSecondaryTransferFunction(enhancedBoneTF);
            volumeRenderer.SetSegmentationRenderMode(SegmentationRenderMode.Isolate);
            volumeRenderer.UpdateMaterialProperties();
            
            Debug.Log("EnhancedBoneRenderer: Pure bone isolation enabled");
        }

        private void EnableEdgeEnhancedMode()
        {
            // 清除之前的分割
            volumeRenderer.ClearSegmentations();
            
            // 使用增强的传递函数，但保持原始数据集
            volumeRenderer.SetTransferFunction(edgeEnhancedTF);
            
            // 启用梯度照明增强边缘
            volumeRenderer.SetLightingEnabled(true);
            volumeRenderer.SetGradientVisibilityThreshold(0.01f);
            volumeRenderer.SetGradientLightingThreshold(new Vector2(0.01f, 0.1f));
            
            volumeRenderer.UpdateMaterialProperties();
            
            Debug.Log("EnhancedBoneRenderer: Edge-enhanced mode enabled");
        }

        private void EnableHybridMode()
        {
            // 清除分割
            volumeRenderer.ClearSegmentations();
            
            // 使用混合传递函数
            volumeRenderer.SetTransferFunction(hybridTF);
            
            // 启用照明
            volumeRenderer.SetLightingEnabled(true);
            volumeRenderer.SetGradientVisibilityThreshold(0.02f);
            volumeRenderer.SetGradientLightingThreshold(new Vector2(0.02f, 0.08f));
            
            volumeRenderer.UpdateMaterialProperties();
            
            Debug.Log("EnhancedBoneRenderer: Hybrid mode enabled");
        }

        private void EnableGradientBasedMode()
        {
            // 创建基于梯度的增强传递函数
            var gradientTF = CreateGradientBasedTF();
            
            // 清除分割
            volumeRenderer.ClearSegmentations();
            volumeRenderer.SetTransferFunction(gradientTF);
            
            // 高度依赖梯度信息
            volumeRenderer.SetLightingEnabled(true);
            volumeRenderer.SetGradientVisibilityThreshold(0.005f);
            volumeRenderer.SetGradientLightingThreshold(new Vector2(0.005f, 0.05f));
            
            volumeRenderer.UpdateMaterialProperties();
            
            Debug.Log("EnhancedBoneRenderer: Gradient-based mode enabled");
        }

        private UnityVolumeRendering.TransferFunction CreateGradientBasedTF()
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            
            tf.alphaControlPoints.Clear();
            tf.colourControlPoints.Clear();
            
            // 基于梯度的传递函数
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.0f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.3f, 0.0f));
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.45f, 0.7f)); // 骨骼边缘
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.65f, 1.0f)); // 骨骼主体
            tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.0f, 1.0f));
            
            // 强调边缘的渐变色彩
            tf.colourControlPoints.Add(new TFColourControlPoint(0.0f, new Color(0.7f, 0.7f, 0.7f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.3f, new Color(0.7f, 0.7f, 0.7f, 0.0f)));
            tf.colourControlPoints.Add(new TFColourControlPoint(0.45f, new Color(0.95f, 0.95f, 0.95f, 0.7f))); // 边缘
            tf.colourControlPoints.Add(new TFColourControlPoint(0.65f, new Color(1.0f, 1.0f, 1.0f, 1.0f))); // 主体
            tf.colourControlPoints.Add(new TFColourControlPoint(1.0f, new Color(1.0f, 1.0f, 1.0f, 1.0f)));
            
            tf.GenerateTexture();
            return tf;
        }

        public void SetRenderMode(BoneRenderMode mode)
        {
            renderMode = mode;
            ApplyRenderMode();
        }

        public void RegenerateBoneMask()
        {
            if (volumeRenderer != null && volumeRenderer.dataset != null)
            {
                boneMaskDataset = VolumeProcessing.BoneMaskExtractor.ExtractBoneMask(volumeRenderer.dataset, boneThresholdHU);
                if (renderMode == BoneRenderMode.PureIsolation)
                {
                    EnablePureBoneIsolation();
                }
            }
        }

        void OnDestroy()
        {
            // 清理资源
            if (enhancedBoneTF != null) Destroy(enhancedBoneTF);
            if (edgeEnhancedTF != null) Destroy(edgeEnhancedTF);
            if (hybridTF != null) Destroy(hybridTF);
        }
    }

    public enum BoneRenderMode
    {
        PureIsolation,      // 纯骨骼隔离 (当前 BoneMask 模式)
        EdgeEnhanced,       // 边缘增强模式
        Hybrid,            // 混合模式 (推荐)
        GradientBased,      // 基于梯度的模式
    
        DefaultCompatible,    // 与默认TF兼容，推荐
        OptimizedBone,       // 优化骨骼显示
        HighContrast        // 高对比度模式
    }
}
