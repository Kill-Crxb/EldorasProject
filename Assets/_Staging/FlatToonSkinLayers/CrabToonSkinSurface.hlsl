// STAGING: CrabToonSurface.hlsl plus skin layers and the stealth fade. Promotes over
// Packages/com.crxb.crabsystem/Runtime/Rendering/Shaders/CrabToonSurface.hlsl.
//
// Every pass reads the base map and clips through here, so the chest slots and the
// stealth dither land in all five passes at once. The layer stack (makeup, tattoos,
// cloth) only matters to colour, so only ForwardLit calls ToonSkinLayers.
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

#ifdef _SKIN_LAYERS

TEXTURE2D(_MakeupMasks);
TEXTURE2D(_TattooAtlas);
TEXTURE2D(_Cloth0Tex);
TEXTURE2D(_Cloth1Tex);
TEXTURE2D(_Cloth2Tex);
TEXTURE2D(_Cloth3Tex);
TEXTURE2D(_Cloth4Tex);
TEXTURE2D(_Cloth5Tex);

// One inline sampler for every layer. Unity builds it from the name (point filter,
// clamp), so it costs one sampler slot instead of eight and ignores import settings.
SAMPLER(sampler_crab_point_clamp);

half3 BlendLayer(half3 below, half4 layer, half4 tint)
{
    return lerp(below, layer.rgb * tint.rgb, layer.a * tint.a);
}

// Bottom to top: makeup, tattoos, then Lining, Underwear, Sheer, Overlay, Clothes,
// Accessories. A layer with tint alpha 0 has no weight.
half3 ToonSkinLayers(half3 albedo, float2 uv, float2 uv2, half4 region)
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
    albedo = BlendLayer(albedo, half4(tattoo.rgb, tattoo.a * tatOn), _TatTint);

    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth0Tex, sampler_crab_point_clamp, uv), _Cloth0Tint);
    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth1Tex, sampler_crab_point_clamp, uv), _Cloth1Tint);
    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth2Tex, sampler_crab_point_clamp, uv), _Cloth2Tint);
    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth3Tex, sampler_crab_point_clamp, uv), _Cloth3Tint);
    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth4Tex, sampler_crab_point_clamp, uv), _Cloth4Tint);
    albedo = BlendLayer(albedo, SAMPLE_TEXTURE2D(_Cloth5Tex, sampler_crab_point_clamp, uv), _Cloth5Tint);

    return albedo;
}

#endif
