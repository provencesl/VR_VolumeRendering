Shader "VolumeRendering/SimplifiedMedicalPostProcessing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeEnhance ("Edge Enhancement", Range(0.0, 1.0)) = 0.3
        _Contrast ("Contrast", Range(0.8, 1.2)) = 1.0
        _Brightness ("Brightness", Range(-0.1, 0.1)) = 0.0
        _Clarity ("Clarity (Mid-tone Contrast)", Range(0.0, 1.0)) = 0.2
        _Sharpness ("Sharpness", Range(0.0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            float _EdgeEnhance;
            float _Contrast;
            float _Brightness;
            float _Clarity;
            float _Sharpness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // Simple Sobel edge detection
            float sobelEdge(float2 uv)
            {
                float texelSize = _MainTex_TexelSize.x;
                
                float gx = 0.0;
                float gy = 0.0;
                
                // 3x3 samples
                float s[9];
                s[0] = tex2D(_MainTex, uv + float2(-texelSize, -texelSize)).r;
                s[1] = tex2D(_MainTex, uv + float2(0, -texelSize)).r;
                s[2] = tex2D(_MainTex, uv + float2(texelSize, -texelSize)).r;
                s[3] = tex2D(_MainTex, uv + float2(-texelSize, 0)).r;
                s[4] = tex2D(_MainTex, uv).r;
                s[5] = tex2D(_MainTex, uv + float2(texelSize, 0)).r;
                s[6] = tex2D(_MainTex, uv + float2(-texelSize, texelSize)).r;
                s[7] = tex2D(_MainTex, uv + float2(0, texelSize)).r;
                s[8] = tex2D(_MainTex, uv + float2(texelSize, texelSize)).r;
                
                gx = s[0] * -1.0 + s[2] * 1.0 + s[3] * -2.0 + s[5] * 2.0 + s[6] * -1.0 + s[8] * 1.0;
                gy = s[0] * -1.0 + s[6] * 1.0 + s[1] * -2.0 + s[7] * 2.0 + s[2] * -1.0 + s[8] * 1.0;
                
                return sqrt(gx * gx + gy * gy) * 0.1;  // Scale down for subtlety
            }

            // Unsharp mask for subtle sharpening
            float3 unsharpMask(float2 uv, float strength)
            {
                float texelSize = _MainTex_TexelSize.x * 2.0;
                
                float3 col = tex2D(_MainTex, uv).rgb;
                float3 blur = float3(0.0, 0.0, 0.0);
                
                blur += tex2D(_MainTex, uv + float2(-texelSize, -texelSize)).rgb * 0.0625;
                blur += tex2D(_MainTex, uv + float2(0, -texelSize)).rgb * 0.125;
                blur += tex2D(_MainTex, uv + float2(texelSize, -texelSize)).rgb * 0.0625;
                blur += tex2D(_MainTex, uv + float2(-texelSize, 0)).rgb * 0.125;
                blur += tex2D(_MainTex, uv).rgb * 0.25;
                blur += tex2D(_MainTex, uv + float2(texelSize, 0)).rgb * 0.125;
                blur += tex2D(_MainTex, uv + float2(-texelSize, texelSize)).rgb * 0.0625;
                blur += tex2D(_MainTex, uv + float2(0, texelSize)).rgb * 0.125;
                blur += tex2D(_MainTex, uv + float2(texelSize, texelSize)).rgb * 0.0625;
                
                return col + (col - blur) * strength;
            }

            // Clarity filter (mid-tone contrast)
            float3 clarity(float3 col, float strength)
            {
                // Simple clarity: enhance mid-tones
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float midTone = abs(lum - 0.5) * 2.0;  // 0 at 0.5, 1 at edges
                midTone = 1.0 - midTone;  // Invert: 1 at 0.5, 0 at edges
                
                col = col + (col - 0.5) * midTone * strength;
                return col;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                
                // Subtle edge enhancement
                float edge = sobelEdge(i.uv);
                col.rgb += edge * _EdgeEnhance;
                
                // Subtle contrast
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                
                // Brightness
                col.rgb += _Brightness;
                
                // Clarity (mid-tone contrast)
                col.rgb = clarity(col.rgb, _Clarity);
                
                // Subtle sharpening
                col.rgb = unsharpMask(i.uv, _Sharpness);
                
                // Clamp to valid range
                col.rgb = clamp(col.rgb, 0.0, 1.0);
                
                return col;
            }
            ENDCG
        }
    }
}
