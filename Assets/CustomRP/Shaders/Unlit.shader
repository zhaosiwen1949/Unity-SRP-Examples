Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0
        [Enum(Off, 0.0, 0n, 1.0)] _ZWrite ("Z Write", Float) = 1.0
        _CutOff ("Alpha CutOff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0.0
        [KeywordEnum(On, Clip, Dither, Off)] _Shadow ("Shadows Mode", Float) = 0.0
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/UnlitInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
        
//        Pass
//        {
//            Tags {
//                "LightMode" = "ShadowCaster"    
//            }
//            ColorMask 0
//            HLSLPROGRAM
//            #pragma target 3.5
//            #pragma shader_feature _ _SHADOW_CLIP _SHADOW_DITHER
//            #pragma multi_compile_instancing
//            #pragma vertex ShadowCasterPassVertex
//            #pragma fragment ShadowCasterPassFragment
//            #include "ShadowCasterPass.hlsl"
//            ENDHLSL
//        }
    }
    
    CustomEditor "CustomShaderGUI"
}
