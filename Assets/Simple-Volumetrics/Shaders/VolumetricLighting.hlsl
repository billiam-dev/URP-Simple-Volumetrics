#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#include "./DeclareDownsampledDepthTexture.hlsl"

float _ScatteringPower;
int _MaxSteps;
float _MaxDistance;
float _Jitter;

float random01(float2 p)
{
    return frac(sin(dot(p, float2(41, 289))) * 45758.5453);
}

// Mie scaterring approximated with Henyey-Greenstein phase function.
float ComputeScattering(float lightDotView, float scattering)
{
    float result = 1.0f - scattering * scattering;
    float a = abs(1.0f + scattering * scattering - (2.0f * scattering) * lightDotView);
    result /= (4.0f * PI * pow(a, 1.5f));

    return result;
}

float VolumetricLighting(Varyings i)
{
    float depth = SampleDownsampledSceneDepth(i.texcoord);
    float3 mainLightDirection = GetMainLight().direction;
    
    // Define start and end positions.
    float3 startPosition = _WorldSpaceCameraPos;
    float3 endPosition = ComputeWorldSpacePosition(i.texcoord, depth, UNITY_MATRIX_I_VP);

    // Create ray from start position to end position, we will march along this ray.
    float3 rayVector = endPosition - startPosition;
    float3 rayDirection = normalize(rayVector);
    float rayLength = clamp(length(rayVector), 0, _MaxDistance);

    // Compute step vector (direction and length of a single step).
    float minStepLength = rayLength / _MaxSteps;
    float3 stepVec = rayDirection * minStepLength;

    // By adding a jitter value to the ray position, we can get away with marching fewer steps.
    float rayStartOffset = random01(i.texcoord) * minStepLength * _Jitter;
    float3 currentPosition = startPosition + rayStartOffset * rayDirection;

    // March ray.
    float d = dot(rayDirection, mainLightDirection);
    float accumulatedLight = 0;
    for (int i = 0; i < _MaxSteps - 1; i++)
    {
        half shadowMapValue = MainLightRealtimeShadow(TransformWorldToShadowCoord(currentPosition));
        if (shadowMapValue > 0)
            accumulatedLight += ComputeScattering(d, _ScatteringPower) * shadowMapValue;

        currentPosition += stepVec;
    }

    // Normalize the accumulated light over the number of steps we took.
    accumulatedLight /= _MaxSteps;
    
    return accumulatedLight;
}
