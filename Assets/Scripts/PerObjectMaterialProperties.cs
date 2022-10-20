using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        cutOffId = Shader.PropertyToID("_CutOff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness"),
        emissionColorId = Shader.PropertyToID("_EmissionColor");
    
    private static MaterialPropertyBlock block;

    [SerializeField]
    private Color baseColor = Color.white;

    [SerializeField]
    private float cutOff = 1.0f;
    
    [SerializeField, Range(0.0f, 1.0f)]
    private float metallic = 0.0f, smoothness = 0.5f;
    
    [SerializeField, ColorUsage(false, true)]
    private Color emissionColor = Color.black;

    private void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutOffId, cutOff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        block.SetColor(emissionColorId, emissionColor);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private void Awake()
    {
        OnValidate();
    }
}
