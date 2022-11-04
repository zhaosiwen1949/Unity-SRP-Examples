using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    enum Pass
    {
        BloomCombine,
        BloomHorizontal,
        BloomPrefilter,
        BloomVertical,
        Copy
    }

    private static int fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity");

    private static int maxBloomPyramidLevels = 16;
    
    private int bloomPyramidId;

    private const string bufferName = "Post FX";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName,
    };

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings = default;

    public bool isActive => settings != null;

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void SetUp(ScriptableRenderContext context, Camera camera, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? postFXSettings : null;
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int) pass, MeshTopology.Triangles, 3);
    }

    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");

        Vector4 threshold;
        threshold.x = settings.Bloom.threshold;
        threshold.y = settings.Bloom.threshold * settings.Bloom.thresholdKnee;
        threshold.z = 2.0f * threshold.y;
        threshold.w = 1.0f / (4.0f * threshold.y + 0.00001f);
        threshold.y += -threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
        Draw(sourceId, bloomPrefilterId, Pass.BloomPrefilter);
        
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        width /= 2;
        height /= 2;

        int i;
        if (settings.Bloom.intensity > 0.0f)
        {
            for (i = 0; i < settings.Bloom.maxIterations; i++)
            {
                if (width < settings.Bloom.downscaleLimit || height < settings.Bloom.downscaleLimit)
                {
                    break;
                }

                int midId = toId - 1;
                buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
                Draw(fromId, midId, Pass.BloomHorizontal);
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
                Draw(midId, toId, Pass.BloomVertical);

                width = width / 2;
                height = height / 2;
                fromId = toId;
                toId += 2;
            }   
        }
        else
        {
            i = 0;
        }

        if (i > 0)
        {
            buffer.SetGlobalFloat(bloomIntensityId, settings.Bloom.intensity);
            buffer.SetGlobalFloat(bloomBicubicUpsamplingId, settings.Bloom.bicubicUpsampling ? 1.0f : 0.0f);
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }

            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);

            buffer.ReleaseTemporaryRT(fromId);
        }
        else
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        }
        
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        buffer.EndSample("Bloom");
    }
}
