using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityVolumeRendering;

/// <summary>
/// Slice orientation enum for medical imaging
/// </summary>
public enum SliceOrientation
{
    Axial,
    Coronal,
    Sagittal
}

/// <summary>
/// 血肿分割主控制器
/// 负责协调整个血肿检测、分割和可视化流程
/// </summary>
public class HematomaSegmentationManager : MonoBehaviour
{
    [Header("引用组件")]
    public VolumeRenderedObject volumeObject;
    public Camera mainCamera;
    public UnityVolumeRendering.Segmentation.SegmentationManager segmentationManager;
    
    [Header("切片视图引用")]
    public UnityEngine.UI.RawImage sliceRawImage; // 切片视图的RawImage组件

    [Header("分割参数")]
    [Range(30f, 100f)]
    public float minHUThreshold = 40f;  // 血肿最小HU值

    [Range(30f, 100f)]
    public float maxHUThreshold = 80f;  // 血肿最大HU值

    [Range(0.1f, 5f)]
    public float similarityThreshold = 10f;  // 区域生长相似度阈值

    [Header("可视化设置")]
    public Material hematomaMaterial;
    public Color hematomaColor = new Color(1f, 0f, 0f, 0.6f);

    [Header("DICOM转换参数")]
    public float rescaleSlope = 1f;
    public float rescaleIntercept = -1024f;

    // 内部状态
    private VolumeDataset dataset;
    private byte[,,] labelMap; // 3D 分割标签图（与 CT 数据同尺寸）0=background, 1=hematoma
    private GameObject hematomaVisualObject;
    private Mesh hematomaMesh;
    private bool isSegmenting = false;
    private HashSet<Vector3Int> hematomaVoxels = new HashSet<Vector3Int>();

    // UI回调
    public delegate void OnSegmentationComplete(float volume, int voxelCount);
    public event OnSegmentationComplete SegmentationCompleted;

    void Start()
    {
        if (volumeObject != null)
        {
            dataset = volumeObject.dataset;
            InitializeLabelMap(dataset);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 创建默认材质
        if (hematomaMaterial == null)
        {
            hematomaMaterial = new Material(Shader.Find("Standard"));
            SetupTransparentMaterial();
        }

        volumeObject = GameObject.FindObjectOfType<VolumeRenderedObject>();
        
        // 确保 SegmentationManager 与 VolumeRenderedObject 关联，避免多个实例
        if (volumeObject != null)
        {
            segmentationManager = volumeObject.GetComponent<UnityVolumeRendering.Segmentation.SegmentationManager>();
            if (segmentationManager == null)
            {
                segmentationManager = volumeObject.gameObject.AddComponent<UnityVolumeRendering.Segmentation.SegmentationManager>();
            }
            // 确保 volumeObject 引用正确
            segmentationManager.volumeObject = volumeObject;
        }
        else
        {
            // 回退方案：从场景中查找
            segmentationManager = GameObject.FindObjectOfType<UnityVolumeRendering.Segmentation.SegmentationManager>();
        }
    }

    void Update()
    {
        // 监听鼠标左键点击
        if (Input.GetMouseButtonDown(0) && !isSegmenting)
        {
            HandleMouseClick();
        }
        
        // G键执行区域生长分割（使用 SegmentationManager）
        if (Input.GetKeyDown(KeyCode.G) && segmentationManager != null)
        {
            ExecuteRegionGrowing();
        }

        // ESC键清除当前分割
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSegmentation();
        }

        // 空格键生成网格
        if (Input.GetKeyDown(KeyCode.Space) && CountLabeledVoxels() > 0)
        {
            GenerateAndVisualizeMesh();
        }
    }

    /// <summary>
    /// 处理鼠标点击事件
    /// </summary>
    private void HandleMouseClick()
    {
        // 检查是否点击在切片视图UI上
        if (sliceRawImage != null && IsPointerOverRawImage(sliceRawImage, out Vector2 localPoint))
        {
            // 点击在切片视图上 - 使用2D切片坐标转换
            Debug.Log("点击在切片上");
            HandleSliceViewClick(localPoint);
            return;
        }
        
        // 点击在3D场景中 - 使用射线检测
        Handle3DViewClick();
    }
    
