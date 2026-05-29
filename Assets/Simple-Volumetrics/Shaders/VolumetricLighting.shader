Shader "Hidden/Volumetric Lighting"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Off
        ZWrite Off
        Cull Off
        Blend Off

        Pass
        {
            Name "Raymarch"

            HLSLPROGRAM

            #include ".//VolumetricLighting.hlsl"

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings i) : SV_Target
            {
                return VolumetricLighting(i);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Blur X"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include ".//DepthAwareGaussianBlur.hlsl"

            float Frag(Varyings i) : SV_Target
            {
                return DepthAwareGaussianBlur(i.texcoord, float2(1, 0), _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy).r;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Blur Y"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include ".//DepthAwareGaussianBlur.hlsl"

            float Frag(Varyings i) : SV_Target
            {
                return DepthAwareGaussianBlur(i.texcoord, float2(0, 1), _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy).r;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Upscale Compositing"

            HLSLPROGRAM

            #include ".//UpscaleCompositing.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float3 Frag(Varyings i) : SV_TARGET
            {
                return UpscaleComposite(i);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Sample Depth"

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings i) : SV_Target
            {
                return SampleSceneDepth(i.texcoord);
            }

            ENDHLSL
        }
    }
}
