#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadow.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float3 normalOS: NORMAL;
    float4 tangentOS: TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    GI_ATTRIBUTE_DATA
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS: VAR_NORMAL;
    #if defined(_NORMAL_MAP)
        float4 tangentWS: VAR_TANGENT;
    #endif
    float2 baseUV : VAR_BASE_UV;
    #if defined(_DETAIL_MAP)
        float2 detailUV: VAR_DETAIL_UV;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    GI_VARINGS_DATA
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output)
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_NORMAL_MAP)
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif
    output.baseUV = TransformBaseUV(input.baseUV);
    #if defined(_DETAIL_MAP)
        output.detailUV = TranformDetailUV(input.baseUV);
    #endif
    return output;
}

float4 LitPassFragment(Varyings input): SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input)
    
    ClipLOD(input.positionCS.xy, unity_LODFade.x);

    #if defined(_DETAIL_MAP)
        InputConfig inputConfig = GetInputConfig(input.baseUV, input.detailUV);
    #else
        InputConfig inputConfig = GetInputConfig(input.baseUV);
    #endif
    
    float4 base = GetBase(inputConfig);
    #if defined(_CLIPPING)
        clip(base.a - GetCutOff(inputConfig));
    #endif

    Surface surface;
    surface.position = input.positionWS;
    #if defined(_NORMAL_MAP)
        surface.normal = TransformNormalTangentToWorld(GetNormalTS(inputConfig), input.normalWS, input.tangentWS);
        surface.interpolatedNormal = normalize(input.normalWS);
    #else
        surface.normal = normalize(input.normalWS);
        surface.interpolatedNormal = surface.normal;
    #endif
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.color = base.rgb;
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(inputConfig);
    surface.smoothness = GetSmoothness(inputConfig);
    surface.fresnel = GetFresnel(inputConfig);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    surface.occlusion = GetOcclusion(inputConfig);

    #if defined(_PREMULTIPLY_ALPHA)
        BRDF brdf = GetBRDF(surface, true);
    #else
        BRDF brdf = GetBRDF(surface);
    #endif
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(inputConfig);
    return float4(color, surface.alpha);
}

#endif