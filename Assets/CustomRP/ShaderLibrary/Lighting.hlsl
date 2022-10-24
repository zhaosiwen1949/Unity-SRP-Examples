#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight(Surface surfaceWS, Light light)
{
    return saturate((dot(surfaceWS.normal, light.direction))) * light.attenuation * light.color;
}

float3 GetLighting(Surface surfaceWS, Light light, BRDF brdf)
{
    return IncomingLight(surfaceWS, light) * DirectBRDF(surfaceWS, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    // 注意：这会让金属度较高的物体，不受GI效果，可以根据 Roughness 增加 Specular 的权重
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    for(int i = 0; i < GetDirectionalLightCount(); i++)
    {
        color += GetLighting(surfaceWS, GetDirectionalLight(i, surfaceWS, shadowData), brdf);
    }
    return color;
}

#endif
