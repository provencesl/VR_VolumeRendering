using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for building intracranial masks (skull stripping)
    /// </summary>
    public static class IntracranialMaskBuilder
    {
        /// <summary>
        /// Build intracranial mask from HU volume data
        /// Simplified skull stripping approach
        /// </summary>
        /// <param name="hu">HU volume data</param>
        /// <param name="dataset">Volume dataset</param>
        /// <returns>Boolean array indicating intracranial space</returns>
        public static bool[] Build(float[] hu, VolumeDataset dataset)
        {
            bool[] mask = new bool[hu.Length];
            for (int i = 0; i < hu.Length; i++)
            {
                // Simplified intracranial mask: not bone (HU > 100 is considered bone-like)
                // More sophisticated approaches would use connected component analysis
                mask[i] = hu[i] <= 100;
            }
            return mask;
        }
    }
}
