namespace UnityVolumeRendering.Segmentation
{
    /// <summary>
    /// 绘制模式枚举 - 支持多种绘制工具
    /// 模仿3D Slicer的交互方式
    /// </summary>
    public enum PaintMode
    {
        /// <summary>
        /// 圆形绘制 - 按住拖动确定半径
        /// </summary>
        Circle = 0,
        
        /// <summary>
        /// 椭圆绘制 - 按住拖动确定椭圆
        /// </summary>
        Ellipse = 1,
        
        /// <summary>
        /// 矩形绘制 - 按住拖动确定矩形
        /// </summary>
        Rectangle = 2,
        
        /// <summary>
        /// 自由绘制 - 手动绘制任意形状
        /// </summary>
        Freehand = 3,
        
        /// <summary>
        /// 多边形绘制 - 点击多个点形成闭合多边形
        /// </summary>
        Polygon = 4,
        
        /// <summary>
        /// 点绘制 - 单个点
        /// </summary>
        Point = 5,
        
        /// <summary>
        /// 橡皮擦 - 擦除分割区域
        /// </summary>
        Eraser = 6
    }
    
    /// <summary>
    /// 切片方向枚举
    /// </summary>
    public enum SliceOrientation
    {
        /// <summary>
        /// 轴向切片 - XY平面
        /// </summary>
        Axial = 0,
        
        /// <summary>
        /// 冠状切片 - XZ平面
        /// </summary>
        Coronal = 1,
        
        /// <summary>
        /// 矢状切片 - YZ平面
        /// </summary>
        Sagittal = 2
    }
    
    /// <summary>
    /// 分割工具类型
    /// </summary>
    public enum SegmentationTool
    {
        /// <summary>
        /// 种子点区域生长
        /// </summary>
        GrowFromSeeds = 0,
        
        /// <summary>
        /// 阈值分割
        /// </summary>
        Threshold = 1,
        
        /// <summary>
        /// 画笔工具
        /// </summary>
        Paint = 2,
        
        /// <summary>
        /// 橡皮擦工具
        /// </summary>
        Eraser = 3,
        
        /// <summary>
        /// 填充工具
        /// </summary>
        FloodFill = 4,
        
        /// <summary>
        /// 形态学操作
        /// </summary>
        Morphology = 5
    }
}
