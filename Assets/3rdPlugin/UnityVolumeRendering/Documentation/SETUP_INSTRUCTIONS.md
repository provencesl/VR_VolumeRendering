# 安装和配置说明

## 系统要求

- Unity 2018.1.5 或更新版本
- 支持的平台：Windows, macOS, Linux
- 最低配置：4GB RAM, 2GB GPU VRAM

---

## 安装步骤

### 步骤1：导入文件

将以下文件夹复制到你的Unity项目：

```
Assets/
├─ Scripts/
│  ├─ TransferFunction/
│  │  ├─ MedicalHURange.cs ✨ 新增
│  │  └─ MedicalTransferFunctionPresets.cs ✨ 新增
│  ├─ PostProcessing/
│  │  ├─ SimplifiedPostProcessing.cs ✨ 新增
│  │  └─ PostProcessingSettings.cs ✨ 新增
│  └─ Examples/
│     └─ MedicalImagingExample.cs ✨ 新增
│
├─ Editor/
│  ├─ HURangeLayerEditorWindow.cs ✨ 新增
│  ├─ SimplifiedPostProcessingWindow.cs ✨ 新增
│  └─ MedicalTransferFunctionWindow.cs ✨ 新增
│
├─ Shaders/
│  ├─ Include/
│  │  └─ MedicalLighting.cginc ✨ 新增
│  ├─ PostProcessing/
│  │  ├─ SimplifiedMedicalPostProcessing.shader ✨ 新增
│  │  └─ MedicalImagePostProcessing.shader (可选)
│  └─ DirectVolumeRenderingShader.shader ✏️ 已修改
│
└─ Documentation/
   ├─ MedicalImagingOptimization_Guide.md ✨ 新增
   ├─ QUICKSTART.md ✨ 新增
   ├─ IMPLEMENTATION_SUMMARY.md ✨ 新增
   └─ SETUP_INSTRUCTIONS.md ✨ 新增
```

### 步骤2：验证导入

1. 打开Unity编辑器
2. 检查Console是否有错误
3. 菜单应该出现新选项：
   - `Window > Medical Imaging > HU Range Layer Manager`
   - `Window > Medical Imaging > Simplified Post-Processing`

### 步骤3：创建示例场景

1. 创建新场景：`File > New Scene`
2. 创建一个Cube作为容器：`GameObject > 3D Object > Cube`
3. 添加VolumeRenderedObject脚本
4. 加载CT数据集

### 步骤4：配置主摄像机

1. 选择Main Camera
2. 添加SimplifiedPostProcessor组件
3. 在Inspector中配置参数

---

## 快速配置（3步）

### 配置A：仅显示骨骼

```
1. 选择VolumeRenderedObject
2. 菜单 > Window > Medical Imaging > HU Range Layer Manager
3. 点击"Bone Only"
4. 完成！
```

### 配置B：骨骼+出血

```
1. HU Range Layer Manager > "Bone + Hemorrhage"
2. Simplified Post-Processing > "Normal"预设
3. 完成！
```

### 配置C：自定义（代码方式）

```csharp
// 在你的脚本中
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
if (layerManager == null)
    layerManager = volumeObject.gameObject.AddComponent<HURangeLayerManager>();

layerManager.ClearAllLayers();
layerManager.AddLayer(new HURangeLayer(MedicalHURange.TISSUE_PRESETS[0]));
```

---

## 文件说明

### 核心脚本

#### MedicalHURange.cs
- **作用：** HU范围定义和转换
- **关键类：**
  - `MedicalHURange`：静态HU值和预设
  - `HURangeLayer`：单个隔离层
  - `HURangeLayerManager`：多层管理

#### SimplifiedPostProcessing.cs
- **作用：** 简化后处理管理
- **关键类：**
  - `SimplifiedPostProcessingSettings`：参数配置
  - `SimplifiedPostProcessor`：后处理执行

#### MedicalTransferFunctionPresets.cs
- **作用：** 医学预设库（备选）
- **注：** 主要使用HU范围系统，此文件为备选

### 着色器

#### MedicalLighting.cginc
- **功能：**
  - Sobel边界检测
  - 医学光照模型
  - 自适应梯度阈值
  - 对比度增强

#### SimplifiedMedicalPostProcessing.shader
- **功能：**
  - 微妙边界增强
  - 清晰度（中间调对比度）
  - 锐化（Unsharp Mask）
  - 亮度调整

### 编辑器工具

#### HURangeLayerEditorWindow.cs
- **功能：**
  - 快速预设按钮
  - 组织预设选择
  - 图层列表管理
  - HU范围编辑

#### SimplifiedPostProcessingWindow.cs
- **功能：**
  - 预设选择
  - 参数调整
  - 启用/禁用开关

---

## 配置检查清单

- [ ] 所有文件已导入
- [ ] Console无错误
- [ ] 菜单出现新选项
- [ ] VolumeRenderedObject已创建
- [ ] 主摄像机已配置
- [ ] 数据集已加载
- [ ] HU范围管理器已添加
- [ ] 后处理器已添加

---

## 常见配置问题

### 问题1：菜单不出现

**原因：** 编辑器脚本未正确导入

