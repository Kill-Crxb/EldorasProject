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

// Per light: 0 in shadow, 1 in light. Cast shadows are folded in before the step so a
// shadow edge lands as hard as the terminator instead of feathering across it.
half ToonRampValue(half ndotl, half attenuation)
{
    half lit = ndotl * lerp(1.0h, attenuation, _ShadowStrength);

    #ifdef _USE_RAMP
        half rampU = saturate(lit * 0.5h + 0.5h);
        half ramp = SAMPLE_TEXTURE2D(_ShadeRamp, sampler_ShadeRamp, float2(rampU, 0.5)).r;
    #else
        // Quantize the RAW light, then soften inside each band. An earlier version
        // stepped first and quantized the result, but the stepped value is already 0 or 1,
        // so quantizing it did nothing and Tone Count was a dead slider.
        //
        // Offsetting by half a band puts _ShadeThreshold on the first band boundary, so
        // raising Tone Count adds bands above the terminator instead of moving it.
        half steps = max(floor(_Bands), 1.0h);
        half level = saturate(lit - _ShadeThreshold + 0.5h / steps);

        half scaled = level * steps;
        half band = floor(scaled);
        half within = smoothstep(0.5h - _ShadeSoftness, 0.5h + _ShadeSoftness + 1e-4h, scaled - band);

        half ramp = (band + within) / steps;
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
// gradient the whole shader exists to avoid, but the constant term still picks up the
// environment's average colour so a character reads as being in the scene.
half3 ToonAmbient()
{
    half3 ambient = SampleSH(0);
    return max(_IndirectMinColor.rgb, ambient) * _IndirectStrength;
}

// Shadow sampling position, pushed along the light direction. Faces are the reason this
// exists: the shadow map resolves a nose or a fringe into blotches across the cheek, and
// stepping the test position away from the surface skips past them.
float4 GetToonShadowCoord(float3 positionWS, float3 lightDirectionWS)
{
    float3 offsetPositionWS = positionWS + lightDirectionWS * (_ShadowPosOffset + _IsFace);
    return TransformWorldToShadowCoord(offsetPositionWS);
}

// Direct light decides the shade tint; ambient acts as a floor under it rather than adding
// to it, so a bright environment lifts the shadows without washing the terminator away.
half3 CompositeToon(half3 albedo, half3 shadeTint, half3 directRamp, half3 ambient)
{
    half3 direct = lerp(shadeTint, half3(1, 1, 1), saturate(directRamp));
    return albedo * max(ambient, direct);
}
