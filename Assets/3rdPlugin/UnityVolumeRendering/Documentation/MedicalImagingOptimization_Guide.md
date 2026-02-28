# 医学影像三维渲染优化指南

## 概述

本指南介绍如何使用HU范围隔离系统和简化后处理来实现高质量的医学影像渲染，特别是骨骼和出血部位的可视化。

---

## 问题分析与解决方案

### 原问题
1. **2D传递函数局限性**：矩形设计不适合医学影像，散点型易报错
2. **器官遮挡**：无法单独显示骨骼，其他器官会遮挡
3. **后处理过度**：激进的增强反而破坏器官表现

### 新方案
采用**HU范围隔离系统**（类似Photoshop图层），结合**简化后处理**

---

## 第1步：使用HU范围隔离系统

### 1.1 什么是HU值？

HU（Hounsfield Unit）是CT扫描的标准单位，定义了不同组织的密度：

| 组织类型 | HU范围 | 说明 |
|---------|--------|------|
| 空气 | -1000 | 完全透明 |
| 肺 | -500 | 低密度 |
| 脂肪 | -100 | 低密度 |
| 水 | 0 | 参考值 |
| 软组织 | 40 | 器官、肌肉 |
| 急性出血 | 60 | 新鲜血液 |
| 骨骼（松质） | 200-800 | 内部骨结构 |
| 骨骼（皮质） | 800-3000 | 密集骨 |

### 1.2 在Unity中使用

#### 步骤1：添加HU范围管理器

```csharp
// 在VolumeRenderedObject所在的GameObject上添加组件
HURangeLayerManager layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();
```

#### 步骤2：打开HU范围图层编辑器

1. 在Unity菜单中：`Window > Medical Imaging > HU Range Layer Manager`
2. 选择你的VolumeRenderedObject
3. 点击"Bone Only"快速预设

![HU Range Manager](./images/hu_range_manager.png)

#### 步骤3：创建自定义隔离

**显示仅骨骼（隐藏其他器官）：**

```csharp
// 代码方式
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
layerManager.ClearAllLayers();

// 只显示皮质骨
var boneLayer = new HURangeLayer(
    "Cortical Bone",
    800f,      // 最小HU
    3000f,     // 最大HU
    Color.white,
    0.95f      // 不透明度
);
layerManager.AddLayer(boneLayer);
```

**显示骨骼+出血：**

```csharp
layerManager.LoadBoneHemorrhagePreset();
```

### 1.3 HU范围隔离的优势

✅ **精确控制** - 基于医学标准HU值  
✅ **无器官遮挡** - 隐藏不需要的组织  
✅ **多层叠加** - 同时显示多个组织  
✅ **临床准确** - 符合医学影像工作流  

---

## 第2步：简化后处理

### 2.1 为什么要简化后处理？

激进的后处理（边界增强、高对比度等）会：
- ❌ 引入伪影
- ❌ 隐藏真实细节
- ❌ 破坏器官表现
- ❌ 影响临床准确性

简化后处理只做**最小化增强**：
- ✅ 保留原始数据
- ✅ 微妙改善可视性
- ✅ 保持临床准确性

### 2.2 使用简化后处理

#### 步骤1：添加到主摄像机

```csharp
Camera mainCamera = Camera.main;
SimplifiedPostProcessor pp = mainCamera.gameObject.AddComponent<SimplifiedPostProcessor>();
```

#### 步骤2：打开编辑窗口

`Window > Medical Imaging > Simplified Post-Processing`

#### 步骤3：选择预设

| 预设 | 用途 | 参数 |
|------|------|------|
| **Minimal** | 最小增强 | 边界0.1, 对比度1.0 |
| **Normal** | 标准使用 | 边界0.25, 对比度1.02 |
| **Bone** | 骨骼优化 | 边界0.4, 对比度1.05 |
| **Hemorrhage** | 出血优化 | 边界0.35, 对比度1.03 |

### 2.3 简化后处理参数说明

```
Edge Enhancement (0.0 - 1.0)
├─ 0.0 = 无边界增强
├─ 0.3 = 推荐值（微妙）
└─ 1.0 = 最大增强（可能过度）

Contrast (0.8 - 1.2)
├─ 0.8 = 降低对比度
├─ 1.0 = 原始对比度（推荐）
└─ 1.2 = 增加对比度

Clarity (0.0 - 1.0)
├─ 0.0 = 无效果
├─ 0.2 = 推荐值（增强中间调）
└─ 1.0 = 最大增强

Sharpness (0.0 - 0.5)
├─ 0.0 = 无锐化
├─ 0.1 = 推荐值（微妙）
└─ 0.5 = 最大锐化（可能产生伪影）
```

---

## 第3步：完整工作流

### 3.1 显示仅骨骼

```csharp
// 1. 获取或创建层管理器
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
if (layerManager == null)
    layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();

// 2. 清除所有层
layerManager.ClearAllLayers();

// 3. 添加仅骨骼层
var boneLayer = new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]); // Cortical bone
layerManager.AddLayer(boneLayer);

// 4. 添加简化后处理
var postProcessor = Camera.main.GetComponent<SimplifiedPostProcessor>();
if (postProcessor == null)
    postProcessor = Camera.main.gameObject.AddComponent<SimplifiedPostProcessor>();
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Bone);
```

