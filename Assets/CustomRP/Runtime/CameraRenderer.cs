using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    private static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    private static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    
    private ScriptableRenderContext context;
    private Camera camera;

    private const string bufferName = "Render Camera";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private CullingResults cullingResults;
    private Lighting lighting = new Lighting();
    private PostFXStack postFXStack = new PostFXStack();
    
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings))
        {
            return;
        }
        
        buffer.BeginSample(sampleName);
        ExecuteBuffer();
        lighting.SetUp(context, cullingResults, shadowSettings, useLightsPerObject);
        postFXStack.SetUp(context, camera, postFXSettings);
        buffer.EndSample(sampleName);
        SetUp();
        DrawVisibleGeometry(useDynamicBatching, useInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();
        DrawGizmosAfterFX();
        if (postFXStack.isActive)
        {
            postFXStack.Render(frameBufferId);
        }
        CleanUp();
        Submit();
    }

    bool Cull(ShadowSettings shadowSettings)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }
    void SetUp()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        if (flags > CameraClearFlags.Color)
        {
            flags = CameraClearFlags.Color;
        }
        
        if (postFXStack.isActive)
        {
            buffer.GetTemporaryRT(frameBufferId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.Default);
            buffer.SetRenderTarget(frameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth, 
            flags <= CameraClearFlags.Color, 
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
            );
        buffer.BeginSample(sampleName);
        ExecuteBuffer();
    }
    void DrawVisibleGeometry(bool useDynamicBatching, bool useInstancing, bool useLightsPerObject)
    {
        // 绘制不透明物体
        PerObjectData lightsPerObject =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None; 
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume 
                            | PerObjectData.ShadowMask | PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume | PerObjectData.ReflectionProbes
                            | lightsPerObject,
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
        
        // 绘制天空盒
        context.DrawSkybox(camera);
        
        // 绘制透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filterSettings);
    }

    partial void DrawUnsupportedShaders();
    partial void DrawGizmosBeforeFX();
    partial void DrawGizmosAfterFX();
    partial void PrepareForSceneWindow();

    partial void PrepareBuffer();

    void Submit()
    {
        buffer.EndSample(sampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void CleanUp()
    {
        lighting.CleanUp();
        if (postFXStack.isActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
}
