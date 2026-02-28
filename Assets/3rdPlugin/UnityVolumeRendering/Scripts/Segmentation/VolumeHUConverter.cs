using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for converting volume data to Hounsfield Units (HU)
    /// </summary>
    public static class VolumeHUConverter
    {
        /// <summary>
        /// Convert volume dataset data to HU values
        /// Note: In this project, volume data is assumed to already be in HU units
        /// </summary>
        /// <param name="dataset">Volume dataset to convert</param>
        /// <returns>Array of HU values</returns>
        public static float[] ToHU(VolumeDataset dataset)
        {
            // In this Unity Volume Rendering project, data is already in HU units
            // No conversion needed - just return a copy
            float[] hu = new float[dataset.data.Length];
            System.Array.Copy(dataset.data, hu, dataset.data.Length);
            return hu;
        }
    }
}
