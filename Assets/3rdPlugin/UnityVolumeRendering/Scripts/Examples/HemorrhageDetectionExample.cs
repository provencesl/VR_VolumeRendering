using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityVolumeRendering;
using VolumeProcessing;

namespace Examples
{
    /// <summary>
    /// Example script demonstrating how to use the hemorrhage detection and visualization system
    /// Attach this to a GameObject in your scene along with a VolumeRenderedObject
    /// </summary>
    public class HemorrhageDetectionExample : MonoBehaviour
    {
        [Header("Volume Rendering Setup")]
        public VolumeRenderedObject volumeRenderer;
        public string datasetPath = "DataFiles/VisMale.raw"; // Adjust path as needed

        [Header("Hemorrhage Detection Settings")]
        public float minHUThreshold = 55f;
        public float maxHUThreshold = 90f;
        public int minVoxelCount = 10;

        [Header("Visualization Settings")]
        public bool enableVisualization = true;
        public Color hemorrhageColor = new Color(1.0f, 0.0f, 0.0f, 0.8f);
        public float hemorrhageIntensity = 0.8f;

        private HemorrhageManager hemorrhageManager;
        private bool isInitialized = false;

        void Start()
        {
            StartCoroutine(InitializeHemorrhageDetection());
        }

        /// <summary>
        /// Initialize the volume rendering and hemorrhage detection system
        /// </summary>
        private IEnumerator InitializeHemorrhageDetection()
        {
            // Wait for volume renderer to be ready
            yield return new WaitUntil(() => volumeRenderer != null && volumeRenderer.dataset != null);

            Debug.Log("HemorrhageDetectionExample: Volume renderer ready, initializing hemorrhage detection...");

            // Create and configure hemorrhage manager
            hemorrhageManager = gameObject.AddComponent<HemorrhageManager>();
            hemorrhageManager.volumeRenderer = volumeRenderer;

            // Set detection parameters
            hemorrhageManager.minHUThreshold = minHUThreshold;
            hemorrhageManager.maxHUThreshold = maxHUThreshold;
            hemorrhageManager.minVoxelCount = minVoxelCount;

            // Set visualization parameters
            hemorrhageManager.visualizationEnabled = enableVisualization;
            hemorrhageManager.hemorrhageColor = hemorrhageColor;
            hemorrhageManager.hemorrhageIntensity = hemorrhageIntensity;

            // The manager will automatically detect and visualize hemorrhage in Start()
            yield return new WaitForSeconds(1f); // Give time for detection to complete

            // Log results
            hemorrhageManager.LogDetectionResults();

            isInitialized = true;
            Debug.Log("HemorrhageDetectionExample: Initialization complete!");
        }

        void Update()
        {
            // Example: Toggle visualization with Space key
            if (Input.GetKeyDown(KeyCode.Space) && isInitialized)
            {
                hemorrhageManager.ToggleVisualization();
                Debug.Log($"Hemorrhage visualization: {(hemorrhageManager.visualizationEnabled ? "ON" : "OFF")}");
            }

            // Example: Re-detect with R key
            if (Input.GetKeyDown(KeyCode.R) && isInitialized)
            {
                Debug.Log("Re-detecting hemorrhage...");
                hemorrhageManager.RedetectHemorrhage();
            }

            // Example: Adjust thresholds with arrow keys
            if (isInitialized)
            {
                bool parametersChanged = false;

                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    minHUThreshold = Mathf.Min(minHUThreshold + 5f, 100f);
                    parametersChanged = true;
                }
                else if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    minHUThreshold = Mathf.Max(minHUThreshold - 5f, 0f);
                    parametersChanged = true;
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    maxHUThreshold = Mathf.Min(maxHUThreshold + 5f, 150f);
                    parametersChanged = true;
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    maxHUThreshold = Mathf.Max(maxHUThreshold - 5f, minHUThreshold + 10f);
                    parametersChanged = true;
                }

                if (parametersChanged)
                {
                    hemorrhageManager.SetDetectionParameters(minHUThreshold, maxHUThreshold, minVoxelCount);
                    Debug.Log($"Updated HU thresholds: [{minHUThreshold}-{maxHUThreshold}]");
                }
            }
        }

        /// <summary>
        /// Example method to programmatically check for hemorrhage
        /// </summary>
        public void CheckForHemorrhage()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("Hemorrhage detection not yet initialized");
                return;
            }

            bool hasHemorrhage = hemorrhageManager.HasHemorrhage();
            var stats = hemorrhageManager.GetHemorrhageStatistics();

            if (hasHemorrhage)
            {
                Debug.Log($"Hemorrhage detected! Voxels: {stats["total_voxels"]}, Volume: {stats["volume_mm3"]} mm³");
            }
            else
            {
                Debug.Log("No hemorrhage detected in the current dataset");
            }
        }

        /// <summary>
        /// Example method to export hemorrhage mask for further analysis
        /// </summary>
        public void ExportHemorrhageMask()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("Hemorrhage detection not yet initialized");
                return;
            }

            VolumeDataset mask = hemorrhageManager.GetHemorrhageMask();
            if (mask != null)
            {
                // In a real application, you might save this to disk or pass to other analysis tools
                Debug.Log($"Hemorrhage mask exported: {mask.dimX}x{mask.dimY}x{mask.dimZ} voxels");
            }
        }

        void OnGUI()
        {
            if (!isInitialized)
            {
                GUI.Label(new Rect(10, 10, 300, 20), "Initializing hemorrhage detection...");
                return;
            }

            // Display status information
            GUI.Label(new Rect(10, 10, 400, 20), $"Hemorrhage Detection Active - Press SPACE to toggle visualization");
            GUI.Label(new Rect(10, 30, 400, 20), $"Press R to re-detect, Arrow keys to adjust HU thresholds");
            GUI.Label(new Rect(10, 50, 400, 20), $"Current HU Range: [{minHUThreshold:F0}-{maxHUThreshold:F0}]");

            bool hasHemorrhage = hemorrhageManager.HasHemorrhage();
            GUI.color = hasHemorrhage ? Color.red : Color.green;
            GUI.Label(new Rect(10, 70, 300, 20), $"Status: {(hasHemorrhage ? "HEMORRHAGE DETECTED" : "No Hemorrhage")}");

            if (hasHemorrhage)
            {
                var stats = hemorrhageManager.GetHemorrhageStatistics();
                GUI.Label(new Rect(10, 90, 300, 20), $"Voxels: {stats["total_voxels"]:F0}, Volume: {stats["volume_mm3"]:F1} mm³");
            }
        }
    }
}
