using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;
using System.Collections.Generic;

namespace UnityVolumeRendering.Segmentation.Editor
{
    /// <summary>
    /// 分割编辑器窗口 - Unity Editor集成
    /// 提供图形界面用于血肿区域的分割和绘制
    /// </summary>
    public class SegmentEditorWindow : EditorWindow
    {
        private SegmentationManager segmentationManager;
        private PaintToolSystem paintTool;
        private VolumeRenderedObject volumeObject;
        
        // 工具栏选项
        private int selectedTool = 0;
        private string[] toolNames = new string[] { "区域生长", "阈值分割", "画笔工具", "橡皮擦", "填充", "形态学" };
        
        // 画笔模式选项
        private int selectedPaintMode = 0;
        private string[] paintModeNames = new string[] { "圆形", "椭圆", "矩形", "自由绘制", "多边形", "点" };
        
        // 形态学操作选项
        private int selectedMorphology = 0;
        private string[] morphologyNames = new string[] { "膨胀", "腐蚀", "开运算", "闭运算" };
        
        // 折叠面板
        private bool showThresholdSettings = true;
        private bool showBrushSettings = true;
        private bool showSegmentList = true;
        
        // 分割列表滚动
        private Vector2 segmentListScroll;
        
        [MenuItem("Window/血肿分割编辑器")]
        public static void ShowWindow()
        {
            GetWindow<SegmentEditorWindow>("血肿分割编辑器");
        }
        
        [MenuItem("Tools/血肿分割/打开编辑器")]
        public static void OpenFromMenu()
        {
            ShowWindow();
        }
        
        void OnEnable()
        {
            FindVolumeObject();
            // 订阅种子点添加事件以触发重绘
            if (segmentationManager != null)
            {
                segmentationManager.OnSeedPointAdded -= OnSeedPointAddedHandler;
                segmentationManager.OnSeedPointAdded += OnSeedPointAddedHandler;
            }
        }
        
        void OnDisable()
        {
            // 取消订阅事件
            if (segmentationManager != null)
            {
                segmentationManager.OnSeedPointAdded -= OnSeedPointAddedHandler;
            }
        }
        
        /// <summary>
        /// 种子点添加事件处理器 - 触发编辑器窗口重绘
        /// </summary>
        private void OnSeedPointAddedHandler(Vector3Int voxelPos)
        {
            Repaint();
        }
        
        void FindVolumeObject()
        {
            // 查找场景中的VolumeRenderedObject
            volumeObject = FindObjectOfType<VolumeRenderedObject>();
            
            if (volumeObject != null)
            {
                // 先取消旧的事件订阅
                if (segmentationManager != null)
                {
                    segmentationManager.OnSeedPointAdded -= OnSeedPointAddedHandler;
                }
                
                // 获取或创建SegmentationManager
                segmentationManager = volumeObject.GetComponent<SegmentationManager>();
                if (segmentationManager == null)
                {
                    segmentationManager = volumeObject.gameObject.AddComponent<SegmentationManager>();
                }
                segmentationManager.volumeObject = volumeObject;
                segmentationManager.InitializeFromVolumeObject();
                
                // 订阅种子点添加事件
                segmentationManager.OnSeedPointAdded -= OnSeedPointAddedHandler;
                segmentationManager.OnSeedPointAdded += OnSeedPointAddedHandler;
                
                // 获取或创建PaintToolSystem
                paintTool = volumeObject.GetComponent<PaintToolSystem>();
                if (paintTool == null)
                {
                    paintTool = volumeObject.gameObject.AddComponent<PaintToolSystem>();
                }
                paintTool.volumeObject = volumeObject;
                paintTool.segmentationManager = segmentationManager;
            }
        }
        
