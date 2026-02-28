using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for hemorrhage segmentation
    /// </summary>
    public static class HemorrhageSegmenter
    {
        /// <summary>
        /// Detect hemorrhage regions from HU data and intracranial mask
        /// </summary>
        /// <param name="hu">HU volume data</param>
        /// <param name="intracranial">Intracranial mask</param>
        /// <param name="dataset">Volume dataset</param>
        /// <returns>LabelVolume containing hemorrhage mask</returns>
        public static LabelVolume Detect(float[] hu, bool[] intracranial, VolumeDataset dataset)
        {
            var label = new LabelVolume(dataset.dimX, dataset.dimY, dataset.dimZ);

            for (int i = 0; i < hu.Length; i++)
            {
                if (!intracranial[i]) continue;

                // Acute hemorrhage HU range: 50-90
                if (hu[i] >= 50 && hu[i] <= 90)
                {
                    label.mask[i] = 1;
                }
            }

            return label;
        }
    }
}
