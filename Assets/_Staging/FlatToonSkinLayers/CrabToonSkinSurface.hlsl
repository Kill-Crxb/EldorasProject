// STAGING: CrabToonSurface.hlsl plus skin layers and the stealth fade. Promotes over
// Packages/com.crxb.crabsystem/Runtime/Rendering/Shaders/CrabToonSurface.hlsl.
//
// Every pass reads the base map and clips through here, so the chest slots and the
// stealth dither land in all five passes at once. The layer stack (makeup, tattoos,
// cloth) only matters to colour: ForwardLit runs all of it, Outline runs the cloth so the
// line takes the cloth's colour.
//
// The including pass declares TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap); and the
// shader declares the uniforms in UnityPerMaterial. _SKIN_LAYERS is declared by
// ForwardLit and Outline; the depth and shadow passes skip the chest offset because the
// skin is opaque in every slot, so where they sample makes no difference.

#pragma once

// 4x4 ordered dither. Each pixel in a 4x4 block gets its own threshold, so as _Fade
// drops the character loses an even scatter of pixels instead of a solid region.
static const float Bayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

half DitherThreshold(float2 pixel)
{
    uint2 p = uint2(pixel) % 4;
    return (Bayer4[p.y * 4 + p.x] + 0.5h) / 16.0h;
}

// The chest island's bounds in base-map UV, slot 0. A rect test needs no vertex data,
// which leaves vertex colour free for the tattoo regions.
half ChestMask(float2 uv)
{
    float2 inside = step(_ChestRect.xy, uv) * step(uv, _ChestRect.zw);
    return inside.x * inside.y;
}

// Gradients always come from the unshifted UV. The chest shift is a constant jump, and
// helper pixels just outside the island would otherwise see it as a huge derivative and
// pick the smallest mip along the island's edge. Without the shift this is an ordinary sample.
half4 ToonBaseAlbedo(float2 uv)
{
    float2 slotUV = uv;

    #ifdef _SKIN_LAYERS
        slotUV += _ChestStep * _ChestSlotOffset.xy * ChestMask(uv);
    #endif

    return SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, slotUV, ddx(uv), ddy(uv)) * _BaseColor;
}

// At _Fade = 1 the largest threshold is 15.5/16, so nothing is ever discarded.
void ToonClip(half alpha, float2 pixel)
{
    #ifdef _ALPHATEST_ON
        clip(alpha - _Cutoff);
    #endif

    clip(_Fade - DitherThreshold(pixel));
}

// What the cloth stack hands to lighting. A layer overrides these by its weight the same
// way it overrides colour, so the top layer wins: cloth gets its own shade tint and
// specular, and an ink line where a painted edge has no mesh edge to put an outline on.
struct ClothSurface
{
    half3 albedo;
    half  shadeMix;   // 0 the skin's Shade Tint, 1 Cloth Shade Tint
    half  specular;   // multiplier on the toon specular
    half3 inkColor;
    half  ink;
};

ClothSurface BareSkin(half3 albedo)
{
    ClothSurface s;
    s.albedo = albedo;
    s.shadeMix = 0.0h;
    s.specular = 1.0h;
    s.inkColor = half3(0, 0, 0);
    s.ink = 0.0h;
    return s;
}

#ifdef _SKIN_LAYERS

TEXTURE2D(_MakeupMasks);
TEXTURE2D(_TattooAtlas);
TEXTURE2D(_Cloth0Tex);
TEXTURE2D(_Cloth1Tex);
TEXTURE2D(_Cloth2Tex);
TEXTURE2D(_Cloth3Tex);
TEXTURE2D(_Cloth4Tex);
TEXTURE2D(_Cloth5Tex);

// Inline samplers: Unity builds them from the name and ignores import settings. Makeup
// and tattoos stay point sampled. Cloth is bilinear with mips and gets its hard edge from
// EdgeDistancePx instead, which stays crisp up close and doesn't shimmer at a distance.
SAMPLER(sampler_crab_point_clamp);
SAMPLER(sampler_crab_linear_clamp);

half3 BlendLayer(half3 below, half4 layer, half4 tint)
{
    return lerp(below, layer.rgb * tint.rgb, layer.a * tint.a);
}

// Bottom to top: makeup, then tattoos. Cloth goes on after, in ToonClothLayers.
half3 ToonSkinMarkings(half3 albedo, float2 uv, float2 uv2, half4 region)
{
    half4 makeup = SAMPLE_TEXTURE2D(_MakeupMasks, sampler_crab_point_clamp, uv);
    albedo = BlendLayer(albedo, half4(1, 1, 1, makeup.r), _MakeupTintR);
    albedo = BlendLayer(albedo, half4(1, 1, 1, makeup.g), _MakeupTintG);
    albedo = BlendLayer(albedo, half4(1, 1, 1, makeup.b), _MakeupTintB);
    albedo = BlendLayer(albedo, half4(1, 1, 1, makeup.a), _MakeupTintA);

    // Vertex colour says which region a pixel is in (R womb, G back, B arm, A spare).
    // Regions never overlap, so summing picks the one region's design offset.
    float2 tatOffset = region.r * _TatWomb.xy + region.g * _TatBack.xy + region.b * _TatArm.xy + region.a * _TatSpare.xy;
    half tatOn = region.r * _TatWomb.w + region.g * _TatBack.w + region.b * _TatArm.w + region.a * _TatSpare.w;
    half4 tattoo = SAMPLE_TEXTURE2D(_TattooAtlas, sampler_crab_point_clamp, uv2 + tatOffset);
    return BlendLayer(albedo, half4(tattoo.rgb, tattoo.a * tatOn), _TatTint);
}

