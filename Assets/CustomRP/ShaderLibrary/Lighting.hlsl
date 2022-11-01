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

    // 方向光
    for(int i = 0; i < GetDirectionalLightCount(); i++)
    {
        color += GetLighting(surfaceWS, GetDirectionalLight(i, surfaceWS, shadowData), brdf);
    }

    // 其他光
    #if defined(_LIGHTS_PER_OBJECT)
        for(int j = 0; j < min(unity_LightData.y, 8); j++)
        {
            int index = unity_LightIndices[(uint) j / 4][(uint) j % 4];
            color += GetLighting(surfaceWS, GetOtherLight(index, surfaceWS, shadowData), brdf);
        }
    #else
        for(int j = 0; j < GetOtherLightCount(); j++)
        {
            color += GetLighting(surfaceWS, GetOtherLight(j, surfaceWS, shadowData), brdf);
        }
    #endif
    
    return color;
}

#endif
