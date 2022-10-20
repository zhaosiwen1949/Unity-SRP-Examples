using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions.Must;

[System.Serializable]
public class ShadowSettings
{
    [Min(0.001f)] public float maxDistance = 20.0f;
    
    [Range(0.001f, 1.0f)] public float shadowDistance = 0.1f;

    public enum TextureSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192,
    }

    public enum FilterMode
    {
        PCF2x2,
        PCF3x3,
        PCF5x5,
        PCF7x7,
    }

    public enum CascadeBlendMode
    {
        Hard,
        Soft,
        Dither,
    }
    
    [System.Serializable]
    public struct Directional
    {
        public TextureSize atlasSize;
        
        [Range(0, 4)]
        public int cascadeCount;

        [Range(0.0f, 1.0f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        public Vector3 cascadeRatio => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
        
        [Range(0.001f, 1.0f)] public float cascadeDistance;

        public FilterMode filter;

        public CascadeBlendMode cascadeBlend;
    }

    public Directional directional = new Directional
    {
        atlasSize = TextureSize._1024,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeDistance = 0.1f,
        filter = FilterMode.PCF2x2,
        cascadeBlend = CascadeBlendMode.Hard,
    };
}
