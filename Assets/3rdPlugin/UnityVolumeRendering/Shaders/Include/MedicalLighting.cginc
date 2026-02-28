// Medical imaging lighting and edge detection
// Optimized for bone and hemorrhage visualization

#ifndef MEDICAL_LIGHTING_INCLUDED
#define MEDICAL_LIGHTING_INCLUDED

// Sobel edge detection kernel
float3 sobelEdgeDetection(sampler3D dataTex, float3 pos, float3 texelSize)
{
    // Sobel kernels for X, Y, Z gradients
    float gx = 0.0;
    float gy = 0.0;
    float gz = 0.0;

    // X-direction Sobel
    gx += -1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, -texelSize.z), 0)).r;
    gx += -2.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, 0, -texelSize.z), 0)).r;
    gx += -1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, texelSize.y, -texelSize.z), 0)).r;
    gx += 1.0 * tex3Dlod(dataTex, float4(pos + float3(texelSize.x, -texelSize.y, -texelSize.z), 0)).r;
    gx += 2.0 * tex3Dlod(dataTex, float4(pos + float3(texelSize.x, 0, -texelSize.z), 0)).r;
    gx += 1.0 * tex3Dlod(dataTex, float4(pos + float3(texelSize.x, texelSize.y, -texelSize.z), 0)).r;

    // Y-direction Sobel
    gy += -1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, -texelSize.z), 0)).r;
    gy += -2.0 * tex3Dlod(dataTex, float4(pos + float3(0, -texelSize.y, -texelSize.z), 0)).r;
    gy += -1.0 * tex3Dlod(dataTex, float4(pos + float3(texelSize.x, -texelSize.y, -texelSize.z), 0)).r;
    gy += 1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, texelSize.y, -texelSize.z), 0)).r;
    gy += 2.0 * tex3Dlod(dataTex, float4(pos + float3(0, texelSize.y, -texelSize.z), 0)).r;
    gy += 1.0 * tex3Dlod(dataTex, float4(pos + float3(texelSize.x, texelSize.y, -texelSize.z), 0)).r;

    // Z-direction Sobel
    gz += -1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, -texelSize.z), 0)).r;
    gz += -2.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, 0), 0)).r;
    gz += -1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, texelSize.z), 0)).r;
    gz += 1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, -texelSize.y, texelSize.z), 0)).r;
    gz += 2.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, 0, texelSize.z), 0)).r;
    gz += 1.0 * tex3Dlod(dataTex, float4(pos + float3(-texelSize.x, texelSize.y, texelSize.z), 0)).r;

    return normalize(float3(gx, gy, gz));
}

// Enhanced medical lighting with subsurface scattering
float3 medicalLighting(float3 col, float3 normal, float3 lightDir, float3 eyeDir, 
                       float specularIntensity, float density, float gradMag)
{
    // Invert normal if facing away from camera
    normal *= (step(0.0, dot(normal, eyeDir)) * 2.0 - 1.0);

    // Main lighting
    float ndotl = max(dot(normal, lightDir), 0.0);
    float3 diffuse = ndotl * col;
    
    // Adaptive ambient based on gradient magnitude (edge regions darker)
    float ambientFactor = 0.25 + 0.15 * (1.0 - saturate(gradMag / 2.0));
    float3 ambient = ambientFactor * col;
    
    // Specular highlight (stronger for bone)
    float3 v = eyeDir;
    float3 r = normalize(reflect(-lightDir, normal));
    float rdotv = max(dot(r, v), 0.0);
    float specularPower = 32.0 + 32.0 * density;  // Denser materials = sharper highlights
    float3 specular = pow(rdotv, specularPower) * float3(1.0, 1.0, 1.0) * specularIntensity;
    
    // Subsurface scattering for soft tissue
    float backLight = max(dot(normal, -lightDir), 0.0) * 0.3;
    float3 sss = backLight * col * 0.2;
    
    float3 result = diffuse + ambient + specular + sss;
    return float3(min(result.r, 1.0), min(result.g, 1.0), min(result.b, 1.0));
}

// Edge enhancement for fracture detection
float edgeEnhancement(float gradMag, float edgeThreshold, float edgeSharpness)
{
    // Enhance edges above threshold
    float edge = smoothstep(edgeThreshold - edgeSharpness, edgeThreshold + edgeSharpness, gradMag);
    return edge;
}

// Adaptive gradient threshold based on density
float adaptiveGradientThreshold(float density, float baseThreshold)
{
    // Bone regions (high density) need higher threshold
    // Soft tissue regions (low density) need lower threshold
    float densityNorm = saturate(density);
    return baseThreshold * (0.5 + 1.5 * densityNorm);
}

// Medical image contrast enhancement
float3 contrastEnhancement(float3 col, float contrast)
{
    // Apply contrast: (value - 0.5) * contrast + 0.5
    return (col - 0.5) * contrast + 0.5;
}

#endif // MEDICAL_LIGHTING_INCLUDED
