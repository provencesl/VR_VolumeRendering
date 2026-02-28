using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections.Generic;

namespace UnityVolumeRendering
{
    /// <summary>
    /// 运行时切片查看UI
    /// 将EditorWindow的功能转换为场景UI
    /// 支持ROI血肿区域勾画（基于轮廓采集+Scanline填充）
    /// </summary>
    public class SliceViewerUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI组件")]
        public RawImage sliceImage; // 显示切片的RawImage
        public RawImage roiOverlayImage; // ROI覆盖层
        public Slider sliceSlider; // 控制切片位置的Slider
        public Text valueText; // 显示值或距离的Text
        public Button moveButton, inspectButton, measureButton, roiButton; // 模式切换按钮
        public Button rotateLeftButton, rotateRightButton; // 旋转按钮
        public Button prevPlaneButton, nextPlaneButton; // 平面切换按钮
        public Button createXYButton, createXZButton, createZYButton; // 创建平面按钮
        public Button removePlaneButton; // 删除平面按钮
        public Button clearROIPButton; // 清除当前层ROI按钮
        public Button undoROIPButton; // 撤销当前层ROI按钮

        [Header("图标")]
        public Texture moveIcon, inspectIcon, measureIcon, rotateLeftIcon, rotateRightIcon, roiIcon;

        [Header("ROI设置")]
        public Color roiColor = new Color(1f, 0f, 0f, 0.4f); // ROI颜色（半透明红）
        public Color contourColor = Color.yellow; // 轮廓颜色
        public float minPointDistance = 2f; // 最小采点距离
        
        [Header("分割管理器")]
        public Segmentation.SegmentationManager segmentationManager;

        private int selectedPlaneIndex = -1;
        private bool mouseIsDown = false;
        private Vector2 mousePressPosition;
        private Vector2 prevMousePos;
        private Vector2 measurePoint;

        private InputMode inputMode = InputMode.Move;

        private enum InputMode
        {
            Move,
            Inspect,
            Measure,
            ROI, // ROI勾画模式
            SeedPoint // 种子点模式
        }

        private VolumeRenderedObject volumeObject;
        
        // 3D标签图（与CT数据同尺寸）
        private int[,,] labelMap;
        // ROI覆盖层纹理
        private Texture2D roiOverlayTexture;
        // 当前切片索引
        private int currentSliceIndex = 0;
        
        // ROI轮廓绘制相关
        private List<Vector2Int> contourPoints = new List<Vector2Int>();
        private bool isDrawingROI = false;
        
        // 存储每层的ROI历史（用于撤销）- 每个切片层存储多个轮廓
        private Dictionary<int, List<List<Vector2Int>>> sliceContourHistory = new Dictionary<int, List<List<Vector2Int>>>();
        
        /// <summary>
        /// 获取当前切片索引
        /// </summary>
        public int CurrentSliceIndex => currentSliceIndex;

        void Start()
        {
            volumeObject = FindObjectOfType<VolumeRenderedObject>();
            if (volumeObject == null)
            {
                Debug.LogError("未找到VolumeRenderedObject!");
                return;
            }

            // 初始化3D标签图
            InitializeLabelMap();

            // 初始化ROI覆盖层
            InitializeROIOverlay();

            // 初始化按钮事件
            // moveButton.onClick.AddListener(() => SetInputMode(InputMode.Move));
            // inspectButton.onClick.AddListener(() => SetInputMode(InputMode.Inspect));
            // measureButton.onClick.AddListener(() => SetInputMode(InputMode.Measure));
            roiButton.onClick.AddListener(() => SetInputMode(InputMode.ROI));

            // rotateLeftButton.onClick.AddListener(RotateLeft);
            // rotateRightButton.onClick.AddListener(RotateRight);

            // prevPlaneButton.onClick.AddListener(PrevPlane);
            // nextPlaneButton.onClick.AddListener(NextPlane);

            // createXYButton.onClick.AddListener(CreateXYPlane);
            // createXZButton.onClick.AddListener(CreateXZPlane);
            // createZYButton.onClick.AddListener(CreateZYPlane);

            // removePlaneButton.onClick.AddListener(RemovePlane);
            // clearROIPButton.onClick.AddListener(ClearCurrentSliceROI);
            // undoROIPButton.onClick.AddListener(UndoCurrentSliceROI);

            CreateXYPlane();
            // Slider事件
            sliceSlider.onValueChanged.AddListener(OnSliceSliderChanged);

            // 初始化选中平面
            UpdateSelectedPlane();
            
            // 初始化按钮状态
            // UpdateButtonStates();
            
            // 初始化ROI覆盖层
            UpdateOverlay(currentSliceIndex);
        }

