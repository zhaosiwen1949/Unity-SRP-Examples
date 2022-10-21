using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using UnityEngine.Rendering;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

public class MeshBall : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        cutOffId = Shader.PropertyToID("_CutOff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");
    
    private static int generateNumber = 100;

    [SerializeField]
    private Mesh mesh = default;

    [SerializeField]
    private Material material = default;

    [SerializeField] private LightProbeProxyVolume lightProbeProxyVolume = null;

    private Matrix4x4[] matrices = new Matrix4x4[generateNumber];

    private Vector4[] baseColors = new Vector4[generateNumber];

    private float[] cutOffs = new float[generateNumber],
        metallics = new float[generateNumber],
        smoothnesses = new float[generateNumber];

    private MaterialPropertyBlock block;

    private void Awake()
    {
        for (int i = 0; i < matrices.Length; i++)
        {
            Vector3 originPosition = GetComponent<Transform>().position;
            Vector3 randomOffset = new Vector3(Random.insideUnitCircle.x, 0.0f, Random.insideUnitCircle.y) * 10.0f;
            matrices[i] = Matrix4x4.TRS(
                originPosition + randomOffset, 
                Quaternion.Euler(Random.value * 360.0f, Random.value * 360.0f, Random.value * 360.0f), 
                Vector3.one * Random.Range(0.5f, 1.0f)
                );
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, 1.0f);
            cutOffs[i] = Random.value;
            metallics[i] = Random.Range(0.05f, 0.8f);
            smoothnesses[i] = Random.Range(0.05f, 0.95f);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(cutOffId, cutOffs);
            block.SetFloatArray(metallicId, metallics);
            block.SetFloatArray(smoothnessId, smoothnesses);

            if (!lightProbeProxyVolume)
            {
                Vector3[] lightProbePosition = new Vector3[generateNumber];
                for (int i = 0; i < matrices.Length; i++)
                {
                    lightProbePosition[i] = matrices[i].GetColumn(3);
                }

                SphericalHarmonicsL2[] lightProbes = new SphericalHarmonicsL2[generateNumber];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(lightProbePosition, lightProbes, null);
                block.CopySHCoefficientArraysFrom(lightProbes);
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, generateNumber, block,
            ShadowCastingMode.On, true, 0, null, lightProbeProxyVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided, lightProbeProxyVolume);
    }
}