    /// <summary>
    /// 检查指针是否在RawImage上，并返回本地坐标
    /// </summary>
    private bool IsPointerOverRawImage(UnityEngine.UI.RawImage rawImage, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        
        if (rawImage == null) return false;
        
        RectTransform rt = rawImage.rectTransform;
        Vector2 screenPos = Input.mousePosition;
        
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt, screenPos, null, out localPoint
        );
    }
    
    /// <summary>
    /// 处理切片视图点击
    /// </summary>
    private void HandleSliceViewClick(Vector2 localPoint)
    {
        if (segmentationManager == null)
        {
            Debug.LogWarning("SegmentationManager未设置，无法添加种子点");
            return;
        }
        
        if (volumeObject == null || volumeObject.dataset == null)
        {
            Debug.LogWarning("VolumeObject或Dataset为空");
            return;
        }
        
        // 获取当前切片索引（需要从SliceViewerUI获取）
        int currentSliceIndex = GetCurrentSliceIndex();
        
        // 将本地坐标转换为切片像素坐标
        RectTransform rt = sliceRawImage.rectTransform;
        Rect rect = rt.rect;
        
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;
        
        VolumeDataset dataset = volumeObject.dataset;
        int sliceX = Mathf.Clamp((int)(u * dataset.dimX), 0, dataset.dimX - 1);
        int sliceY = Mathf.Clamp((int)(v * dataset.dimY), 0, dataset.dimY - 1);
        
        // 使用AddSeedPointFromSlice方法添加种子点
        bool success = segmentationManager.AddSeedPointFromSlice(sliceX, sliceY, currentSliceIndex);
        
        if (success)
        {
            Debug.Log($"✓ 切片视图种子点添加成功: ({sliceX}, {sliceY}, {currentSliceIndex})");
        }
    }
    
    /// <summary>
    /// 处理3D视图点击
    /// </summary>
    private void Handle3DViewClick()
    {
        // 优先使用 SegmentationManager 添加种子点
        if (segmentationManager != null)
        {
            bool success = segmentationManager.AddSeedPoint(Input.mousePosition);
            if (success)
            {
                Debug.Log("种子点添加成功，按 G 键执行区域生长分割");
            }
            return;
        }
        
        // 如果没有 SegmentationManager，使用内置逻辑
        if (volumeObject == null)
        {
            Debug.LogWarning("VolumeObject 或 Dataset 为空，无法添加种子点");
            return;
        }
        
        if (Camera.main == null)
        {
            Debug.LogError("主相机未找到");
            return;
        }
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Renderer renderer = volumeObject.transform.GetChild(0).GetComponent<Renderer>();

        if (renderer == null)
        {
            Debug.LogError("VolumeObject 缺少 Renderer 组件");
            return;
        }
        
        Bounds bounds = renderer.bounds;
        float enter;
        
        if (bounds.IntersectRay(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3Int voxelPos = WorldToVoxelCoordinate(hitPoint);
            
            if (IsValidVoxel(voxelPos))
            {
                Debug.Log($"点击体素坐标: {voxelPos}, HU值: {GetHounsfieldValue(voxelPos)}");
                StartSegmentation(voxelPos);
            }
        }
    }
    
    /// <summary>
    /// 获取当前切片索引
    /// </summary>
    private int GetCurrentSliceIndex()
    {
        // 尝试从SliceViewerUI获取当前切片索引
        var sliceViewerUI = FindObjectOfType<UnityVolumeRendering.SliceViewerUI>();
        if (sliceViewerUI != null)
        {
            return sliceViewerUI.CurrentSliceIndex;
        }
        
        // 默认返回中间切片
        if (volumeObject != null && volumeObject.dataset != null)
        {
            return volumeObject.dataset.dimZ / 2;
        }
        
        return 0;
    }
    
    /// <summary>
    /// 执行区域生长分割（通过 SegmentationManager）
    /// </summary>
    public void ExecuteRegionGrowing()
    {
        if (segmentationManager == null)
        {
            Debug.LogWarning("SegmentationManager 未设置");
            return;
        }
        
        segmentationManager.ExecuteGrowFromSeeds(similarityThreshold);
    }

    /// <summary>
    /// 开始血肿分割
    /// </summary>
    public void StartSegmentation(Vector3Int seedPoint)
    {
        if (dataset == null || labelMap == null)
        {
            Debug.LogError("数据集未加载!");
            return;
        }

        isSegmenting = true;

        float seedHU = GetHounsfieldValue(seedPoint);
        Debug.Log($"种子点HU值: {seedHU}");

        // 检查种子点是否在血肿范围内
        if (seedHU < minHUThreshold || seedHU > maxHUThreshold)
        {
            Debug.LogWarning($"种子点HU值({seedHU})不在血肿范围内({minHUThreshold}-{maxHUThreshold})");
            isSegmenting = false;
            return;
        }

        // 执行3D区域生长并写入labelMap
        RegionGrowing3D(seedPoint, seedHU);

        Debug.Log($"分割完成!");

        isSegmenting = false;

        // 触发回调
        int voxelCount = CountLabeledVoxels();
        float volume = CalculateVolumeFromVoxels();
        SegmentationCompleted?.Invoke(volume, voxelCount);
    }

    /// <summary>
    /// 3D区域生长算法
    /// </summary>
    private void RegionGrowing3D(Vector3Int seedPoint, float seedHU)
    {
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        queue.Enqueue(seedPoint);
        visited.Add(seedPoint);

        int processedCount = 0;
        int maxVoxels = 100000; // 安全限制

        while (queue.Count > 0 && processedCount < maxVoxels)
        {
            Vector3Int current = queue.Dequeue();
            float currentHU = GetHounsfieldValue(current);

            // 检查是否满足生长条件
            bool inRange = currentHU >= minHUThreshold && currentHU <= maxHUThreshold;
            bool similar = Mathf.Abs(currentHU - seedHU) <= similarityThreshold;

            if (inRange && similar)
            {
                hematomaVoxels.Add(current);
                processedCount++;

                // 检查6邻域(可选择26邻域以获得更平滑结果)
                Vector3Int[] neighbors = Get6Neighbors(current);

                foreach (Vector3Int neighbor in neighbors)
                {
                    if (IsValidVoxel(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        if (processedCount >= maxVoxels)
        {
            Debug.LogWarning("达到最大体素限制,分割可能不完整");
        }
    }

    /// <summary>
    /// 获取6邻域
    /// </summary>
    private Vector3Int[] Get6Neighbors(Vector3Int voxel)
    {
        return new Vector3Int[]
        {
            new Vector3Int(voxel.x + 1, voxel.y, voxel.z),
            new Vector3Int(voxel.x - 1, voxel.y, voxel.z),
            new Vector3Int(voxel.x, voxel.y + 1, voxel.z),
            new Vector3Int(voxel.x, voxel.y - 1, voxel.z),
            new Vector3Int(voxel.x, voxel.y, voxel.z + 1),
            new Vector3Int(voxel.x, voxel.y, voxel.z - 1)
        };
    }

    /// <summary>
    /// 获取26邻域(更平滑的结果)
    /// </summary>
    private List<Vector3Int> Get26Neighbors(Vector3Int voxel)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;

                    neighbors.Add(new Vector3Int(
                        voxel.x + dx,
                        voxel.y + dy,
                        voxel.z + dz
                    ));
                }
            }
        }

        return neighbors;
    }

    /// <summary>
    /// 生成并可视化血肿网格
    /// </summary>
    public void GenerateAndVisualizeMesh()
    {
        if (CountLabeledVoxels() == 0)
        {
            Debug.LogWarning("没有分割数据,无法生成网格");
            return;
        }

        Debug.Log("开始生成血肿网格...");

        // 使用Marching Cubes生成网格
        hematomaMesh = GenerateMarchingCubesMesh();

        if (hematomaMesh == null)
        {
            Debug.LogError("网格生成失败");
            return;
        }

        // 可视化
        VisualizeHematoma(hematomaMesh);

        // 计算并显示体积
        float volume = CalculateMeshVolume(hematomaMesh);
        Debug.Log($"血肿体积: {volume:F2} cm³");
    }

    /// <summary>
    /// 简化的Marching Cubes实现
    /// </summary>
    private Mesh GenerateMarchingCubesMesh()
    {
        // 创建体素网格
        bool[,,] voxelGrid = CreateVoxelGrid();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // 获取边界
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;

        for (int x = 0; x < labelMap.GetLength(0); x++)
        {
            for (int y = 0; y < labelMap.GetLength(1); y++)
            {
                for (int z = 0; z < labelMap.GetLength(2); z++)
                {
                    if (labelMap[x, y, z] == 1)
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                        minZ = Mathf.Min(minZ, z);
                        maxZ = Mathf.Max(maxZ, z);
                    }
                }
            }
        }

        // 遍历边界内的所有立方体
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    ProcessCube(x, y, z, voxelGrid, vertices, triangles);
                }
            }
        }

        if (vertices.Count == 0)
        {
            Debug.LogWarning("未生成任何顶点");
            return null;
        }

        // 创建网格
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 支持大网格
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log($"生成网格: {vertices.Count} 顶点, {triangles.Count / 3} 三角形");

        return mesh;
    }

    /// <summary>
    /// 创建体素网格
    /// </summary>
    private bool[,,] CreateVoxelGrid()
    {
        bool[,,] grid = new bool[dataset.dimX, dataset.dimY, dataset.dimZ];

        for (int x = 0; x < labelMap.GetLength(0); x++)
        {
            for (int y = 0; y < labelMap.GetLength(1); y++)
            {
                for (int z = 0; z < labelMap.GetLength(2); z++)
                {
                    grid[x, y, z] = labelMap[x, y, z] == 1;
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// 处理单个立方体(简化版Marching Cubes)
    /// </summary>
    private void ProcessCube(int x, int y, int z, bool[,,] grid,
                            List<Vector3> vertices, List<int> triangles)
    {
        // 获取立方体8个顶点的值
        int cubeIndex = 0;
        if (GetGridValue(grid, x, y, z)) cubeIndex |= 1;
        if (GetGridValue(grid, x + 1, y, z)) cubeIndex |= 2;
        if (GetGridValue(grid, x + 1, y, z + 1)) cubeIndex |= 4;
        if (GetGridValue(grid, x, y, z + 1)) cubeIndex |= 8;
        if (GetGridValue(grid, x, y + 1, z)) cubeIndex |= 16;
        if (GetGridValue(grid, x + 1, y + 1, z)) cubeIndex |= 32;
        if (GetGridValue(grid, x + 1, y + 1, z + 1)) cubeIndex |= 64;
        if (GetGridValue(grid, x, y + 1, z + 1)) cubeIndex |= 128;

        // 如果立方体完全在内部或外部,跳过
        if (cubeIndex == 0 || cubeIndex == 255)
            return;

        // 简化处理:在边界处创建面
        CreateCubeFaces(x, y, z, cubeIndex, vertices, triangles);
    }

    /// <summary>
    /// 创建立方体面(简化实现)
    /// </summary>
    private void CreateCubeFaces(int x, int y, int z, int cubeIndex,
                                List<Vector3> vertices, List<int> triangles)
    {
        Vector3 voxelSize = GetVoxelSize();
        Vector3 basePos = VoxelToWorldPosition(new Vector3Int(x, y, z));

        int baseIndex = vertices.Count;

        // 创建立方体的6个面(根据cubeIndex决定哪些面需要渲染)
        // 这里简化为创建所有边界面

        // 前面 (z+)
        if ((cubeIndex & 4) == 0 || (cubeIndex & 8) == 0)
        {
            vertices.Add(basePos + new Vector3(0, 0, voxelSize.z));
            vertices.Add(basePos + new Vector3(voxelSize.x, 0, voxelSize.z));
            vertices.Add(basePos + new Vector3(voxelSize.x, voxelSize.y, voxelSize.z));
            vertices.Add(basePos + new Vector3(0, voxelSize.y, voxelSize.z));

            AddQuad(triangles, baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
            baseIndex += 4;
        }
    }

    /// <summary>
    /// 添加四边形(两个三角形)
    /// </summary>
    private void AddQuad(List<int> triangles, int v0, int v1, int v2, int v3)
    {
        triangles.Add(v0);
        triangles.Add(v2);
        triangles.Add(v1);

        triangles.Add(v0);
        triangles.Add(v3);
        triangles.Add(v2);
    }

    /// <summary>
    /// 可视化血肿
    /// </summary>
    private void VisualizeHematoma(Mesh mesh)
    {
        // 删除旧的可视化对象
        if (hematomaVisualObject != null)
        {
            Destroy(hematomaVisualObject);
        }

        // 创建新对象
        hematomaVisualObject = new GameObject("Hematoma_Visualization");
        hematomaVisualObject.transform.SetParent(volumeObject.transform);
        hematomaVisualObject.transform.localPosition = Vector3.zero;
        hematomaVisualObject.transform.localRotation = Quaternion.identity;
        hematomaVisualObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = hematomaVisualObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = hematomaVisualObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = mesh;
        meshRenderer.material = hematomaMaterial;

        Debug.Log("血肿可视化完成");
    }

    /// <summary>
    /// 设置透明材质
    /// </summary>
    private void SetupTransparentMaterial()
    {
        hematomaMaterial.SetFloat("_Mode", 3); // Transparent
        hematomaMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        hematomaMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        hematomaMaterial.SetInt("_ZWrite", 0);
        hematomaMaterial.DisableKeyword("_ALPHATEST_ON");
        hematomaMaterial.EnableKeyword("_ALPHABLEND_ON");
        hematomaMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        hematomaMaterial.renderQueue = 3000;
        hematomaMaterial.color = hematomaColor;
    }

    /// <summary>
    /// 获取HU值
    /// </summary>
    private float GetHounsfieldValue(Vector3Int voxel)
    {
        if (!IsValidVoxel(voxel))
            return -1000f;

        int index = voxel.x + voxel.y * dataset.dimX + voxel.z * dataset.dimX * dataset.dimY;

        if (index < 0 || index >= dataset.data.Length)
            return -1000f;

        // 从原始数据获取值
        float rawValue = dataset.data[index] / 255f;

        // 转换为HU值
        float huValue = rawValue * 4096f - 1024f; // 根据实际DICOM参数调整

        return huValue;
    }

    /// <summary>
    /// 世界坐标转体素坐标
    /// </summary>
    private Vector3Int WorldToVoxelCoordinate(Vector3 worldPos)
    {
        Vector3 localPos = volumeObject.transform.InverseTransformPoint(worldPos);

        // 归一化到[0,1]
        localPos += Vector3.one * 0.5f;

        return new Vector3Int(
            Mathf.Clamp(Mathf.RoundToInt(localPos.x * dataset.dimX), 0, dataset.dimX - 1),
            Mathf.Clamp(Mathf.RoundToInt(localPos.y * dataset.dimY), 0, dataset.dimY - 1),
            Mathf.Clamp(Mathf.RoundToInt(localPos.z * dataset.dimZ), 0, dataset.dimZ - 1)
        );
    }

    /// <summary>
    /// 体素坐标转世界坐标
    /// </summary>
    private Vector3 VoxelToWorldPosition(Vector3Int voxel)
    {
        Vector3 normalized = new Vector3(
            (float)voxel.x / dataset.dimX,
            (float)voxel.y / dataset.dimY,
            (float)voxel.z / dataset.dimZ
        );

        normalized -= Vector3.one * 0.5f;

        return volumeObject.transform.TransformPoint(normalized);
    }

    /// <summary>
    /// 获取体素尺寸(世界空间)
    /// </summary>
    private Vector3 GetVoxelSize()
    {
        Vector3 scale = volumeObject.transform.lossyScale;
        return new Vector3(
            scale.x / dataset.dimX,
            scale.y / dataset.dimY,
            scale.z / dataset.dimZ
        );
    }

    /// <summary>
    /// 初始化3D分割标签图
    /// </summary>
    public void InitializeLabelMap(VolumeDataset dataset)
    {
        labelMap = new byte[dataset.dimX, dataset.dimY, dataset.dimZ];
    }

    /// <summary>
    /// 在指定slice上设置2D掩膜
    /// </summary>
    public void SetSliceMask(int sliceIndex, bool[,] mask, SliceOrientation orientation)
    {
        switch (orientation)
        {
            case SliceOrientation.Axial:
                for (int x = 0; x < mask.GetLength(0); x++)
                    for (int y = 0; y < mask.GetLength(1); y++)
                        labelMap[x, y, sliceIndex] = mask[x, y] ? (byte)1 : (byte)0;
                break;
            case SliceOrientation.Coronal:
                for (int x = 0; x < mask.GetLength(0); x++)
                    for (int z = 0; z < mask.GetLength(1); z++)
                        labelMap[x, sliceIndex, z] = mask[x, z] ? (byte)1 : (byte)0;
                break;
            case SliceOrientation.Sagittal:
                for (int y = 0; y < mask.GetLength(0); y++)
                    for (int z = 0; z < mask.GetLength(1); z++)
                        labelMap[sliceIndex, y, z] = mask[y, z] ? (byte)1 : (byte)0;
                break;
        }
    }

    /// <summary>
    /// 获取当前slice的2D掩膜用于显示
    /// </summary>
    public bool[,] GetSliceMask(int sliceIndex, SliceOrientation orientation)
    {
        int w, h;
        bool[,] mask;

        switch (orientation)
        {
            case SliceOrientation.Axial:
                w = labelMap.GetLength(0);
                h = labelMap.GetLength(1);
                mask = new bool[w, h];
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        mask[x, y] = labelMap[x, y, sliceIndex] == 1;
                return mask;
            case SliceOrientation.Coronal:
                w = labelMap.GetLength(0);
                h = labelMap.GetLength(2);
                mask = new bool[w, h];
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < h; z++)
                        mask[x, z] = labelMap[x, sliceIndex, z] == 1;
                return mask;
            case SliceOrientation.Sagittal:
                w = labelMap.GetLength(1);
                h = labelMap.GetLength(2);
                mask = new bool[w, h];
                for (int y = 0; y < w; y++)
                    for (int z = 0; z < h; z++)
                        mask[y, z] = labelMap[sliceIndex, y, z] == 1;
                return mask;
        }
        return null;
    }

    /// <summary>
    /// 统计标记的体素数量
    /// </summary>
    private int CountLabeledVoxels()
    {
        int count = 0;
        for (int x = 0; x < labelMap.GetLength(0); x++)
            for (int y = 0; y < labelMap.GetLength(1); y++)
                for (int z = 0; z < labelMap.GetLength(2); z++)
                    if (labelMap[x, y, z] == 1) count++;
        return count;
    }

    /// <summary>
    /// 清除分割结果
    /// </summary>
    public void ClearSegmentation()
    {
        if (labelMap != null)
        {
            System.Array.Clear(labelMap, 0, labelMap.Length);
        }

        hematomaVoxels.Clear();

        if (hematomaVisualObject != null)
        {
            Destroy(hematomaVisualObject);
            hematomaVisualObject = null;
        }

        Debug.Log("分割结果已清除");
    }

    /// <summary>
    /// 检查体素是否有效
    /// </summary>
    private bool IsValidVoxel(Vector3Int voxel)
    {
        return voxel.x >= 0 && voxel.x < dataset.dimX &&
               voxel.y >= 0 && voxel.y < dataset.dimY &&
               voxel.z >= 0 && voxel.z < dataset.dimZ;
    }

    /// <summary>
    /// 获取网格值
    /// </summary>
    private bool GetGridValue(bool[,,] grid, int x, int y, int z)
    {
        if (x < 0 || x >= dataset.dimX ||
            y < 0 || y >= dataset.dimY ||
            z < 0 || z >= dataset.dimZ)
            return false;

        return grid[x, y, z];
    }

    /// <summary>
    /// 从体素计算体积
    /// </summary>
    private float CalculateVolumeFromVoxels()
    {
        Vector3 voxelSize = GetVoxelSize();
        float voxelVolume = voxelSize.x * voxelSize.y * voxelSize.z;

        // 转换为cm³ (假设Unity单位是米)
        return hematomaVoxels.Count * voxelVolume * 1000000f;
    }

    /// <summary>
    /// 计算网格体积
    /// </summary>
    private float CalculateMeshVolume(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        float volume = 0f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p1 = vertices[triangles[i]];
            Vector3 p2 = vertices[triangles[i + 1]];
            Vector3 p3 = vertices[triangles[i + 2]];

            volume += SignedVolumeOfTriangle(p1, p2, p3);
        }

        // 转换为cm³
        return Mathf.Abs(volume) * 1000000f;
    }

    /// <summary>
    /// 计算三角形的有向体积
    /// </summary>
    private float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
    }


    /// <summary>
    /// 手动添加体素
    /// </summary>
    public void AddVoxel(Vector3Int voxel)
    {
        if (IsValidVoxel(voxel))
        {
            hematomaVoxels.Add(voxel);
        }
    }

    /// <summary>
    /// 手动删除体素
    /// </summary>
    public void RemoveVoxel(Vector3Int voxel)
    {
        hematomaVoxels.Remove(voxel);
    }

    /// <summary>
    /// 导出分割数据
    /// </summary>
    public void ExportSegmentation(string filepath)
    {
        // 导出为点云或网格文件
        // 可实现OBJ、STL等格式导出
        Debug.Log($"导出分割数据到: {filepath}");
    }
}