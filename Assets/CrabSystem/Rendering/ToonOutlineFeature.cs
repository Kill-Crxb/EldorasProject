// URP 17 / Unity 6 Render Graph renderer feature.
//
// Draws outlines by finding depth and normal discontinuities in screen space instead of
// extruding geometry. Lines are a uniform thickness regardless of mesh topology, and they
// appear wherever two surfaces meet - including between two separate meshes that overlap,
// which an inverted hull cannot do.
//
// Setup:
//   1. Add this feature to your URP Renderer asset (Add Renderer Feature > Toon Outline).
//   2. Assign Crab/FlatToonOutlinePost to the Shader field.
//   3. Set Outline Width to 0 on your FlatToon materials, or leave a thin hull for
//      character silhouettes and let this handle everything else.
//
// Requires the DepthNormals prepass, which ConfigureInput requests below. Any shader that
// should receive lines needs a DepthNormals pass - FlatToon has one.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class ToonOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingSkybox;
        public Color outlineColor = Color.black;
        [Range(0.5f, 8f)] public float thickness = 1.4f;
        [Header("Depth edges - silhouettes against whatever is behind them")]
        [Range(0f, 1f)] public float depthStrength = 1f;
        [Range(0.001f, 0.5f)] public float depthThreshold = 0.02f;
        [Range(0f, 50f)] public float depthGrazingBias = 12f;

        [Header("Normal edges - creases and folds")]
        [Range(0f, 1f)] public float normalStrength = 1f;
        [Range(0.05f, 2f)] public float normalThreshold = 0.4f;

        [Header("ID edges - boundaries between different Object IDs")]

        [Tooltip("Lines wherever two materials carry different Object IDs. Exact, no tuning. Set Object ID per material - leave the environment at 0 and give each character part its own value.")]
        [Range(0f, 1f)] public float idStrength = 1f;
        [Range(0.002f, 0.2f)] public float idThreshold = 0.02f;
        [Tooltip("Distance fade for the whole effect, in metres.")]
        public float fadeStart = 40f;
        public float fadeEnd = 120f;
    }

    [SerializeField] Shader shader;
    [SerializeField] Settings settings = new Settings();

    Material material;
    ToonOutlinePass pass;

    public override void Create()
    {
        if (shader == null)
            return;

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new ToonOutlinePass(material, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || pass == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        pass.renderPassEvent = settings.injectionPoint;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }
}

public class ToonOutlinePass : ScriptableRenderPass
{
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    static readonly int DepthStrengthId = Shader.PropertyToID("_DepthStrength");
    static readonly int NormalStrengthId = Shader.PropertyToID("_NormalStrength");
    static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
    static readonly int DepthGrazingBiasId = Shader.PropertyToID("_DepthGrazingBias");
    static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
    static readonly int IDStrengthId = Shader.PropertyToID("_IDStrength");
    static readonly int IDThresholdId = Shader.PropertyToID("_IDThreshold");
    static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");

    readonly Material material;
    readonly ToonOutlineFeature.Settings settings;

    public ToonOutlinePass(Material material, ToonOutlineFeature.Settings settings)
    {
        this.material = material;
        this.settings = settings;
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        material.SetColor(OutlineColorId, settings.outlineColor);
        material.SetFloat(ThicknessId, settings.thickness);
        material.SetFloat(DepthStrengthId, settings.depthStrength);
        material.SetFloat(NormalStrengthId, settings.normalStrength);
        material.SetFloat(DepthThresholdId, settings.depthThreshold);
        material.SetFloat(DepthGrazingBiasId, settings.depthGrazingBias);
        material.SetFloat(NormalThresholdId, settings.normalThreshold);
        material.SetFloat(IDStrengthId, settings.idStrength);
        material.SetFloat(IDThresholdId, settings.idThreshold);
        material.SetFloat(FadeStartId, settings.fadeStart);
        material.SetFloat(FadeEndId, settings.fadeEnd);

        TextureHandle source = resourceData.activeColorTexture;

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;

        TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_ToonOutlineTemp", false);

        RenderGraphUtils.BlitMaterialParameters blit = new RenderGraphUtils.BlitMaterialParameters(source, temp, material, 0);
        renderGraph.AddBlitPass(blit, "Toon Outline");

        // NOT AddCopyPass: a copy requires source and destination to have the same MSAA
        // sample count, and the camera target is multisampled while the temp is not.
        // A blit resolves across the mismatch.
        renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero, passName: "Toon Outline Copy Back");
    }
}
