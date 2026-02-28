using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;
using System.Collections.Generic;

public class HURangeLayerEditorWindow : EditorWindow
{
    private HURangeLayerManager layerManager;
    private VolumeRenderedObject volumeObject;
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedLayerIndex = -1;
    private int selectedPresetIndex = 0;

    [MenuItem("Window/Medical Imaging/HU Range Layer Manager")]
    public static void ShowWindow()
    {
        GetWindow<HURangeLayerEditorWindow>("HU Range Layers");
    }

    private void OnGUI()
    {
        GUILayout.Label("HU Range Layer Manager", EditorStyles.boldLabel);
        GUILayout.Label("Isolate tissues by Hounsfield Unit ranges", EditorStyles.miniLabel);
        GUILayout.Space(10);

        // Find volume object
        volumeObject = EditorGUILayout.ObjectField(
            "Volume Object",
            volumeObject,
            typeof(VolumeRenderedObject),
            true
        ) as VolumeRenderedObject;

        if (volumeObject == null)
        {
            EditorGUILayout.HelpBox("Select a VolumeRenderedObject to manage layers", MessageType.Info);
            return;
        }

        // Get or create layer manager
        if (layerManager == null)
        {
            layerManager = volumeObject.GetComponent<HURangeLayerManager>();
            if (layerManager == null)
            {
                layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Quick Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bone Only", GUILayout.Height(30)))
        {
            layerManager.ClearAllLayers();
            layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]));
        }
        if (GUILayout.Button("Hemorrhage Only", GUILayout.Height(30)))
        {
            layerManager.ClearAllLayers();
            layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2]));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bone + Hemorrhage", GUILayout.Height(30)))
        {
            layerManager.LoadBoneHemorrhagePreset();
        }
        if (GUILayout.Button("Soft Tissue", GUILayout.Height(30)))
        {
            layerManager.ClearAllLayers();
            layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4]));
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Density-Priority Presets", EditorStyles.boldLabel);
        GUILayout.Label("Medical-grade rendering with tissue priority", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Pure Bone", GUILayout.Height(30)))
        {
            layerManager.LoadDensityPriorityPreset(UnityVolumeRendering.DensityPrioritySystem.PresetMode.PureBone);
        }
        if (GUILayout.Button("Bone + Vessels", GUILayout.Height(30)))
        {
            layerManager.LoadDensityPriorityPreset(UnityVolumeRendering.DensityPrioritySystem.PresetMode.BoneAndVessels);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bone + Organs", GUILayout.Height(30)))
        {
            layerManager.LoadDensityPriorityPreset(UnityVolumeRendering.DensityPrioritySystem.PresetMode.BoneAndOrgans);
        }
        if (GUILayout.Button("Full Anatomy", GUILayout.Height(30)))
        {
            layerManager.LoadDensityPriorityPreset(UnityVolumeRendering.DensityPrioritySystem.PresetMode.FullAnatomy);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        float newPriorityStrength = EditorGUILayout.Slider("Priority Strength", layerManager.GetDensityPriorityStrength(), 0.0f, 1.0f);
        if (newPriorityStrength != layerManager.GetDensityPriorityStrength())
        {
            layerManager.SetDensityPriorityStrength(newPriorityStrength);
        }

        GUILayout.Space(10);
        GUILayout.Label("Tissue Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        selectedPresetIndex = EditorGUILayout.Popup(selectedPresetIndex, MedicalHURange.GetAllPresetNames());
        if (GUILayout.Button("Add Layer", GUILayout.Width(80)))
        {
            var preset = MedicalHURange.TISSUE_PRESETS[selectedPresetIndex];
            layerManager.AddLayer(new HURangeLayer(preset));
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Layers", EditorStyles.boldLabel);

        DrawLayerList();

        GUILayout.Space(10);

        if (selectedLayerIndex >= 0 && selectedLayerIndex < layerManager.GetAllLayers().Count)
        {
            DrawLayerEditor();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "HU Range Layers work like Photoshop layers:\n" +
            "• Add multiple tissue layers\n" +
            "• Toggle visibility to isolate tissues\n" +
            "• Adjust HU ranges for precise control\n" +
            "• Combine layers for complex visualization",
            MessageType.Info
        );
    }

    private void DrawLayerList()
    {
        var layers = layerManager.GetAllLayers();

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            DrawLayerListItem(i, layer);
        }

        GUILayout.EndScrollView();
    }

    private void DrawLayerListItem(int index, HURangeLayer layer)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // Visibility toggle
        bool newVisible = EditorGUILayout.Toggle(layer.visible, GUILayout.Width(20));
        if (newVisible != layer.visible)
        {
            layerManager.ToggleLayerVisibility(index);
        }

        // Layer color indicator
        // EditorGUI.DrawRect(EditorGUILayout.GetControlRect(20, 20), layer.color);

        // Layer name and HU range
        EditorGUILayout.BeginVertical();
        GUILayout.Label(layer.layerName, EditorStyles.boldLabel);
        GUILayout.Label($"HU: {layer.minHU:F0} - {layer.maxHU:F0}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // Selection
        if (GUILayout.Button("Edit", GUILayout.Width(50)))
        {
            selectedLayerIndex = index;
        }

        // Delete button
        if (GUILayout.Button("X", GUILayout.Width(30)))
        {
            layerManager.RemoveLayer(index);
            selectedLayerIndex = -1;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLayerEditor()
    {
        var layer = layerManager.GetLayer(selectedLayerIndex);
        if (layer == null)
            return;

        GUILayout.Label("Layer Editor", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Layer name
        layer.layerName = EditorGUILayout.TextField("Layer Name", layer.layerName);

        GUILayout.Space(5);

        // HU Range with visual feedback
        EditorGUILayout.LabelField("HU Range", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Min HU:", GUILayout.Width(60));
        layer.minHU = EditorGUILayout.FloatField(layer.minHU, GUILayout.Width(80));
        EditorGUILayout.LabelField("Max HU:", GUILayout.Width(60));
        layer.maxHU = EditorGUILayout.FloatField(layer.maxHU, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // HU Range slider
        EditorGUILayout.MinMaxSlider("HU Range", ref layer.minHU, ref layer.maxHU, -1000, 3000);

        // Show normalized values
        float minNorm = MedicalHURange.HUToNormalized(layer.minHU);
        float maxNorm = MedicalHURange.HUToNormalized(layer.maxHU);
        EditorGUILayout.LabelField($"Normalized: {minNorm:F3} - {maxNorm:F3}", EditorStyles.miniLabel);

        GUILayout.Space(5);

        // Color
        layer.color = EditorGUILayout.ColorField("Color", layer.color);

        // Opacity
        layer.opacity = EditorGUILayout.Slider("Opacity", layer.opacity, 0.0f, 1.0f);

        GUILayout.Space(5);

        // Density-Priority settings
        GUILayout.Label("Density-Priority Settings", EditorStyles.boldLabel);
        
        layer.tissueType = (UnityVolumeRendering.DensityPrioritySystem.TissueType)EditorGUILayout.EnumPopup("Tissue Type", layer.tissueType);
        
        if (GUILayout.Button("Apply Tissue Preset", GUILayout.Height(25)))
        {
            layer.ApplyTissueTypePreset(layer.tissueType);
        }

        layer.densityPriority = EditorGUILayout.IntSlider("Priority", layer.densityPriority, 0, 5);
        layer.densityWeightFactor = EditorGUILayout.Slider("Density Weight", layer.densityWeightFactor, 0.0f, 2.0f);

        GUILayout.Space(5);

        // Lock toggle
        layer.locked = EditorGUILayout.Toggle("Locked", layer.locked);

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // Apply changes
        if (GUILayout.Button("Apply Changes", GUILayout.Height(40)))
        {
            // Trigger transfer function update
            EditorApplication.delayCall += () =>
            {
                var manager = layerManager;
                manager.RemoveLayer(selectedLayerIndex);
                manager.AddLayer(layer);
            };
        }
    }
}
