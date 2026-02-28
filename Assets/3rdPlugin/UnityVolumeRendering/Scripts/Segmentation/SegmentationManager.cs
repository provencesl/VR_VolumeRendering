using UnityEngine;
using UnityVolumeRendering;
using System.Collections.Generic;

namespace UnityVolumeRendering.Segmentation
{
    /// <summary>
    /// 分割管理器 - 集成到UnityVolumeRendering
    /// 管理所有分割对象和编辑器效果
    /// 用于血肿区域的分割和绘制
    /// </summary>
    public class SegmentationManager : MonoBehaviour
    {
        [Header("UnityVolumeRendering集成")]
        public VolumeRenderedObject volumeObject;
        private VolumeDataset volumeDataset;
        
        [Header("分割数据")]
        public List<Segment> segments = new List<Segment>();
        public Segment activeSegment;
        
        [Header("显示设置")]
        public Material segmentOverlayMaterial;
        public bool showSegmentOverlay = true;
        [Range(0f, 1f)]
        public float segmentOpacity = 0.6f;
        
        [Header("血肿分割设置")]
        [Tooltip("启用自动HU阈值检测")]
        public bool autoThreshold = false;
        [Tooltip("血肿最小HU值")]
        [Range(-100, 200)]
        public float minHU = 30f;
        [Tooltip("血肿最大HU值")]
        [Range(-100, 200)]
        public float maxHU = 80f;
        [Tooltip("区域生长相似度阈值")]
        [Range(1f, 50f)]
        public float similarityTolerance = 15f;
        
        // 种子点
        private List<Vector3Int> seedPoints = new List<Vector3Int>();
        private List<GameObject> seedMarkers = new List<GameObject>();
        
        // 公开种子点数量供编辑器使用
        public int SeedPointCount => seedPoints.Count;
        
        // 撤销/重做
        private Stack<SegmentSnapshot> undoStack = new Stack<SegmentSnapshot>();
        private Stack<SegmentSnapshot> redoStack = new Stack<SegmentSnapshot>();
        private const int MAX_UNDO_STEPS = 20;
        
        // 事件
        public delegate void OnSegmentationChanged();
        public event OnSegmentationChanged SegmentationUpdated;
        public delegate void OnSegmentAdded(Segment segment);
        public event OnSegmentAdded SegmentAdded;
        public event System.Action<Vector3Int> OnSeedPointAdded;
        
        void Start()
        {
            InitializeFromVolumeObject();
        }
        
        void Update()
        {
            HandleInput();
        }
        
        /// <summary>
        /// 处理输入
        /// </summary>
        void HandleInput()
        {
            // Ctrl+Z: 撤销
            if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftControl))
            {
                Undo();
            }
            
            // Ctrl+Y: 重做
            if (Input.GetKeyDown(KeyCode.Y) && Input.GetKey(KeyCode.LeftControl))
            {
                Redo();
            }
            
