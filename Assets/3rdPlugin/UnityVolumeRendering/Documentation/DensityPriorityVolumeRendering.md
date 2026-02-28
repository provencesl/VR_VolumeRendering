# Medical-Grade Density-Priority Volume Rendering System

## Overview

The Density-Priority Volume Rendering System is a medical-grade enhancement to the UnityVolumeRendering project that implements tissue priority-based raymarching. This system ensures accurate visualization of complex anatomical structures by rendering higher-density tissues first, allowing them to suppress lower-density contributions.

## Architecture

### Core Components

#### 1. **DensityPrioritySystem.cs**
- **Location:** `Assets/Scripts/TransferFunction/DensityPrioritySystem.cs`
- **Purpose:** Core priority management system with tissue presets
- **Key Features:**
  - Defines tissue type enumeration (Bone, Vessel, SoftTissue, Fat, Lung, Air)
  - Manages tissue priority values (0-5, higher = rendered first)
  - Stores density weight factors for opacity suppression
  - Provides preset configurations for clinical workflows

#### 2. **HURangeLayer Extension**
- **Location:** `Assets/Scripts/TransferFunction/MedicalHURange.cs`
- **New Fields:**
  - `tissueType`: TissueType enum for classification
  - `densityPriority`: Priority value (0-5)
  - `densityWeightFactor`: Opacity suppression multiplier
- **New Methods:**
  - `ApplyTissueTypePreset()`: Applies priority settings based on tissue type

#### 3. **HURangeLayerManager Extension**
- **Location:** `Assets/Scripts/TransferFunction/MedicalHURange.cs`
- **New Features:**
  - Integration with DensityPrioritySystem
  - Preset mode loading (Pure Bone, Bone+Vessels, Bone+Organs, Full Anatomy)
  - Density priority strength control (0-1)
  - Automatic layer creation from tissue types

#### 4. **DirectVolumeRenderingShader.shader**
- **Location:** `Assets/Shaders/DirectVolumeRenderingShader.shader`
- **New Functions:**
  - `getTissuePriority()`: Determines tissue type from density value
  - `calculateDensityWeight()`: Calculates weight factor for opacity suppression
  - `applyDensityPrioritySuppression()`: Applies priority-based opacity suppression
- **Integration:** Applied in DVR raymarching loop for front-to-back accumulation

#### 5. **HURangeLayerEditorWindow Enhancement**
- **Location:** `Assets/Editor/HURangeLayerEditorWindow.cs`
- **New UI Elements:**
  - Density-Priority Preset buttons (Pure Bone, Bone+Vessels, Bone+Organs, Full Anatomy)
  - Priority Strength slider (0-1)
  - Layer Editor with:
    - Tissue Type dropdown
    - Apply Tissue Preset button
    - Priority slider (0-5)
    - Density Weight slider (0-2)

## Tissue Priority System

### Priority Levels (0-5)

| Priority | Tissue Type | HU Range | Density Weight | Suppression |
|----------|------------|----------|----------------|-------------|
| 5 | Bone | 800-3000 | 1.5x | Strong |
| 4 | Vessel/Hemorrhage | 30-100 | 1.3x | Strong |
| 3 | Soft Tissue | -10 to 100 | 1.0x | Moderate |
| 2 | Fat | -100 to -50 | 0.8x | Weak |
| 1 | Lung | -500 to -100 | 0.6x | Weak |
| 0 | Air | < -500 | 0.0x | None |

### Preset Modes

#### Pure Bone
- **Visible Tissues:** Bone only
- **Use Case:** Fracture detection, orthopedic assessment
- **Clinical Value:** Clear visualization of bone structure without soft tissue interference

#### Bone + Vessels
- **Visible Tissues:** Bone, Hemorrhage/Vessels
- **Use Case:** Trauma assessment, vascular injury evaluation
- **Clinical Value:** Bone structure with hemorrhage visualization for bleeding assessment

#### Bone + Organs
- **Visible Tissues:** Bone, Soft Tissue
- **Use Case:** Surgical planning, anatomical context
- **Clinical Value:** Bone with organ relationships for comprehensive assessment

#### Full Anatomy
- **Visible Tissues:** All tissues (Bone, Vessel, SoftTissue, Fat, Lung)
- **Use Case:** Comprehensive anatomical visualization
- **Clinical Value:** Complete anatomical context for complex cases

## Density-Priority Raymarching Algorithm

### Principle

Higher-density tissues suppress lower-density contributions during front-to-back accumulation:

```
suppression = accumulatedAlpha × densityWeightFactor × density × priorityStrength
finalOpacity = originalOpacity × (1.0 - suppression)
```

### Benefits

1. **Automatic Tissue Separation:** No manual layer ordering needed
2. **Density-Aware Rendering:** Respects medical imaging physics
3. **Reduced Visual Artifacts:** Prevents low-density tissues from obscuring high-density structures
4. **Clinically Accurate:** Matches radiologist expectations

