Shader "Hidden/Custom RP/Post FX Stack"
{
    SubShader
    {
        // No culling or depth
        Cull Off 
        ZWrite Off 
        ZTest Always
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "./PostFX/PostFXStackPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Bloom Combine"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomCombinePassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Horizontal"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Prefilter"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }
        
        
        Pass
        {
            Name "Bloom Vertical"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Copy"
            
            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment CopyPassFragment
            ENDHLSL
        }
    }
}
