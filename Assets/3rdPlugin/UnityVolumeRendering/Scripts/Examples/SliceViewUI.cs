using UnityEngine;
using UnityEngine.UI;

namespace UnityVolumeRendering
{
    /// <summary>
    /// 简单的切片查看UI
    /// 从绑定的SlicingPlane获取切面图片并显示在RawImage上
    /// </summary>
    public class SliceViewUI : MonoBehaviour
    {
        [Header("UI显示")]
        public RawImage sliceImage;

        [Header("绑定的切片平面")]
        public SlicingPlane slicingPlane;

        private Material sliceMat;
        private RenderTexture renderTexture;

        private VolumeRenderedObject volumeObject;

        void Start()
        {
           

            // if (slicingPlane == null)
            //     slicingPlane = FindObjectOfType<SlicingPlane>();

            volumeObject = FindObjectOfType<VolumeRenderedObject>();
            CreateXYPlane();
            if (slicingPlane == null)
                slicingPlane = FindObjectOfType<SlicingPlane>();
            if (volumeObject == null)
            {
                Debug.LogError("未找到VolumeRenderedObject!");
                return;
            }
            // CreateXYPlane();

            if (slicingPlane != null)
            {
                sliceMat = slicingPlane.GetComponent<MeshRenderer>().sharedMaterial;
                
                // 创建RenderTexture用于渲染切片
                int size = 512;
                renderTexture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                
                //
                Texture sliceTex = sliceMat.GetTexture("_DataTex");

                if (sliceTex != null)
                {
                    sliceImage.texture = sliceTex;
                }

                // 将RenderTexture作为sliceImage的纹理
                // sliceImage.texture = renderTexture;
            }
            else
            {
                Debug.LogError("未找到SlicingPlane!");
            }
        }

        private void CreateXYPlane()
        {
            if (volumeObject != null)
            {
                SlicingPlane plane = volumeObject.CreateSlicingPlane();
                // selectedPlaneIndex = FindObjectsOfType<SlicingPlane>().Length - 1;
                // UpdateSelectedPlane();
            }
        }



        void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        void Update()
        {
            if (sliceMat == null || renderTexture == null) return;

            // 使用Graphics.DrawTexture渲染切片（与SliceRenderingEditorWindow相同的方式）
            // Rect drawRect = new Rect(0, 0, renderTexture.width, renderTexture.height);
            // Graphics.DrawTexture(drawRect, sliceMat.GetTexture("_DataTex"), sliceMat);
        }
    }
}
