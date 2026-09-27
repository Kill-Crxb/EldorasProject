// Shared toon lighting equation. Included by Crab/FlatToon and Crab/FlatToonTriplanar so
// characters and environment cannot drift apart.
//
// This is the file to edit when you want to change how light behaves. The .shader files
// are plumbing - surface data in, this equation applied, passes out.
//
// Structural idea and several of the fixes below are adapted from ColinLeung-NiloCat's
// UnityURPToonLitShaderExample (MIT). Specifically: the separated lighting-equation file,
// the constant-term-only ambient, the light clamping, and the shadow test position offset.
// https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
//
// Uniforms this file expects the including shader to declare in UnityPerMaterial:
//   half4 _ShadeColor; half _ShadeThreshold; half _ShadeSoftness; half _Bands;
//   half _LightColorInfluence; half _ShadowStrength; half _AdditionalLightScale;
//   half4 _IndirectMinColor; half _IndirectStrength;
//   half _IsFace; half _FaceShadowLift; float _ShadowPosOffset;
//   half _ShadowTexAmount; half _ShadowTexSoftness; half _ShadowTexBlend;
// And, when _USE_RAMP is enabled: TEXTURE2D(_ShadeRamp); SAMPLER(sampler_ShadeRamp);

#pragma once

// Per light: 0 in shadow, 1 in light.
//
// The band comes from N dot L alone and the cast shadow is applied afterwards as a
// multiplier. An earlier version multiplied the signed N dot L by the shadow term, which
// pulls a shadowed pixel toward 0 - the terminator - rather than toward shade: with
// Terminator Position at or below 0 cast shadows never showed on lit surfaces, and the
// dark side could flip to lit. Stepping the filtered attenuation keeps the shadow edge as
// hard as the terminator.
half ToonRampValue(half ndotl, half attenuation)
{
    half castLit = smoothstep(0.5h - _ShadeSoftness, 0.5h + _ShadeSoftness + 1e-4h, attenuation);
    half castShadow = lerp(1.0h, castLit, _ShadowStrength);

    #ifdef _USE_RAMP
        half rampU = saturate(ndotl * 0.5h + 0.5h) * castShadow;
        half ramp = SAMPLE_TEXTURE2D(_ShadeRamp, sampler_ShadeRamp, float2(rampU, 0.5)).r;
    #else
        // Quantize the RAW light, then soften inside each band. An earlier version
        // stepped first and quantized the result, but the stepped value is already 0 or 1,
        // so quantizing it did nothing and Tone Count was a dead slider.
        //
        // Offsetting by half a band puts _ShadeThreshold on the first band boundary, so
        // raising Tone Count adds bands above the terminator instead of moving it.
        half steps = max(floor(_Bands), 1.0h);
        half level = saturate(ndotl - _ShadeThreshold + 0.5h / steps);

        half scaled = level * steps;
        half band = floor(scaled);
        half within = smoothstep(0.5h - _ShadeSoftness, 0.5h + _ShadeSoftness + 1e-4h, scaled - band);

        half ramp = (band + within) / steps * castShadow;
    #endif

    // A face shaded by raw N dot L gives blobs around the nose and brow no matter how
    // clean the normals are. Lifting the floor stops it ever going fully dark, which is
    // most of what a face-specific shadow solution buys, for one lerp.
    ramp = lerp(ramp, lerp(_FaceShadowLift, 1.0h, ramp), _IsFace);

    return ramp;
}

half3 ToonLightContribution(Light light, float3 normalWS, half occlusion, bool isAdditional)
{
    // Clamped: a point light parked on a surface otherwise returns a huge attenuation and
    // blows the flat shading out to white.
    half distanceAttenuation = min(4.0h, light.distanceAttenuation);

    half ndotl = dot(normalWS, light.direction);
    half ramp = ToonRampValue(ndotl, light.shadowAttenuation) * occlusion * distanceAttenuation;

    // saturate() keeps an over-bright light's hue without letting its magnitude through.
    half3 tint = lerp(half3(1, 1, 1), saturate(light.color), _LightColorInfluence);

    return ramp * tint * (isAdditional ? _AdditionalLightScale : 1.0h);
}

// Halftone. The pattern texture supplies the THRESHOLD and the light level is what gets
// compared against it, so dots grow in the dark and shrink toward the light instead of
// being a fixed stamp over the shadow region. It modulates the lighting rather than the
// colour, which is why it reads as tone rather than as a decal.
//
// Callers decide what space the pattern is sampled in and pass the sampled value here:
// characters use screen or object-anchored UVs, static geometry uses world projection.
half3 ApplyHalftone(half3 directRamp, half pattern)
{
    half level = saturate(max(max(directRamp.r, directRamp.g), directRamp.b));
    half target = level + _ShadowTexAmount;

    half shaped = clamp(smoothstep(pattern - _ShadowTexSoftness, pattern + _ShadowTexSoftness + 1e-4h, target), 0.0h, target);
    return lerp(directRamp, half3(shaped, shaped, shaped), _ShadowTexBlend);
}

// Only the constant SH term. Taking the directional part back would reintroduce the smooth
// gradient the whole shader exists to avoid, but the constant term still carries the
// environment's colour, which is what CompositeToon wants from it.
//
// No _IndirectStrength here any more - this returns ambient at full brightness and
// CompositeToon decides how much of its HUE to take. See the note there.
half3 ToonAmbient()
{
    return max(_IndirectMinColor.rgb, SampleSH(0));
}

// Shadow sampling position, pushed along the light direction. Faces are the reason this
// exists: the shadow map resolves a nose or a fringe into blotches across the cheek, and
// stepping the test position away from the surface skips past them.
float4 GetToonShadowCoord(float3 positionWS, float3 lightDirectionWS)
{
    float3 offsetPositionWS = positionWS + lightDirectionWS * (_ShadowPosOffset + _IsFace);
    return TransformWorldToShadowCoord(offsetPositionWS);
}

// Ambient supplies HUE, never brightness. The shade tint is tinted toward the
// environment's colour at unchanged luminance, so a character picks up the location while
// the ramp keeps sole control of contrast.
//
// This used to be max(ambient, direct), a floor: below the tint's luminance ambient did
// nothing at all, and above it, it replaced the tint outright. At Indirect Strength 2 the
// Zoo's sky drove the shade to within 15% of lit, which erased the terminator and every
// cast shadow with it.
half3 CompositeToon(half3 albedo, half3 shadeTint, half3 directRamp, half3 ambient)
{
    // Rec. 709 luminance inline rather than Luminance() from Color.hlsl - this include
    // only assumes Core.hlsl, and Color.hlsl is not guaranteed to be in scope.
    half ambientLuma = max(dot(ambient, half3(0.2126h, 0.7152h, 0.0722h)), 1e-4h);
    half3 ambientHue = ambient / ambientLuma;
    half3 shade = shadeTint * lerp(half3(1, 1, 1), ambientHue, saturate(_IndirectStrength));

    half3 direct = lerp(shade, half3(1, 1, 1), saturate(directRamp));
    return albedo * direct;
}
