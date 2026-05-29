#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

TEXTURE2D_X_FLOAT(_DownscaledDepthTexture);
float4 _DownscaledDepthTexture_TexelSize;

// Samples the downsampled camera depth texture.
float SampleDownsampledSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_DownscaledDepthTexture, sampler_PointClamp, uv).r;
}

float DepthAwareUpsampleTexture(TEXTURE2D_X(tex), float2 uv, float depth)
{
    float color = 0;
    int offset = 0;
    
    float d0 = depth;
    float d1 = _DownscaledDepthTexture.Sample(sampler_PointClamp, uv, int2(0, 1)).r;
    float d2 = _DownscaledDepthTexture.Sample(sampler_PointClamp, uv, int2(0, -1)).r;
    float d3 = _DownscaledDepthTexture.Sample(sampler_PointClamp, uv, int2(1, 0)).r;
    float d4 = _DownscaledDepthTexture.Sample(sampler_PointClamp, uv, int2(-1, 0)).r;
    
    d1 = abs(d0 - d1);
    d2 = abs(d0 - d2);
    d3 = abs(d0 - d3);
    d4 = abs(d0 - d4);
    
    real dmin = min(min(d1, d2), min(d3, d4));

    if (dmin == d1)
        offset = 0;
    else if (dmin == d2)
        offset = 1;
    else if (dmin == d3)
        offset = 2;
    else if (dmin == d4)
        offset = 3;
    
    switch (offset)
    {
        case 0:
            color = tex.Sample(sampler_PointClamp, uv, int2(0, 1)).r;
            break;

        case 1:
            color = tex.Sample(sampler_PointClamp, uv, int2(0, -1)).r;
            break;

        case 2:
            color = tex.Sample(sampler_PointClamp, uv, int2(1, 0)).r;
            break;

        case 3:
            color = tex.Sample(sampler_PointClamp, uv, int2(-1, 0)).r;
            break;

        default:
            color = tex.Sample(sampler_PointClamp, uv).r;
            break;
    }
    
    return color;
}