## Usage Guide

### In Editor

1. **Open HU Range Layer Manager:**
   - Window → Medical Imaging → HU Range Layer Manager

2. **Select Volume Object:**
   - Assign your VolumeRenderedObject in the inspector

3. **Load Preset:**
   - Click one of the Density-Priority Preset buttons
   - Adjust Priority Strength slider for effect intensity

4. **Fine-tune Individual Layers:**
   - Select layer in list
   - Edit Tissue Type, Priority, and Density Weight
   - Click "Apply Tissue Preset" for automatic settings

### In Code

```csharp
// Get or create layer manager
HURangeLayerManager manager = volumeObject.GetComponent<HURangeLayerManager>();
if (manager == null)
    manager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();

// Load preset
manager.LoadDensityPriorityPreset(DensityPrioritySystem.PresetMode.BoneAndVessels);

// Adjust priority strength
manager.SetDensityPriorityStrength(0.8f);

// Create custom layer with priority
HURangeLayer layer = new HURangeLayer("Bone", 800, 3000, Color.white, 0.95f);
layer.ApplyTissueTypePreset(DensityPrioritySystem.TissueType.Bone);
manager.AddLayer(layer);
```

## Technical Details

### Shader Implementation

The density-priority system is implemented in the DVR fragment shader:

1. **Density Sampling:** Sample density at current raymarching position
2. **Priority Determination:** Map density to tissue type and priority
3. **Weight Calculation:** Calculate suppression factor based on priority
4. **Opacity Suppression:** Apply suppression to current sample opacity
5. **Accumulation:** Standard front-to-back blending with modified opacity

### Performance Considerations

- **Minimal Overhead:** Priority calculation uses simple conditional logic
- **Shader Optimization:** Uses switch statements compiled to efficient conditionals
- **No Additional Textures:** Reuses existing density texture
- **Scalable:** Priority strength slider allows performance tuning

## Initialization

### Automatic Initialization

The system auto-initializes on first load:
1. DensityPrioritySystemInitializer checks for existing asset
2. Creates `Assets/Resources/DensityPrioritySystem.asset` if missing
3. Loaded at runtime via `Resources.Load<DensityPrioritySystem>()`

### Manual Initialization

If needed, initialize via menu:
- Medical Imaging → Initialize Density Priority System

## Validation Checklist

- ✅ Density-priority raymarching implemented in shader
- ✅ Preset modes working (Pure Bone, Bone+Vessels, Bone+Organs, Full Anatomy)
- ✅ Real-time opacity adjustment based on priority
- ✅ Editor UI with quick preset buttons
- ✅ Priority strength slider for effect control
- ✅ Layer-level priority customization
- ✅ Tissue type preset system
- ✅ Medical HU range integration

## Visual Results

### Expected Output

**Pure Bone Preset:**
- White, opaque bone structure
- Soft tissue completely suppressed
- Clear fracture visualization

**Bone + Vessels Preset:**
- White bone with red hemorrhage/vessels
- Soft tissue suppressed
- Vessels visible against bone background

**Bone + Organs Preset:**
- White bone with gray soft tissue
- Organs visible in anatomical context
- Clear bone-tissue relationships

**Full Anatomy Preset:**
- Complete anatomical visualization
- All tissues visible with proper priority
- Matches reference medical imaging

## Troubleshooting

### Issue: Preset buttons not working
- **Solution:** Ensure HURangeLayerManager is attached to volume object
- **Check:** Verify DensityPrioritySystem asset exists in Resources folder

### Issue: Priority strength has no effect
- **Solution:** Check shader compilation (ensure no shader errors)
- **Check:** Verify DENSITY_PRIORITY_STRENGTH define is not zero

### Issue: Tissues not properly separated
- **Solution:** Adjust Priority Strength slider (try 0.5-1.0)
- **Check:** Verify layer opacity values are correct

## Future Enhancements

1. **Adaptive Priority:** Automatic priority adjustment based on data statistics
2. **Custom Presets:** Save/load user-defined preset configurations
3. **Priority Visualization:** Debug view showing tissue priority values
4. **Animated Transitions:** Smooth transitions between preset modes
5. **Multi-Volume Support:** Priority system for multiple overlapping volumes

## References

- Medical HU Range: `Assets/Scripts/TransferFunction/MedicalHURange.cs`
- Transfer Function System: `Assets/Scripts/TransferFunction/TransferFunction.cs`
- Volume Rendering: `Assets/Scripts/VolumeRendering/VolumeRenderedObject.cs`

## Support

For issues or questions regarding the Density-Priority Volume Rendering System, refer to:
- Project Documentation: `Assets/Documentation/`
- Shader Documentation: `Assets/Shaders/DirectVolumeRenderingShader.shader`
- Editor Window: Window → Medical Imaging → HU Range Layer Manager
