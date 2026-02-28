using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering.Segmentation;

namespace UnityVolumeRendering
{
    /// <summary>
    /// Main manager for deriving segments from CT volume data, similar to 3D Slicer's Segment Editor.
    /// This class coordinates the processing pipeline to create label volumes and meshes from CT data.
    /// </summary>
    public class VolumeDerivationManager
    {
        /// <summary>
        /// Source CT volume dataset
        /// </summary>
        private VolumeDataset ctDataset;

        /// <summary>
        /// HU-converted volume data
        /// </summary>
        private float[] huVolume;

        /// <summary>
        /// Generated hemorrhage segment
        /// </summary>
        public Segment hemorrhageSegment;

        /// <summary>
        /// Build segments from CT dataset
        /// </summary>
        /// <param name="dataset">Source CT volume dataset</param>
        public void BuildFromCT(VolumeDataset dataset)
        {
            ctDataset = dataset;
            huVolume = VolumeHUConverter.ToHU(dataset);

            // Build intracranial mask
            bool[] intracranialMask = IntracranialMaskBuilder.Build(huVolume, dataset);

            // Detect hemorrhage
            LabelVolume hemorrhageMask = HemorrhageSegmenter.Detect(
                huVolume, intracranialMask, dataset);

            // Keep only largest connected component
            hemorrhageMask = ConnectedComponentAnalyzer.KeepLargest(hemorrhageMask, dataset);

            // Apply morphological closing
            hemorrhageMask = MorphologyProcessor.Close(hemorrhageMask, dataset, radius: 2);

            // Generate mesh
            Mesh mesh = SegmentMeshBuilder.BuildMesh(hemorrhageMask, dataset);

            // Create segment
            hemorrhageSegment = new Segment(SegmentType.Hemorrhage, hemorrhageMask, mesh);
        }

        /// <summary>
        /// Get the source CT dataset
        /// </summary>
        /// <returns>CT volume dataset</returns>
        public VolumeDataset GetCTDataset()
        {
            return ctDataset;
        }

        /// <summary>
        /// Get HU volume data
        /// </summary>
        /// <returns>HU-converted volume array</returns>
        public float[] GetHUVolume()
        {
            return huVolume;
        }
    }
}
