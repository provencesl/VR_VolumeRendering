using System;
using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Represents a label volume (mask) for segmentation, similar to 3D Slicer's LabelMap.
    /// Contains binary mask data where 0 = background, 1 = segment.
    /// </summary>
    [Serializable]
    public class LabelVolume
    {
        /// <summary>
        /// Width of the volume
        /// </summary>
        public int width;

        /// <summary>
        /// Height of the volume
        /// </summary>
        public int height;

        /// <summary>
        /// Depth of the volume
        /// </summary>
        public int depth;

        /// <summary>
        /// Flattened 3D array of mask values (0 = background, 1 = segment)
        /// </summary>
        public byte[] mask;

        /// <summary>
        /// Constructor for LabelVolume
        /// </summary>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        public LabelVolume(int width, int height, int depth)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.mask = new byte[width * height * depth];
        }

        /// <summary>
        /// Constructor for LabelVolume with existing mask data
        /// </summary>
        /// <param name="width">Volume width</param>
        /// <param name="height">Volume height</param>
        /// <param name="depth">Volume depth</param>
        /// <param name="mask">Mask data array</param>
        public LabelVolume(int width, int height, int depth, byte[] mask)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.mask = mask;
        }

        /// <summary>
        /// Get mask value at specified coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="z">Z coordinate</param>
        /// <returns>Mask value (0 or 1)</returns>
        public byte GetMask(int x, int y, int z)
        {
            int index = x + y * width + z * width * height;
            if (index >= 0 && index < mask.Length)
                return mask[index];
            return 0;
        }

        /// <summary>
        /// Set mask value at specified coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="z">Z coordinate</param>
        /// <param name="value">Mask value (0 or 1)</param>
        public void SetMask(int x, int y, int z, byte value)
        {
            int index = x + y * width + z * width * height;
            if (index >= 0 && index < mask.Length)
                mask[index] = value;
        }

        /// <summary>
        /// Clear the mask (set all values to 0)
        /// </summary>
        public void Clear()
        {
            Array.Clear(mask, 0, mask.Length);
        }

        /// <summary>
        /// Create a copy of this LabelVolume
        /// </summary>
        /// <returns>New LabelVolume instance with copied data</returns>
        public LabelVolume Clone()
        {
            byte[] maskCopy = new byte[mask.Length];
            Array.Copy(mask, maskCopy, mask.Length);
            return new LabelVolume(width, height, depth, maskCopy);
        }
    }
}
