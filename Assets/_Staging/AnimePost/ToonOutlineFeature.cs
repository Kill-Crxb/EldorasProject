// URP 17 / Unity 6 Render Graph renderer feature. Character materials carry an Object ID at
// or above Character Id Min; Target picks whether lines go on characters, the environment
// or both, and characters get their own thickness. Give each character part (skin, hair,
// clothes, eyes) a different ID above the minimum and ID edges draw clean lines between parts.
//
// Draws outlines by finding depth and normal discontinuities in screen space instead of
// extruding geometry. Lines are a uniform thickness regardless of mesh topology, and they
// appear wherever two surfaces meet - including between two separate meshes that overlap,
// which an inverted hull cannot do.
//
// Setup:
//   1. Add this feature to your URP Renderer asset (Add Renderer Feature > Toon Outline).
//   2. Assign NinjaGame/ToonOutlinePost to the Shader field.
//   3. Set Object ID on character materials (0.6 skin, 0.7 hair, 0.8 clothes, ...).
//
// Requires the DepthNormals prepass, which ConfigureInput requests below. Any shader that
// should receive lines needs a DepthNormals pass - FlatToon has one.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public enum OutlineTarget
{
    Both,
    Characters,
    Environment
}

public class ToonOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingSkybox;
        public Color outlineColor = Color.black;
        [Range(0.5f, 8f)] public float thickness = 1.4f;
        [Tooltip("Width of the soft ramp above each threshold, as a fraction of it. 0 = hard 1-px edges.")]
        [Range(0f, 2f)] public float edgeSoftness = 0.5f;
        [Tooltip("Object IDs at or above this count as character pixels.")]
        [Range(0.01f, 1f)] public float characterIdMin = 0.5f;
        [Tooltip("Characters needs Object IDs set on character materials, or nothing is drawn.")]
        public OutlineTarget target = OutlineTarget.Both;
        [Tooltip("Line thickness on character pixels, in pixels.")]
        [Range(0.5f, 8f)] public float characterThickness = 1.2f;

        [Header("Colour - anime lines are a darker shade of the surface, not pure black")]
        [Tooltip("0 = Outline Color, 1 = the surface colour darkened.")]
        [Range(0f, 1f)] public float colorFromSurface = 0.6f;
        [Range(0f, 1f)] public float surfaceDarkness = 0.35f;
        [Header("Depth edges - silhouettes against whatever is behind them")]
        [Range(0f, 1f)] public float depthStrength = 1f;
        [Range(0.001f, 0.5f)] public float depthThreshold = 0.02f;
        [Range(0f, 50f)] public float depthGrazingBias = 12f;

        [Header("Normal edges - creases and folds")]
        [Range(0f, 1f)] public float normalStrength = 1f;
        [Range(0.05f, 2f)] public float normalThreshold = 0.4f;
        [Tooltip("Crease lines on character pixels. A low-poly body turns sharply under the chest and arms, so it wants a higher threshold than the environment. 0 strength = silhouettes and ID lines only.")]
        [Range(0f, 1f)] public float characterNormalStrength = 0.5f;
        [Range(0.05f, 2f)] public float characterNormalThreshold = 1f;

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
    static readonly int CharacterNormalStrengthId = Shader.PropertyToID("_CharacterNormalStrength");
    static readonly int CharacterNormalThresholdId = Shader.PropertyToID("_CharacterNormalThreshold");
    static readonly int IDStrengthId = Shader.PropertyToID("_IDStrength");
    static readonly int IDThresholdId = Shader.PropertyToID("_IDThreshold");
    static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int CharacterIdMinId = Shader.PropertyToID("_CharacterIdMin");
    static readonly int TargetWeightsId = Shader.PropertyToID("_TargetWeights");
    static readonly int CharacterThicknessId = Shader.PropertyToID("_CharacterThickness");
    static readonly int ColorFromSurfaceId = Shader.PropertyToID("_ColorFromSurface");
    static readonly int SurfaceDarknessId = Shader.PropertyToID("_SurfaceDarkness");

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
        material.SetFloat(CharacterNormalStrengthId, settings.characterNormalStrength);
        material.SetFloat(CharacterNormalThresholdId, settings.characterNormalThreshold);
        material.SetFloat(IDStrengthId, settings.idStrength);
        material.SetFloat(IDThresholdId, settings.idThreshold);
        material.SetFloat(FadeStartId, settings.fadeStart);
        material.SetFloat(FadeEndId, settings.fadeEnd);
        material.SetFloat(EdgeSoftnessId, settings.edgeSoftness);
        material.SetFloat(CharacterIdMinId, settings.characterIdMin);
        material.SetVector(TargetWeightsId, TargetWeights(settings.target));
        material.SetFloat(CharacterThicknessId, settings.characterThickness);
        material.SetFloat(ColorFromSurfaceId, settings.colorFromSurface);
        material.SetFloat(SurfaceDarknessId, settings.surfaceDarkness);

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

    // x = environment weight, y = character weight.
    static Vector4 TargetWeights(OutlineTarget target)
    {
        if (target == OutlineTarget.Characters)
            return new Vector4(0f, 1f, 0f, 0f);

        if (target == OutlineTarget.Environment)
            return new Vector4(1f, 0f, 0f, 0f);

        return new Vector4(1f, 1f, 0f, 0f);
    }
}
