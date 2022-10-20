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
    float3 color = gi.diffuse * brdf.diffuse;
    for(int i = 0; i < GetDirectionalLightCount(); i++)
    {
        color += GetLighting(surfaceWS, GetDirectionalLight(i, surfaceWS, shadowData), brdf);
    }
    return color;
}

#endif
