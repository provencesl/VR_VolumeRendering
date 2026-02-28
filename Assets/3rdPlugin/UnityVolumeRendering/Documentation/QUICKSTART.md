# 快速开始指南 - 医学影像三维渲染

## 5分钟快速上手

### 步骤1：准备工作（1分钟）

1. 在Unity中打开你的医学影像项目
2. 加载一个CT数据集（DICOM、NRRD等）
3. 创建VolumeRenderedObject

### 步骤2：添加HU范围隔离（2分钟）

**方式A：使用编辑器窗口（推荐新手）**

1. 菜单：`Window > Medical Imaging > HU Range Layer Manager`
2. 在检查器中选择你的VolumeRenderedObject
3. 点击"Bone Only"按钮

**方式B：使用代码**

```csharp
// 在你的脚本中
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
if (layerManager == null)
    layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();

layerManager.ClearAllLayers();
layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0])); // 骨骼
```

### 步骤3：添加后处理（1分钟）

1. 菜单：`Window > Medical Imaging > Simplified Post-Processing`
2. 点击"Create on Main Camera"
3. 选择"Bone"预设

### 步骤4：调整参数（1分钟）

在后处理窗口中：
- Edge Enhancement: 0.3（推荐）
- Contrast: 1.05（推荐）
- 其他保持默认

✅ **完成！** 你现在应该看到清晰的骨骼渲染

---

## 常用场景

### 场景1：仅显示骨骼

```csharp
// 清除所有层
layerManager.ClearAllLayers();

// 添加骨骼层
var boneLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]);
layerManager.AddLayer(boneLayer);

// 应用后处理
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Bone);
```

**HU范围：** 800-3000  
**颜色：** 白色  
**用途：** 骨折检测、骨密度分析

---

### 场景2：仅显示出血

```csharp
layerManager.ClearAllLayers();

// 添加出血层（急性）
var hemorrhageLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2]);
layerManager.AddLayer(hemorrhageLayer);

postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Hemorrhage);
```

**HU范围：** 30-100  
**颜色：** 红色  
**用途：** 出血检测、颅内血肿评估

---

### 场景3：骨骼+出血（创伤评估）

```csharp
layerManager.LoadBoneHemorrhagePreset();
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
```

**显示：** 骨骼（白色）+ 出血（红色）  
**用途：** 创伤患者评估、骨折伴出血

---

### 场景4：软组织

```csharp
layerManager.ClearAllLayers();

// 软组织层
var softTissueLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4]);
layerManager.AddLayer(softTissueLayer);

postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
```

**HU范围：** -10 to 100  
**用途：** 器官、肌肉、肿瘤可视化

---

## HU值参考表

| 组织 | HU范围 | 标准化范围 |
|------|--------|-----------|
| 空气 | -1000 | 0.0 |
| 肺 | -500 | 0.167 |
| 脂肪 | -100 | 0.3 |
| 水 | 0 | 0.333 |
| 软组织 | 40 | 0.347 |
| 出血（急性） | 60 | 0.353 |
| 骨（松质） | 200-800 | 0.4-0.533 |
| 骨（皮质） | 800-3000 | 0.533-1.0 |

---

## 故障排除

### 问题1：渲染全黑

**原因：** HU范围不正确或层不可见

**解决：**
```csharp
// 检查层是否可见
var layers = layerManager.GetAllLayers();
foreach (var layer in layers)
{
    Debug.Log($"{layer.layerName}: visible={layer.visible}, HU={layer.minHU}-{layer.maxHU}");
}

// 确保至少有一个可见层
if (layers.Count == 0)
{
    layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]));
}
```

### 问题2：器官仍然可见（没有隐藏）

**原因：** HU范围太宽

**解决：** 缩小HU范围
```csharp
// 只显示高密度骨（>1000 HU）
var layer = new HURangeLayer("Dense Bone", 1000f, 3000f, Color.white, 0.95f);
layerManager.ClearAllLayers();
layerManager.AddLayer(layer);
```

### 问题3：渲染有伪影

**原因：** 后处理过度

**解决：** 使用Minimal预设
```csharp
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Minimal);
```

### 问题4：性能差

**原因：** 采样率太高或立方插值启用

**解决：** 在VolumeRenderedObject中调整
```csharp
// 降低采样率
volumeObject._SamplingRateMultiplier = 0.5f;

// 禁用立方插值（如果不需要）
// 在Shader中禁用CUBIC_INTERPOLATION_ON
```

---

## 下一步

1. **阅读完整指南** → `Assets/Documentation/MedicalImagingOptimization_Guide.md`
2. **查看示例代码** → `Assets/Scripts/Examples/MedicalImagingExample.cs`
3. **尝试不同预设** → 在编辑器窗口中实验
4. **自定义HU范围** → 根据诊断需求调整

---

## 关键概念

### HU值（Hounsfield Unit）
CT扫描的标准单位，定义组织密度：
- **负值** = 低密度（空气、脂肪）
- **0** = 水的参考值
- **正值** = 高密度（软组织、骨骼）

### HU范围隔离
基于HU值范围显示/隐藏组织，类似Photoshop图层：
- 精确控制
- 无伪影
- 符合医学标准

### 简化后处理
微妙的增强而非激进的处理：
- 保留临床准确性
- 最小化伪影
- 改善可视性

---

## 常见参数

| 参数 | 范围 | 推荐值 | 说明 |
|------|------|--------|------|
| Edge Enhance | 0.0-1.0 | 0.3 | 边界增强强度 |
| Contrast | 0.8-1.2 | 1.05 | 对比度调整 |
| Clarity | 0.0-1.0 | 0.2 | 中间调对比度 |
| Sharpness | 0.0-0.5 | 0.1 | 锐化强度 |

---

## 获取帮助

1. 检查`MedicalImagingOptimization_Guide.md`
2. 查看`MedicalImagingExample.cs`中的示例
3. 在编辑器窗口中查看帮助提示
4. 检查Console输出的错误信息

---

**祝你成功！** 🎉

如有问题，请参考完整文档或查看示例代码。
