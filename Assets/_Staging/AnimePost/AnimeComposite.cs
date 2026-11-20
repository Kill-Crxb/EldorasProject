// Anime "satsuei" compositing layer, tuned per Volume. Drawn by AnimeCompositeFeature.
// Every intensity defaults to 0, so adding the override changes nothing until it is tuned.

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[VolumeComponentMenu("NinjaGame/Anime Composite")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class AnimeComposite : VolumeComponent
{
    [Header("Diffusion - soft glow over the whole frame")]
    [Tooltip("Strength of the blurred copy screened over the frame.")]
    public ClampedFloatParameter diffusionIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Blur width. Higher is softer and wider.")]
    public ClampedFloatParameter diffusionSpread = new ClampedFloatParameter(1.5f, 0.5f, 4f);
    [Tooltip("Luminance below which a pixel adds no glow. 0 = the whole frame glows.")]
    public ClampedFloatParameter diffusionThreshold = new ClampedFloatParameter(0f, 0f, 2f);
    [Tooltip("Softness of the threshold as a fraction of it. 0 = hard cutoff, 1 = fades in from black.")]
    public ClampedFloatParameter diffusionKnee = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Header("Gradient overlay - coloured light across the frame")]
    public ClampedFloatParameter gradientIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    public ColorParameter gradientTop = new ColorParameter(new Color(1f, 0.85f, 0.65f), false, false, true);
    public ColorParameter gradientBottom = new ColorParameter(new Color(0.55f, 0.65f, 0.9f), false, false, true);
    [Tooltip("Direction the top colour comes from, in degrees. 90 = from the top of the screen.")]
    public ClampedFloatParameter gradientAngle = new ClampedFloatParameter(90f, 0f, 360f);

    [Header("Depth tint - distance fades toward the sky colour")]
    public ClampedFloatParameter depthTintIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    public ColorParameter depthTintColor = new ColorParameter(new Color(0.72f, 0.82f, 0.95f), false, false, true);
    [Tooltip("Metres where the tint starts.")]
    public MinFloatParameter depthTintStart = new MinFloatParameter(30f, 0f);
    [Tooltip("Metres where the tint reaches full intensity.")]
    public MinFloatParameter depthTintEnd = new MinFloatParameter(250f, 0f);

    public bool IsActive()
    {
        return diffusionIntensity.value > 0f || gradientIntensity.value > 0f || depthTintIntensity.value > 0f;
    }
}