        void Update()
        {
            UpdateSliceDisplay();
            UpdateUI();
            
            // G键执行区域生长分割
            if (Input.GetKeyDown(KeyCode.G) && segmentationManager != null)
            {
                ExecuteRegionGrowing();
            }
            
            // S键切换到种子点模式
            if (Input.GetKeyDown(KeyCode.S))
            {
                SetSeedPointMode();
            }
        }

        /// <summary>
        /// 初始化3D标签图
        /// </summary>
        private void InitializeLabelMap()
        {
            VolumeDataset dataset = volumeObject.dataset;
            if (dataset != null)
            {
                labelMap = new int[dataset.dimX, dataset.dimY, dataset.dimZ];
                Debug.Log($"已初始化3D标签图: {dataset.dimX}x{dataset.dimY}x{dataset.dimZ}");
            }
        }

        /// <summary>
        /// 初始化ROI覆盖层
        /// </summary>
        private void InitializeROIOverlay()
        {
            if (volumeObject == null) return;
            
            VolumeDataset dataset = volumeObject.dataset;
            if (dataset == null) return;
            
            // 创建2D纹理用于显示ROI覆盖层
            roiOverlayTexture = new Texture2D(dataset.dimX, dataset.dimY, TextureFormat.RGBA32, false);
            roiOverlayTexture.filterMode = FilterMode.Point;
            
            // 设置给UI
            if (roiOverlayImage != null)
            {
                roiOverlayImage.texture = roiOverlayTexture;
                roiOverlayImage.color = Color.white;
            }
        }

        private void SetInputMode(InputMode mode)
        {
            inputMode = mode;
            // UpdateButtonStates();
            
            // 切换到ROI模式时，显示当前层的ROI
            if (inputMode == InputMode.ROI)
            {
                UpdateOverlay(currentSliceIndex);
            }
            
            // 切换到种子点模式时，提示用户
            if (inputMode == InputMode.SeedPoint)
            {
                Debug.Log("已切换到种子点模式 - 点击切片添加种子点，按 G 键执行区域生长");
            }
        }
        
        /// <summary>
        /// 设置种子点模式
        /// </summary>
        public void SetSeedPointMode()
        {
            SetInputMode(InputMode.SeedPoint);
        }
        
        /// <summary>
        /// 执行区域生长分割
        /// </summary>
        public void ExecuteRegionGrowing()
        {
            if (segmentationManager == null)
            {
                Debug.LogWarning("SegmentationManager未设置，无法执行区域生长");
                return;
            }
            
            segmentationManager.ExecuteGrowFromSeeds(15f); // 使用默认容差
            Debug.Log("区域生长分割已执行");
        }

        private void UpdateButtonStates()
        {
            moveButton.image.sprite = Sprite.Create((Texture2D)moveIcon, new Rect(0, 0, moveIcon.width, moveIcon.height), Vector2.zero);
            inspectButton.image.sprite = Sprite.Create((Texture2D)inspectIcon, new Rect(0, 0, inspectIcon.width, inspectIcon.height), Vector2.zero);
            measureButton.image.sprite = Sprite.Create((Texture2D)measureIcon, new Rect(0, 0, measureIcon.width, measureIcon.height), Vector2.zero);
            roiButton.image.sprite = Sprite.Create((Texture2D)roiIcon, new Rect(0, 0, roiIcon.width, roiIcon.height), Vector2.zero);

            // 高亮当前模式
            moveButton.image.color = inputMode == InputMode.Move ? Color.yellow : Color.white;
            inspectButton.image.color = inputMode == InputMode.Inspect ? Color.yellow : Color.white;
            measureButton.image.color = inputMode == InputMode.Measure ? Color.yellow : Color.white;
            roiButton.image.color = inputMode == InputMode.ROI ? Color.yellow : Color.white;
        }

