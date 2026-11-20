// URP 17 Render Graph feature for the AnimeComposite volume override.
// Diffusion: prefilter to half res, down to quarter, separable blur, then one composite pass
// that also applies the depth tint and gradient overlay. Runs before URP post, so bloom,
// grading and tonemapping still act on the result.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class AnimeCompositeFeature : ScriptableRendererFeature
{
    [SerializeField] Shader shader;
    [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    Material material;
    AnimeCompositePass pass;

    public override void Create()
    {
        if (shader == null)
            return;

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new AnimeCompositePass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null)
            return;

        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        if (!renderingData.cameraData.postProcessEnabled)
            return;

        AnimeComposite settings = VolumeManager.instance.stack.GetComponent<AnimeComposite>();
        if (settings == null || !settings.IsActive())
            return;

        pass.renderPassEvent = injectionPoint;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }
}

public class AnimeCompositePass : ScriptableRenderPass
{
    const int PrefilterPass = 0;
    const int BlurHorizontalPass = 1;
    const int BlurVerticalPass = 2;
    const int CompositePass = 3;

    static readonly int DiffusionTexId = Shader.PropertyToID("_DiffusionTex");
    static readonly int DiffusionIntensityId = Shader.PropertyToID("_DiffusionIntensity");
    static readonly int DiffusionThresholdId = Shader.PropertyToID("_DiffusionThreshold");
    static readonly int DiffusionKneeId = Shader.PropertyToID("_DiffusionKnee");
    static readonly int BlurTexelId = Shader.PropertyToID("_BlurTexel");
    static readonly int GradientIntensityId = Shader.PropertyToID("_GradientIntensity");
    static readonly int GradientTopId = Shader.PropertyToID("_GradientTop");
    static readonly int GradientBottomId = Shader.PropertyToID("_GradientBottom");
    static readonly int GradientDirectionId = Shader.PropertyToID("_GradientDirection");
    static readonly int DepthTintIntensityId = Shader.PropertyToID("_DepthTintIntensity");
    static readonly int DepthTintColorId = Shader.PropertyToID("_DepthTintColor");
    static readonly int DepthTintRangeId = Shader.PropertyToID("_DepthTintRange");
    static readonly Vector4 FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

    readonly Material material;

    class CompositeData
    {
        public TextureHandle source;
        public TextureHandle diffusion;
        public Material material;
    }

    public AnimeCompositePass(Material material)
    {
        this.material = material;
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        AnimeComposite settings = VolumeManager.instance.stack.GetComponent<AnimeComposite>();

        RenderTextureDescriptor full = cameraData.cameraTargetDescriptor;
        full.depthBufferBits = 0;
        full.msaaSamples = 1;

        RenderTextureDescriptor half = full;
        half.width = Mathf.Max(1, full.width / 2);
        half.height = Mathf.Max(1, full.height / 2);

        RenderTextureDescriptor quarter = half;
        quarter.width = Mathf.Max(1, half.width / 2);
        quarter.height = Mathf.Max(1, half.height / 2);

        ApplySettings(settings, quarter);

        TextureHandle source = resourceData.activeColorTexture;
        TextureHandle halfTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, half, "_AnimeDiffusionHalf", false);
        TextureHandle quarterA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, quarter, "_AnimeDiffusionA", false);
        TextureHandle quarterB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, quarter, "_AnimeDiffusionB", false);
        TextureHandle composite = UniversalRenderer.CreateRenderGraphTexture(renderGraph, full, "_AnimeComposite", false);

        renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, halfTex, material, PrefilterPass), "Anime Diffusion Prefilter");
        renderGraph.AddBlitPass(halfTex, quarterA, Vector2.one, Vector2.zero, passName: "Anime Diffusion Downsample");
        renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(quarterA, quarterB, material, BlurHorizontalPass), "Anime Diffusion Blur H");
        renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(quarterB, quarterA, material, BlurVerticalPass), "Anime Diffusion Blur V");

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Anime Composite", out CompositeData data))
        {
            data.source = source;
            data.diffusion = quarterA;
            data.material = material;
            builder.UseTexture(source);
            builder.UseTexture(quarterA);
            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(composite, 0);
            builder.SetRenderFunc(static (CompositeData d, RasterGraphContext context) => ExecuteComposite(d, context));
        }

        // Not AddCopyPass: the camera target can be multisampled and the temp is not.
        renderGraph.AddBlitPass(composite, source, Vector2.one, Vector2.zero, passName: "Anime Composite Copy Back");
    }

    static void ExecuteComposite(CompositeData data, RasterGraphContext context)
    {
        data.material.SetTexture(DiffusionTexId, data.diffusion);
        Blitter.BlitTexture(context.cmd, data.source, FullScaleBias, data.material, CompositePass);
    }

    void ApplySettings(AnimeComposite settings, RenderTextureDescriptor quarter)
    {
        float spread = settings.diffusionSpread.value;
        float angle = settings.gradientAngle.value * Mathf.Deg2Rad;

        material.SetFloat(DiffusionIntensityId, settings.diffusionIntensity.value);
        material.SetFloat(DiffusionThresholdId, settings.diffusionThreshold.value);
        material.SetFloat(DiffusionKneeId, settings.diffusionKnee.value);
        material.SetVector(BlurTexelId, new Vector4(spread / quarter.width, spread / quarter.height, 0f, 0f));
        material.SetFloat(GradientIntensityId, settings.gradientIntensity.value);
        material.SetColor(GradientTopId, settings.gradientTop.value);
        material.SetColor(GradientBottomId, settings.gradientBottom.value);
        material.SetVector(GradientDirectionId, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0f, 0f));
        material.SetFloat(DepthTintIntensityId, settings.depthTintIntensity.value);
        material.SetColor(DepthTintColorId, settings.depthTintColor.value);
        material.SetVector(DepthTintRangeId, new Vector4(settings.depthTintStart.value, settings.depthTintEnd.value, 0f, 0f));
    }
}
