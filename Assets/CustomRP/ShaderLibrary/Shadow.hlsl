#ifndef CUSTOM_SHADOW_INCLUDED
#define CUSTOM_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadow)
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4 _ShadowDistanceFade;
    float4 _ShadowAtlasSize;
    int _CascadeCullingSphereCount;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int titleIndex;
    float normalBias;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
    float cascadeBlend;
};

float SampleDirectionShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FadeShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData shadowData;
    shadowData.strength = 0.0;
    shadowData.cascadeBlend = 1.0;
    
    int i;
    for(i = 0; i < _CascadeCullingSphereCount; i++)
    {
        float distanceSquared = DistanceSquared(surfaceWS.position, _CascadeCullingSpheres[i].xyz);
        if(distanceSquared < _CascadeCullingSpheres[i].w)
        {
            shadowData.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
            if(i == _CascadeCullingSphereCount - 1)
            {
                shadowData.strength *= FadeShadowStrength(distanceSquared, _CascadeData[i].x, _ShadowDistanceFade.z);
            }
            else
            {
                shadowData.cascadeBlend = FadeShadowStrength(distanceSquared, _CascadeData[i].x, _ShadowDistanceFade.z);
                #if defined(_CASCADE_BLEND_DITHER)
                    if(shadowData.cascadeBlend < surfaceWS.dither)
                    {
                        i += 1;
                    }
                #endif
            }
            break;
        }
    }
    #if !defined(_CASCADE_BLEND_SOFT)
        shadowData.cascadeBlend = 1.0;
    #endif
    shadowData.cascadeIndex = i;
    
    return shadowData;
}

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0.0;
        for(int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleDirectionShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
        return shadow;
    #else
        return SampleDirectionShadowAtlas(positionSTS);
    #endif
}

float GetDirectionalShadowAttenuation(DirectionalShadowData data, ShadowData shadowData, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return  1.0;
    #endif
    
    if(data.strength <= 0.0)
    {
        return 1.0;
    }

    float3 positionSTS = mul(_DirectionalShadowMatrices[data.titleIndex], float4(surfaceWS.position + surfaceWS.normal * _CascadeData[shadowData.cascadeIndex].y * data.normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);

    if(shadowData.cascadeBlend < 1.0)
    {
        positionSTS = mul(_DirectionalShadowMatrices[data.titleIndex + 1], float4(surfaceWS.position + surfaceWS.normal * _CascadeData[shadowData.cascadeIndex + 1].y * data.normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, shadowData.cascadeBlend);
    }
    
    return lerp(1.0, shadow, data.strength);
}

#endif