        private void RotateLeft()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                SlicingPlane plane = planes[selectedPlaneIndex];
                Vector3 planeNormal = -plane.transform.up;
                plane.transform.Rotate(planeNormal * -90.0f, Space.World);
            }
        }

        private void RotateRight()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                SlicingPlane plane = planes[selectedPlaneIndex];
                Vector3 planeNormal = -plane.transform.up;
                plane.transform.Rotate(planeNormal * 90.0f, Space.World);
            }
        }

        private void PrevPlane()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (planes.Length > 0)
            {
                selectedPlaneIndex = selectedPlaneIndex == 0 ? planes.Length - 1 : selectedPlaneIndex - 1;
                UpdateSelectedPlane();
            }
        }

        private void NextPlane()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (planes.Length > 0)
            {
                selectedPlaneIndex = (selectedPlaneIndex + 1) % planes.Length;
                UpdateSelectedPlane();
            }
        }

        private void CreateXYPlane()
        {
            if (volumeObject != null)
            {
                SlicingPlane plane = volumeObject.CreateSlicingPlane();
                selectedPlaneIndex = FindObjectsOfType<SlicingPlane>().Length - 1;
                UpdateSelectedPlane();
            }
        }

        private void CreateXZPlane()
        {
            if (volumeObject != null)
            {
                SlicingPlane plane = volumeObject.CreateSlicingPlane();
                plane.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                selectedPlaneIndex = FindObjectsOfType<SlicingPlane>().Length - 1;
                UpdateSelectedPlane();
            }
        }

        private void CreateZYPlane()
        {
            if (volumeObject != null)
            {
                SlicingPlane plane = volumeObject.CreateSlicingPlane();
                plane.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);
                selectedPlaneIndex = FindObjectsOfType<SlicingPlane>().Length - 1;
                UpdateSelectedPlane();
            }
        }

        private void RemovePlane()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (planes.Length > 0 && selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                Destroy(planes[selectedPlaneIndex].gameObject);
                selectedPlaneIndex = Mathf.Max(0, selectedPlaneIndex - 1);
                UpdateSelectedPlane();
            }
        }

        /// <summary>
        /// 清除当前切片的ROI
        /// </summary>
        private void ClearCurrentSliceROI()
        {
            if (labelMap == null) return;
            
            VolumeDataset dataset = volumeObject.dataset;
            for (int x = 0; x < dataset.dimX; x++)
            {
                for (int y = 0; y < dataset.dimY; y++)
                {
                    labelMap[x, y, currentSliceIndex] = 0;
                }
            }
            
            // 清除历史记录
            if (sliceContourHistory.ContainsKey(currentSliceIndex))
            {
                sliceContourHistory.Remove(currentSliceIndex);
            }
            
            UpdateOverlay(currentSliceIndex);
            Debug.Log($"已清除第 {currentSliceIndex} 层的ROI");
        }

        /// <summary>
        /// 撤销当前层的上一次ROI
        /// </summary>
        private void UndoCurrentSliceROI()
        {
            if (!sliceContourHistory.ContainsKey(currentSliceIndex))
            {
                Debug.Log($"第 {currentSliceIndex} 层没有可撤销的ROI");
                return;
            }
            
            var history = sliceContourHistory[currentSliceIndex];
            if (history.Count == 0) return;
            
            // 移除最后一次的轮廓点
            history.RemoveAt(history.Count - 1);
            
            // 重新填充labelMap
            VolumeDataset dataset = volumeObject.dataset;
            for (int x = 0; x < dataset.dimX; x++)
            {
                for (int y = 0; y < dataset.dimY; y++)
                {
                    labelMap[x, y, currentSliceIndex] = 0;
                }
            }
            
            // 重新填充所有历史轮廓
            foreach (var contour in history)
            {
                // 这里简化处理：只保留最后一个轮廓
            }
            
            UpdateOverlay(currentSliceIndex);
            Debug.Log($"已撤销第 {currentSliceIndex} 层的ROI");
        }

        private void UpdateSelectedPlane()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (planes.Length > 0)
            {
                selectedPlaneIndex = Mathf.Clamp(selectedPlaneIndex, 0, planes.Length - 1);
            }
            else
            {
                selectedPlaneIndex = -1;
            }
        }

        private void OnSliceSliderChanged(float value)
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                SlicingPlane plane = planes[selectedPlaneIndex];
                Vector3 moveDir = plane.transform.up;
                float moveAmount = (value - 0.5f) * 2.0f;
                plane.transform.position = volumeObject.transform.position + moveDir * moveAmount;
            }
            
            // 更新当前切片索引
            VolumeDataset dataset = volumeObject.dataset;
            if (dataset != null)
            {
                currentSliceIndex = Mathf.RoundToInt(value * (dataset.dimZ - 1));
                currentSliceIndex = Mathf.Clamp(currentSliceIndex, 0, dataset.dimZ - 1);
                
                // 切换切片时自动刷新ROI显示
                UpdateOverlay(currentSliceIndex);
            }
        }

        private void UpdateSliceDisplay()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                SlicingPlane plane = planes[selectedPlaneIndex];
                Texture sliceTexture = plane.GetSliceTexture();
                if (sliceTexture != null)
                {
                    sliceImage.texture = sliceTexture;
                }
            }
        }

        private void UpdateUI()
        {
            SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
            if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
            {
                SlicingPlane plane = planes[selectedPlaneIndex];
                Vector3 localPos = volumeObject.transform.InverseTransformPoint(plane.transform.position);
                float sliderValue = (localPos.magnitude / 2.0f) + 0.5f;
                sliceSlider.value = Mathf.Clamp01(sliderValue);
            }
        }

        // =========================
        // ROI轮廓绘制接口实现
        // =========================

        /// <summary>
        /// 鼠标按下 → 开始采点
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            // 种子点模式 - 直接添加种子点
            if (inputMode == InputMode.SeedPoint)
            {
                HandleSeedPointClick(eventData);
                return;
            }
            
            if (inputMode != InputMode.ROI) return;
            
            contourPoints.Clear();
            isDrawingROI = true;

            Vector2Int voxel = GetVoxelFromMouse(eventData);
            contourPoints.Add(voxel);

            Debug.Log("开始绘制ROI轮廓");
        }
        
        /// <summary>
        /// 处理种子点点击
        /// </summary>
        private void HandleSeedPointClick(PointerEventData eventData)
        {
            if (segmentationManager == null)
            {
                Debug.LogWarning("SegmentationManager未设置，无法添加种子点");
                return;
            }
            
            Vector2Int voxel2D = GetVoxelFromMouse(eventData);
            Vector3Int voxel3D = new Vector3Int(voxel2D.x, voxel2D.y, currentSliceIndex);
            
            bool success = segmentationManager.AddSeedPointFromVoxel(voxel3D);
            if (success)
            {
                Debug.Log($"✓ 种子点添加成功: 切片坐标({voxel2D.x}, {voxel2D.y}), 切片索引={currentSliceIndex}");
                
                // 在切片上显示种子点标记
                ShowSeedPointOnSlice(voxel2D);
            }
        }
        
        /// <summary>
        /// 在切片上显示种子点标记
        /// </summary>
        private void ShowSeedPointOnSlice(Vector2Int voxel2D)
        {
            if (roiOverlayTexture != null)
            {
                // 绘制一个小十字标记
                int size = 3;
                Color seedColor = Color.green;
                
                for (int dx = -size; dx <= size; dx++)
                {
                    int x = voxel2D.x + dx;
                    if (x >= 0 && x < roiOverlayTexture.width)
                    {
                        roiOverlayTexture.SetPixel(x, voxel2D.y, seedColor);
                    }
                }
                
                for (int dy = -size; dy <= size; dy++)
                {
                    int y = voxel2D.y + dy;
                    if (y >= 0 && y < roiOverlayTexture.height)
                    {
                        roiOverlayTexture.SetPixel(voxel2D.x, y, seedColor);
                    }
                }
                
                roiOverlayTexture.Apply();
            }
        }

        /// <summary>
        /// 鼠标拖动 → 采集轮廓点
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDrawingROI || inputMode != InputMode.ROI) return;

            Vector2Int voxel = GetVoxelFromMouse(eventData);

            // 距离太近不采样
            if (contourPoints.Count > 0)
            {
                if (Vector2.Distance(contourPoints[contourPoints.Count - 1], voxel) < minPointDistance)
                    return;
            }

            contourPoints.Add(voxel);

            // 实时画轮廓线预览
            DrawContourPreview();
        }

        /// <summary>
        /// 鼠标松开 → 自动闭合+填充
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDrawingROI || inputMode != InputMode.ROI) return;
            isDrawingROI = false;

            Debug.Log("轮廓绘制结束 → 自动闭合");

            // 自动闭合轮廓
            CloseContour();

            // 填充ROI区域
            FillPolygon(contourPoints, currentSliceIndex);

            // 保存到历史记录
            if (!sliceContourHistory.ContainsKey(currentSliceIndex))
            {
                sliceContourHistory[currentSliceIndex] = new List<List<Vector2Int>>();
            }
            // 保存轮廓点的副本
            sliceContourHistory[currentSliceIndex].Add(new List<Vector2Int>(contourPoints));

            // 刷新Overlay显示
            UpdateOverlay(currentSliceIndex);

            Debug.Log("ROI封闭区域生成完成");
        }

        /// <summary>
        /// 自动闭合轮廓
        /// </summary>
        private void CloseContour()
        {
            if (contourPoints.Count < 3) return;
            // 添加首点形成闭环
            contourPoints.Add(contourPoints[0]);
        }

        /// <summary>
        /// Scanline填充封闭区域
        /// </summary>
        private void FillPolygon(List<Vector2Int> pts, int slice)
        {
            if (pts.Count < 4) return;

            VolumeDataset dataset = volumeObject.dataset;

            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (var p in pts)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            // Scanline扫描
            for (int y = minY; y <= maxY; y++)
            {
                List<int> nodes = new List<int>();

                // 找交点
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2Int p1 = pts[i];
                    Vector2Int p2 = pts[i + 1];

                    if ((p1.y < y && p2.y >= y) || (p2.y < y && p1.y >= y))
                    {
                        int x = p1.x + (y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y);
                        nodes.Add(x);
                    }
                }

                nodes.Sort();

                // 填充区间
                for (int i = 0; i < nodes.Count; i += 2)
                {
                    if (i + 1 >= nodes.Count) break;

                    for (int x = nodes[i]; x < nodes[i + 1]; x++)
                    {
                        if (IsInsideVolume(x, y, slice))
                            labelMap[x, y, slice] = 1;
                    }
                }
            }
        }

        /// <summary>
        /// 实时轮廓预览（只画线）
        /// </summary>
        private void DrawContourPreview()
        {
            ClearOverlay();

            foreach (var p in contourPoints)
            {
                roiOverlayTexture.SetPixel(p.x, p.y, contourColor);
            }

            roiOverlayTexture.Apply();
        }

        /// <summary>
        /// 清除Overlay
        /// </summary>
        private void ClearOverlay()
        {
            VolumeDataset dataset = volumeObject.dataset;
            for (int x = 0; x < dataset.dimX; x++)
            {
                for (int y = 0; y < dataset.dimY; y++)
                {
                    roiOverlayTexture.SetPixel(x, y, Color.clear);
                }
            }
            roiOverlayTexture.Apply();
        }

        /// <summary>
        /// 更新ROI覆盖层显示
        /// </summary>
        public void UpdateOverlay(int slice)
        {
            ClearOverlay();

            VolumeDataset dataset = volumeObject.dataset;

            for (int x = 0; x < dataset.dimX; x++)
            {
                for (int y = 0; y < dataset.dimY; y++)
                {
                    if (labelMap[x, y, slice] == 1)
                        roiOverlayTexture.SetPixel(x, y, roiColor);
                }
            }

            roiOverlayTexture.Apply();
        }

        /// <summary>
        /// 鼠标位置 → 切片Voxel坐标
        /// </summary>
        private Vector2Int GetVoxelFromMouse(PointerEventData eventData)
        {
            RectTransform rt = sliceImage.rectTransform;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt,
                eventData.position,
                eventData.pressEventCamera,
                out local
            );

            Rect rect = rt.rect;

            float u = (local.x - rect.xMin) / rect.width;
            float v = (local.y - rect.yMin) / rect.height;

            VolumeDataset dataset = volumeObject.dataset;
            int x = Mathf.Clamp((int)(u * dataset.dimX), 0, dataset.dimX - 1);
            int y = Mathf.Clamp((int)(v * dataset.dimY), 0, dataset.dimY - 1);

            return new Vector2Int(x, y);
        }

        /// <summary>
        /// 检查坐标是否在体积内
        /// </summary>
        private bool IsInsideVolume(int x, int y, int z)
        {
            VolumeDataset dataset = volumeObject.dataset;
            return x >= 0 && x < dataset.dimX &&
                   y >= 0 && y < dataset.dimY &&
                   z >= 0 && z < dataset.dimZ;
        }

        // 鼠标事件处理（非ROI模式）
        private void OnPointerDown(BaseEventData data)
        {
            if (inputMode == InputMode.ROI) return; // ROI模式由接口处理
            
            PointerEventData pointerData = data as PointerEventData;
            if (pointerData.button == PointerEventData.InputButton.Left)
            {
                mouseIsDown = true;
                RectTransform rectTransform = sliceImage.rectTransform;
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pointerData.position, pointerData.pressEventCamera, out localPoint);
                Vector2 normalizedPoint = new Vector2(
                    (localPoint.x - rectTransform.rect.xMin) / rectTransform.rect.width,
                    (localPoint.y - rectTransform.rect.yMin) / rectTransform.rect.height
                );
                mousePressPosition = prevMousePos = normalizedPoint;
            }
        }

        private void OnPointerUp(BaseEventData data)
        {
            mouseIsDown = false;
        }

        private void OnPointerDrag(BaseEventData data)
        {
            PointerEventData pointerData = data as PointerEventData;
            if (mouseIsDown && inputMode != InputMode.ROI)
            {
                RectTransform rectTransform = sliceImage.rectTransform;
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pointerData.position, pointerData.pressEventCamera, out localPoint);
                Vector2 normalizedPoint = new Vector2(
                    (localPoint.x - rectTransform.rect.xMin) / rectTransform.rect.width,
                    (localPoint.y - rectTransform.rect.yMin) / rectTransform.rect.height
                );

                SlicingPlane[] planes = FindObjectsOfType<SlicingPlane>();
                if (selectedPlaneIndex >= 0 && selectedPlaneIndex < planes.Length)
                {
                    SlicingPlane plane = planes[selectedPlaneIndex];

                    if (inputMode == InputMode.Move)
                    {
                        Vector2 mouseOffset = normalizedPoint - prevMousePos;
                        if (Mathf.Abs(mouseOffset.y) > 0.00001f)
                            plane.transform.Translate(plane.transform.up * mouseOffset.y, Space.World);
                    }
                    else if (inputMode == InputMode.Inspect)
                    {
                        measurePoint = normalizedPoint;
                        float value = GetValueAtPosition(measurePoint, plane);
                        valueText.text = $"Value: {value}";
                    }
                    else if (inputMode == InputMode.Measure)
                    {
                        measurePoint = normalizedPoint;
                        Vector3 startDataPos = GetDataPosition(mousePressPosition, plane);
                        Vector3 endDataPos = GetDataPosition(measurePoint, plane);
                        float distance = Vector3.Distance(startDataPos, endDataPos);
                        valueText.text = $"Distance: {distance}";
                    }
                }

                prevMousePos = normalizedPoint;
            }
        }

        // 辅助方法
        private Vector3 GetWorldPosition(Vector2 relativeMousePosition, SlicingPlane slicingPlane)
        {
            Vector3 planePoint = new Vector3(0.5f - relativeMousePosition.x, 0.0f, relativeMousePosition.y - 0.5f) * 10.0f;
            return slicingPlane.transform.TransformPoint(planePoint);
        }

        private Vector3 GetDataPosition(Vector2 relativeMousePosition, SlicingPlane slicingPlane)
        {
            Vector3 worldSpacePosition = GetWorldPosition(relativeMousePosition, slicingPlane);
            Vector3 objSpacePoint = slicingPlane.targetObject.volumeContainerObject.transform.InverseTransformPoint(worldSpacePosition);
            Vector3 uvw = objSpacePoint + Vector3.one * 0.5f;
            VolumeDataset dataset = slicingPlane.targetObject.dataset;
            return new Vector3(uvw.x * dataset.scale.x, uvw.y * dataset.scale.y, uvw.z * dataset.scale.z);
        }

        private float GetValueAtPosition(Vector2 relativeMousePosition, SlicingPlane slicingPlane)
        {
            Vector3 worldSpacePosition = GetWorldPosition(relativeMousePosition, slicingPlane);
            Vector3 objSpacePoint = slicingPlane.targetObject.volumeContainerObject.transform.InverseTransformPoint(worldSpacePosition);
            VolumeDataset dataset = slicingPlane.targetObject.dataset;
            Vector3 uvw = objSpacePoint + Vector3.one * 0.5f;
            Vector3Int index = new Vector3Int((int)(uvw.x * dataset.dimX), (int)(uvw.y * dataset.dimY), (int)(uvw.z * dataset.dimZ));
            index.x = Mathf.Clamp(index.x, 0, dataset.dimX - 1);
            index.y = Mathf.Clamp(index.y, 0, dataset.dimY - 1);
            index.z = Mathf.Clamp(index.z, 0, dataset.dimZ - 1);
            return dataset.GetData(index.x, index.y, index.z);
        }
    }
}
