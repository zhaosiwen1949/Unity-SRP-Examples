using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;
    
    [System.NonSerialized] private Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }
    
    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0.0f, 16.0f)] public int maxIterations;

        [Min(1.0f)] public int downscaleLimit;

        public bool bicubicUpsampling;
        
        [Min(0.0f)] public float threshold;

        [Range(0.0f, 1.0f)] public float thresholdKnee;

        [Min(0.0f)] public float intensity;
    }

    [SerializeField]
    private BloomSettings bloom = new BloomSettings
    {
        maxIterations = 16,
        downscaleLimit = 2,
        bicubicUpsampling = false,
    };

    public BloomSettings Bloom => bloom;
}
