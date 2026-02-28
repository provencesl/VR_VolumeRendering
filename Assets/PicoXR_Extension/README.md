20221227

https://hackmd.io/@jcxyisncu1111/jc_picoxr_extension
https://hackmd.io/@jcxyisncu1102/steamvr-to-pico

# PicoXR Extension - 骨骼模式渲染功能

## 概述

PicoXR Extension 项目集成了先进的医学影像骨骼渲染功能，为Unity Volume Rendering插件提供了多模式骨骼可视化支持。该功能专门为CT扫描数据优化，能够提供清晰的骨骼结构显示和多种渲染模式选择。

## 核心组件

### 1. EnhancedBoneRenderer (增强骨骼渲染器)

**位置**: `Assets/3rdPlugin/UnityVolumeRendering/Scripts/VolumeProcessing/EnhancedBoneRenderer.cs`

#### 功能特性

- **多渲染模式支持**：
  - `PureIsolation`: 纯骨骼隔离模式 - 完全提取骨骼结构
  - `EdgeEnhanced`: 边缘增强模式 - 突出骨骼边界
  - `Hybrid`: 混合模式 - 骨骼与周围软组织对比显示
  - `GradientBased`: 基于梯度模式 - 智能选择最佳传递函数

- **自动身体部位识别**：
  - 支持头部、胸部、腹部、上胸部等部位自动识别
  - 基于HU直方图分析的智能识别算法

- **传递函数管理**：
  - 根据不同身体部位自动加载对应的TF文件
  - 支持自定义传递函数配置

#### 主要方法

```csharp
// 设置渲染模式
public void SetRenderMode(BoneRenderMode mode)

// 重新生成骨骼遮罩
public void RegenerateBoneMask()

// 设置身体部位类型
public void SetBodyPartType(BodyPartType bodyPart)

// 设置归一化身体区域
public void SetNormalizedRegion(DICOMBodyPartNormalizer.BodyRegion region)

// 自动识别身体部位
public BodyPartType AutoDetectBodyPart()
```

#### 配置参数

- `boneThresholdHU`: 骨骼检测HU阈值 (默认: 300f)
- `edgeEnhancement`: 边缘增强因子 (0.5-3.0)
- `softTissuePreservation`: 软组织保留度 (0.0-0.5)
- `edgeThickness`: 边缘厚度 (1-10)

### 2. BoneRenderModeController (骨骼渲染模式控制器)

**位置**: `Assets/3rdPlugin/UnityVolumeRendering/Scripts/VolumeProcessing/BoneRenderModeController.cs`

#### 功能特性

- **GUI界面控制**：
  - 实时显示Volume渲染器连接状态
  - 提供骨骼渲染器状态监控
  - 交互式按钮控制渲染模式切换

- **自动Volume发现**：
  - 场景中自动查找VolumeRenderedObject
  - 动态配置EnhancedBoneRenderer组件

- **状态反馈系统**：
  - 实时状态消息显示
  - 错误处理和用户提示

#### 主要方法

```csharp
// 启用骨骼隔离模式
public void EnableBoneIsolationMode()

// 切换GUI可见性
public void ToggleGUI()

// 查找并配置Volume渲染器
private void FindVolumeRenderer()

// 显示状态消息
private void ShowStatus(string message, Color color)
```

#### GUI功能

- **状态显示**：
  - Volume连接状态 (已连接/未找到)
  - 骨骼渲染器状态 (就绪/未配置)

- **控制按钮**：
  - "启用纯骨骼隔离模式" - 切换到梯度基础模式
  - "重新查找Volume" - 刷新Volume渲染器连接

## 使用方法

### 基本设置

1. **添加组件**：
   ```csharp
   // 在场景中添加BoneRenderModeController
   GameObject controllerObj = new GameObject("BoneController");
   BoneRenderModeController controller = controllerObj.AddComponent<BoneRenderModeController>();
   ```

2. **配置Volume渲染器**：
   ```csharp
   // 获取VolumeRenderedObject
   VolumeRenderedObject volumeRenderer = FindObjectOfType<VolumeRenderedObject>();

   // 添加EnhancedBoneRenderer组件
   EnhancedBoneRenderer boneRenderer = volumeRenderer.gameObject.AddComponent<EnhancedBoneRenderer>();
   boneRenderer.volumeRenderer = volumeRenderer;
   ```

3. **初始化渲染设置**：
   ```csharp
   boneRenderer.InitRenderSetting();
   ```

### 编程控制

```csharp
// 启用梯度基础骨骼渲染
controller.EnableBoneIsolationMode();

// 设置特定渲染模式
boneRenderer.SetRenderMode(BoneRenderMode.GradientBased);

// 自动识别身体部位并应用最佳设置
BodyPartType detectedPart = boneRenderer.AutoDetectBodyPart();
boneRenderer.SetBodyPartType(detectedPart);
```

### GUI操作

运行时通过Unity GUI界面：
1. 查看连接状态
2. 点击"启用纯骨骼隔离模式"切换渲染模式
3. 使用"重新查找Volume"刷新连接

## 技术细节

### 传递函数文件

骨骼渲染使用预定义的传递函数文件，存储在 `Assets/Resources/tf/` 目录：

- `medical-headBone.tf`: 头部骨骼优化
- `medical-chestBone.tf`: 胸部骨骼优化
- `medical-fubuBone.tf`: 腹部骨骼优化
- `medical-shangXiongBone.tf`: 上胸部骨骼优化
- `bonedefault.tf`: 默认通用骨骼

### HU阈值分析

系统使用HU (Hounsfield Unit) 值进行骨骼检测：
- 空气: ~-1000 HU
- 脂肪: ~-120 HU
- 水: ~0 HU
- 肌肉: ~40 HU
- 骨骼: >200 HU

### 渲染优化

- **梯度照明**: 增强骨骼边缘细节
- **遮罩提取**: 基于阈值的骨骼分割
- **自适应TF**: 根据身体部位自动选择最佳传递函数

## 兼容性

- **C#版本**: 兼容C# 4.0及以上 (已转换目标类型new表达式)
- **Unity版本**: Unity 2019.4+
- **平台**: 支持PicoXR VR设备

## 故障排除

### 常见问题

1. **Volume未找到**：
   - 确保场景中有VolumeRenderedObject组件
   - 检查数据是否正确加载

2. **骨骼渲染器未配置**：
   - 调用 `FindVolumeRenderer()` 重新查找
   - 手动添加EnhancedBoneRenderer组件

3. **传递函数加载失败**：
   - 检查 `Resources/tf/` 目录中的文件
   - 确认TF文件格式正确

### 调试信息

系统提供详细的控制台日志：
- 初始化状态
- 渲染模式切换
- 身体部位识别结果
- TF文件加载状态

## 扩展开发

### 添加新的渲染模式

1. 在 `BoneRenderMode` 枚举中添加新模式
2. 在 `EnhancedBoneRenderer.ApplyRenderMode()` 中添加处理逻辑
3. 实现对应的 `EnableXXXMode()` 方法

### 自定义传递函数

1. 创建新的TF文件
2. 在 `GetTFFileNameForNormalizedRegion()` 中添加映射
3. 测试不同身体部位的效果

---

*该文档描述了PicoXR Extension项目的骨骼渲染功能。如有问题请参考源代码注释或联系开发团队。*
