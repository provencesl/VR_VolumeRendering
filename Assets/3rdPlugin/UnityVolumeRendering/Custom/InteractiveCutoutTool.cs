using UnityEngine;
using System.Collections.Generic;

namespace UnityVolumeRendering
{
    /// <summary>
    /// 交互式切割工具 - 替代BoxCutout的更直观方案
    /// </summary>
    [ExecuteInEditMode]
    public class InteractiveCutoutTool : MonoBehaviour
    {
        [Header("目标对象")]
        [SerializeField]
        private VolumeRenderedObject volumeObject;

        [Header("切割模式")]
        [SerializeField]
        private CutoutMode cutoutMode = CutoutMode.KeepInside;

        public enum CutoutMode
        {
            KeepInside,  // 只显示框内部分（查看特定器官）
            KeepOutside  // 只显示框外部分（移除特定区域）
        }

        [Header("边界框设置")]
        [SerializeField]
        private Vector3 boxCenter = Vector3.zero;

        [SerializeField]
        private Vector3 boxSize = Vector3.one * 0.5f;

        [SerializeField]
        private bool showWireframe = true;

        [SerializeField]
        private Color wireframeColor = new Color(0f, 1f, 1f, 0.8f);

        [SerializeField]
        [Range(0f, 1f)]
        private float wireframeThickness = 0.02f;

        [Header("可视化辅助")]
        [SerializeField]
        private bool showVolumeGhost = true; // 显示半透明的完整体数据

        [SerializeField]
        [Range(0f, 1f)]
        private float ghostOpacity = 0.2f;

        [Header("快速预设")]
        [SerializeField]
        private OrganPreset organPreset = OrganPreset.Custom;

        public enum OrganPreset
        {
            Custom,
            Head,           // 头部
            Chest,          // 胸部
            Heart,          // 心脏
            Abdomen,        // 腹部
            LeftLung,       // 左肺
            RightLung,      // 右肺
            Spine,          // 脊柱
            LeftArm,        // 左臂
            RightArm,       // 右臂
            LeftLeg,        // 左腿
            RightLeg        // 右腿
        }

        private GameObject wireframeObject;
        private Material cutoutMaterial;
        private Material ghostMaterial;
        private GameObject ghostObject;

        private void OnEnable()
        {
            if (volumeObject == null)
                volumeObject = GetComponentInParent<VolumeRenderedObject>();

            CreateWireframe();
            ApplyCutout();

            if (showVolumeGhost)
                CreateGhostVolume();
        }

        private void OnDisable()
        {
            RemoveCutout();
            DestroyWireframe();
            DestroyGhostVolume();
        }

        private void Update()
        {
            UpdateWireframe();
            ApplyCutout();

            if (showVolumeGhost)
            {
                if (ghostObject == null)
                    CreateGhostVolume();
                UpdateGhostVolume();
            }
            else if (ghostObject != null)
            {
                DestroyGhostVolume();
            }
        }

        /// <summary>
        /// 应用切割效果到Shader
        /// </summary>
        private void ApplyCutout()
        {
            if (volumeObject == null) return;

            Material[] materials = volumeObject.meshRenderer.sharedMaterials;
            foreach (Material mat in materials)
            {
                if (mat == null) continue;

                // 启用切割
                mat.EnableKeyword("CUTOUT_BOX");

                // 计算世界空间边界
                Vector3 worldCenter = transform.TransformPoint(boxCenter);
                Vector3 worldMin = worldCenter - boxSize * 0.5f;
                Vector3 worldMax = worldCenter + boxSize * 0.5f;

                // 转换到体数据本地空间
                Matrix4x4 volumeMatrix = volumeObject.transform.worldToLocalMatrix;
                Vector3 localMin = volumeMatrix.MultiplyPoint3x4(worldMin);
                Vector3 localMax = volumeMatrix.MultiplyPoint3x4(worldMax);

                // 归一化到[0,1]空间（体数据纹理坐标）
                Vector3 normalizedMin = localMin + Vector3.one * 0.5f;
                Vector3 normalizedMax = localMax + Vector3.one * 0.5f;

                // 传递到Shader
                mat.SetVector("_CutoutBoxMin", normalizedMin);
                mat.SetVector("_CutoutBoxMax", normalizedMax);
                mat.SetInt("_CutoutMode", (int)cutoutMode);
            }
        }

        private void RemoveCutout()
        {
            if (volumeObject == null) return;

            Material[] materials = volumeObject.meshRenderer.sharedMaterials;
            foreach (Material mat in materials)
            {
                if (mat != null)
                    mat.DisableKeyword("CUTOUT_BOX");
            }
        }

        /// <summary>
        /// 创建可视化线框
        /// </summary>
        private void CreateWireframe()
        {
            if (wireframeObject != null) return;

            wireframeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wireframeObject.name = "CutoutWireframe";
            wireframeObject.transform.SetParent(transform);

            //DestroyImmediate(wireframeObject.GetComponent<Collider>());

            MeshRenderer renderer = wireframeObject.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("VolumeRendering/CutoutWireframe"));
            mat.SetColor("_WireframeColor", wireframeColor);
            mat.SetFloat("_Thickness", wireframeThickness);
            renderer.material = mat;

            UpdateWireframe();
        }