            // Ctrl+S: 保存分割
            if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl))
            {
                SaveSegmentation();
            }
            
            // Ctrl+N: 新建分割
            if (Input.GetKeyDown(KeyCode.N) && Input.GetKey(KeyCode.LeftControl))
            {
                CreateNewSegment();
            }
            
            // Delete: 删除选中分割
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                DeleteActiveSegment();
            }
        }
        
        /// <summary>
        /// 从UnityVolumeRendering的VolumeRenderedObject初始化
        /// </summary>
        public void InitializeFromVolumeObject()
        {
         
            
            // 初始化体数据信息
            volumeObject = GameObject.FindObjectOfType<VolumeRenderedObject>();
            volumeDataset = volumeObject.dataset;
            Debug.Log($"分割管理 volumeDataset: {volumeDataset}");
            Debug.Log($"分割管理器初始化完成");
            Debug.Log($"体数据尺寸: {volumeDataset.dimX} x {volumeDataset.dimY} x {volumeDataset.dimZ}");
            
            CreateNewSegment("血肿区域");
        }
        
        /// <summary>
        /// 创建新的分割对象
        /// </summary>
        public Segment CreateNewSegment(string name = null)
        {
            Segment segment = new Segment();
            segment.name = name ?? $"Segment_{segments.Count + 1}";
            segment.color = GetNextSegmentColor();
            segment.Initialize(volumeDataset.dimX, volumeDataset.dimY, volumeDataset.dimZ);
            
            segments.Add(segment);
            activeSegment = segment;
            
            SegmentAdded?.Invoke(segment);
            Debug.Log($"创建新分割: {segment.name}");
            
            return segment;
        }
        
        /// <summary>
        /// 获取下一个分割的颜色
        /// </summary>
        Color GetNextSegmentColor()
        {
            Color[] colors = new Color[]
            {
                new Color(1f, 0f, 0f, 0.6f),    // 红色
                new Color(0f, 1f, 0f, 0.6f),    // 绿色
                new Color(0f, 0f, 1f, 0.6f),    // 蓝色
                new Color(1f, 1f, 0f, 0.6f),    // 黄色
                new Color(1f, 0f, 1f, 0.6f),    // 品红
                new Color(0f, 1f, 1f, 0.6f),    // 青色
                new Color(1f, 0.5f, 0f, 0.6f),  // 橙色
                new Color(0.5f, 0f, 1f, 0.6f)   // 紫色
            };
            
            return colors[segments.Count % colors.Length];
        }
        
        /// <summary>
        /// 设置活动分割
        /// </summary>
        public void SetActiveSegment(Segment segment)
        {
            if (segments.Contains(segment))
            {
                activeSegment = segment;
                Debug.Log($"切换到分割: {segment.name}");
            }
        }
        
        /// <summary>
        /// 删除分割
        /// </summary>
        public void DeleteSegment(Segment segment)
        {
            if (segments.Contains(segment))
            {
                segments.Remove(segment);
                
                if (activeSegment == segment)
                {
                    activeSegment = segments.Count > 0 ? segments[segments.Count - 1] : null;
                }
                
                Debug.Log($"删除分割: {segment.name}");
            }
        }
        
        /// <summary>
        /// 删除活动分割
        /// </summary>
        public void DeleteActiveSegment()
        {
            if (activeSegment != null)
            {
                DeleteSegment(activeSegment);
            }
        }
        
        /// <summary>
        /// 添加种子点（从屏幕坐标）
        /// </summary>
        /// <param name="screenPosition">屏幕坐标位置</param>
        /// <returns>
        /// 返回添加结果：
        /// - true: 成功添加种子点
        /// - false: 添加失败（射线未击中体积、坐标无效等）
        /// </returns>
        public bool AddSeedPoint(Vector2 screenPosition)
        {
            // 1. 验证必要组件
            if (volumeObject == null || volumeDataset == null)
            {
                Debug.LogWarning("无法添加种子点: VolumeObject或VolumeDataset为空");
                return false;
            }
            
            // 2. 验证相机
            if (Camera.main == null)
            {
                Debug.LogError("无法添加种子点: 主相机未找到");
                return false;
            }
            
            // 3. 从屏幕坐标创建射线
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            
            // 4. 获取体积包围盒
            Renderer renderer = volumeObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("无法添加种子点: VolumeObject缺少Renderer组件");
                return false;
            }
            
            Bounds bounds = renderer.bounds;
            float enter;
            
            // 5. 检测射线与包围盒是否相交
            if (!bounds.IntersectRay(ray, out enter))
            {
                Debug.LogWarning($"射线未击中体积包围盒 (屏幕坐标: {screenPosition})");
                return false;
            }
            
            // 6. 计算射线击中点的世界坐标
            Vector3 hitPoint = ray.GetPoint(enter);
            
            // 7. 转换到体积局部坐标系（归一化坐标，范围[-0.5, 0.5]）
            Vector3 localPoint = volumeObject.transform.InverseTransformPoint(hitPoint);
            
            // 8. 转换到体素坐标（整数索引）
            Vector3Int voxelPos = new Vector3Int(
                Mathf.RoundToInt((localPoint.x + 0.5f) * volumeDataset.dimX),
                Mathf.RoundToInt((localPoint.y + 0.5f) * volumeDataset.dimY),
                Mathf.RoundToInt((localPoint.z + 0.5f) * volumeDataset.dimZ)
            );
            
            // 9. 验证体素坐标是否在有效范围内
            if (!IsValidVoxel(voxelPos))
            {
                Debug.LogWarning($"体素坐标超出范围: {voxelPos}, 数据集尺寸: ({volumeDataset.dimX}, {volumeDataset.dimY}, {volumeDataset.dimZ})");
                return false;
            }
            
            // 10. 检查是否已存在相同种子点（避免重复添加）
            if (seedPoints.Contains(voxelPos))
            {
                Debug.LogWarning($"种子点已存在: {voxelPos}");
                return false;
            }
            
            // 11. 添加种子点到列表
            seedPoints.Add(voxelPos);
            
            // 12. 创建可视化标记
            bool markerCreated = CreateSeedMarker(voxelPos);
            if (!markerCreated)
            {
                Debug.LogWarning($"种子点标记创建失败: {voxelPos}");
                // 即使标记创建失败，种子点仍然有效，所以返回true
            }
            
            // 13. 记录成功信息
            Debug.Log($"✓ 成功添加种子点 #{seedPoints.Count}: 体素坐标={voxelPos}, 世界坐标={hitPoint:F3}, 局部坐标={localPoint:F3}");
            
            // 14. 触发种子点添加事件
            OnSeedPointAdded?.Invoke(voxelPos);
            
            return true;
        }
        
        /// <summary>
        /// 直接从体素坐标添加种子点（用于2D切片视图）
        /// </summary>
        /// <param name="voxelPos">体素坐标</param>
        /// <returns>是否成功添加</returns>
        public bool AddSeedPointFromVoxel(Vector3Int voxelPos)
        {
            // 继续查看数据集

            // 1. 验证数据集
            if (volumeDataset == null)
            {
                Debug.LogWarning("无法添加种子点: VolumeDataset为空");
                return false;
            }
            
            // 2. 验证体素坐标是否在有效范围内
            if (!IsValidVoxel(voxelPos))
            {
                Debug.LogWarning($"体素坐标超出范围: {voxelPos}, 数据集尺寸: ({volumeDataset.dimX}, {volumeDataset.dimY}, {volumeDataset.dimZ})");
                return false;
            }
            
            // 3. 检查是否已存在相同种子点（避免重复添加）
            if (seedPoints.Contains(voxelPos))
            {
                Debug.LogWarning($"种子点已存在: {voxelPos}");
                return false;
            }
            
            // 4. 添加种子点到列表
            seedPoints.Add(voxelPos);
            
            // 5. 创建可视化标记
            bool markerCreated = CreateSeedMarker(voxelPos);
            if (!markerCreated)
            {
                Debug.LogWarning($"种子点标记创建失败: {voxelPos}");
            }
            
            // 6. 记录成功信息
            Debug.Log($"✓ 成功添加种子点 #{seedPoints.Count}: 体素坐标={voxelPos}");
            
            // 7. 触发种子点添加事件
            OnSeedPointAdded?.Invoke(voxelPos);
            
            return true;
        }
        
        /// <summary>
        /// 从2D切片坐标添加种子点
        /// </summary>
        /// <param name="sliceX">切片X坐标</param>
        /// <param name="sliceY">切片Y坐标</param>
        /// <param name="sliceIndex">切片索引（Z轴）</param>
        /// <returns>是否成功添加</returns>
        public bool AddSeedPointFromSlice(int sliceX, int sliceY, int sliceIndex)
        {
            Vector3Int voxelPos = new Vector3Int(sliceX, sliceY, sliceIndex);
            return AddSeedPointFromVoxel(voxelPos);
        }
        
        /// <summary>
        /// 创建种子点可视化标记
        /// </summary>
        /// <param name="voxelPos">体素坐标</param>
        /// <returns>是否成功创建标记</returns>
        private bool CreateSeedMarker(Vector3Int voxelPos)
        {
            try
            {
                if (volumeObject == null || volumeDataset == null) return false;
                
                // 将体素坐标转换回世界坐标
                Vector3 normalizedPos = new Vector3(
                    voxelPos.x / (float)volumeDataset.dimX - 0.5f,
                    voxelPos.y / (float)volumeDataset.dimY - 0.5f,
                    voxelPos.z / (float)volumeDataset.dimZ - 0.5f
                );
                Vector3 worldPos = volumeObject.transform.TransformPoint(normalizedPos);
                
                // 创建球体标记
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"SeedPoint_{seedPoints.Count}";
                marker.transform.position = worldPos;
                marker.transform.localScale = Vector3.one * 0.01f; // 小球标记
                
                // 设置材质（红色高亮）
                Renderer markerRenderer = marker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.material = new Material(Shader.Find("Standard"));
                    markerRenderer.material.color = Color.red;
                    markerRenderer.material.SetFloat("_Metallic", 0.5f);
                    markerRenderer.material.SetFloat("_Glossiness", 0.8f);
                }
                
                // 移除碰撞体（避免干扰射线检测）
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    Destroy(markerCollider);
                }
                
                // 设置父对象
                marker.transform.SetParent(volumeObject.transform);
                
                // 保存到标记列表（便于后续管理）
                seedMarkers.Add(marker);
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"创建种子点标记失败: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 清除所有种子点
        /// </summary>
        public void ClearSeedPoints()
        {
            seedPoints.Clear();
            
            // 销毁所有可视化标记
            foreach (var marker in seedMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            seedMarkers.Clear();
            
            Debug.Log("已清除所有种子点");
        }
        
        /// <summary>
        /// 移除最后一个种子点
        /// </summary>
        public bool RemoveLastSeedPoint()
        {
            if (seedPoints.Count == 0)
            {
                Debug.LogWarning("没有种子点可移除");
                return false;
            }
            
            int lastIndex = seedPoints.Count - 1;
            seedPoints.RemoveAt(lastIndex);
            
            // 移除对应的标记
            if (lastIndex < seedMarkers.Count && seedMarkers[lastIndex] != null)
            {
                Destroy(seedMarkers[lastIndex]);
                seedMarkers.RemoveAt(lastIndex);
            }
            
            Debug.Log($"已移除最后一个种子点，剩余: {seedPoints.Count}");
            return true;
        }
        
        /// <summary>
        /// 执行区域生长（血肿分割核心算法）
        /// </summary>
        public void ExecuteGrowFromSeeds(float intensityTolerance, int maxIterations = 100000)
        {
            if (seedPoints.Count == 0)
            {
                Debug.LogWarning("请先添加种子点！");
                return;
            }
            
            if (activeSegment == null)
            {
                CreateNewSegment();
            }
            
            SaveUndoState();
            
            // 获取种子点的平均强度和标准差
            float avgIntensity = 0f;
            List<float> intensities = new List<float>();
            
            foreach (var seed in seedPoints)
            {
                float value = GetVoxelValue(seed);
                intensities.Add(value);
                avgIntensity += value;
            }
            avgIntensity /= seedPoints.Count;
            
            // 计算标准差
            float variance = 0f;
            foreach (float intensity in intensities)
            {
                variance += (intensity - avgIntensity) * (intensity - avgIntensity);
            }
            variance /= seedPoints.Count;
            float stdDev = Mathf.Sqrt(variance);
            
            // 使用标准差作为容差基础
            float effectiveTolerance = intensityTolerance > 0 ? intensityTolerance : stdDev * 2f;
            
            Debug.Log($"区域生长参数: 平均值={avgIntensity:F2}, 标准差={stdDev:F2}, 容差={effectiveTolerance:F2}");
            
            // BFS区域生长
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            
            foreach (var seed in seedPoints)
            {
                queue.Enqueue(seed);
                visited.Add(seed);
                activeSegment.SetVoxel(seed.x, seed.y, seed.z, 255);
            }
            
            int iterations = 0;
            int filledVoxels = seedPoints.Count;
            
            while (queue.Count > 0 && iterations < maxIterations)
            {
                Vector3Int current = queue.Dequeue();
                float currentValue = GetVoxelValue(current);
                
                // 6邻域
                Vector3Int[] neighbors = Get6Neighbors(current);
                
                foreach (var neighbor in neighbors)
                {
                    if (!IsValidVoxel(neighbor)) continue;
                    if (visited.Contains(neighbor)) continue;
                    
                    float neighborValue = GetVoxelValue(neighbor);
                    float diff = Mathf.Abs(neighborValue - avgIntensity);
                    
                    // 血肿检查：如果启用自动阈值，检查HU范围
                    if (autoThreshold)
                    {
                        float huValue = RawToHU(neighborValue);
                        bool inRange = huValue >= minHU && huValue <= maxHU;
                        bool similar = diff <= effectiveTolerance;
                        
                        if (inRange && similar)
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                            activeSegment.SetVoxel(neighbor.x, neighbor.y, neighbor.z, 255);
                            filledVoxels++;
                        }
                    }
                    else
                    {
                        if (diff <= effectiveTolerance)
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                            activeSegment.SetVoxel(neighbor.x, neighbor.y, neighbor.z, 255);
                            filledVoxels++;
                        }
                    }
                }
                
                iterations++;
            }
            
            activeSegment.UpdateTexture();
            activeSegment.voxelCount = filledVoxels;
            
            Debug.Log($"区域生长完成: {filledVoxels} 个体素, 迭代次数: {iterations}");
            
            ClearSeedPoints();
            SegmentationUpdated?.Invoke();
        }
        
        /// <summary>
        /// 血肿阈值分割
        /// </summary>
        public void ExecuteHematomaThreshold()
        {
            if (activeSegment == null)
            {
                CreateNewSegment();
            }
            
            SaveUndoState();
            
            int filledVoxels = 0;
            
            for (int z = 0; z < volumeDataset.dimZ; z++)
            {
                for (int y = 0; y < volumeDataset.dimY; y++)
                {
                    for (int x = 0; x < volumeDataset.dimX; x++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);
                        float value = GetVoxelValue(pos);
                        float huValue = RawToHU(value);
                        
                        if (huValue >= minHU && huValue <= maxHU)
                        {
                            activeSegment.SetVoxel(x, y, z, 255);
                            filledVoxels++;
                        }
                    }
                }
            }
            
            activeSegment.UpdateTexture();
            activeSegment.voxelCount = filledVoxels;
            
            Debug.Log($"血肿阈值分割完成: {filledVoxels} 个体素, HU范围: {minHU}-{maxHU}");
            
            SegmentationUpdated?.Invoke();
        }
        
        /// <summary>
        /// 自动阈值检测（基于Otsu方法简化版）
        /// </summary>
        public void AutoDetectThreshold()
        {
            if (volumeDataset == null) return;
            
            // 计算直方图
            int[] histogram = new int[256];
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            
            foreach (float value in volumeDataset.data)
            {
                int bin = Mathf.RoundToInt(value * 255);
                bin = Mathf.Clamp(bin, 0, 255);
                histogram[bin]++;
                
                minVal = Mathf.Min(minVal, value);
                maxVal = Mathf.Max(maxVal, value);
            }
            
            // 简化Otsu方法
            float bestThreshold = 0;
            float bestVariance = 0;
            
            for (int t = 1; t < 255; t++)
            {
                // 计算类间方差
                float w0 = 0, w1 = 0;
                float m0 = 0, m1 = 0;
                
                for (int i = 0; i <= t; i++)
                {
                    w0 += histogram[i];
                    m0 += i * histogram[i];
                }
                
                for (int i = t + 1; i < 256; i++)
                {
                    w1 += histogram[i];
                    m1 += i * histogram[i];
                }
                
                if (w0 > 0) m0 /= w0;
                if (w1 > 0) m1 /= w1;
                
                float variance = w0 * w1 * (m0 - m1) * (m0 - m1);
                
                if (variance > bestVariance)
                {
                    bestVariance = variance;
                    bestThreshold = t;
                }
            }
            
            // 设置阈值（基于检测的阈值调整）
            minHU = HUFromRaw(bestThreshold / 255f) - 15f;
            maxHU = HUFromRaw(bestThreshold / 255f) + 20f;
            
            Debug.Log($"自动阈值检测完成: {minHU:F1} - {maxHU:F1} HU");
        }
        
        /// <summary>
        /// 从UnityVolumeRendering的VolumeDataset获取体素值
        /// </summary>
        float GetVoxelValue(Vector3Int pos)
        {
            int index = pos.x + pos.y * volumeDataset.dimX + pos.z * volumeDataset.dimX * volumeDataset.dimY;
            
            if (index < 0 || index >= volumeDataset.data.Length)
                return 0f;
            
            return volumeDataset.data[index];
        }
        
        bool IsValidVoxel(Vector3Int pos)
        {
            if (volumeDataset == null) return false;
            
            return pos.x >= 0 && pos.x < volumeDataset.dimX &&
                   pos.y >= 0 && pos.y < volumeDataset.dimY &&
                   pos.z >= 0 && pos.z < volumeDataset.dimZ;
        }
        
        Vector3Int[] Get6Neighbors(Vector3Int current)
        {
            return new Vector3Int[]
            {
                current + Vector3Int.right,
                current + Vector3Int.left,
                current + Vector3Int.up,
                current + Vector3Int.down,
                new Vector3Int(current.x, current.y, current.z + 1),
                new Vector3Int(current.x, current.y, current.z - 1)
            };
        }
        
        /// <summary>
        /// 原始值转HU值
        /// </summary>
        float RawToHU(float rawValue)
        {
            // 假设原始数据范围是0-1，转换为-1024到3071 HU
            return rawValue * 4096f - 1024f;
        }
        
        /// <summary>
        /// HU值转原始值
        /// </summary>
        float HUFromRaw(float huValue)
        {
            return (huValue + 1024f) / 4096f;
        }
        
        /// <summary>
        /// 执行阈值分割
        /// </summary>
        public void ExecuteThreshold(float minValue, float maxValue)
        {
            if (activeSegment == null)
            {
                CreateNewSegment();
            }
            
            SaveUndoState();
            
            for (int z = 0; z < volumeDataset.dimZ; z++)
            {
                for (int y = 0; y < volumeDataset.dimY; y++)
                {
                    for (int x = 0; x < volumeDataset.dimX; x++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);
                        float value = GetVoxelValue(pos);
                        
                        if (value >= minValue && value <= maxValue)
                        {
                            activeSegment.SetVoxel(x, y, z, 255);
                        }
                    }
                }
            }
            
            activeSegment.UpdateTexture();
            Debug.Log($"阈值分割完成: {minValue} - {maxValue}");
            SegmentationUpdated?.Invoke();
        }
        
        /// <summary>
        /// 形态学操作 - 膨胀
        /// </summary>
        public void MorphologyDilate(int iterations = 1)
        {
            if (activeSegment == null) return;
            
            SaveUndoState();
            
            for (int i = 0; i < iterations; i++)
            {
                byte[,,] newMask = new byte[volumeDataset.dimX, volumeDataset.dimY, volumeDataset.dimZ];
                
                for (int z = 0; z < volumeDataset.dimZ; z++)
                {
                    for (int y = 0; y < volumeDataset.dimY; y++)
                    {
                        for (int x = 0; x < volumeDataset.dimX; x++)
                        {
                            if (activeSegment.GetVoxel(x, y, z) > 0)
                            {
                                // 标记当前体素及其6邻域
                                newMask[x, y, z] = 255;
                                
                                foreach (var neighbor in Get6Neighbors(new Vector3Int(x, y, z)))
                                {
                                    if (IsValidVoxel(neighbor))
                                    {
                                        newMask[neighbor.x, neighbor.y, neighbor.z] = 255;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 应用新掩码
                for (int z = 0; z < volumeDataset.dimZ; z++)
                {
                    for (int y = 0; y < volumeDataset.dimY; y++)
                    {
                        for (int x = 0; x < volumeDataset.dimX; x++)
                        {
                            activeSegment.SetVoxel(x, y, z, newMask[x, y, z]);
                        }
                    }
                }
            }
            
            activeSegment.UpdateTexture();
            Debug.Log($"膨胀操作完成，迭代次数: {iterations}");
        }
        
        /// <summary>
        /// 形态学操作 - 腐蚀
        /// </summary>
        public void MorphologyErode(int iterations = 1)
        {
            if (activeSegment == null) return;
            
            SaveUndoState();
            
            for (int i = 0; i < iterations; i++)
            {
                byte[,,] newMask = new byte[volumeDataset.dimX, volumeDataset.dimY, volumeDataset.dimZ];
                
                for (int z = 1; z < volumeDataset.dimZ - 1; z++)
                {
                    for (int y = 1; y < volumeDataset.dimY - 1; y++)
                    {
                        for (int x = 1; x < volumeDataset.dimX - 1; x++)
                        {
                            // 检查6邻域是否都在内部
                            bool allInside = true;
                            foreach (var neighbor in Get6Neighbors(new Vector3Int(x, y, z)))
                            {
                                if (activeSegment.GetVoxel(neighbor.x, neighbor.y, neighbor.z) == 0)
                                {
                                    allInside = false;
                                    break;
                                }
                            }
                            
                            if (allInside && activeSegment.GetVoxel(x, y, z) > 0)
                            {
                                newMask[x, y, z] = 255;
                            }
                        }
                    }
                }
                
                // 应用新掩码
                for (int z = 0; z < volumeDataset.dimZ; z++)
                {
                    for (int y = 0; y < volumeDataset.dimY; y++)
                    {
                        for (int x = 0; x < volumeDataset.dimX; x++)
                        {
                            activeSegment.SetVoxel(x, y, z, newMask[x, y, z]);
                        }
                    }
                }
            }
            
            activeSegment.UpdateTexture();
            Debug.Log($"腐蚀操作完成，迭代次数: {iterations}");
        }
        
        /// <summary>
        /// 形态学操作 - 开运算（先腐蚀后膨胀）
        /// </summary>
        public void MorphologyOpen(int iterations = 1)
        {
            MorphologyErode(iterations);
            MorphologyDilate(iterations);
        }
        
        /// <summary>
        /// 形态学操作 - 闭运算（先膨胀后腐蚀）
        /// </summary>
        public void MorphologyClose(int iterations = 1)
        {
            MorphologyDilate(iterations);
            MorphologyErode(iterations);
        }
        
        /// <summary>
        /// 在UnityVolumeRendering的渲染上叠加显示分割
        /// </summary>
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!showSegmentOverlay || activeSegment == null || segmentOverlayMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            
            // 设置分割纹理和颜色
            segmentOverlayMaterial.SetTexture("_SegmentMask", activeSegment.texture);
            segmentOverlayMaterial.SetColor("_SegmentColor", activeSegment.color);
            segmentOverlayMaterial.SetFloat("_SegmentOpacity", segmentOpacity);
            
            Graphics.Blit(source, destination, segmentOverlayMaterial);
        }
        
        // ========== 撤销/重做系统 ==========
        
        void SaveUndoState()
        {
            if (activeSegment == null) return;
            
            SegmentSnapshot snapshot = new SegmentSnapshot();
            snapshot.data = activeSegment.GetDataCopy();
            snapshot.segmentName = activeSegment.name;
            undoStack.Push(snapshot);
            
            // 限制撤销步数
            if (undoStack.Count > MAX_UNDO_STEPS)
            {
                var items = new SegmentSnapshot[MAX_UNDO_STEPS];
                for (int i = 0; i < MAX_UNDO_STEPS; i++)
                {
                    items[MAX_UNDO_STEPS - 1 - i] = undoStack.Pop();
                }
                undoStack = new Stack<SegmentSnapshot>(items);
            }
            
            redoStack.Clear();
        }
        
        public void Undo()
        {
            if (undoStack.Count == 0 || activeSegment == null) return;
            
            redoStack.Push(new SegmentSnapshot { 
                data = activeSegment.GetDataCopy(),
                segmentName = activeSegment.name
            });
            
            SegmentSnapshot snapshot = undoStack.Pop();
            activeSegment.SetData(snapshot.data);
            activeSegment.UpdateTexture();
            
            Debug.Log($"撤销成功: {snapshot.segmentName}");
        }
        
        public void Redo()
        {
            if (redoStack.Count == 0 || activeSegment == null) return;
            
            undoStack.Push(new SegmentSnapshot { 
                data = activeSegment.GetDataCopy(),
                segmentName = activeSegment.name
            });
            
            SegmentSnapshot snapshot = redoStack.Pop();
            activeSegment.SetData(snapshot.data);
            activeSegment.UpdateTexture();
            
            Debug.Log($"重做成功: {snapshot.segmentName}");
        }
        
        /// <summary>
        /// 保存分割结果
        /// </summary>
        public void SaveSegmentation()
        {
            if (activeSegment == null)
            {
                Debug.LogWarning("没有活动的分割可保存");
                return;
            }
            
            string path = UnityEditor.EditorUtility.SaveFilePanel(
                "保存分割",
                "Assets/",
                activeSegment.name + "_segmentation",
                "bytes"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                byte[] data = activeSegment.GetDataCopy();
                System.IO.File.WriteAllBytes(path, data);
                Debug.Log($"分割已保存: {path}");
            }
        }
        
        /// <summary>
        /// 加载分割结果
        /// </summary>
        public void LoadSegmentation()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel(
                "加载分割",
                "Assets/",
                "bytes"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                
                if (activeSegment == null)
                {
                    CreateNewSegment();
                }
                
                activeSegment.SetData(data);
                Debug.Log($"分割已加载: {path}");
            }
        }
        
        /// <summary>
        /// 计算分割体积
        /// </summary>
        public float CalculateVolume()
        {
            if (activeSegment == null || volumeDataset == null) return 0f;
            
            // 获取体素尺寸（假设各向同性）
            float voxelSize = 1.0f; // 需要从VolumeDataset获取实际尺寸
            
            return activeSegment.voxelCount * voxelSize * voxelSize * voxelSize;
        }
        
        /// <summary>
        /// 获取所有分割的统计信息
        /// </summary>
        public string GetSegmentationStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("分割统计信息:");
            sb.AppendLine($"活动分割: {activeSegment?.name ?? "无"}");
            sb.AppendLine($"分割数量: {segments.Count}");
            
            foreach (var segment in segments)
            {
                sb.AppendLine($"- {segment.name}: {segment.voxelCount} 体素");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// 分割快照（用于撤销/重做）
    /// </summary>
    public class SegmentSnapshot
    {
        public byte[] data;
        public string segmentName;
    }
}
