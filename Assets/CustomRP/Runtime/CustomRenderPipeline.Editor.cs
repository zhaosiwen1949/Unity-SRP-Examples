using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

partial class CustomRenderPipeline
{
    partial void InitializeForEditor();
    
#if UNITY_EDITOR
    private static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            LightDataGI lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var areaLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref areaLight);
                        areaLight.mode = LightMode.Baked;
                        lightData.Init(ref areaLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };

    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }
#endif

}