        private void UpdateWireframe()
        {
            if (wireframeObject == null) return;

            wireframeObject.SetActive(showWireframe);
            wireframeObject.transform.localPosition = boxCenter;
            wireframeObject.transform.localScale = boxSize;

            if (wireframeObject.GetComponent<MeshRenderer>().material != null)
            {
                wireframeObject.GetComponent<MeshRenderer>().material.SetColor("_WireframeColor", wireframeColor);
                wireframeObject.GetComponent<MeshRenderer>().material.SetFloat("_Thickness", wireframeThickness);
            }
        }

        private void DestroyWireframe()
        {
            if (wireframeObject != null)
            {
                DestroyImmediate(wireframeObject);
                wireframeObject = null;
            }
        }

        /// <summary>
        /// 创建半透明的完整体数据（幽灵视图）
        /// </summary>
        private void CreateGhostVolume()
        {
            if (volumeObject == null || ghostObject != null) return;

            ghostObject = new GameObject("VolumeGhost");
            ghostObject.transform.SetParent(volumeObject.transform);
            ghostObject.transform.localPosition = Vector3.zero;
            ghostObject.transform.localRotation = Quaternion.identity;
            ghostObject.transform.localScale = Vector3.one;

            MeshFilter meshFilter = ghostObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = volumeObject.GetComponent<MeshFilter>().sharedMesh;

            MeshRenderer renderer = ghostObject.AddComponent<MeshRenderer>();
            ghostMaterial = new Material(volumeObject.meshRenderer.sharedMaterial);
            ghostMaterial.SetFloat("_Alpha", ghostOpacity);
            renderer.material = ghostMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void UpdateGhostVolume()
        {
            if (ghostObject == null || ghostMaterial == null) return;

            ghostMaterial.SetFloat("_Alpha", ghostOpacity);
        }

        private void DestroyGhostVolume()
        {
            if (ghostObject != null)
            {
                DestroyImmediate(ghostObject);
                ghostObject = null;
            }
            if (ghostMaterial != null)
            {
                DestroyImmediate(ghostMaterial);
                ghostMaterial = null;
            }
        }

        /// <summary>
        /// 应用器官预设
        /// </summary>
        public void ApplyOrganPreset(OrganPreset preset)
        {
            organPreset = preset;

            switch (preset)
            {
                case OrganPreset.Head:
                    boxCenter = new Vector3(0f, 0.35f, 0f);
                    boxSize = new Vector3(0.4f, 0.3f, 0.4f);
                    break;

                case OrganPreset.Chest:
                    boxCenter = new Vector3(0f, 0.1f, 0f);
                    boxSize = new Vector3(0.5f, 0.3f, 0.4f);
                    break;

                case OrganPreset.Heart:
                    boxCenter = new Vector3(0.05f, 0.05f, 0.1f);
                    boxSize = new Vector3(0.2f, 0.2f, 0.2f);
                    break;

                case OrganPreset.Abdomen:
                    boxCenter = new Vector3(0f, -0.15f, 0f);
                    boxSize = new Vector3(0.45f, 0.25f, 0.35f);
                    break;

                case OrganPreset.LeftLung:
                    boxCenter = new Vector3(0.15f, 0.1f, 0f);
                    boxSize = new Vector3(0.2f, 0.3f, 0.25f);
                    break;

                case OrganPreset.RightLung:
                    boxCenter = new Vector3(-0.15f, 0.1f, 0f);
                    boxSize = new Vector3(0.2f, 0.3f, 0.25f);
                    break;

                case OrganPreset.Spine:
                    boxCenter = new Vector3(0f, 0f, -0.1f);
                    boxSize = new Vector3(0.15f, 0.8f, 0.15f);
                    break;

                case OrganPreset.LeftArm:
                    boxCenter = new Vector3(0.35f, 0.05f, 0f);
                    boxSize = new Vector3(0.2f, 0.6f, 0.2f);
                    break;

                case OrganPreset.RightArm:
                    boxCenter = new Vector3(-0.35f, 0.05f, 0f);
                    boxSize = new Vector3(0.2f, 0.6f, 0.2f);
                    break;

                case OrganPreset.LeftLeg:
                    boxCenter = new Vector3(0.15f, -0.45f, 0f);
                    boxSize = new Vector3(0.2f, 0.5f, 0.2f);
                    break;

                case OrganPreset.RightLeg:
                    boxCenter = new Vector3(-0.15f, -0.45f, 0f);
                    boxSize = new Vector3(0.2f, 0.5f, 0.2f);
                    break;
            }

            ApplyCutout();
        }

        /// <summary>
        /// 切换切割模式
        /// </summary>
        public void ToggleCutoutMode()
        {
            cutoutMode = (cutoutMode == CutoutMode.KeepInside)
                ? CutoutMode.KeepOutside
                : CutoutMode.KeepInside;
            ApplyCutout();
        }

        /// <summary>
        /// 重置到完整视图
        /// </summary>
        public void ResetToFullView()
        {
            boxCenter = Vector3.zero;
            boxSize = Vector3.one;
            organPreset = OrganPreset.Custom;
            ApplyCutout();
        }

        // Gizmos绘制
        private void OnDrawGizmos()
        {
            if (!showWireframe) return;

            Gizmos.color = wireframeColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCenter, boxSize);
        }
    }
}