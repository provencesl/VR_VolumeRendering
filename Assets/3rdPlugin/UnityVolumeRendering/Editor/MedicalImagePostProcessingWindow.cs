using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;

public class MedicalImagePostProcessingWindow : EditorWindow
{
    private MedicalImagePostProcessor postProcessor;
    private PostProcessingSettings settings;
    private int selectedPreset = 0;
    private string[] presetNames = new string[]
    {
        "Custom",
        "Bone",
        "Hemorrhage",
        "Soft Tissue",
        "High Contrast",
        "Cinematic"
    };

    private Vector2 scrollPosition = Vector2.zero;

    [MenuItem("Window/Medical Imaging/Post-Processing")]
    public static void ShowWindow()
    {
        GetWindow<MedicalImagePostProcessingWindow>("Medical Post-Processing");
    }

    private void OnGUI()
    {
        GUILayout.Label("Medical Image Post-Processing", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Find post processor in scene
        if (postProcessor == null)
        {
            postProcessor = FindObjectOfType<MedicalImagePostProcessor>();
        }

        if (postProcessor == null)
        {
            EditorGUILayout.HelpBox("No MedicalImagePostProcessor found in scene. " +
                "Add it to your main camera.", MessageType.Info);

            if (GUILayout.Button("Create Post-Processor on Main Camera", GUILayout.Height(40)))
            {
                CreatePostProcessor();
            }
            return;
        }

        // Get current settings
        settings = postProcessor.GetSettings();

        GUILayout.Space(10);
        GUILayout.Label("Presets", EditorStyles.boldLabel);

        // Preset selection
        int newPreset = GUILayout.SelectionGrid(selectedPreset, presetNames, 2);
        if (newPreset != selectedPreset)
        {
            selectedPreset = newPreset;
            postProcessor.ApplyPreset((MedicalImagePostProcessor.PostProcessingPreset)selectedPreset);
            settings = postProcessor.GetSettings();
        }

        GUILayout.Space(10);
        GUILayout.Label("Settings", EditorStyles.boldLabel);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Edge Detection
        EditorGUILayout.LabelField("Edge Enhancement", EditorStyles.boldLabel);
        settings.edgeThreshold = EditorGUILayout.Slider("Edge Threshold", settings.edgeThreshold, 0.0f, 1.0f);
        settings.edgeIntensity = EditorGUILayout.Slider("Edge Intensity", settings.edgeIntensity, 0.0f, 2.0f);

        GUILayout.Space(10);

        // Contrast & Brightness
        EditorGUILayout.LabelField("Contrast & Brightness", EditorStyles.boldLabel);
        settings.contrastStrength = EditorGUILayout.Slider("Contrast Strength", settings.contrastStrength, 0.0f, 2.0f);
        settings.brightnessOffset = EditorGUILayout.Slider("Brightness Offset", settings.brightnessOffset, -0.5f, 0.5f);

        GUILayout.Space(10);

        // Color Enhancement
        EditorGUILayout.LabelField("Color Enhancement", EditorStyles.boldLabel);
        settings.saturationBoost = EditorGUILayout.Slider("Saturation Boost", settings.saturationBoost, 0.0f, 2.0f);

        GUILayout.Space(10);

        // Tone Mapping
        EditorGUILayout.LabelField("Tone Mapping (HDR Effect)", EditorStyles.boldLabel);
        settings.tonemapStrength = EditorGUILayout.Slider("Tonemap Strength", settings.tonemapStrength, 0.0f, 2.0f);

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        // Apply settings
        if (GUILayout.Button("Apply Settings", GUILayout.Height(40)))
        {
            postProcessor.SetSettings(settings);
            selectedPreset = 0;  // Set to Custom
        }

        GUILayout.Space(10);

        // Quick presets
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset to Default"))
        {
            postProcessor.ApplyPreset(MedicalImagePostProcessor.PostProcessingPreset.Bone);
            settings = postProcessor.GetSettings();
            selectedPreset = 1;
        }
        if (GUILayout.Button("Cinematic"))
        {
            postProcessor.ApplyPreset(MedicalImagePostProcessor.PostProcessingPreset.Cinematic);
            settings = postProcessor.GetSettings();
            selectedPreset = 5;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Info
        EditorGUILayout.HelpBox(
            "Post-processing settings:\n" +
            "• Edge Enhancement: Highlights boundaries and fractures\n" +
            "• Contrast: Increases visual separation\n" +
            "• Brightness: Overall image brightness\n" +
            "• Saturation: Color intensity\n" +
            "• Tonemap: HDR effect for cinematic look",
            MessageType.Info
        );
    }

    private void CreatePostProcessor()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            EditorUtility.DisplayDialog("Error", "No main camera found in scene", "OK");
            return;
        }

        MedicalImagePostProcessor pp = mainCamera.gameObject.AddComponent<MedicalImagePostProcessor>();
        postProcessor = pp;
        EditorUtility.DisplayDialog("Success", "Post-processor added to main camera", "OK");
    }
}
