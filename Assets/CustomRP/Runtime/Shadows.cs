using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeCullingSphereCountId = Shader.PropertyToID("_CascadeCullingSphereCount"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static string[] directionalFilterKeywords =
            {
                "_DIRECTIONAL_PCF3",
                "_DIRECTIONAL_PCF5",
                "_DIRECTIONAL_PCF7"
            };

    private static string[] otherFilterKeywords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7"
    };

    private static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE",
    };

    private static int maxShadowedDirectionalLightCount = 4, maxCascades = 4, maxShadowedOtherLightCount = 16;
    
    private const string bufferName = "Shadows";

    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private CullingResults cullingResults;

    private ShadowSettings settings;

    private int shadowDirectionalLightCount, shadowOtherLightCount;
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopScaleBias;
        public float nearClipOffset;
    }

    private ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    private ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

    private Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    private Vector4[] cascadeCullingSpheres = new Vector4[maxCascades], otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
    
    private Vector4[] cascadeDatas = new Vector4[maxCascades];

    private bool useShadowMask;

    private Vector4 shadowAtlasSize;
    
    public void SetUp(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = shadowSettings;

        shadowDirectionalLightCount = 0;
        shadowOtherLightCount = 0;
        
        useShadowMask = false;
    }

    public void Render()
    {
        buffer.BeginSample(bufferName);
        if (shadowDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        
        if (shadowOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }
        
        buffer.SetGlobalInt(cascadeCullingSphereCountId, shadowDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0);
        float cascadeDistance = 1.0f - settings.directional.cascadeDistance;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1.0f / settings.maxDistance, 1.0f / settings.shadowDistance, 1.0f / (1.0f - cascadeDistance * cascadeDistance)));
        buffer.SetGlobalVector(shadowAtlasSizeId, shadowAtlasSize);
        SetKeywords(shadowMaskKeywords, useShadowMask ? 
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1
            : -1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void CleanUp()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if(shadowOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);    
        }
        ExecuteBuffer();
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (
            shadowDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0.0f
        )
        {
            // 判断该方向光渲染阴影时，是否启用 Baked 的 ShadowMask
            int maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0, 0, maskChannel);
            }
            
            int shadowDirectionalLightIndex = shadowDirectionalLightCount;
            shadowedDirectionalLights[shadowDirectionalLightIndex] = new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex,
                slopScaleBias = light.shadowBias,
                nearClipOffset = light.shadowNearPlane,
            };

            shadowDirectionalLightCount += 1;
            
            return new Vector4(light.shadowStrength, shadowDirectionalLightIndex * settings.directional.cascadeCount, light.shadowNormalBias, maskChannel);
        }

        return Vector4.zero;
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        bool isPoint = light.type == LightType.Point;
        
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0.0f)
        {
            return new Vector4(0.0f, 0.0f, isPoint ? 1.0f : 0.0f, -1);
        }
        
        int maskChannel = -1;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask 
        )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }
        
        int newShadowOtherLightCount = shadowOtherLightCount + (isPoint ? 6 : 1);
        
        if (
            newShadowOtherLightCount > maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
            )
        {
            return new Vector4(-light.shadowStrength, 0.0f, isPoint ? 1.0f : 0.0f, maskChannel);
        }
        
        int shadowOtherLightIndex = shadowOtherLightCount;
        shadowedOtherLights[shadowOtherLightIndex] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint,
        };

        shadowOtherLightCount = newShadowOtherLightCount;

        return new Vector4(light.shadowStrength, shadowOtherLightIndex, isPoint ? 1.0f : 0.0f, maskChannel);
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int) settings.directional.atlasSize;
        shadowAtlasSize.x = atlasSize;
        shadowAtlasSize.y = 1.0f / atlasSize;
        
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false,Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1.0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = shadowDirectionalLightCount * settings.directional.cascadeCount <= 1 ? 1 : shadowDirectionalLightCount * settings.directional.cascadeCount <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeDatas);
        SetKeywords(directionalFilterKeywords, (int) settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int) settings.directional.cascadeBlend - 1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        int cascadeCount = settings.directional.cascadeCount;
        for (int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, settings.directional.cascadeRatio, tileSize,light.nearClipOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData
            );

            float cullingFactor = Mathf.Max(0.0f, 0.8f - settings.directional.cascadeDistance);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;

            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = index * cascadeCount + i;
            Vector2 offset = new Vector2(tileIndex % split, tileIndex / split);
            dirShadowMatrices[tileIndex] = ConverToAtlasMatrix(projMatrix * viewMatrix, offset, split);
        
            buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            buffer.SetGlobalDepthBias(0.0f, light.slopScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
    }
    
    void RenderOtherShadows()
    {
        int atlasSize = (int) settings.other.atlasSize;
        shadowAtlasSize.z = atlasSize;
        shadowAtlasSize.w = 1.0f / atlasSize;
        
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0.0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = shadowOtherLightCount <= 1 ? 1 : shadowOtherLightCount <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }
        
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords(otherFilterKeywords, (int) settings.other.filter - 1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex,
            out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;
        
        Vector2 offset = new Vector2(index % split, index / split);
        otherShadowMatrices[index] = ConverToAtlasMatrix(projMatrix * viewMatrix, offset, split);
        
        float texelSize = 2.0f / (tileSize * projMatrix.m00);
        float filterSize = texelSize * ((int)settings.other.filter + 1);
        float bias = 1.4142136f * filterSize * light.normalBias;
        SetOtherTileData(index, offset, 1.0f / split, bias);
        
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        buffer.SetGlobalDepthBias(0.0f, light.slopScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0.0f, 0.0f);
    }
    
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        float texelSize = 2.0f / tileSize;
        float filterSize = texelSize * ((int)settings.other.filter + 1);
        float bias = 1.4142136f * filterSize * light.normalBias;

        for (int i = 0; i < 6; i++)
        {
            float fovBias = Mathf.Atan(1.0f + filterSize + bias) * Mathf.Rad2Deg * 2.0f - 90.0f;
            
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData
            );
            shadowSettings.splitData = splitData;

            // 翻转 y 轴
            viewMatrix.m10 *= -1.0f;
            viewMatrix.m11 *= -1.0f;
            viewMatrix.m12 *= -1.0f;
            viewMatrix.m13 *= -1.0f;

            int tileIndex = index + i;
            Vector2 offset = new Vector2(tileIndex % split, tileIndex / split);
            otherShadowMatrices[tileIndex] = ConverToAtlasMatrix(projMatrix * viewMatrix, offset, split);

            SetOtherTileData(tileIndex, offset, 1.0f / split, bias);
        
            buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            buffer.SetGlobalDepthBias(0.0f, light.slopScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0.0f, 0.0f);   
        }
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
        
    }

    void SetCascadeData(int index, Vector4 cullingSphere, int tileSize)
    {
        float texelSize = 2.0f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((int)settings.directional.filter + 1);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeDatas[index] = new Vector4(
            1.0f / cullingSphere.w,
            1.4142136f * filterSize
        );
    }

    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = shadowAtlasSize.w * 0.5f;
        float x = offset.x * scale + border;
        float y = offset.y * scale + border;
        
        otherShadowTiles[index] = new Vector4(x, y, scale - border - border, bias); 
    }

    Matrix4x4 ConverToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        // Z轴翻转
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        
        // 从 [-1, 1] 变换到 [0, 1]
        m.m00 = 0.5f * (m.m00 + m.m30);
        m.m01 = 0.5f * (m.m01 + m.m31);
        m.m02 = 0.5f * (m.m02 + m.m32);
        m.m03 = 0.5f * (m.m03 + m.m33);
        m.m10 = 0.5f * (m.m10 + m.m30);
        m.m11 = 0.5f * (m.m11 + m.m31);
        m.m12 = 0.5f * (m.m12 + m.m32);
        m.m13 = 0.5f * (m.m13 + m.m33);
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        
        // 增加 Atlas 偏移变换
        float scale = 1.0f / split;
        m.m00 = scale * (m.m00 + offset.x * m.m30);
        m.m01 = scale * (m.m01 + offset.x * m.m31);
        m.m02 = scale * (m.m02 + offset.x * m.m32);
        m.m03 = scale * (m.m03 + offset.x * m.m33);
        m.m10 = scale * (m.m10 + offset.y * m.m30);
        m.m11 = scale * (m.m11 + offset.y * m.m31);
        m.m12 = scale * (m.m12 + offset.y * m.m32);
        m.m13 = scale * (m.m13 + offset.y * m.m33);

        return m;
    }
}
