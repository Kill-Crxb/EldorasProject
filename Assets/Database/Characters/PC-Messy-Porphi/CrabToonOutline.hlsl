// Shared outline maths: hull extrusion and the depth offset trick.
//
// The Z offset is adapted from ColinLeung-NiloCat's UnityURPToonLitShaderExample (MIT).
// It is the most useful thing in that repo and has nothing to do with toon shading:
// https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
//
// Uniforms the including shader must declare:
//   half _OutlineWidth; half _OutlineWidthPixels; half _OutlineSilhouetteBias;
//   float _OutlineFadeNear; float _OutlineFadeFar; float _OutlineZOffset;

#pragma once

// Moves a vertex's DEPTH without moving it on screen. An imaginary vertex is pushed along
// view Z, and only the resulting clip-space z is kept.
//
// Positive offset pushes away from the camera. Use it to sink the outline hull behind the
// face so a character's features never get a line drawn through them, to lift eyebrows in
// front of a fringe, or to settle z-fighting without touching geometry.
float4 ApplyZOffset(float4 positionCS, float viewSpaceOffset)
{
    if (unity_OrthoParams.w == 0)
    {
        float2 projZW = UNITY_MATRIX_P[2].zw;
        float offsetViewZ = -positionCS.w - viewSpaceOffset;
        float offsetClipZ = offsetViewZ * projZW[0] + projZW[1];
        positionCS.z = offsetClipZ * positionCS.w / (-offsetViewZ);
        return positionCS;
    }

    positionCS.z += -viewSpaceOffset / _ProjectionParams.z;
    return positionCS;
}

// No FOV compensation here on purpose. A width given in world units should stay world
// units; a width given in pixels is already FOV-independent because it is applied after
// projection. An earlier version divided the world width by tan(halfFov) read from
// UNITY_MATRIX_P, which was wrong twice over: the correction was inverted, and on any
// platform or target where the projection matrix has a flipped Y that element is negative,
// so the divisor clamped to its epsilon and the hull expanded by hundreds of metres.

// Direction and amount are decided separately. Using the raw length of the view-space
// normal as the amount thins the line wherever the nearest vertex sits off the true
// silhouette, worst on whichever axis has fewer edge loops. Normalizing alone shoves
// front-facing verts sideways in an arbitrary direction and speckles the surface. Ramping
// quickly to full width gives even thickness without the specks.
float2 GetOutlineOffset(float3 normalVS, half bias)
{
    float length2D = length(normalVS.xy);
    float2 direction = length2D > 1e-5 ? normalVS.xy / length2D : float2(0.0, 0.0);
    return direction * saturate(length2D * bias);
}

float GetOutlineDistanceFade(float3 positionWS)
{
    float viewDistance = length(GetCameraPositionWS() - positionWS);
    return 1.0 - saturate((viewDistance - _OutlineFadeNear) / max(_OutlineFadeFar - _OutlineFadeNear, 1e-4));
}