        void OnGUI()
        {
            // 标题
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("血肿分割编辑器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("使用此工具对医学体数据进行血肿区域分割和绘制", MessageType.Info);
            
            // 场景对象引用
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("场景引用", EditorStyles.boldLabel);
            volumeObject = (VolumeRenderedObject)EditorGUILayout.ObjectField("体积对象", volumeObject, typeof(VolumeRenderedObject), true);
            
            if (volumeObject == null)
            {
                EditorGUILayout.HelpBox("请在场景中选择一个VolumeRenderedObject", MessageType.Warning);
                if (GUILayout.Button("查找体积对象"))
                {
                    FindVolumeObject();
                }
                return;
            }
            
            // 确保组件存在
            if (segmentationManager == null || paintTool == null)
            {
                FindVolumeObject();
            }
            
            // 工具栏
            EditorGUILayout.Space();
            selectedTool = GUILayout.Toolbar(selectedTool, toolNames);
            
            EditorGUILayout.Space();
            
            // 根据选中的工具显示不同的设置
            switch (selectedTool)
            {
                case 0: // 区域生长
                    DrawGrowFromSeedsTool();
                    break;
                case 1: // 阈值分割
                    DrawThresholdTool();
                    break;
                case 2: // 画笔工具
                    DrawPaintTool();
                    break;
                case 3: // 橡皮擦
                    DrawEraserTool();
                    break;
                case 4: // 填充
                    DrawFloodFillTool();
                    break;
                case 5: // 形态学
                    DrawMorphologyTool();
                    break;
            }
            
            // 分割列表
            EditorGUILayout.Space();
            DrawSegmentList();
            
            // 撤销/重做按钮
            EditorGUILayout.Space();
            DrawUndoRedoButtons();
            
            // 保存/加载按钮
            EditorGUILayout.Space();
            DrawSaveLoadButtons();
            
            // 统计信息
            EditorGUILayout.Space();
            DrawStatistics();
        }
        
        void DrawGrowFromSeedsTool()
        {
            EditorGUILayout.LabelField("区域生长工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在视图中点击添加种子点，然后点击执行开始区域生长", MessageType.None);
            
            // 显示种子点数量
            EditorGUILayout.LabelField($"种子点数量: {segmentationManager?.SeedPointCount ?? 0}");
            
            // 参数设置
            EditorGUILayout.Space();
            showThresholdSettings = EditorGUILayout.Foldout(showThresholdSettings, "血肿参数");
            if (showThresholdSettings)
            {
                segmentationManager.minHU = EditorGUILayout.Slider("最小HU值", segmentationManager.minHU, -100f, 200f);
                segmentationManager.maxHU = EditorGUILayout.Slider("最大HU值", segmentationManager.maxHU, -100f, 200f);
                segmentationManager.similarityTolerance = EditorGUILayout.Slider("相似度容差", segmentationManager.similarityTolerance, 1f, 50f);
            }
            
            EditorGUILayout.Space();
            
            // 按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("执行区域生长", GUILayout.Height(30)))
            {
                if (segmentationManager.SeedPointCount > 0)
                {
                    segmentationManager.ExecuteGrowFromSeeds(segmentationManager.similarityTolerance);
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请先添加种子点", "确定");
                }
            }
            
            if (GUILayout.Button("清除种子点", GUILayout.Height(30)))
            {
                segmentationManager.ClearSeedPoints();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("提示：在视图中按住Ctrl+点击添加种子点", MessageType.Info);
        }
        
        void DrawThresholdTool()
        {
            EditorGUILayout.LabelField("阈值分割工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("根据HU值范围自动分割血肿区域", MessageType.None);
            
            // 参数设置
            showThresholdSettings = EditorGUILayout.Foldout(showThresholdSettings, "阈值参数");
            if (showThresholdSettings)
            {
                segmentationManager.minHU = EditorGUILayout.Slider("最小HU值", segmentationManager.minHU, -100f, 200f);
                segmentationManager.maxHU = EditorGUILayout.Slider("最大HU值", segmentationManager.maxHU, -100f, 200f);
            }
            
            EditorGUILayout.Space();
            
            // 自动检测按钮
            if (GUILayout.Button("自动检测阈值", GUILayout.Height(25)))
            {
                segmentationManager.AutoDetectThreshold();
            }
            
            EditorGUILayout.Space();
            
            // 执行按钮
            if (GUILayout.Button("执行阈值分割", GUILayout.Height(30)))
            {
                segmentationManager.ExecuteHematomaThreshold();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("典型血肿HU值范围: 30-80 HU\\n(根据实际情况调整)", MessageType.Info);
        }
        
        void DrawPaintTool()
        {
            EditorGUILayout.LabelField("画笔工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在切片视图中手动绘制分割区域", MessageType.None);
            
            // 画笔模式
            selectedPaintMode = EditorGUILayout.Popup("绘制模式", selectedPaintMode, paintModeNames);
            paintTool.currentMode = (PaintMode)selectedPaintMode;
            
            // 画笔设置
            showBrushSettings = EditorGUILayout.Foldout(showBrushSettings, "画笔设置");
            if (showBrushSettings)
            {
                paintTool.brushRadius = EditorGUILayout.IntSlider("画笔半径", paintTool.brushRadius, 1, 50);
                paintTool.fillShape = EditorGUILayout.Toggle("填充形状", paintTool.fillShape);
                paintTool.use3DBrush = EditorGUILayout.Toggle("3D画笔", paintTool.use3DBrush);
            }
            
            // 切片方向
            EditorGUILayout.Space();
            paintTool.sliceOrientation = (SliceOrientation)EditorGUILayout.EnumPopup("切片方向", paintTool.sliceOrientation);
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("操作说明:\\n- 左键拖动: 绘制形状\\n- 右键: 取消绘制\\n- [ / ]: 调整画笔大小\\n- F: 切换填充模式\\n- Q: 切换切片方向", MessageType.Info);
        }
        
        void DrawEraserTool()
        {
            EditorGUILayout.LabelField("橡皮擦工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("擦除已分割的区域", MessageType.None);
            
            paintTool.brushRadius = EditorGUILayout.IntSlider("橡皮擦半径", paintTool.brushRadius, 1, 50);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("操作说明:\\n- 左键拖动: 擦除区域\\n- 右键: 取消操作", MessageType.Info);
        }
        
        void DrawFloodFillTool()
        {
            EditorGUILayout.LabelField("填充工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("从种子点填充连通的区域（点击视图中的一点）", MessageType.None);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("执行填充", GUILayout.Height(30)))
            {
                // 需要实现FloodFill算法
                Debug.Log("FloodFill功能待实现");
            }
        }
        
        void DrawMorphologyTool()
        {
            EditorGUILayout.LabelField("形态学操作", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("对分割结果进行形态学处理", MessageType.None);
            
            // 操作选择
            selectedMorphology = EditorGUILayout.Popup("操作类型", selectedMorphology, morphologyNames);
            
            EditorGUILayout.Space();
            
            // 迭代次数
            int iterations = EditorGUILayout.IntSlider("迭代次数", 1, 1, 10);
            
            EditorGUILayout.Space();
            
            // 执行按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("执行", GUILayout.Height(30)))
            {
                switch (selectedMorphology)
                {
                    case 0:
                        segmentationManager.MorphologyDilate(iterations);
                        break;
                    case 1:
                        segmentationManager.MorphologyErode(iterations);
                        break;
                    case 2:
                        segmentationManager.MorphologyOpen(iterations);
                        break;
                    case 3:
                        segmentationManager.MorphologyClose(iterations);
                        break;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("膨胀: 扩大分割区域\\n腐蚀: 收缩分割区域\\n开运算: 先腐蚀后膨胀（去噪）\\n闭运算: 先膨胀后腐蚀（填补空洞）", MessageType.Info);
        }
        
        void DrawSegmentList()
        {
            showSegmentList = EditorGUILayout.Foldout(showSegmentList, "分割列表");
            if (showSegmentList)
            {
                segmentListScroll = EditorGUILayout.BeginScrollView(segmentListScroll, GUILayout.Height(150));
                
                if (segmentationManager.segments.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无分割");
                }
                else
                {
                    for (int i = 0; i < segmentationManager.segments.Count; i++)
                    {
                        Segment segment = segmentationManager.segments[i];
                        EditorGUILayout.BeginHorizontal();
                        
                        // 选择活动分割
                        bool isActive = (segment == segmentationManager.activeSegment);
                        bool newIsActive = EditorGUILayout.Toggle(isActive, GUILayout.Width(20));
                        
                        if (newIsActive && !isActive)
                        {
                            segmentationManager.SetActiveSegment(segment);
                        }
                        
                        // 颜色选择
                        segment.color = EditorGUILayout.ColorField(segment.color, GUILayout.Width(50));
                        
                        // 名称
                        segment.name = EditorGUILayout.TextField(segment.name);
                        
                        // 可见性
                        segment.visible = EditorGUILayout.Toggle(segment.visible, GUILayout.Width(20));
                        
                        // 删除按钮
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            segmentationManager.DeleteSegment(segment);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                        
                        // 显示体素数量
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField($"体素: {segment.voxelCount}");
                        EditorGUI.indentLevel--;
                    }
                }
                
                EditorGUILayout.EndScrollView();
                
                // 新建分割按钮
                if (GUILayout.Button("新建分割"))
                {
                    segmentationManager.CreateNewSegment();
                }
            }
        }
        
        void DrawUndoRedoButtons()
        {
            EditorGUILayout.LabelField("编辑操作", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("撤销 (Ctrl+Z)"))
            {
                segmentationManager.Undo();
            }
            
            if (GUILayout.Button("重做 (Ctrl+Y)"))
            {
                segmentationManager.Redo();
            }
            
            if (GUILayout.Button("清除当前分割"))
            {
                if (EditorUtility.DisplayDialog("确认", "确定要清除当前分割吗？", "确定", "取消"))
                {
                    paintTool.ClearCurrentSegment();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawSaveLoadButtons()
        {
            EditorGUILayout.LabelField("数据管理", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存分割"))
            {
                segmentationManager.SaveSegmentation();
            }
            
            if (GUILayout.Button("加载分割"))
            {
                segmentationManager.LoadSegmentation();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawStatistics()
        {
            EditorGUILayout.LabelField("统计信息", EditorStyles.boldLabel);
            
            if (segmentationManager.activeSegment != null)
            {
                EditorGUILayout.LabelField($"活动分割: {segmentationManager.activeSegment.name}");
                EditorGUILayout.LabelField($"体素数量: {segmentationManager.activeSegment.voxelCount}");
                
                // 计算体积（假设体素尺寸为1mm）
                float volume = segmentationManager.CalculateVolume();
                EditorGUILayout.LabelField($"估计体积: {volume:F2} mm³");
            }
            else
            {
                EditorGUILayout.LabelField("无活动分割");
            }
        }
    }
}
