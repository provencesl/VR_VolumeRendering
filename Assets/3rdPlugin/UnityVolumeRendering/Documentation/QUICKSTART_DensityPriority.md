# Density-Priority Volume Rendering - Quick Start Guide

## 🚀 Getting Started in 5 Minutes

### Step 1: Initialize the System (First Time Only)
1. In Unity Editor, go to: **Medical Imaging → Initialize Density Priority System**
2. This creates the DensityPrioritySystem asset in `Assets/Resources/`

### Step 2: Open the HU Range Layer Manager
1. Go to: **Window → Medical Imaging → HU Range Layer Manager**
2. Assign your **VolumeRenderedObject** in the "Volume Object" field

### Step 3: Load a Preset
Click one of the **Density-Priority Preset buttons:**

- **Pure Bone** - For fracture detection (bone only)
- **Bone + Vessels** - For trauma assessment (bone + hemorrhage)
- **Bone + Organs** - For surgical planning (bone + soft tissue)
- **Full Anatomy** - For comprehensive visualization (all tissues)

### Step 4: Adjust Priority Strength (Optional)
Use the **Priority Strength** slider (0-1) to control effect intensity:
- **0.0** = Density-priority disabled
- **0.5** = Moderate effect
- **1.0** = Full effect (default)

### Step 5: Fine-Tune Individual Layers (Optional)
1. Select a layer in the list
2. Edit in the **Layer Editor** section:
   - **Tissue Type**: Choose from dropdown
   - **Priority**: 0-5 (higher = rendered first)
   - **Density Weight**: 0-2 (higher = stronger suppression)
3. Click **Apply Changes**

---

## 📊 Preset Quick Reference

| Preset | Best For | Visible Tissues |
|--------|----------|-----------------|
| **Pure Bone** | Fracture detection | Bone only |
| **Bone + Vessels** | Trauma assessment | Bone + Hemorrhage |
| **Bone + Organs** | Surgical planning | Bone + Soft Tissue |
| **Full Anatomy** | Complete visualization | All tissues |

---

## 💻 Code Usage

### Load Preset Programmatically
```csharp
HURangeLayerManager manager = volumeObject.GetComponent<HURangeLayerManager>();
manager.LoadDensityPriorityPreset(DensityPrioritySystem.PresetMode.BoneAndVessels);
```

### Adjust Priority Strength
```csharp
manager.SetDensityPriorityStrength(0.8f);
```

### Create Custom Layer with Priority
```csharp
HURangeLayer layer = new HURangeLayer("Bone", 800, 3000, Color.white, 0.95f);
layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Bone);
manager.AddLayer(layer);
```

---

## 🎯 Common Workflows

### Workflow 1: Fracture Detection
1. Load **Pure Bone** preset
2. Set **Priority Strength** to 1.0
3. Adjust **Opacity** slider if needed
4. Result: Clear bone structure without interference

### Workflow 2: Trauma Assessment
1. Load **Bone + Vessels** preset
2. Set **Priority Strength** to 0.8-1.0
3. Vessels appear red against white bone
4. Result: Hemorrhage clearly visible

### Workflow 3: Surgical Planning
1. Load **Bone + Organs** preset
2. Set **Priority Strength** to 0.6-0.8
3. Soft tissue context visible with bone
4. Result: Anatomical relationships clear

### Workflow 4: Comprehensive Analysis
1. Load **Full Anatomy** preset
2. Set **Priority Strength** to 0.5-0.7
3. Adjust individual layer opacities as needed
4. Result: Complete anatomical visualization

---

## 🔍 Troubleshooting

### Issue: Preset buttons not appearing
**Solution:** Ensure HURangeLayerManager is attached to your volume object
```csharp
if (layerManager == null)
    layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();
```

### Issue: Priority strength has no visible effect
**Solution:** Check shader compilation
- Look for shader errors in Console
- Verify DENSITY_PRIORITY_STRENGTH is not zero
- Try adjusting Priority Strength slider

### Issue: Tissues not properly separated
**Solution:** Adjust Priority Strength slider
- Try values between 0.5 and 1.0
- Verify layer opacity values (should be > 0)
- Check that correct preset is loaded

### Issue: DensityPrioritySystem asset not found
**Solution:** Initialize manually
1. Go to: Medical Imaging → Initialize Density Priority System
2. Or create asset manually in Assets/Resources/

---

## 📚 Learn More

For detailed information, see:
- **Main Documentation:** `Assets/Documentation/DensityPriorityVolumeRendering.md`
- **Implementation Report:** `Assets/Documentation/IMPLEMENTATION_REPORT.md`
- **Code Comments:** Check inline comments in source files

---

## ✨ Key Features

✅ **One-Click Presets** - Load complete configurations instantly
✅ **Real-Time Control** - Adjust priority strength on the fly
✅ **Medical Accuracy** - Based on HU ranges and tissue properties
✅ **Layer Customization** - Fine-tune individual tissue properties
✅ **Auto-Initialization** - System creates itself on first use
✅ **Backward Compatible** - Works with existing volume rendering

---

## 🎓 Next Steps

1. **Try all presets** - See how each one looks with your data
2. **Experiment with Priority Strength** - Find the sweet spot for your use case
3. **Customize layers** - Adjust tissue types and priorities as needed
4. **Integrate into workflow** - Use preset loading in your application

---

**Ready to use?** Open Window → Medical Imaging → HU Range Layer Manager and start rendering!