### 3.2 显示骨骼+出血

```csharp
// 使用预设方法
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
layerManager.LoadBoneHemorrhagePreset();

// 应用对应的后处理
var postProcessor = Camera.main.GetComponent<SimplifiedPostProcessor>();
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Normal);
```

### 3.3 自定义隔离

```csharp
// 创建自定义HU范围
var customLayer = new HURangeLayer(
    "Custom Tissue",
    200f,      // 最小HU
    500f,      // 最大HU
    new Color(0.8f, 0.6f, 0.4f),  // 颜色
    0.8f       // 不透明度
);

var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
layerManager.AddLayer(customLayer);
```

---

## 第4步：优化1D传递函数

### 4.1 为骨骼优化的1D传递函数

```csharp
TransferFunction tf = ScriptableObject.CreateInstance<TransferFunction>();

// 骨骼范围：HU 200-3000
// 添加颜色控制点
tf.AddControlPoint(new TFColourControlPoint(
    MedicalHURange.HUToNormalized(200f),   // 最小骨骼HU
    Color.white
));
tf.AddControlPoint(new TFColourControlPoint(
    MedicalHURange.HUToNormalized(3000f),  // 最大骨骼HU
    Color.white
));

// 添加透明度控制点
tf.AddControlPoint(new TFAlphaControlPoint(
    MedicalHURange.HUToNormalized(200f),
    0.8f
));
tf.AddControlPoint(new TFAlphaControlPoint(
    MedicalHURange.HUToNormalized(3000f),
    0.95f
));

// 其他HU值设为透明
tf.AddControlPoint(new TFAlphaControlPoint(0.0f, 0.0f));
tf.AddControlPoint(new TFAlphaControlPoint(
    MedicalHURange.HUToNormalized(199f),
    0.0f
));

tf.GenerateTexture();
volumeObject.SetTransferFunctionAsync(tf);
```

### 4.2 为出血优化的1D传递函数

```csharp
TransferFunction tf = ScriptableObject.CreateInstance<TransferFunction>();

// 出血范围：HU 30-100
tf.AddControlPoint(new TFColourControlPoint(
    MedicalHURange.HUToNormalized(30f),
    new Color(1.0f, 0.2f, 0.1f)  // 红色
));
tf.AddControlPoint(new TFColourControlPoint(
    MedicalHURange.HUToNormalized(100f),
    new Color(1.0f, 0.5f, 0.1f)  // 橙色
));

// 透明度
tf.AddControlPoint(new TFAlphaControlPoint(
    MedicalHURange.HUToNormalized(30f),
    0.7f
));
tf.AddControlPoint(new TFAlphaControlPoint(
    MedicalHURange.HUToNormalized(100f),
    0.85f
));

tf.GenerateTexture();
volumeObject.SetTransferFunctionAsync(tf);
```

---

## 常见问题

### Q1: 为什么不使用2D传递函数？

**A:** 2D TF的矩形设计在医学影像中有局限：
- 医学影像需要基于单一维度（HU值）的精确隔离
- 梯度维度（2D TF的Y轴）在医学诊断中不如HU值重要
- HU范围隔离更直观、更符合临床工作流

### Q2: 如何同时显示多个器官？

**A:** 使用多层系统：

```csharp
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();

// 添加多个层
layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0])); // 骨骼
layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[2])); // 出血
layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[4])); // 软组织
```

### Q3: 后处理太强了怎么办？

**A:** 使用Minimal预设或禁用后处理：

```csharp
postProcessor.ApplyPreset(SimplifiedPostProcessor.PostProcessingPreset.Minimal);
// 或
postProcessor.SetEnabled(false);
```

### Q4: 如何导出高质量渲染？

**A:** 建议设置：
- 分辨率：至少1920x1080
- 后处理：Minimal或Normal
- 抗锯齿：4x或8x
- 导出格式：PNG或TGA（保留Alpha通道）

---

## 性能优化建议

| 优化项 | 设置 | 性能影响 |
|-------|------|--------|
| 采样率 | 1.0x | 基准 |
| 采样率 | 0.5x | +50% FPS（质量下降） |
| 立方插值 | 启用 | -20% FPS（质量提升） |
| 后处理 | 禁用 | +10% FPS |
| 阴影 | 禁用 | +15% FPS |

---

## 最佳实践

✅ **DO:**
- 使用HU范围隔离而非2D TF
- 保持后处理参数接近1.0
- 为不同诊断目的创建预设
- 定期验证渲染的临床准确性

❌ **DON'T:**
- 过度增强边界（>0.5）
- 使用过高的对比度（>1.3）
- 混合太多层（>5个）
- 忽视原始数据的完整性

---

## 参考资源

- [Hounsfield Unit标准](https://en.wikipedia.org/wiki/Hounsfield_scale)
- [CT影像诊断指南](https://www.radiologyinfo.org/en/info/ct)
- [医学影像处理最佳实践](https://www.dicomstandard.org/)

---

## 技术支持

遇到问题？检查以下内容：

1. **HU范围不正确** → 检查`MedicalHURange.cs`中的预设值
2. **渲染全黑** → 检查层的可见性和HU范围
3. **性能问题** → 降低采样率或禁用立方插值
4. **后处理伪影** → 使用Minimal预设或禁用后处理