// The outline is a world width. The ink line matches it, converted to pixels at this
// depth, and fades out with the outline.
half OutlinePixels(float3 positionWS)
{
    float depth = -TransformWorldToView(positionWS).z;
    float pixelsPerUnit = UNITY_MATRIX_P[1][1] * _ScreenParams.y * 0.5 / max(depth, 1e-3);
    return _OutlineWidth * pixelsPerUnit * GetOutlineDistanceFade(positionWS);
}

// The edge band reads as a second outline on a small, distant figure, so it fades out
// between Sheer Edge Fade Near and Far.
half SheerBandFade(float3 positionWS)
{
    float range = max(_SheerEdgeFadeFar - _SheerEdgeFadeNear, 1e-3);
    return 1.0h - saturate((distance(positionWS, _WorldSpaceCameraPos) - _SheerEdgeFadeNear) / range);
}

// A sheer is thicker at a glancing angle: the view crosses more of the weave, so less
// skin shows. Drawn as two flat tones like the cel terminator: face-on keeps the painted
// opacity, past the edge line it is stacked _SheerEdgeDensity times, 1 - (1 - a)^n.
half SheerOpacity(half opacity, half ndotv, half bandFade)
{
    half band = smoothstep(_SheerEdgeStart - _SheerEdgeSoftness, _SheerEdgeStart + _SheerEdgeSoftness, 1.0h - ndotv);
    half edge = 1.0h - pow(saturate(1.0h - opacity), _SheerEdgeDensity);
    return lerp(opacity, edge, band * bandFade);
}

// Pixels from the painted edge (alpha 0.5), positive inside. Bilinear alpha ramps across
// one texel; dividing by its rate on screen gives the same hard edge at any distance.
half EdgeDistancePx(half alpha)
{
    return (alpha - 0.5h) / max(fwidth(alpha), 1e-4h);
}

// params: x filter (0 paints over, 1 multiplies what's under it, so skin keeps its hue),
// y cloth shade, z specular, w ink weight (1 = outline weight, 0 = off).
void AddClothLayer(inout ClothSurface s, half4 texel, half weight, half3 tint, half4 params, half inkPx)
{
    half3 paint = texel.rgb * tint;
    half3 color = lerp(paint, s.albedo * paint, params.x);
    s.albedo = lerp(s.albedo, color, weight);
    s.shadeMix = lerp(s.shadeMix, params.y, weight);
    s.specular = lerp(s.specular, params.z, weight);
    s.ink *= 1.0h - weight;

    half edge = EdgeDistancePx(texel.a);
    half inkLine = step(0.0h, edge) * saturate(inkPx * params.w - edge);
    half3 ink = lerp(_OutlineColor.rgb, paint * _OutlineTintDarkness, _OutlineTintStrength);
    s.inkColor = lerp(s.inkColor, ink, inkLine);
    s.ink = max(s.ink, inkLine);
}

// A slot with tint alpha 0 is off. The branch is on a uniform, so every pixel takes the
// same path and an empty slot costs no texture read.
void HardLayer(inout ClothSurface s, TEXTURE2D_PARAM(tex, smp), float2 uv, half4 tint, half4 params, half inkPx)
{
    if (tint.a <= 0.0h) return;

    half4 texel = SAMPLE_TEXTURE2D(tex, smp, uv);
    half weight = saturate(EdgeDistancePx(texel.a) + 0.5h) * tint.a;
    AddClothLayer(s, texel, weight, tint.rgb, params, inkPx);
}

// The sheer keeps its painted alpha inside (dense sections are steps in it) and is only
// cut hard at its outer edge, so paint it at 0.5 or more wherever it exists.
void SheerLayer(inout ClothSurface s, float2 uv, half ndotv, half bandFade, half inkPx)
{
    if (_Cloth2Tint.a <= 0.0h) return;

    half4 texel = SAMPLE_TEXTURE2D(_Cloth2Tex, sampler_crab_linear_clamp, uv);
    half cut = saturate(EdgeDistancePx(texel.a) + 0.5h);
    half weight = SheerOpacity(texel.a * _Cloth2Tint.a, ndotv, bandFade) * cut;
    AddClothLayer(s, texel, weight, _Cloth2Tint.rgb, _Cloth2Params, inkPx);
}

// Bottom to top: Lining, Underwear, Sheer, Overlay, Clothes, Accessories.
ClothSurface ToonClothLayers(half3 albedo, float2 uv, half ndotv, half bandFade, half inkPx)
{
    ClothSurface s = BareSkin(albedo);
    HardLayer(s, TEXTURE2D_ARGS(_Cloth0Tex, sampler_crab_linear_clamp), uv, _Cloth0Tint, _Cloth0Params, inkPx);
    HardLayer(s, TEXTURE2D_ARGS(_Cloth1Tex, sampler_crab_linear_clamp), uv, _Cloth1Tint, _Cloth1Params, inkPx);
    SheerLayer(s, uv, ndotv, bandFade, inkPx);
    HardLayer(s, TEXTURE2D_ARGS(_Cloth3Tex, sampler_crab_linear_clamp), uv, _Cloth3Tint, _Cloth3Params, inkPx);
    HardLayer(s, TEXTURE2D_ARGS(_Cloth4Tex, sampler_crab_linear_clamp), uv, _Cloth4Tint, _Cloth4Params, inkPx);
    HardLayer(s, TEXTURE2D_ARGS(_Cloth5Tex, sampler_crab_linear_clamp), uv, _Cloth5Tint, _Cloth5Params, inkPx);
    return s;
}

#endif
