using UnityEngine;
using System;
using UnityVolumeRendering;

namespace UnityVolumeRendering.Segmentation
{
    /// <summary>
    /// Segment type enumeration
    /// </summary>
    public enum SegmentType
    {
        Hemorrhage,
        NormalTissue,
        Tumor,
        Other
    }

    /// <summary>
    /// 分割对象 - 存储3D掩码数据
    /// 用于血肿区域分割的Segment类
    /// </summary>
    [Serializable]
    public class Segment
    {
        public string name = "Segment";
        public Color color = Color.red;
        public bool visible = true;
        
        // 3D掩码数据（0或1）
        private byte[,,] mask;
        
        // 用于渲染的3D纹理
        public Texture3D texture;
        
        // 网格数据（用于3D重建渲染）
        public Mesh mesh;
        
        // 尺寸
        private int dimX, dimY, dimZ;
        
        // 统计信息
        public int voxelCount = 0;
        public Vector3 centroid;
        
        // 血肿特有属性
        public float hounsfieldMin = float.MaxValue;
        public float hounsfieldMax = float.MinValue;
        public float hounsfieldMean = 0f;
        
        // Segment type
        public SegmentType segmentType;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public Segment()
        {
        }
        
        /// <summary>
        /// Constructor with type, LabelVolume, and Mesh
        /// </summary>
        /// <param name="type">Segment type</param>
        /// <param name="volume">LabelVolume containing mask data</param>
        /// <param name="mesh">Mesh for 3D rendering</param>
        public Segment(SegmentType type, LabelVolume volume, Mesh mesh)
        {
            segmentType = type;
            this.mesh = mesh;
            
            // Initialize with volume dimensions
            Initialize(volume.width, volume.height, volume.depth);
            
            // Copy mask data from LabelVolume
            SetData(volume.mask);
            
            // Update texture
            UpdateTexture();
        }
        
        public void Initialize(int x, int y, int z)
        {
            dimX = x;
            dimY = y;
            dimZ = z;
            
            mask = new byte[x, y, z];
            
            // 创建3D纹理
            texture = new Texture3D(x, y, z, TextureFormat.R8, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            
            Color[] colors = new Color[x * y * z];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(0, 0, 0, 0);
            
            texture.SetPixels(colors);
            texture.Apply();
            
            Debug.Log($"Segment初始化: {x}x{y}x{z}");
        }
        
        public void SetVoxel(int x, int y, int z, byte value)
        {
            if (x < 0 || x >= dimX || y < 0 || y >= dimY || z < 0 || z >= dimZ)
                return;
            
            mask[x, y, z] = value;
        }
        
        public byte GetVoxel(int x, int y, int z)
        {
            if (x < 0 || x >= dimX || y < 0 || y >= dimY || z < 0 || z >= dimZ)
                return 0;
            
            return mask[x, y, z];
        }
        
        /// <summary>
        /// Get the X dimension of the segment
        /// </summary>
        public int GetDimensionX() => dimX;
        
        /// <summary>
        /// Get the Y dimension of the segment
        /// </summary>
        public int GetDimensionY() => dimY;
        
        /// <summary>
        /// Get the Z dimension of the segment
        /// </summary>
        public int GetDimensionZ() => dimZ;
        
        public void UpdateTexture()
        {
            Color[] colors = new Color[dimX * dimY * dimZ];
            voxelCount = 0;
            
            for (int z = 0; z < dimZ; z++)
            {
                for (int y = 0; y < dimY; y++)
                {
                    for (int x = 0; x < dimX; x++)
                    {
                        int index = x + y * dimX + z * dimX * dimY;
                        float value = mask[x, y, z] / 255f;
                        colors[index] = new Color(value, value, value, value);
                        
                        if (mask[x, y, z] > 0)
                            voxelCount++;
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            Debug.Log($"Segment更新: {voxelCount} 个体素");
        }
        
        public byte[] GetDataCopy()
        {
            byte[] data = new byte[dimX * dimY * dimZ];
            int index = 0;
            
            for (int z = 0; z < dimZ; z++)
            {
                for (int y = 0; y < dimY; y++)
                {
                    for (int x = 0; x < dimX; x++)
                    {
                        data[index++] = mask[x, y, z];
                    }
                }
            }
            
            return data;
        }
        
        public void SetData(byte[] data)
        {
            int index = 0;
            
            for (int z = 0; z < dimZ; z++)
            {
                for (int y = 0; y < dimY; y++)
                {
                    for (int x = 0; x < dimX; x++)
                    {
                        mask[x, y, z] = data[index++];
                    }
                }
            }
            
            UpdateTexture();
        }
        
        public void Clear()
        {
            for (int z = 0; z < dimZ; z++)
            {
                for (int y = 0; y < dimY; y++)
                {
                    for (int x = 0; x < dimX; x++)
                    {
                        mask[x, y, z] = 0;
                    }
                }
            }
            
            // 重置统计信息
            voxelCount = 0;
            hounsfieldMin = float.MaxValue;
            hounsfieldMax = float.MinValue;
            hounsfieldMean = 0f;
            
            UpdateTexture();
        }
        
        /// <summary>
        /// 计算分割区域的质心
        /// </summary>
        public void CalculateCentroid()
        {
            if (voxelCount == 0)
            {
                centroid = Vector3.zero;
                return;
            }
            
            float sumX = 0, sumY = 0, sumZ = 0;
            
            for (int z = 0; z < dimZ; z++)
            {
                for (int y = 0; y < dimY; y++)
                {
                    for (int x = 0; x < dimX; x++)
                    {
                        if (mask[x, y, z] > 0)
                        {
                            sumX += x;
                            sumY += y;
                            sumZ += z;
                        }
                    }
                }
            }
            
            centroid = new Vector3(sumX / voxelCount, sumY / voxelCount, sumZ / voxelCount);
        }
        
        /// <summary>
        /// 获取分割体积（基于体素数量）
        /// </summary>
        public float GetVolume(float voxelSize)
        {
            return voxelCount * voxelSize * voxelSize * voxelSize;
        }
        
        /// <summary>
        /// 检查体素是否在分割区域内
        /// </summary>
        public bool Contains(int x, int y, int z)
        {
            if (x < 0 || x >= dimX || y < 0 || y >= dimY || z < 0 || z >= dimZ)
                return false;
            
            return mask[x, y, z] > 0;
        }
        
        /// <summary>
        /// 复制Segment
        /// </summary>
        public Segment Clone()
        {
            Segment newSegment = new Segment();
            newSegment.name = name + "_copy";
            newSegment.color = color;
            newSegment.visible = visible;
            newSegment.Initialize(dimX, dimY, dimZ);
            
            byte[] data = GetDataCopy();
            newSegment.SetData(data);
            
            return newSegment;
        }
    }
}
