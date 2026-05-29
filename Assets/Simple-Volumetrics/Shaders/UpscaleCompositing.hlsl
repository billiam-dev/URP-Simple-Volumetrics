#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl" 
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#include "./DeclareDownsampledDepthTexture.hlsl"

TEXTURE2D(_VolumetricLighting);
SAMPLER(sampler_VolumetricLighting);

float _Intensity;
float _Downsample;

float3 LightColor(Varyings i)
{
    float3 pos = ComputeWorldSpacePosition(i.texcoord, SampleDownsampledSceneDepth(i.texcoord), UNITY_MATRIX_I_VP);
    float4 shadowCoord = TransformWorldToShadowCoord(pos);

    return GetMainLight(shadowCoord).color;
}

float3 UpscaleComposite(Varyings i)
{
    float col = 0;
    int offset = 0;

    float d0 = SampleDownsampledSceneDepth(i.texcoord);

    float d1 = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(0, 1)).x;
    float d2 = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(0, -1)).x;
    float d3 = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(1, 0)).x;
    float d4 = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(-1, 0)).x;

    d1 = abs(d0 - d1);
    d2 = abs(d0 - d2);
    d3 = abs(d0 - d3);
    d4 = abs(d0 - d4);

    real dmin = min(min(d1, d2), min(d3, d4));

    if (dmin == d1)
    {
        offset = 0;
    }
    else if (dmin == d2)
    {
        offset = 1;
    }
    else if (dmin == d3)
    {
        offset = 2;
    }
    else if (dmin == d4)
    {
        offset = 3;
    }
             
    col = 0;
    switch (offset)
    {
        case 0:
            col = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(0, 1)).r;
            break;

        case 1:
            col = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(0, -1)).r;
            break;

        case 2:
            col = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(1, 0)).r;
            break;

        case 3:
            col = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord, int2(-1, 0)).r;
            break;

        default:
            col = _VolumetricLighting.Sample(sampler_VolumetricLighting, i.texcoord).r;
            break;
    }

    float3 finalShaft = saturate(col) * LightColor(i) * _Intensity;
    float3 screen = _BlitTexture.Sample(sampler_PointClamp, i.texcoord).rgb;

    return screen + finalShaft;
}