**解决：**
```
1. 检查文件是否在Assets/Editor/目录
2. 重启Unity编辑器
3. 检查Console是否有编译错误
```

### 问题2：组件找不到

**原因：** 脚本命名空间或路径错误

**解决：**
```csharp
// 确保使用正确的命名空间
using UnityVolumeRendering;

// 获取组件
var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
```

### 问题3：着色器编译错误

**原因：** 着色器文件路径不正确

**解决：**
1. 检查`MedicalLighting.cginc`是否在`Assets/Shaders/Include/`
2. 检查`DirectVolumeRenderingShader.shader`中的include路径
3. 重新导入着色器

### 问题4：后处理不工作

**原因：** SimplifiedPostProcessor未正确添加到摄像机

**解决：**
```csharp
// 手动添加
Camera mainCamera = Camera.main;
SimplifiedPostProcessor pp = mainCamera.gameObject.AddComponent<SimplifiedPostProcessor>();
```

---

## 性能优化建议

### 初始配置

```
采样率：1.0x
立方插值：启用
后处理：启用（Normal预设）
阴影：启用
```

### 高性能模式

```
采样率：0.5x
立方插值：禁用
后处理：禁用
阴影：禁用
```

### 高质量模式

```
采样率：1.5x
立方插值：启用
后处理：启用（Bone预设）
阴影：启用
```

---

## 数据集准备

### 支持的格式

- ✅ DICOM (.dcm)
- ✅ NRRD (.nrrd)
- ✅ RAW (.raw)
- ✅ Nifti (.nii)
- ✅ VASP

### 数据转换

如果你的数据是其他格式，使用以下工具转换：

1. **SimpleITK**（推荐）
   ```python
   import SimpleITK as sitk
   img = sitk.ReadImage("input.dcm")
   sitk.WriteImage(img, "output.nrrd")
   ```

2. **GDCM**
   ```bash
   gdcmconv input.dcm output.nrrd
   ```

3. **dcm2niix**
   ```bash
   dcm2niix -o output/ input_folder/
   ```

---

## 测试验证

### 测试1：HU范围隔离

```csharp
// 在Play模式下运行
var example = GetComponent<MedicalImagingExample>();
example.ShowBoneOnly();
// 应该只看到白色骨骼，其他组织隐藏
```

### 测试2：多层显示

```csharp
example.ShowBoneAndHemorrhage();
// 应该看到白色骨骼和红色出血
```

### 测试3：后处理

```csharp
// 打开后处理窗口
// 调整参数，实时查看效果
```

---

## 升级指南

### 从旧版本升级

如果你有旧的2D TF配置：

1. **备份旧配置**
   ```
   Assets/Resources/TransferFunctionPresets/ (备份)
   ```

2. **迁移到新系统**
   ```csharp
   // 旧方式（不推荐）
   volumeObject.SetTransferFunction2DAsync(tf2d);
   
   // 新方式（推荐）
   var layerManager = volumeObject.GetComponent<HURangeLayerManager>();
   layerManager.AddLayer(new HURangeLayer(...));
   ```

3. **测试新配置**
   - 验证渲染质量
   - 检查性能
   - 调整参数

---

## 卸载说明

如果需要移除本优化：

1. **删除文件**
   ```
   Assets/Scripts/TransferFunction/MedicalHURange.cs
   Assets/Scripts/TransferFunction/MedicalTransferFunctionPresets.cs
   Assets/Scripts/PostProcessing/SimplifiedPostProcessing.cs
   Assets/Scripts/PostProcessing/PostProcessingSettings.cs
   Assets/Scripts/Examples/MedicalImagingExample.cs
   Assets/Editor/HURangeLayerEditorWindow.cs
   Assets/Editor/SimplifiedPostProcessingWindow.cs
   Assets/Editor/MedicalTransferFunctionWindow.cs
   Assets/Shaders/Include/MedicalLighting.cginc
   Assets/Shaders/PostProcessing/SimplifiedMedicalPostProcessing.shader
   Assets/Documentation/
   ```

2. **恢复DirectVolumeRenderingShader.shader**
   ```
   - 移除 #include "Include/MedicalLighting.cginc"
   - 移除 calculateMedicalLighting() 函数
   - 恢复原始 calculateLighting() 调用
   ```

3. **重启编辑器**

---

## 获取支持

### 文档
- `QUICKSTART.md` - 5分钟快速开始
- `MedicalImagingOptimization_Guide.md` - 完整指南
- `IMPLEMENTATION_SUMMARY.md` - 技术细节

### 代码示例
- `MedicalImagingExample.cs` - 10个使用示例

### 编辑器工具
- HU Range Layer Manager - 图形界面
- Simplified Post-Processing - 参数调整

---

## 反馈与改进

如有建议或问题：

1. 检查文档和示例
2. 查看Console错误信息
3. 尝试不同的参数组合
4. 参考医学影像标准

---

**安装完成！** 🎉

现在你可以开始使用HU范围隔离系统和简化后处理来创建高质量的医学影像渲染了。

建议下一步：
1. 阅读 `QUICKSTART.md`
2. 尝试 `MedicalImagingExample.cs` 中的示例
3. 在编辑器中实验不同的预设

祝你成功！
