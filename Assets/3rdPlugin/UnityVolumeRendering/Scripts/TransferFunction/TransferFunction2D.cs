using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityVolumeRendering
{
    [Serializable]
    public class TransferFunction2D : ScriptableObject
    {
        [System.Serializable]
        public struct TF2DBox
        {
            public Color colour;
            public float alpha;
            public float minAlpha;
            public float minX;  // Normalized X (density/HU value)
            public float maxX;
            public float minY;  // Normalized Y (gradient magnitude)
            public float maxY;
            public Rect rect;   // Legacy support
        }

        [SerializeField]
        public List<TF2DBox> boxes = new List<TF2DBox>();

        private Texture2D texture = null;

        // Upgraded resolution for high-definition medical imaging
        private const int TEXTURE_WIDTH = 2048;
        private const int TEXTURE_HEIGHT = 2048;

        public void AddBox(float x, float y, float width, float height, Color colour, float alpha)
        {
            TF2DBox box = new TF2DBox();
            box.rect.x = x;
            box.rect.y = y;
            box.rect.width = width;
            box.rect.height = height;
            box.colour = colour;
            box.alpha = alpha;
            boxes.Add(box);
        }

        public Texture2D GetTexture()
        {
            if(texture == null)
                GenerateTexture();

            return texture;
        }

        private void CreateTexture()
        {
            TextureFormat texformat = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf) ? TextureFormat.RGBAHalf : TextureFormat.RGBAFloat;
            texture = new Texture2D(TEXTURE_WIDTH, TEXTURE_HEIGHT, texformat, false);
        }

        public void GenerateTexture()
        {
            if (texture == null)
                CreateTexture();

            Color[] cols = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            
            for (int iX = 0; iX < TEXTURE_WIDTH; iX++)
            {
                for (int iY = 0; iY < TEXTURE_HEIGHT; iY++)
                {
                    float normX = iX / (float)TEXTURE_WIDTH;
                    float normY = iY / (float)TEXTURE_HEIGHT;
                    
                    Color pixelColor = Color.clear;
                    float maxAlpha = 0.0f;

                    // Check all boxes and blend overlapping regions
                    foreach (TF2DBox box in boxes)
                    {
                        bool inBox = false;
                        
                        // Support both new (minX/maxX/minY/maxY) and legacy (rect) formats
                        if (box.minX != 0 || box.maxX != 0)
                        {
                            inBox = (normX >= box.minX && normX <= box.maxX && 
                                    normY >= box.minY && normY <= box.maxY);
                        }
                        else if (box.rect.width > 0)
                        {
                            inBox = box.rect.Contains(new Vector2(normX, normY));
                        }

                        if (inBox)
                        {
                            // Calculate alpha with smooth falloff at edges
                            float edgeFalloff = 1.0f;
                            float edgeMargin = 0.02f;
                            
                            float distToEdgeX = Mathf.Min(
                                Mathf.Abs(normX - box.minX),
                                Mathf.Abs(normX - box.maxX)
                            );
                            float distToEdgeY = Mathf.Min(
                                Mathf.Abs(normY - box.minY),
                                Mathf.Abs(normY - box.maxY)
                            );
                            
                            edgeFalloff = Mathf.Clamp01(Mathf.Min(distToEdgeX, distToEdgeY) / edgeMargin);
                            
                            float alpha = Mathf.Lerp(box.minAlpha, box.alpha, edgeFalloff);
                            
                            // Blend with existing color
                            if (alpha > maxAlpha)
                            {
                                pixelColor = new Color(box.colour.r, box.colour.g, box.colour.b, alpha);
                                maxAlpha = alpha;
                            }
                        }
                    }

                    cols[iX + iY * TEXTURE_WIDTH] = pixelColor;
                }
            }
            
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;  // Better filtering for high-res texture
            texture.SetPixels(cols);
            texture.Apply();
        }
    }

}
