using UnityEngine;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Static utility class for morphological operations
    /// </summary>
    public static class MorphologyProcessor
    {
        /// <summary>
        /// Apply morphological closing (dilation followed by erosion)
        /// </summary>
        /// <param name="src">Source label volume</param>
        /// <param name="dataset">Volume dataset</param>
        /// <param name="radius">Morphological operation radius</param>
        /// <returns>Processed label volume</returns>
        public static LabelVolume Close(LabelVolume src, VolumeDataset dataset, int radius)
        {
            var dilated = Dilate(src, dataset, radius);
            return Erode(dilated, dataset, radius);
        }

        /// <summary>
        /// Apply morphological dilation
        /// </summary>
        /// <param name="src">Source label volume</param>
        /// <param name="dataset">Volume dataset</param>
        /// <param name="radius">Dilation radius</param>
        /// <returns>Dilated label volume</returns>
        public static LabelVolume Dilate(LabelVolume src, VolumeDataset dataset, int radius)
        {
            var result = new LabelVolume(src.width, src.height, src.depth);

            for (int z = 0; z < src.depth; z++)
            {
                for (int y = 0; y < src.height; y++)
                {
                    for (int x = 0; x < src.width; x++)
                    {
                        if (src.GetMask(x, y, z) == 1)
                        {
                            // Dilate: set all neighbors within radius to 1
                            for (int dz = -radius; dz <= radius; dz++)
                            {
                                for (int dy = -radius; dy <= radius; dy++)
                                {
                                    for (int dx = -radius; dx <= radius; dx++)
                                    {
                                        int nx = x + dx;
                                        int ny = y + dy;
                                        int nz = z + dz;

                                        if (nx >= 0 && nx < src.width &&
                                            ny >= 0 && ny < src.height &&
                                            nz >= 0 && nz < src.depth)
                                        {
                                            result.SetMask(nx, ny, nz, 1);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Apply morphological erosion
        /// </summary>
        /// <param name="src">Source label volume</param>
        /// <param name="dataset">Volume dataset</param>
        /// <param name="radius">Erosion radius</param>
        /// <returns>Eroded label volume</returns>
        public static LabelVolume Erode(LabelVolume src, VolumeDataset dataset, int radius)
        {
            var result = new LabelVolume(src.width, src.height, src.depth);

            for (int z = 0; z < src.depth; z++)
            {
                for (int y = 0; y < src.height; y++)
                {
                    for (int x = 0; x < src.width; x++)
                    {
                        bool keep = true;

                        // Check if all neighbors within radius are also 1
                        for (int dz = -radius; dz <= radius && keep; dz++)
                        {
                            for (int dy = -radius; dy <= radius && keep; dy++)
                            {
                                for (int dx = -radius; dx <= radius && keep; dx++)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    int nz = z + dz;

                                    if (nx >= 0 && nx < src.width &&
                                        ny >= 0 && ny < src.height &&
                                        nz >= 0 && nz < src.depth)
                                    {
                                        if (src.GetMask(nx, ny, nz) == 0)
                                        {
                                            keep = false;
                                        }
                                    }
                                }
                            }
                        }

                        if (keep)
                        {
                            result.SetMask(x, y, z, 1);
                        }
                    }
                }
            }

            return result;
        }
    }
}
