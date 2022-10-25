#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig
{
    float2 baseUV;
    float2 detailUV;
    bool useMask;
    bool useDetail;
};

InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0)
{
    InputConfig inputConfig;
    inputConfig.baseUV = baseUV;
    inputConfig.detailUV = detailUV;
    inputConfig.useMask = false;
    inputConfig.useDetail = false;

    #if defined(_MASK_MAP)
        inputConfig.useMask = true;
    #endif

    #if defined(_DETAIL_MAP)
        inputConfig.useDetail = true;
    #endif
    
    return inputConfig;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV.xy * baseST.xy + baseST.zw;
}

float2 TranformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float4 GetMask(InputConfig inputConfig)
{
    if(inputConfig.useMask)
    {
        return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, inputConfig.baseUV);
    }
    return 1.0;
}

float4 GetDetail(InputConfig inputConfig)
{
    if(inputConfig.useDetail)
    {
        float4 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, inputConfig.detailUV);
        return detail * 2.0 - 1.0;
    }
    return 0.0;
}

float4 GetBase(InputConfig inputConfig)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, inputConfig.baseUV);
    float4 color = INPUT_PROP(_BaseColor);
    if(inputConfig.useDetail)
    {
        float mask = GetMask(inputConfig).b;
        float detail = GetDetail(inputConfig).r * INPUT_PROP(_DetailAlbedo);
        map.rgb = lerp(sqrt(map.rgb), detail > 0.0 ? 1.0 : 0.0, abs(detail) * mask);
        map.rgb *= map.rgb;
    }
    return map * color;
}

float3 GetEmission(InputConfig inputConfig)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, inputConfig.baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float3 GetNormalTS(InputConfig inputConfig)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, inputConfig.baseUV);
    float scale = INPUT_PROP(_NormalScale);
    float3 normal = DecodeNormal(map, scale);

    if(inputConfig.useDetail)
    {
        float mask = GetMask(inputConfig);
        map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, inputConfig.detailUV);
        scale = INPUT_PROP(_DetailNormalScale);
        float3 detailNormal = DecodeNormal(map, scale * mask);
        normal = BlendNormalRNM(normal, detailNormal);
    }
    
    return normal;
}

float GetCutOff(InputConfig inputConfig)
{
    return INPUT_PROP(_CutOff);
}

float GetMetallic(InputConfig inputConfig)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(inputConfig).r;
    return metallic;
}

float GetSmoothness(InputConfig inputConfig)
{
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(inputConfig).a;

    if(inputConfig.useDetail)
    {
        float mask = GetMask(inputConfig).b;
        float detail = GetDetail(inputConfig).b * INPUT_PROP(_DetailSmoothness);
        smoothness = lerp(smoothness, detail > 0.0 ? 1.0 : 0.0, abs(detail) * mask);   
    }

    return smoothness;
}

float GetFresnel(InputConfig inputConfig)
{
    return INPUT_PROP(_Fresnel);
}

float GetOcclusion(InputConfig inputConfig)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(inputConfig).g;
    return lerp(occlusion, 1.0, strength);
}

#endif