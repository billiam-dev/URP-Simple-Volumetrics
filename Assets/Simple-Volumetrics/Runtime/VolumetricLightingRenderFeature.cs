using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.Universal.ShaderInput;

namespace Billiam.SimpleVolumetrics
{
    public class VolumetricLightingRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] VolumetricLightingSettings m_Settings;
        
        VolumetricLightingPass m_ScriptablePass;
        Material m_Material;

        public override void Create()
        {
            m_ScriptablePass = new();
            m_Material = CoreUtils.CreateEngineMaterial("Hidden/Volumetric Lighting");
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_ScriptablePass.Setup(m_Settings, m_Material))
                renderer.EnqueuePass(m_ScriptablePass);
        }

        [Serializable]
        public class VolumetricLightingSettings
        {
            public enum Downsample
            {
                None    = 0,
                Half    = 1,
                Quarter = 2,
                Eighth  = 3
            }

            [Range(0.0f, 1.0f)] public float ScatteringPower = 0.04f;
            [Range(25, 75)] public int MaxSteps = 25;
            [Min(0.0f)] public float MaxDistance = 50.0f;
            [Min(0.0f)] public float Jitter = 2.0f;

            public Downsample Downsampling = Downsample.Half;
            [Range(1, 10)] public int BlurIterations = 3;

            [Range(0.0f, 1.0f)] public float Intensity = 1.0f;
        }

        internal class VolumetricLightingPass : ScriptableRenderPass
        {
            VolumetricLightingSettings m_CurrentSettings;
            Material m_Material;

            public VolumetricLightingPass()
            {
                m_CurrentSettings = new();
            }

            internal bool Setup(VolumetricLightingSettings featureSettings, Material material)
            {
                m_Material = material;
                m_CurrentSettings = featureSettings;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

                return m_Material != null
                    && featureSettings.Intensity > 0.0f;
            }

            class PassData
            {
                internal TextureHandle source;
                internal TextureHandle target;

                internal Material material;
                internal int materialPassIndex;
                internal int materialAdditionalPassIndex;

                internal TextureHandle downsampledDepthTarget;
                internal TextureHandle volumetricLightingRenderTarget;
                internal UniversalLightData lightData;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                CreateRenderTextureHandles(
                    renderGraph,
                    cameraData,
                    out TextureHandle downsampleDepthTexture,
                    out TextureHandle volumetricLightTexture,
                    out TextureHandle volumetricBlurTexture,
                    out TextureHandle upscaleCompositionTexture
                );

                // Downsample depth
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Downsample Depth", out var passData, profilingSampler))
                {
                    passData.source = resourceData.cameraDepthTexture;
                    passData.target = downsampleDepthTexture;
                    passData.material = m_Material;
                    passData.materialPassIndex = (int)ShaderPasses.DownsampleDepth;

                    builder.SetRenderAttachment(downsampleDepthTexture, 0, AccessFlags.WriteAll);
                    builder.UseTexture(resourceData.cameraDepthTexture);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
                    });
                }

                // Volumetric lighting
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Volumetric Lighting", out PassData passData, profilingSampler))
                {
                    passData.source = downsampleDepthTexture;
                    passData.target = volumetricLightTexture;
                    passData.material = m_Material;
                    passData.materialPassIndex = (int)ShaderPasses.VolumetricLighting;
                    passData.downsampledDepthTarget = downsampleDepthTexture;
                    passData.lightData = lightData;

                    builder.SetRenderAttachment(volumetricLightTexture, 0, AccessFlags.WriteAll);
                    builder.UseTexture(downsampleDepthTexture);
                    if (resourceData.mainShadowsTexture.IsValid())
                        builder.UseTexture(resourceData.mainShadowsTexture);
                    if (resourceData.additionalShadowsTexture.IsValid())
                        builder.UseTexture(resourceData.additionalShadowsTexture);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        passData.material.SetTexture(ShaderIDs.DownsampledDepthTexture, passData.downsampledDepthTarget);
                        UpdateMaterialParameters(passData.material, passData.lightData.mainLightIndex, passData.lightData.additionalLightsCount, passData.lightData.visibleLights);

                        Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
                    });
                }

                // Guassian blur
                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Blur", out PassData passData, profilingSampler))
                {
                    passData.source = volumetricLightTexture;
                    passData.target = volumetricBlurTexture;
                    passData.material = m_Material;
                    passData.materialPassIndex = (int)ShaderPasses.BlurX;
                    passData.materialAdditionalPassIndex = (int)ShaderPasses.BlurY;

                    builder.UseTexture(volumetricLightTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(volumetricBlurTexture, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        for (int i = 0; i < m_CurrentSettings.BlurIterations; i++)
                        {
                            Blitter.BlitCameraTexture(unsafeCmd, passData.source, passData.target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, passData.materialPassIndex);
                            Blitter.BlitCameraTexture(unsafeCmd, passData.target, passData.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, passData.materialAdditionalPassIndex);
                        }
                    });
                }

                // Upsample composition
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Upsample Composition", out PassData passData, profilingSampler))
                {
                    passData.source = resourceData.cameraColor;
                    passData.target = upscaleCompositionTexture;
                    passData.material = m_Material;
                    passData.materialPassIndex = (int)ShaderPasses.UpscaleComposite;
                    passData.volumetricLightingRenderTarget = volumetricLightTexture;

                    builder.SetRenderAttachment(upscaleCompositionTexture, 0, AccessFlags.WriteAll);
                    builder.UseTexture(resourceData.cameraDepthTexture);
                    builder.UseTexture(downsampleDepthTexture);
                    builder.UseTexture(volumetricLightTexture);
                    builder.UseTexture(resourceData.cameraColor);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        passData.material.SetTexture(ShaderIDs.VolumetricLighting, passData.volumetricLightingRenderTarget);
                        passData.material.SetFloat(ShaderIDs.Intensity, m_CurrentSettings.Intensity);

                        Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
                    });
                }

                resourceData.cameraColor = upscaleCompositionTexture;
            }

            void CreateRenderTextureHandles(
                RenderGraph renderGraph,
                UniversalCameraData cameraData,
                out TextureHandle downsampleDepthTexture,
                out TextureHandle volumetricLightTexture,
                out TextureHandle volumetricBlurTexture,
                out TextureHandle upscaleCompositionTexture)
            {
                RenderTextureDescriptor cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = (int)DepthBits.None;

                RenderTextureFormat originalColorFormat = cameraTargetDescriptor.colorFormat;
                Vector2Int originalResolution = new(cameraTargetDescriptor.width, cameraTargetDescriptor.height);

                int d = 1 << (int)m_CurrentSettings.Downsampling;
                cameraTargetDescriptor.width /= d;
                cameraTargetDescriptor.height /= d;
                cameraTargetDescriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                downsampleDepthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_DownscaleDepth", false);

                cameraTargetDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                volumetricLightTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_VolumetricLight", false);
                volumetricBlurTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_VolumetricLightBlur", false);

                cameraTargetDescriptor.width = originalResolution.x;
                cameraTargetDescriptor.height = originalResolution.y;
                cameraTargetDescriptor.colorFormat = originalColorFormat;
                upscaleCompositionTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_UpscaleCompositionTexture", false);
            }

            void UpdateMaterialParameters(Material material, int mainLightIndex, int additionalLightsCount, NativeArray<VisibleLight> visibleLights)
            {
                // UpdateLightsParameters(volumetricFogMaterial, fogVolume, enableMainLightContribution, enableAdditionalLightsContribution, mainLightIndex, visibleLights);

                material.SetFloat(ShaderIDs.ScatteringPower, m_CurrentSettings.ScatteringPower);
                material.SetInt(ShaderIDs.MaxSteps, m_CurrentSettings.MaxSteps);
                material.SetFloat(ShaderIDs.MaxDistance, m_CurrentSettings.MaxDistance);
                material.SetFloat(ShaderIDs.Jitter, m_CurrentSettings.Jitter);
            }
        }
    }
}
