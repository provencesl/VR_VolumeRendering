using UnityEngine;
using UnityEditor;
using UnityVolumeRendering;

/// <summary>
/// Initializes the DensityPrioritySystem asset in Resources folder
/// This ensures the system is available for runtime use
/// </summary>
public class DensityPrioritySystemInitializer
{
    [MenuItem("Medical Imaging/Initialize Density Priority System")]
    public static void InitializeDensityPrioritySystem()
    {
        string resourcePath = "Assets/Resources/DensityPrioritySystem.asset";
        
        // Check if asset already exists
        DensityPrioritySystem existingAsset = AssetDatabase.LoadAssetAtPath<DensityPrioritySystem>(resourcePath);
        
        if (existingAsset != null)
        {
            EditorUtility.DisplayDialog("Density Priority System", "DensityPrioritySystem asset already exists at:\n" + resourcePath, "OK");
            return;
        }
        
        // Create new instance
        DensityPrioritySystem system = ScriptableObject.CreateInstance<DensityPrioritySystem>();
        
        // Ensure Resources directory exists
        if (!System.IO.Directory.Exists("Assets/Resources"))
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
        }
        
        // Save asset
        AssetDatabase.CreateAsset(system, resourcePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Success", "DensityPrioritySystem initialized successfully at:\n" + resourcePath, "OK");
        EditorGUIUtility.PingObject(system);
    }

    [InitializeOnLoadMethod]
    private static void AutoInitializeOnLoad()
    {
        // Auto-initialize on first load if not present
        DensityPrioritySystem existingAsset = Resources.Load<DensityPrioritySystem>("DensityPrioritySystem");
        
        if (existingAsset == null)
        {
            // Create it silently on first load
            string resourcePath = "Assets/Resources/DensityPrioritySystem.asset";
            
            if (!System.IO.Directory.Exists("Assets/Resources"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
            }
            
            DensityPrioritySystem system = ScriptableObject.CreateInstance<DensityPrioritySystem>();
            AssetDatabase.CreateAsset(system, resourcePath);
            AssetDatabase.SaveAssets();
        }
    }
}
