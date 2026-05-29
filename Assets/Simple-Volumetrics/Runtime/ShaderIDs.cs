namespace Billiam.SimpleVolumetrics
{
    internal class ShaderIDs
    {
        public const string DownsampledDepthTexture = "_DownsampledDepthTexture";
        public const string VolumetricLighting = "_VolumetricLighting";

        public const string ScatteringPower = "_ScatteringPower";
        public const string MaxSteps = "_MaxSteps";
        public const string MaxDistance = "_MaxDistance";
        public const string Jitter = "_Jitter";

        public const string Downsampling = "_Downsampling";
        public const string GuassSamples = "_GuassSamples";
        public const string GuassAmount = "_GuassAmount";
        
        public const string Intensity = "_Intensity";
    }

    internal enum ShaderPasses
    {
        VolumetricLighting = 0,
        BlurX = 1,
        BlurY = 2,
        UpscaleComposite = 3,
        DownsampleDepth = 4,
    }
}
