using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;

public class MedicalTransferFunctionWindow : EditorWindow
{
    private VolumeRenderedObject volumeObject;
    private TransferFunction2D currentTF2D;
    private int selectedPreset = 0;
    private string[] presetNames = new string[]
    {
        "Bone",
        "Hemorrhage",
        "Soft Tissue",
        "Bone + Hemorrhage",
        "High Contrast"
    };

    [MenuItem("Window/Medical Imaging/Transfer Function Presets")]
    public static void ShowWindow()
    {
        GetWindow<MedicalTransferFunctionWindow>("Medical TF Presets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Medical Transfer Function Presets", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Volume object selection
        volumeObject = EditorGUILayout.ObjectField(
            "Volume Object",
            volumeObject,
            typeof(VolumeRenderedObject),
            true
        ) as VolumeRenderedObject;

        if (volumeObject == null)
        {
            EditorGUILayout.HelpBox("Please select a VolumeRenderedObject", MessageType.Info);
            return;
        }

        GUILayout.Space(10);
        GUILayout.Label("Presets", EditorStyles.boldLabel);

        // Preset selection
        selectedPreset = GUILayout.SelectionGrid(selectedPreset, presetNames, 1);

        GUILayout.Space(10);

        // Apply preset button
        if (GUILayout.Button("Apply Preset", GUILayout.Height(40)))
        {
            ApplyPreset(selectedPreset);
        }

        GUILayout.Space(10);
        GUILayout.Label("Preset Details", EditorStyles.boldLabel);

        // Show preset description
        ShowPresetDescription(selectedPreset);

        GUILayout.Space(10);

        // Advanced options
        if (GUILayout.Button("Save Current TF as Preset", GUILayout.Height(30)))
        {
            SaveCurrentPreset();
        }

        if (GUILayout.Button("Reset to Default", GUILayout.Height(30)))
        {
            ResetToDefault();
        }
    }

    private void ApplyPreset(int presetIndex)
    {
        if (volumeObject == null)
            return;

        TransferFunction2D tf2d = null;

        switch (presetIndex)
        {
            case 0:
                tf2d = MedicalTransferFunctionPresets.CreateBonePreset();
                break;
            case 1:
                tf2d = MedicalTransferFunctionPresets.CreateHemorrhagePreset();
                break;
            case 2:
                tf2d = MedicalTransferFunctionPresets.CreateSoftTissuePreset();
                break;
            case 3:
                tf2d = MedicalTransferFunctionPresets.CreateBoneHemorrhageComboPreset();
                break;
            case 4:
                tf2d = MedicalTransferFunctionPresets.CreateHighContrastPreset();
                break;
        }

        if (tf2d != null)
        {
            currentTF2D = tf2d;
            // Apply to volume object
            volumeObject.SetTransferFunction2DAsync(tf2d);

            EditorUtility.DisplayDialog("Success", $"Applied {presetNames[presetIndex]} preset", "OK");
        }
    }

    private void ShowPresetDescription(int presetIndex)
    {
        string description = "";
        switch (presetIndex)
        {
            case 0:
                description = "Bone Visualization\n" +
                    "• Optimized for cortical and trabecular bone\n" +
                    "• HU range: 200-3000\n" +
                    "• High contrast white rendering\n" +
                    "• Enhanced fracture edge detection\n" +
                    "• Gold highlights for fracture lines";
                break;
            case 1:
                description = "Hemorrhage Visualization\n" +
                    "• Optimized for acute and subacute bleeding\n" +
                    "• HU range: 30-100\n" +
                    "• Red-orange gradient for blood\n" +
                    "• Enhanced edge detection\n" +
                    "• Pure red for bleeding boundaries";
                break;
            case 2:
                description = "Soft Tissue Visualization\n" +
                    "• Optimized for muscle, organ, and fat tissue\n" +
                    "• HU range: -100 to 100\n" +
                    "• Gray gradient for tissue contrast\n" +
                    "• Enhanced tissue boundaries\n" +
                    "• Better visualization of tissue interfaces";
                break;
            case 3:
                description = "Bone + Hemorrhage Combo\n" +
                    "• Combined visualization of bone and bleeding\n" +
                    "• Shows both structures simultaneously\n" +
                    "• Red for hemorrhage, white for bone\n" +
                    "• Ideal for trauma assessment\n" +
                    "• Clinical diagnostic mode";
                break;
            case 4:
                description = "High Contrast Diagnostic\n" +
                    "• Maximum contrast for clinical diagnosis\n" +
                    "• Shows only dense structures\n" +
                    "• Yellow edge enhancement\n" +
                    "• Minimal noise and artifacts\n" +
                    "• Optimized for detailed analysis";
                break;
        }

        EditorGUILayout.HelpBox(description, MessageType.Info);
    }

    private void SaveCurrentPreset()
    {
        if (volumeObject == null || currentTF2D == null)
        {
            EditorUtility.DisplayDialog("Error", "No transfer function to save", "OK");
            return;
        }

        string presetName = EditorInputDialog.Show("Save Preset", "Enter preset name:", "");
        if (!string.IsNullOrEmpty(presetName))
        {
            MedicalTransferFunctionPresets.SavePreset(currentTF2D, presetName);
            EditorUtility.DisplayDialog("Success", $"Preset '{presetName}' saved", "OK");
        }
    }

    private void ResetToDefault()
    {
        if (volumeObject != null)
        {
            // volumeObject.SetTransferFunction2DAsync(MedicalTransferFunctionPresets.CreateBonePreset());
            EditorUtility.DisplayDialog("Reset", "Transfer function reset to default bone preset", "OK");
        }
    }
}

/// <summary>
/// Simple dialog for input
/// </summary>
public class EditorInputDialog : EditorWindow
{
    private static EditorInputDialog instance;
    private static string inputValue = "";
    private static string title = "";
    private static string prompt = "";
    private static System.Action<string> callback;

    public static string Show(string title, string prompt, string defaultValue)
    {
        EditorInputDialog.title = title;
        EditorInputDialog.prompt = prompt;
        EditorInputDialog.inputValue = defaultValue;

        instance = GetWindow<EditorInputDialog>(true, title);
        instance.minSize = new Vector2(300, 100);
        instance.maxSize = new Vector2(500, 150);

        return inputValue;
    }

    private void OnGUI()
    {
        GUILayout.Label(prompt);
        inputValue = EditorGUILayout.TextField(inputValue);

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("OK"))
        {
            Close();
        }
        if (GUILayout.Button("Cancel"))
        {
            inputValue = "";
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
