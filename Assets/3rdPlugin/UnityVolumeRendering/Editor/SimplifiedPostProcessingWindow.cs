using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;

public class SimplifiedPostProcessingWindow : EditorWindow
{
    private SimplifiedPostProcessor postProcessor;
    private SimplifiedPostProcessingSettings settings;
    private int selectedPreset = 1;  // Default to Normal
    private string[] presetNames = new string[]
    {
        "Minimal",
        "Normal",
        "Bone",
        "Hemorrhage"
    };

    [MenuItem("Window/Medical Imaging/Simplified Post-Processing")]
    public static void ShowWindow()
    {
        GetWindow<SimplifiedPostProcessingWindow>("Simplified Post-Processing");
    }

    private void OnGUI()
    {
        GUILayout.Label("Simplified Medical Image Post-Processing", EditorStyles.boldLabel);
        GUILayout.Label("Subtle enhancement for clinical accuracy", EditorStyles.miniLabel);
        GUILayout.Space(10);

        // Find post processor
        if (postProcessor == null)
        {
            postProcessor = FindObjectOfType<SimplifiedPostProcessor>();
        }

        if (postProcessor == null)
        {
            EditorGUILayout.HelpBox("No SimplifiedPostProcessor found in scene. " +
                "Add it to your main camera.", MessageType.Info);

            if (GUILayout.Button("Create on Main Camera", GUILayout.Height(40)))
            {
                CreatePostProcessor();
            }
            return;
        }

        settings = postProcessor.GetSettings();

        GUILayout.Space(10);
        GUILayout.Label("Presets", EditorStyles.boldLabel);

        int newPreset = GUILayout.SelectionGrid(selectedPreset, presetNames, 2);
        if (newPreset != selectedPreset)
        {
            selectedPreset = newPreset;
            postProcessor.ApplyPreset((SimplifiedPostProcessor.PostProcessingPreset)selectedPreset);
            settings = postProcessor.GetSettings();
        }

        GUILayout.Space(10);
        GUILayout.Label("Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Edge Enhancement
        EditorGUILayout.LabelField("Edge Enhancement (Subtle)", EditorStyles.boldLabel);
        settings.edgeEnhance = EditorGUILayout.Slider("Edge Enhance", settings.edgeEnhance, 0.0f, 1.0f);
        EditorGUILayout.HelpBox("Subtle edge enhancement for better definition. Keep low for clinical accuracy.", MessageType.Info);

        GUILayout.Space(5);

        // Contrast
        EditorGUILayout.LabelField("Contrast (Minimal)", EditorStyles.boldLabel);
        settings.contrast = EditorGUILayout.Slider("Contrast", settings.contrast, 0.8f, 1.2f);
        EditorGUILayout.HelpBox("Minimal contrast adjustment. Values near 1.0 preserve original appearance.", MessageType.Info);

        GUILayout.Space(5);

        // Brightness
        EditorGUILayout.LabelField("Brightness (Very Subtle)", EditorStyles.boldLabel);
        settings.brightness = EditorGUILayout.Slider("Brightness", settings.brightness, -0.1f, 0.1f);

        GUILayout.Space(5);

        // Clarity
        EditorGUILayout.LabelField("Clarity (Mid-tone Contrast)", EditorStyles.boldLabel);
        settings.clarity = EditorGUILayout.Slider("Clarity", settings.clarity, 0.0f, 1.0f);
        EditorGUILayout.HelpBox("Enhances mid-tones without affecting shadows and highlights.", MessageType.Info);

        GUILayout.Space(5);

        // Sharpness
        EditorGUILayout.LabelField("Sharpness (Subtle)", EditorStyles.boldLabel);
        settings.sharpness = EditorGUILayout.Slider("Sharpness", settings.sharpness, 0.0f, 0.5f);
        EditorGUILayout.HelpBox("Subtle sharpening via unsharp mask. Keep low to avoid artifacts.", MessageType.Info);

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // Apply button
        if (GUILayout.Button("Apply Settings", GUILayout.Height(40)))
        {
            postProcessor.SetSettings(settings);
            selectedPreset = 1;  // Reset to Custom (Normal)
        }

        GUILayout.Space(10);

        // Comparison info
        EditorGUILayout.HelpBox(
            "Philosophy: Subtle Enhancement\n\n" +
            "This post-processor uses minimal values to preserve clinical accuracy:\n" +
            "• Edge Enhancement: 0.3 max (vs 2.0 in aggressive mode)\n" +
            "• Contrast: 1.0-1.2 range (vs 1.6 in aggressive mode)\n" +
            "• Sharpness: 0.1-0.15 (vs 0.5+ in aggressive mode)\n\n" +
            "Goal: Enhance visibility without introducing artifacts or misleading details.",
            MessageType.Info
        );

        GUILayout.Space(10);

        // Toggle on/off
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Enable Post-Processing", GUILayout.Height(30)))
        {
            postProcessor.SetEnabled(true);
        }
        if (GUILayout.Button("Disable Post-Processing", GUILayout.Height(30)))
        {
            postProcessor.SetEnabled(false);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void CreatePostProcessor()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            EditorUtility.DisplayDialog("Error", "No main camera found in scene", "OK");
            return;
        }

        SimplifiedPostProcessor pp = mainCamera.gameObject.AddComponent<SimplifiedPostProcessor>();
        postProcessor = pp;
        EditorUtility.DisplayDialog("Success", "Post-processor added to main camera", "OK");
    }
}
