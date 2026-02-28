using UnityEngine;
using System.Collections.Generic;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Medical imaging transfer function presets for bone, hemorrhage, and soft tissue visualization
    /// Optimized for high-definition medical image rendering
    /// </summary>
    public static class MedicalTransferFunctionPresets
    {
        /// <summary>
        /// Bone visualization preset
        /// Optimized for CT imaging with HU range 200-3000
        /// High contrast white/gray rendering with sharp edges
        /// </summary>
        public static TransferFunction2D CreateBonePreset()
        {
            TransferFunction2D tf2d = ScriptableObject.CreateInstance<TransferFunction2D>();
            
            // Bone region: HU 200-1000 (cortical bone)
            // High opacity, white color for sharp visualization
            var boneBox1 = new TransferFunction2D.TF2DBox
            {
                minX = 0.15f,  // HU 200 (normalized)
                maxX = 0.50f,  // HU 1000
                minY = 0.6f,   // Gradient threshold
                maxY = 1.0f,
                colour = new Color(1.0f, 1.0f, 1.0f, 1.0f),  // White
                alpha = 0.95f,
                minAlpha = 0.8f
            };
            tf2d.boxes.Add(boneBox1);

            // Dense bone: HU 1000-3000 (very bright)
            var boneBox2 = new TransferFunction2D.TF2DBox
            {
                minX = 0.50f,  // HU 1000
                maxX = 1.0f,   // HU 3000+
                minY = 0.5f,
                maxY = 1.0f,
                colour = new Color(1.0f, 1.0f, 1.0f, 1.0f),  // White
                alpha = 1.0f,
                minAlpha = 0.9f
            };
            tf2d.boxes.Add(boneBox2);

            // Fracture edges (high gradient): HU 200-800 with high gradient
            var fractureBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.15f,
                maxX = 0.40f,
                minY = 0.7f,   // High gradient for edge detection
                maxY = 1.0f,
                colour = new Color(1.0f, 0.8f, 0.0f, 1.0f),  // Gold for fracture edges
                alpha = 1.0f,
                minAlpha = 0.9f
            };
            tf2d.boxes.Add(fractureBox);

            tf2d.GenerateTexture();
            return tf2d;
        }

        /// <summary>
        /// Hemorrhage visualization preset
        /// Optimized for CT imaging with HU range 30-100 (acute bleeding)
        /// Red/orange gradient for blood visualization
        /// </summary>
        public static TransferFunction2D CreateHemorrhagePreset()
        {
            TransferFunction2D tf2d = ScriptableObject.CreateInstance<TransferFunction2D>();

            // Acute hemorrhage: HU 30-80 (bright red)
            var acuteBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.20f,  // HU 30
                maxX = 0.40f,  // HU 80
                minY = 0.4f,
                maxY = 1.0f,
                colour = new Color(1.0f, 0.2f, 0.1f, 1.0f),  // Bright red
                alpha = 0.85f,
                minAlpha = 0.7f
            };
            tf2d.boxes.Add(acuteBox);

            // Subacute hemorrhage: HU 40-100 (orange-red)
            var subacuteBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.25f,  // HU 40
                maxX = 0.50f,  // HU 100
                minY = 0.3f,
                maxY = 0.7f,
                colour = new Color(1.0f, 0.5f, 0.1f, 1.0f),  // Orange
                alpha = 0.75f,
                minAlpha = 0.6f
            };
            tf2d.boxes.Add(subacuteBox);

            // Hemorrhage edges (high gradient)
            var edgeBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.20f,
                maxX = 0.45f,
                minY = 0.75f,  // High gradient for edge detection
                maxY = 1.0f,
                colour = new Color(1.0f, 0.0f, 0.0f, 1.0f),  // Pure red for edges
                alpha = 1.0f,
                minAlpha = 0.85f
            };
            tf2d.boxes.Add(edgeBox);

            tf2d.GenerateTexture();
            return tf2d;
        }

        /// <summary>
        /// Soft tissue visualization preset
        /// Optimized for CT imaging with HU range -100 to 100
        /// Gray gradient for soft tissue contrast
        /// </summary>
        public static TransferFunction2D CreateSoftTissuePreset()
        {
            TransferFunction2D tf2d = ScriptableObject.CreateInstance<TransferFunction2D>();

            // Fat tissue: HU -100 to -50
            var fatBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.0f,
                maxX = 0.25f,
                minY = 0.3f,
                maxY = 1.0f,
                colour = new Color(0.8f, 0.7f, 0.6f, 1.0f),  // Light gray
                alpha = 0.6f,
                minAlpha = 0.4f
            };
            tf2d.boxes.Add(fatBox);

            // Muscle/organ tissue: HU 0-50
            var muscleBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.30f,
                maxX = 0.50f,
                minY = 0.3f,
                maxY = 1.0f,
                colour = new Color(0.6f, 0.5f, 0.5f, 1.0f),  // Medium gray
                alpha = 0.7f,
                minAlpha = 0.5f
            };
            tf2d.boxes.Add(muscleBox);

            // Dense tissue: HU 50-100
            var denseBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.50f,
                maxX = 0.70f,
                minY = 0.3f,
                maxY = 1.0f,
                colour = new Color(0.4f, 0.3f, 0.3f, 1.0f),  // Dark gray
                alpha = 0.8f,
                minAlpha = 0.6f
            };
            tf2d.boxes.Add(denseBox);

            // Tissue edges (high gradient)
            var edgeBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.25f,
                maxX = 0.70f,
                minY = 0.75f,
                maxY = 1.0f,
                colour = new Color(0.9f, 0.9f, 0.9f, 1.0f),  // Bright gray for edges
                alpha = 0.9f,
                minAlpha = 0.7f
            };
            tf2d.boxes.Add(edgeBox);

            tf2d.GenerateTexture();
            return tf2d;
        }

        /// <summary>
        /// Combined visualization: Bone + Hemorrhage
        /// Shows both bone structure and bleeding areas simultaneously
        /// </summary>
        public static TransferFunction2D CreateBoneHemorrhageComboPreset()
        {
            TransferFunction2D tf2d = ScriptableObject.CreateInstance<TransferFunction2D>();

            // Hemorrhage: HU 30-100 (red)
            var hemorrhageBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.20f,
                maxX = 0.50f,
                minY = 0.4f,
                maxY = 1.0f,
                colour = new Color(1.0f, 0.3f, 0.2f, 1.0f),
                alpha = 0.8f,
                minAlpha = 0.6f
            };
            tf2d.boxes.Add(hemorrhageBox);

            // Bone: HU 200-3000 (white)
            var boneBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.50f,
                maxX = 1.0f,
                minY = 0.5f,
                maxY = 1.0f,
                colour = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                alpha = 0.9f,
                minAlpha = 0.8f
            };
            tf2d.boxes.Add(boneBox);

            tf2d.GenerateTexture();
            return tf2d;
        }

        /// <summary>
        /// High-contrast diagnostic preset
        /// Maximum contrast for clinical diagnosis
        /// </summary>
        public static TransferFunction2D CreateHighContrastPreset()
        {
            TransferFunction2D tf2d = ScriptableObject.CreateInstance<TransferFunction2D>();

            // Very high contrast: only show dense structures
            var denseBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.40f,
                maxX = 1.0f,
                minY = 0.6f,
                maxY = 1.0f,
                colour = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                alpha = 1.0f,
                minAlpha = 0.95f
            };
            tf2d.boxes.Add(denseBox);

            // Edge enhancement
            var edgeBox = new TransferFunction2D.TF2DBox
            {
                minX = 0.30f,
                maxX = 0.70f,
                minY = 0.8f,
                maxY = 1.0f,
                colour = new Color(1.0f, 1.0f, 0.0f, 1.0f),
                alpha = 1.0f,
                minAlpha = 0.9f
            };
            tf2d.boxes.Add(edgeBox);

            tf2d.GenerateTexture();
            return tf2d;
        }

        /// <summary>
        /// Save preset to file
        /// </summary>
        public static void SavePreset(TransferFunction2D tf2d, string presetName)
        {
            string path = $"Assets/Resources/TransferFunctionPresets/{presetName}.asset";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.CreateAsset(tf2d, path);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }

        /// <summary>
        /// Load preset from file
        /// </summary>
        public static TransferFunction2D LoadPreset(string presetName)
        {
            string path = $"Assets/Resources/TransferFunctionPresets/{presetName}.asset";
            return Resources.Load<TransferFunction2D>(path);
        }
    }
}
