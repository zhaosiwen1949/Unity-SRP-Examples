using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();
    private bool useDynamicBatching, useInstancing, useLightsPerObject;
    private ShadowSettings shadowSettings;
    private PostFXSettings postFXSettings;

    public CustomRenderPipeline(bool useDynamicBatching, bool useInstancing, bool useSRPBatching, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useInstancing = useInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatching;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useInstancing, useLightsPerObject, shadowSettings, postFXSettings);
        }
    }
}
