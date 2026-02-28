using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityVolumeRendering;

namespace UnityVolumeRendering.Segmentation
{
    /// <summary>
    /// Paint工具系统 - 支持圆形、椭圆、矩形、自由绘制
    /// 完全模仿3D Slicer的交互方式
    /// 用于血肿区域的勾画和绘制
    /// </summary>
    public class PaintToolSystem : MonoBehaviour
    {
        [Header("核心引用")]
        public SegmentationManager segmentationManager;
        public VolumeRenderedObject volumeObject;
        public Camera sliceCamera;
        
        [Header("Paint模式")]
        public PaintMode currentMode = PaintMode.Circle;
        
        [Header("画笔设置")]
        [Range(1, 50)]
        public int brushRadius = 5;
        [Range(0f, 1f)]
        public float brushStrength = 1.0f;
        public bool use3DBrush = false;
        
        [Header("形状工具")]
        public bool fillShape = true;        // 填充形状
        public bool showPreview = true;      // 显示预览
        public Color previewColor = new Color(1, 1, 0, 0.5f);
        
        [Header("当前切片")]
        public SliceOrientation sliceOrientation = SliceOrientation.Axial;
        public int currentSliceIndex = 0;
        
        [Header("血肿特定设置")]
        [Tooltip("血肿HU值范围下限")]
        [Range(-100, 200)]
        public float minHU = 30f;
        [Tooltip("血肿HU值范围上限")]
        [Range(-100, 200)]
        public float maxHU = 80f;
        public bool autoThreshold = true;
        
        // 绘制状态
        private bool isDrawing = false;
        private Vector2 startPoint;
        private Vector2 currentPoint;
        private List<Vector2> freehandPoints = new List<Vector2>();
        private List<Vector3Int> polygonPoints = new List<Vector3Int>();
        
        // 预览
        private GameObject previewObject;
        private LineRenderer previewLineRenderer;
        
        // 体数据尺寸
        private Vector3Int volumeDimensions;
        private VolumeDataset dataset;
        
        // 撤销系统
        private Stack<byte[]> undoStack = new Stack<byte[]>();
        private const int MAX_UNDO_STEPS = 20;
        
        void Start()
        {
            InitializeFromVolumeObject();
            CreatePreviewObject();
        }
        
        void InitializeFromVolumeObject()
        {
            if (volumeObject != null)
            {
                dataset = volumeObject.dataset;
                volumeDimensions = new Vector3Int(dataset.dimX, dataset.dimY, dataset.dimZ);
                Debug.Log($"PaintToolSystem初始化: 体积尺寸 {volumeDimensions.x}x{volumeDimensions.y}x{volumeDimensions.z}");
            }
            
            if (segmentationManager == null)
            {
                segmentationManager = FindObjectOfType<SegmentationManager>();
            }
        }
        
        void CreatePreviewObject()
        {
            previewObject = new GameObject("PaintPreview");
            previewLineRenderer = previewObject.AddComponent<LineRenderer>();
            previewLineRenderer.startWidth = 0.005f;
            previewLineRenderer.endWidth = 0.005f;
            previewLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            previewLineRenderer.startColor = previewColor;
            previewLineRenderer.endColor = previewColor;
            previewLineRenderer.enabled = false;
        }
        
        void Update()
        {
            HandleKeyboardShortcuts();
            HandleMouseInput();
            
            if (showPreview && isDrawing)
            {
                UpdatePreview();
            }
        }
        
        void HandleKeyboardShortcuts()
        {
            // 1-7: 切换模式
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(PaintMode.Circle);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(PaintMode.Ellipse);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(PaintMode.Rectangle);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchMode(PaintMode.Freehand);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SwitchMode(PaintMode.Polygon);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SwitchMode(PaintMode.Point);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SwitchMode(PaintMode.Eraser);
            
            // F: 切换填充模式
            if (Input.GetKeyDown(KeyCode.F))
            {
                fillShape = !fillShape;
                Debug.Log($"填充模式: {(fillShape ? "开启" : "关闭")}");
            }
            
            // [ ]: 调整画笔大小
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                brushRadius = Mathf.Max(1, brushRadius - 1);
                Debug.Log($"画笔大小: {brushRadius}");
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                brushRadius = Mathf.Min(50, brushRadius + 1);
                Debug.Log($"画笔大小: {brushRadius}");
            }
            
