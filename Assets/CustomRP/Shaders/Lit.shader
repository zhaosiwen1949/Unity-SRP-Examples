Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("AlbedoColor", Color) = (0.5, 0.5, 0.5, 1.0)
        [HideInInspector] _MainTex ("Texture for Transparency Lightmap", 2D) = "white" {}
        [HideInInspector] _Color ("Texture for Transparency Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
        [NoScaleOffset] _EmissionMap ("Emission", 2D) = "white" {}
        [HDR] _EmissionColor ("EmissionColor", Color) = (0.0, 0.0, 0.0, 1.0)
        [Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0.0
        [NoScaleOffset] _NormalMap ("Normal", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0.0, 1.0)) = 1.0
        [Toggle(_MASK_MAP)] _MaskMapToggle ("Mask (MODS) Map", Float) = 0.0
        [NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
        [Toggle(_DETAIL_MAP)] _DetailMapToggle ("Detail Map", Float) = 0.0
        _DetailMap ("Detail", 2D) = "white" {}
        [NoScaleOffset] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailAlbedo ("Detail Albedo", Range(0.0, 1.0)) = 1.0
        _DetailSmoothness ("Detail Smoothness", Range(0.0, 1.0)) = 1.0
        _DetailNormalScale("Detail Normal Scale", Range(0.0, 1.0)) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0
        [Enum(Off, 0.0, 0n, 1.0)] _ZWrite ("Z Write", Float) = 1.0
        _CutOff ("Alpha CutOff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0.0
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0.0
        _Metallic ("Metallic", Range(0.0, 1.0)) = 1.0
        _Occlusion ("Occlusion", Range(0.0, 1.0)) = 1.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 1.0
        _Fresnel ("Fresnel", Range(0.0, 1.0)) = 0.0
        [KeywordEnum(On, Clip, Dither, Off)] _Shadow ("Shadows Mode", Float) = 0.0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/LitInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Tags {
                "LightMode" = "CustomLit"    
            }   
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _NORMAL_MAP
            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _DETAIL_MAP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Tags {
                "LightMode" = "ShadowCaster"    
            }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOW_CLIP _SHADOW_DITHER
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Tags {
                "LightMode" = "Meta"    
            }
            
            Cull Off
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }
    }
    
    CustomEditor "CustomShaderGUI"
}
