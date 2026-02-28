Shader "VolumeRendering/MedicalImagePostProcessing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeThreshold ("Edge Detection Threshold", Range(0.0, 1.0)) = 0.3
        _EdgeIntensity ("Edge Intensity", Range(0.0, 2.0)) = 1.0
        _ContrastStrength ("Contrast Strength", Range(0.0, 2.0)) = 1.2
        _BrightnessOffset ("Brightness Offset", Range(-0.5, 0.5)) = 0.0
        _SaturationBoost ("Saturation Boost", Range(0.0, 2.0)) = 1.2
        _TonemapStrength ("Tonemap Strength (HDR)", Range(0.0, 2.0)) = 1.0
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
            
            float _EdgeThreshold;
            float _EdgeIntensity;
            float _ContrastStrength;
            float _BrightnessOffset;
            float _SaturationBoost;
            float _TonemapStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // Sobel edge detection
            float sobelEdgeDetection(float2 uv)
            {
                float texelSize = _MainTex_TexelSize.x;
                
                // Sobel kernels
                float gx = 0.0;
                float gy = 0.0;
                
                // Sample 3x3 neighborhood
                float samples[9];
                samples[0] = tex2D(_MainTex, uv + float2(-texelSize, -texelSize)).r;
                samples[1] = tex2D(_MainTex, uv + float2(0, -texelSize)).r;
                samples[2] = tex2D(_MainTex, uv + float2(texelSize, -texelSize)).r;
                samples[3] = tex2D(_MainTex, uv + float2(-texelSize, 0)).r;
                samples[4] = tex2D(_MainTex, uv).r;
                samples[5] = tex2D(_MainTex, uv + float2(texelSize, 0)).r;
                samples[6] = tex2D(_MainTex, uv + float2(-texelSize, texelSize)).r;
                samples[7] = tex2D(_MainTex, uv + float2(0, texelSize)).r;
                samples[8] = tex2D(_MainTex, uv + float2(texelSize, texelSize)).r;
                
                // Sobel X
                gx = samples[0] * -1.0 + samples[2] * 1.0 +
                     samples[3] * -2.0 + samples[5] * 2.0 +
                     samples[6] * -1.0 + samples[8] * 1.0;
                
                // Sobel Y
                gy = samples[0] * -1.0 + samples[6] * 1.0 +
                     samples[1] * -2.0 + samples[7] * 2.0 +
                     samples[2] * -1.0 + samples[8] * 1.0;
                
                float edge = sqrt(gx * gx + gy * gy);
                return edge;
            }

            // RGB to HSV conversion
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB conversion
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }

            // Reinhard tone mapping for HDR
            float3 reinhardTonemap(float3 color, float strength)
            {
                color *= strength;
                return color / (1.0 + color);
            }

            // ACES tone mapping (more cinematic)
            float3 acesTonemap(float3 x)
            {
                float a = 2.51;
                float b = 0.03;
                float c = 2.43;
                float d = 0.59;
                float e = 0.14;
                return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                
                // Edge detection
                float edge = sobelEdgeDetection(i.uv);
                edge = smoothstep(_EdgeThreshold - 0.1, _EdgeThreshold + 0.1, edge);
                
                // Apply edge enhancement
                float3 edgeColor = col.rgb + edge * _EdgeIntensity * float3(0.5, 0.5, 0.0);
                col.rgb = lerp(col.rgb, edgeColor, edge);
                
                // Contrast enhancement
                col.rgb = (col.rgb - 0.5) * _ContrastStrength + 0.5;
                
                // Brightness adjustment
                col.rgb += _BrightnessOffset;
                
                // Saturation boost
                float3 hsv = rgb2hsv(col.rgb);
                hsv.y *= _SaturationBoost;
                col.rgb = hsv2rgb(hsv);
                
                // Tone mapping for HDR effect
                col.rgb = acesTonemap(col.rgb * _TonemapStrength);
                
                return col;
            }
            ENDCG
        }
    }
}
