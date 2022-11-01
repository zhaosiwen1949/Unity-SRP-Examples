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

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
SAMPLER(sampler_OtherShadowAtlas);

CBUFFER_START(_CustomShadow)
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _ShadowDistanceFade;
    float4 _ShadowAtlasSize;
    int _CascadeCullingSphereCount;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 spotDirectionWS;
};

struct ShadowMask
{
    float4 shadows;
    bool distance;
    bool always;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
    float cascadeBlend;
    ShadowMask shadowMask;
};

static const float3 pointShadowPlanes[6] = {
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0),
}; 

float SampleDirectionShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FadeShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData shadowData;
    shadowData.strength = 1.0; // 注意：该初始值是为了，在没有任何方向光不启用级联阴影时，不计算距离的衰减 Strength
    shadowData.cascadeBlend = 1.0;
    shadowData.shadowMask.shadows = 1.0;
    shadowData.shadowMask.distance = false;
    shadowData.shadowMask.always = false;
    
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

    if(i == _CascadeCullingSphereCount && _CascadeCullingSphereCount > 0)
    {
        // 启用了 Cascade 级联阴影，并且该像素点不在任何一级级联阴影里面
        shadowData.strength = 0.0;
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
        real weights[DIRECTIONAL_FILTER_SAMPLES];
        real2 positions[DIRECTIONAL_FILTER_SAMPLES];
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

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
    #if defined(OTHER_FILTER_SETUP)
        real weights[OTHER_FILTER_SAMPLES];
        real2 positions[OTHER_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.wwzz;
        OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0.0;
        for(int i = 0; i < OTHER_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
        }
        return shadow;
    #else
        return SampleOtherShadowAtlas(positionSTS, bounds);
    #endif
}

float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + surfaceWS.interpolatedNormal * _CascadeData[global.cascadeIndex].y * directional.normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);

    if(global.cascadeBlend < 1.0)
    {
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + surfaceWS.interpolatedNormal * _CascadeData[global.cascadeIndex + 1].y * directional.normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return shadow;
}

float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    float3 ray = other.lightPositionWS - surfaceWS.position;
    int tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    
    if(other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-ray);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    
    // 计算 normalBias
    float distanceToLightPlane = dot(ray, lightPlane);
    float4 titleData = _OtherShadowTiles[tileIndex];
    float normalBias = distanceToLightPlane * titleData.w;
    

    float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], float4(surfaceWS.position + surfaceWS.interpolatedNormal * normalBias, 1.0));

    return SampleOtherShadowAtlas(positionSTS.xyz / positionSTS.w, titleData.xyz);
}

float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if(mask.distance || mask.always)
    {
        shadow = mask.shadows[channel];
    }
    return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    float shadow = 1.0;
    if(mask.distance || mask.always)
    {
        shadow = lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return shadow;
}

float MixBakedAndRealtimeShadows(ShadowData global, float shadow, float strength, int channel)
{
    float baked = GetBakedShadow(global.shadowMask, channel);
    if(global.shadowMask.always)
    {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    else if(global.shadowMask.distance)
    {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    
    return lerp(1.0, shadow, strength * global.strength);
}

float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return  1.0;
    #endif
    
    if(directional.strength * global.strength <= 0.0)
    {
        return GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
    }

    float shadow = GetCascadedShadow(directional, global, surfaceWS);
    
    return MixBakedAndRealtimeShadows(global, shadow, directional.strength, directional.shadowMaskChannel);
}

float GetOtherShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return  1.0;
    #endif

    float shadow;
    if(other.strength * global.strength <= 0.0)
    {
        shadow = lerp(1.0, GetBakedShadow(global.shadowMask, other.shadowMaskChannel, other.strength), abs(other.strength));
    }
    else
    {
        shadow = GetOtherShadow(other, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow, other.strength, other.shadowMaskChannel);
    }

    return shadow;
}

#endif