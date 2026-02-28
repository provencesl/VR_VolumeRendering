using UnityEngine;

namespace UnityVolumeRendering
{
    [ExecuteInEditMode]
    public class SlicingPlane : MonoBehaviour
    {
        public VolumeRenderedObject targetObject;
        private MeshRenderer meshRenderer;

        // 切片渲染输出
        public RenderTexture sliceRT; // 切片RenderTexture
        private Material sliceMaterial; // 切片渲染材质

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            // 创建切片RenderTexture
            int textureSize = 512;
            sliceRT = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            sliceRT.Create();

            // 创建切片渲染材质（使用SliceRenderingShader）
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                sliceMaterial = new Material(Shader.Find("VolumeRendering/SliceRenderingShader"));
                // 复制原始材质的纹理和属性
                sliceMaterial.SetTexture("_DataTex", meshRenderer.sharedMaterial.GetTexture("_DataTex"));
                sliceMaterial.SetTexture("_TFTex", meshRenderer.sharedMaterial.GetTexture("_TFTex"));
            }
        }

        private void Update()
        {
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial.SetMatrix("_parentInverseMat", transform.parent.worldToLocalMatrix);
                meshRenderer.sharedMaterial.SetMatrix("_planeMat", transform.localToWorldMatrix);

                // 更新切片材质的矩阵
                if (sliceMaterial != null)
                {
                    sliceMaterial.SetMatrix("_parentInverseMat", transform.parent.worldToLocalMatrix);
                    sliceMaterial.SetMatrix("_planeMat", transform.localToWorldMatrix);
                }
            }
        }

        private void OnDestroy()
        {
            // 清理RenderTexture
            if (sliceRT != null)
            {
                sliceRT.Release();
                Destroy(sliceRT);
            }
            // 清理切片材质
            if (sliceMaterial != null)
            {
                Destroy(sliceMaterial);
            }
        }

        // 每帧Blit输出切片
        private void LateUpdate()
        {
            if (sliceMaterial != null && sliceRT != null)
            {
                Graphics.Blit(null, sliceRT, sliceMaterial);
            }
        }

        // 提供接口给UI获取切片纹理
        public Texture GetSliceTexture()
        {
            return sliceRT;
        }
    }
}
