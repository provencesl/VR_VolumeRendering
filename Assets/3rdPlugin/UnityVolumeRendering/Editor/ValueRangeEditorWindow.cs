using UnityEditor;
using UnityEngine;


namespace UnityVolumeRendering
{
    public class ValueRangeEditorWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            ValueRangeEditorWindow wnd = new ValueRangeEditorWindow();
            wnd.Show();
        }

        private void OnGUI()
        {
            // Update selected object
            VolumeRenderedObject volRendObject = SelectionHelper.GetSelectedVolumeObject();
            if (volRendObject == null)
                volRendObject = GameObject.FindObjectOfType<VolumeRenderedObject>();
            if (volRendObject == null)
                return;

            EditorGUILayout.LabelField("Edit the visible value range (min/max value) with the slider.");
            EditorGUILayout.Space();

            Vector2 visibilityWindow = volRendObject.GetVisibilityWindow();

            // MinMaxSlider
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Visible value range", GUILayout.Width(120));
            EditorGUILayout.MinMaxSlider(ref visibilityWindow.x, ref visibilityWindow.y, 0.0f, 1.0f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 显示和编辑Min/Max数值
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Min:", GUILayout.Width(40));
            float newMin = EditorGUILayout.FloatField(visibilityWindow.x, GUILayout.Width(80));
            
            GUILayout.FlexibleSpace();
            
            GUILayout.Label("Max:", GUILayout.Width(40));
            float newMax = EditorGUILayout.FloatField(visibilityWindow.y, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // 限制范围并更新
            visibilityWindow.x = Mathf.Clamp(newMin, 0.0f, 1.0f);
            visibilityWindow.y = Mathf.Clamp(newMax, 0.0f, 1.0f);
            
            // 确保min <= max
            if (visibilityWindow.x > visibilityWindow.y)
            {
                float temp = visibilityWindow.x;
                visibilityWindow.x = visibilityWindow.y;
                visibilityWindow.y = temp;
            }

            // 显示当前范围
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox($"Range: {visibilityWindow.x:F6} - {visibilityWindow.y:F6}", MessageType.Info);

            volRendObject.SetVisibilityWindow(visibilityWindow);
        }
    }
}