            // Q/E: 切换切片方向
            if (Input.GetKeyDown(KeyCode.Q))
            {
                CycleSliceOrientation();
            }
            
            // Z: 撤销
            if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftControl))
            {
                Undo();
            }
            
            // Y: 重做
            if (Input.GetKeyDown(KeyCode.Y) && Input.GetKey(KeyCode.LeftControl))
            {
                Redo();
            }
            
            // Delete: 清除当前分割
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                ClearCurrentSegment();
            }
        }
        
        void CycleSliceOrientation()
        {
            switch (sliceOrientation)
            {
                case SliceOrientation.Axial:
                    sliceOrientation = SliceOrientation.Coronal;
                    break;
                case SliceOrientation.Coronal:
                    sliceOrientation = SliceOrientation.Sagittal;
                    break;
                case SliceOrientation.Sagittal:
                    sliceOrientation = SliceOrientation.Axial;
                    break;
            }
            Debug.Log($"切片方向: {sliceOrientation}");
        }
        
        void HandleMouseInput()
        {
            // 避免在UI上操作
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            
            // 滚轮: 调整切片位置
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                int newSlice = currentSliceIndex + (scroll > 0 ? 1 : -1);
                currentSliceIndex = Mathf.Clamp(newSlice, 0, GetSliceCount() - 1);
            }
            
            // 左键按下：开始绘制
            if (Input.GetMouseButtonDown(0))
            {
                StartDrawing();
            }
            
            // 左键拖拽：更新
            if (Input.GetMouseButton(0) && isDrawing)
            {
                UpdateDrawing();
            }
            
            // 左键释放：完成
            if (Input.GetMouseButtonUp(0) && isDrawing)
            {
                FinishDrawing();
            }
            
            // 右键：取消
            if (Input.GetMouseButtonDown(1) && isDrawing)
            {
                CancelDrawing();
            }
        }
        
        void SwitchMode(PaintMode newMode)
        {
            currentMode = newMode;
            Debug.Log($"切换到 {currentMode} 模式");
            
            // 如果切换到Eraser模式，自动设置brushStrength为0
            if (currentMode == PaintMode.Eraser)
            {
                brushStrength = 0f;
            }
            else
            {
                brushStrength = 1.0f;
            }
        }
        
        void StartDrawing()
        {
            isDrawing = true;
            startPoint = Input.mousePosition;
            currentPoint = startPoint;
            
            // 保存撤销状态
            SaveUndoState();
            
            if (currentMode == PaintMode.Freehand)
            {
                freehandPoints.Clear();
                freehandPoints.Add(startPoint);
            }
            else if (currentMode == PaintMode.Polygon)
            {
                polygonPoints.Clear();
                AddPolygonPoint();
            }
            
            Debug.Log($"开始绘制 {currentMode}");
        }
        
        void UpdateDrawing()
        {
            currentPoint = Input.mousePosition;
            
            if (currentMode == PaintMode.Freehand)
            {
                if (Vector2.Distance(currentPoint, freehandPoints[freehandPoints.Count - 1]) > 2f)
                {
                    freehandPoints.Add(currentPoint);
                }
            }
        }
        
        void FinishDrawing()
        {
            isDrawing = false;
            previewLineRenderer.enabled = false;
            
            switch (currentMode)
            {
                case PaintMode.Circle:
                    DrawCircle(startPoint, currentPoint);
                    break;
                case PaintMode.Ellipse:
                    DrawEllipse(startPoint, currentPoint);
                    break;
                case PaintMode.Rectangle:
                    DrawRectangle(startPoint, currentPoint);
                    break;
                case PaintMode.Freehand:
                    DrawFreehand(freehandPoints);
                    break;
                case PaintMode.Point:
                    DrawPoint(startPoint);
                    break;
                case PaintMode.Eraser:
                    DrawEraser(startPoint);
                    break;
            }
        }
        
        void CancelDrawing()
        {
            isDrawing = false;
            freehandPoints.Clear();
            polygonPoints.Clear();
            previewLineRenderer.enabled = false;
            
            // 撤销刚才的保存
            if (undoStack.Count > 0)
            {
                undoStack.Pop();
            }
            
            Debug.Log("取消绘制");
        }
        
        void AddPolygonPoint()
        {
            Vector3Int voxelPos = ScreenToVoxel(Input.mousePosition);
            if (IsValidVoxel(voxelPos))
            {
                polygonPoints.Add(voxelPos);
                Debug.Log($"多边形点: {voxelPos}");
            }
        }
        
        // ========== 绘制函数 ==========
        
        void DrawCircle(Vector2 center, Vector2 edge)
        {
            if (segmentationManager?.activeSegment == null) return;
            
            float radiusPixels = Vector2.Distance(center, edge);
            Vector3Int centerVoxel = ScreenToVoxel(center);
            
            if (use3DBrush)
            {
                DrawSphere3D(centerVoxel, radiusPixels);
            }
            else
            {
                DrawCircle2D(centerVoxel, radiusPixels);
            }
            
            segmentationManager.activeSegment.UpdateTexture();
            Debug.Log($"绘制圆形完成，半径: {radiusPixels}");
        }
        
        void DrawCircle2D(Vector3Int center, float radiusPixels)
        {
            var segment = segmentationManager.activeSegment;
            int radius = Mathf.RoundToInt(radiusPixels / GetPixelToVoxelScale());
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            if (fillShape)
            {
                // 填充圆
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= radius * radius)
                        {
                            Vector3Int pos = GetVoxelOnSlice(center, x, y);
                            if (IsValidVoxel(pos))
                            {
                                segment.SetVoxel(pos.x, pos.y, pos.z, value);
                            }
                        }
                    }
                }
            }
            else
            {
                // 只绘制边界
                DrawCircleOutline(center, radius, segment, value);
            }
        }
        
        void DrawSphere3D(Vector3Int center, float radiusPixels)
        {
            var segment = segmentationManager.activeSegment;
            int radius = Mathf.RoundToInt(radiusPixels / GetPixelToVoxelScale());
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            for (int z = -radius; z <= radius; z++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y + z * z <= radius * radius)
                        {
                            Vector3Int pos = new Vector3Int(center.x + x, center.y + y, center.z + z);
                            if (IsValidVoxel(pos))
                            {
                                segment.SetVoxel(pos.x, pos.y, pos.z, value);
                            }
                        }
                    }
                }
            }
        }
        
        void DrawEllipse(Vector2 center, Vector2 corner)
        {
            if (segmentationManager?.activeSegment == null) return;
            
            Vector2 size = corner - center;
            float radiusX = Mathf.Abs(size.x);
            float radiusY = Mathf.Abs(size.y);
            
            Vector3Int centerVoxel = ScreenToVoxel(center);
            float scale = GetPixelToVoxelScale();
            int rxVoxels = Mathf.RoundToInt(radiusX / scale);
            int ryVoxels = Mathf.RoundToInt(radiusY / scale);
            
            DrawEllipse2D(centerVoxel, rxVoxels, ryVoxels);
            segmentationManager.activeSegment.UpdateTexture();
            
            Debug.Log($"绘制椭圆完成，半径: {rxVoxels} x {ryVoxels}");
        }
        
        void DrawEllipse2D(Vector3Int center, int radiusX, int radiusY)
        {
            var segment = segmentationManager.activeSegment;
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            if (fillShape)
            {
                for (int y = -radiusY; y <= radiusY; y++)
                {
                    for (int x = -radiusX; x <= radiusX; x++)
                    {
                        float norm = (float)(x * x) / (radiusX * radiusX) + 
                                    (float)(y * y) / (radiusY * radiusY);
                        
                        if (norm <= 1.0f)
                        {
                            Vector3Int pos = GetVoxelOnSlice(center, x, y);
                            if (IsValidVoxel(pos))
                            {
                                segment.SetVoxel(pos.x, pos.y, pos.z, value);
                            }
                        }
                    }
                }
            }
            else
            {
                // 椭圆轮廓
                int steps = Mathf.Max(radiusX, radiusY) * 8;
                for (int i = 0; i < steps; i++)
                {
                    float angle = 2 * Mathf.PI * i / steps;
                    int x = Mathf.RoundToInt(radiusX * Mathf.Cos(angle));
                    int y = Mathf.RoundToInt(radiusY * Mathf.Sin(angle));
                    
                    Vector3Int pos = GetVoxelOnSlice(center, x, y);
                    if (IsValidVoxel(pos))
                    {
                        segment.SetVoxel(pos.x, pos.y, pos.z, value);
                    }
                }
            }
        }
        
        void DrawRectangle(Vector2 corner1, Vector2 corner2)
        {
            if (segmentationManager?.activeSegment == null) return;
            
            Vector3Int v1 = ScreenToVoxel(corner1);
            Vector3Int v2 = ScreenToVoxel(corner2);
            
            DrawRectangle2D(v1, v2);
            segmentationManager.activeSegment.UpdateTexture();
            
            Debug.Log($"绘制矩形完成");
        }
        
        void DrawRectangle2D(Vector3Int corner1, Vector3Int corner2)
        {
            var segment = segmentationManager.activeSegment;
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            int minX = Mathf.Min(corner1.x, corner2.x);
            int maxX = Mathf.Max(corner1.x, corner2.x);
            int minY = Mathf.Min(corner1.y, corner2.y);
            int maxY = Mathf.Max(corner1.y, corner2.y);
            int z = corner1.z;
            
            if (fillShape)
            {
                // 填充
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        segment.SetVoxel(x, y, z, value);
                    }
                }
            }
            else
            {
                // 边框
                DrawHorizontalLine(minX, maxX, minY, z, segment, value);
                DrawHorizontalLine(minX, maxX, maxY, z, segment, value);
                DrawVerticalLine(minY, maxY, minX, z, segment, value);
                DrawVerticalLine(minY, maxY, maxX, z, segment, value);
            }
        }
        
        void DrawFreehand(List<Vector2> screenPoints)
        {
            if (segmentationManager?.activeSegment == null) return;
            if (screenPoints.Count < 2) return;
            
            List<Vector3Int> voxelPoints = new List<Vector3Int>();
            foreach (var sp in screenPoints)
            {
                voxelPoints.Add(ScreenToVoxel(sp));
            }
            
            // 绘制路径
            for (int i = 0; i < voxelPoints.Count - 1; i++)
            {
                DrawLine2D(voxelPoints[i], voxelPoints[i + 1]);
            }
            
            // 可选：闭合并填充
            if (fillShape && voxelPoints.Count > 3)
            {
                FillPolygon2D(voxelPoints);
            }
            
            segmentationManager.activeSegment.UpdateTexture();
            Debug.Log($"绘制自由曲线完成，点数: {voxelPoints.Count}");
        }
        
        void DrawPoint(Vector2 screenPoint)
        {
            if (segmentationManager?.activeSegment == null) return;
            
            Vector3Int voxelPos = ScreenToVoxel(screenPoint);
            if (IsValidVoxel(voxelPos))
            {
                byte value = (byte)(brushStrength > 0 ? 255 : 0);
                segmentationManager.activeSegment.SetVoxel(voxelPos.x, voxelPos.y, voxelPos.z, value);
                segmentationManager.activeSegment.UpdateTexture();
            }
        }
        
        void DrawEraser(Vector2 screenPoint)
        {
            if (segmentationManager?.activeSegment == null) return;
            
            // 临时设置brushStrength为0来擦除
            float originalStrength = brushStrength;
            brushStrength = 0f;
            
            DrawCircle(screenPoint, screenPoint + new Vector2(brushRadius, 0));
            
            brushStrength = originalStrength;
        }
        
        void DrawLine2D(Vector3Int p1, Vector3Int p2)
        {
            var segment = segmentationManager.activeSegment;
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            // Bresenham直线算法
            int dx = Mathf.Abs(p2.x - p1.x);
            int dy = Mathf.Abs(p2.y - p1.y);
            int sx = p1.x < p2.x ? 1 : -1;
            int sy = p1.y < p2.y ? 1 : -1;
            int err = dx - dy;
            
            int x = p1.x;
            int y = p1.y;
            
            while (true)
            {
                // 绘制点及其周围（画笔大小）
                for (int dy2 = -brushRadius; dy2 <= brushRadius; dy2++)
                {
                    for (int dx2 = -brushRadius; dx2 <= brushRadius; dx2++)
                    {
                        if (dx2 * dx2 + dy2 * dy2 <= brushRadius * brushRadius)
                        {
                            Vector3Int pos = GetVoxelOnSlice(new Vector3Int(x, y, p1.z), dx2, dy2);
                            if (IsValidVoxel(pos))
                            {
                                segment.SetVoxel(pos.x, pos.y, pos.z, value);
                            }
                        }
                    }
                }
                
                if (x == p2.x && y == p2.y) break;
                
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
        
        void FillPolygon2D(List<Vector3Int> points)
        {
            var segment = segmentationManager.activeSegment;
            byte value = (byte)(brushStrength > 0 ? 255 : 0);
            
            if (points.Count < 3) return;
            
            // 扫描线填充算法
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            
            for (int y = minY; y <= maxY; y++)
            {
                List<int> intersections = new List<int>();
                
                for (int i = 0; i < points.Count; i++)
                {
                    int j = (i + 1) % points.Count;
                    Vector3Int p1 = points[i];
                    Vector3Int p2 = points[j];
                    
                    if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    {
                        float t = (float)(y - p1.y) / (p2.y - p1.y);
                        int x = Mathf.RoundToInt(p1.x + t * (p2.x - p1.x));
                        intersections.Add(x);
                    }
                }
                
                intersections.Sort();
                
                for (int i = 0; i < intersections.Count - 1; i += 2)
                {
                    for (int x = intersections[i]; x <= intersections[i + 1]; x++)
                    {
                        segment.SetVoxel(x, y, points[0].z, value);
                    }
                }
            }
        }
        
        // ========== 辅助函数 ==========
        
        void DrawCircleOutline(Vector3Int center, int radius, Segment segment, byte value)
        {
            // Bresenham圆算法
            int x = 0;
            int y = radius;
            int d = 3 - 2 * radius;
            
            void PlotPoints(int px, int py)
            {
                Vector3Int[] positions = new Vector3Int[]
                {
                    GetVoxelOnSlice(center, px, py),
                    GetVoxelOnSlice(center, -px, py),
                    GetVoxelOnSlice(center, px, -py),
                    GetVoxelOnSlice(center, -px, -py),
                    GetVoxelOnSlice(center, py, px),
                    GetVoxelOnSlice(center, -py, px),
                    GetVoxelOnSlice(center, py, -px),
                    GetVoxelOnSlice(center, -py, -px)
                };
                
                foreach (var pos in positions)
                {
                    if (IsValidVoxel(pos))
                    {
                        segment.SetVoxel(pos.x, pos.y, pos.z, value);
                    }
                }
            }
            
            PlotPoints(0, 0);
            
            while (x < y)
            {
                if (d < 0)
                {
                    d = d + 4 * x + 6;
                }
                else
                {
                    d = d + 4 * (x - y) + 10;
                    y--;
                }
                x++;
                PlotPoints(x, y);
            }
        }
        
        void DrawHorizontalLine(int x1, int x2, int y, int z, Segment segment, byte value)
        {
            for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
            {
                segment.SetVoxel(x, y, z, value);
            }
        }
        
        void DrawVerticalLine(int y1, int y2, int x, int z, Segment segment, byte value)
        {
            for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
            {
                segment.SetVoxel(x, y, z, value);
            }
        }
        
        Vector3Int ScreenToVoxel(Vector2 screenPos)
        {
            if (volumeObject == null || dataset == null)
                return Vector3Int.zero;
            
            UnityEngine.Ray ray = Camera.main.ScreenPointToRay(screenPos);
            UnityEngine.RaycastHit hit;

            if (Physics.Raycast(ray.origin,ray.direction, out hit))
            {
                Vector3 localPoint = volumeObject.transform.InverseTransformPoint(hit.point);
                
                return new Vector3Int(
                    Mathf.RoundToInt((localPoint.x + 0.5f) * volumeDimensions.x),
                    Mathf.RoundToInt((localPoint.y + 0.5f) * volumeDimensions.y),
                    Mathf.RoundToInt((localPoint.z + 0.5f) * volumeDimensions.z)
                );
            }
            
            return Vector3Int.zero;
        }
        
        Vector3Int GetVoxelOnSlice(Vector3Int center, int offsetX, int offsetY)
        {
            switch (sliceOrientation)
            {
                case SliceOrientation.Axial:
                    return new Vector3Int(center.x + offsetX, center.y + offsetY, center.z);
                case SliceOrientation.Coronal:
                    return new Vector3Int(center.x + offsetX, center.y, center.z + offsetY);
                case SliceOrientation.Sagittal:
                    return new Vector3Int(center.x, center.y + offsetX, center.z + offsetY);
            }
            return center;
        }
        
        bool IsValidVoxel(Vector3Int pos)
        {
            return pos.x >= 0 && pos.x < volumeDimensions.x &&
                   pos.y >= 0 && pos.y < volumeDimensions.y &&
                   pos.z >= 0 && pos.z < volumeDimensions.z;
        }
        
        float GetPixelToVoxelScale()
        {
            // 简化估算：假设屏幕高度对应体积高度
            return Screen.height / (float)volumeDimensions.y;
        }
        
        int GetSliceCount()
        {
            switch (sliceOrientation)
            {
                case SliceOrientation.Axial:
                    return volumeDimensions.z;
                case SliceOrientation.Coronal:
                    return volumeDimensions.y;
                case SliceOrientation.Sagittal:
                    return volumeDimensions.x;
            }
            return 1;
        }
        
        void UpdatePreview()
        {
            if (!showPreview || segmentationManager?.activeSegment == null)
                return;
            
            previewLineRenderer.enabled = true;
            
            // 根据绘制模式更新预览
            switch (currentMode)
            {
                case PaintMode.Circle:
                    UpdateCirclePreview();
                    break;
                case PaintMode.Ellipse:
                    UpdateEllipsePreview();
                    break;
                case PaintMode.Rectangle:
                    UpdateRectanglePreview();
                    break;
                case PaintMode.Freehand:
                    UpdateFreehandPreview();
                    break;
            }
        }
        
        void UpdateCirclePreview()
        {
            Vector3Int center = ScreenToVoxel(startPoint);
            float radius = Vector2.Distance(startPoint, currentPoint) / GetPixelToVoxelScale();
            
            int segments = 36;
            Vector3[] points = new Vector3[segments + 1];
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2 * Mathf.PI * i / segments;
                Vector3 worldPos = VoxelToWorldPosition(new Vector3Int(
                    center.x + Mathf.RoundToInt(radius * Mathf.Cos(angle)),
                    center.y + Mathf.RoundToInt(radius * Mathf.Sin(angle)),
                    center.z
                ));
                points[i] = worldPos;
            }
            
            previewLineRenderer.positionCount = points.Length;
            previewLineRenderer.SetPositions(points);
        }
        
        void UpdateEllipsePreview()
        {
            Vector3Int center = ScreenToVoxel(startPoint);
            Vector2 size = currentPoint - startPoint;
            float radiusX = Mathf.Abs(size.x) / GetPixelToVoxelScale();
            float radiusY = Mathf.Abs(size.y) / GetPixelToVoxelScale();
            
            int segments = 36;
            Vector3[] points = new Vector3[segments + 1];
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2 * Mathf.PI * i / segments;
                Vector3 worldPos = VoxelToWorldPosition(new Vector3Int(
                    center.x + Mathf.RoundToInt(radiusX * Mathf.Cos(angle)),
                    center.y + Mathf.RoundToInt(radiusY * Mathf.Sin(angle)),
                    center.z
                ));
                points[i] = worldPos;
            }
            
            previewLineRenderer.positionCount = points.Length;
            previewLineRenderer.SetPositions(points);
        }
        
        void UpdateRectanglePreview()
        {
            Vector3Int v1 = ScreenToVoxel(startPoint);
            Vector3Int v2 = ScreenToVoxel(currentPoint);
            
            Vector3[] points = new Vector3[5];
            points[0] = VoxelToWorldPosition(v1);
            points[1] = VoxelToWorldPosition(new Vector3Int(v2.x, v1.y, v1.z));
            points[2] = VoxelToWorldPosition(v2);
            points[3] = VoxelToWorldPosition(new Vector3Int(v1.x, v2.y, v1.z));
            points[4] = points[0];
            
            previewLineRenderer.positionCount = points.Length;
            previewLineRenderer.SetPositions(points);
        }
        
        void UpdateFreehandPreview()
        {
            if (freehandPoints.Count < 2) return;
            
            Vector3[] points = new Vector3[freehandPoints.Count];
            for (int i = 0; i < freehandPoints.Count; i++)
            {
                Vector3Int voxel = ScreenToVoxel(freehandPoints[i]);
                points[i] = VoxelToWorldPosition(voxel);
            }
            
            previewLineRenderer.positionCount = points.Length;
            previewLineRenderer.SetPositions(points);
        }
        
        Vector3 VoxelToWorldPosition(Vector3Int voxel)
        {
            if (volumeObject == null) return Vector3.zero;
            
            Vector3 localPos = new Vector3(
                (float)voxel.x / volumeDimensions.x - 0.5f,
                (float)voxel.y / volumeDimensions.y - 0.5f,
                (float)voxel.z / volumeDimensions.z - 0.5f
            );
            
            return volumeObject.transform.TransformPoint(localPos);
        }
        
        // ========== 撤销系统 ==========
        
        void SaveUndoState()
        {
            if (segmentationManager?.activeSegment == null) return;
            
            byte[] data = segmentationManager.activeSegment.GetDataCopy();
            undoStack.Push(data);
            
            // 限制撤销步数
            if (undoStack.Count > MAX_UNDO_STEPS)
            {
                var temp = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 0; i < MAX_UNDO_STEPS; i++)
                {
                    undoStack.Push(temp[i]);
                }
            }
        }
        
        public void Undo()
        {
            if (undoStack.Count == 0 || segmentationManager?.activeSegment == null)
                return;
            
            byte[] data = undoStack.Pop();
            segmentationManager.activeSegment.SetData(data);
            Debug.Log("撤销成功");
        }
        
        public void Redo()
        {
            // 重做功能需要更复杂的实现（需要redoStack）
            Debug.Log("重做功能暂未实现");
        }
        
        public void ClearCurrentSegment()
        {
            if (segmentationManager?.activeSegment != null)
            {
                SaveUndoState();
                segmentationManager.activeSegment.Clear();
                Debug.Log("已清除当前分割");
            }
        }
        
        /// <summary>
        /// 设置血肿阈值（用于自动阈值分割）
        /// </summary>
        public void SetHematomaThreshold(float min, float max)
        {
            minHU = min;
            maxHU = max;
            autoThreshold = false;
            Debug.Log($"血肿阈值设置: {minHU} - {maxHU} HU");
        }
        
        /// <summary>
        /// 启用自动阈值检测
        /// </summary>
        public void EnableAutoThreshold()
        {
            autoThreshold = true;
            Debug.Log("自动阈值检测已启用");
        }
    }
}
