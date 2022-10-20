using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    private MaterialEditor editor;
    private Object[] materials;
    private MaterialProperty[] properties;

    private bool showPresets;
    
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;
        
        BakeEmission();
        
        EditorGUILayout.Space();
        if (showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true))
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }
    }

    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1.0f : 0.0f))
        {
            SetKeyword(keyword, value);
        }
    }
    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }

        return false;
    }

    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    private bool HasProperty (string name) => FindProperty(name, properties, false) != null;

    private bool clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    private bool premultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    private BlendMode srcBlend
    {
        set => SetProperty("_SrcBlend", (float) value);
    }

    private BlendMode dstBlend
    {
        set => SetProperty("_DstBlend", (float) value);
    }

    private bool zWrite
    {
        set => SetProperty("_ZWrite", value ? 1.0f : 0.0f);
    }

    enum ShadowMode
    {
        On, Clip, Dither, Off
    }

    ShadowMode shadowMode
    {
        set
        {
            if (SetProperty("_Shadow", (float) value))
            {
                SetKeyword("_SHADOW_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOW_DITHER", value == ShadowMode.Dither);
            }
        }
    }
    
    RenderQueue renderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int) value;
            }
        }
    }

    private bool hasPremultiplyAlpha => HasProperty("_PremulAlpha");

    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }

        return false;
    }

    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            clipping = false;
            premultiplyAlpha = false;
            srcBlend = BlendMode.One;
            dstBlend = BlendMode.Zero;
            zWrite = true;
            renderQueue = RenderQueue.Geometry;
            shadowMode = ShadowMode.On;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            clipping = true;
            premultiplyAlpha = false;
            srcBlend = BlendMode.One;
            dstBlend = BlendMode.Zero;
            zWrite = true;
            renderQueue = RenderQueue.AlphaTest;
            shadowMode = ShadowMode.Clip;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            clipping = false;
            premultiplyAlpha = false;
            srcBlend = BlendMode.SrcAlpha;
            dstBlend = BlendMode.OneMinusSrcAlpha;
            zWrite = false;
            renderQueue = RenderQueue.Transparent;
            shadowMode = ShadowMode.Dither;
        }
    }

    void TransparentPreset()
    {
        if (hasPremultiplyAlpha && PresetButton("Transparent"))
        {
            clipping = false;
            premultiplyAlpha = true;
            srcBlend = BlendMode.One;
            dstBlend = BlendMode.OneMinusSrcAlpha;
            zWrite = false;
            renderQueue = RenderQueue.Transparent;
            shadowMode = ShadowMode.Dither;
        }
    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadowMode = FindProperty("_Shadow", properties, false);
        if (shadowMode == null || shadowMode.hasMixedValue)
        {
            return;
        }

        bool enabled = shadowMode.floatValue < (float) ShadowMode.Off;
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

    void BakeEmission()
    {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in materials)
            {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }
}
