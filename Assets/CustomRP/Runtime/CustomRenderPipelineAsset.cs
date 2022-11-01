using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    private bool useDynamicBatching = true, useInstancing = true, useSRPBatching = true, useLightsPerObject = true;

    [SerializeField] 
    private ShadowSettings shadows = default;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBatching, useInstancing, useSRPBatching, useLightsPerObject, shadows);
    }
}
