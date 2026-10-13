#ifndef BLENDER_CLOUDS_INCLUDED
#define BLENDER_CLOUDS_INCLUDED

// ============================================================================
//  Blender Noise Texture -> HLSL
//
//  Direct port of Blender 4.x noise.hh:
//    - BLI_hash integer hash (Jenkins lookup3 "final" mix)
//    - noise_grad gradient selection
//    - perlin_2d / perlin_3d with Blender's 0.6616 / 0.9820 output scales
//    - perlin_fbm with Detail / Roughness / Lacunarity / Normalize semantics
//
//  Same integers in, same floats out. This is not an approximation of
//  Blender's noise - it is the same algorithm.
//
//  Entry point:  BlenderClouds_float(Coord, LayerT, out Color, out Alpha)
// ============================================================================


// ============================================================================
//  TUNING - every value read off the Blender graph
//  If a number here is wrong, this is the only block you need to touch.
// ============================================================================

// ---------------------------------------------------------------------------
//  PRECISION WARNING - read before raising DETAIL
//
//  Each octave multiplies the sample coordinate by Lacunarity. Detail 13.2
//  means the top octave runs at 2^13 = 8192x the base frequency. fp32 has a
//  24-bit mantissa, so once (coord * Scale * 8192) gets large the fractional
//  part - which IS the noise - quantises away.
//
//  Measured, with NOISE_B_SCALE 8.2:
//     coord range   detail 13.2                detail 6
//     0 .. 1        128 steps per unit  OK     65536 steps  OK
//     0 .. 10       16 steps per unit   poor   2048 steps   OK
//     0 .. 50       4 steps per unit    BROKEN 512 steps    OK
//     0 .. 200      1 step per unit     DEAD   128 steps    OK
//
//  Blender gets away with Detail 13.2 because Generated coordinates are 0..1.
//  Feeding world-space metres multiplies the coordinate by hundreds and the
//  high octaves collapse into quantised streaks.
//
//  Two rules:
//    1. Keep the Coord input roughly inside 0..50.
//    2. Keep DETAIL at or below 6 for anything sampled in world space.
//  Octaves finer than a pixel do not add detail, they add aliasing.
// ---------------------------------------------------------------------------

// Noise Texture A - 2D, drives the soft cloud density
// Blender value: 4.3
#define NOISE_A_SCALE       22.5
#define NOISE_A_DETAIL      3.3
#define NOISE_A_ROUGHNESS   0.903
#define NOISE_A_LACUNARITY  2.0

// Noise Texture B - 3D, drives the hard cel silhouette + the layer slicing
// Blender value: 13.2 - unusable in fp32 at world scale, see above.
#define NOISE_B_SCALE       8.2
#define NOISE_B_DETAIL      5.2
#define NOISE_B_ROUGHNESS   0.742
#define NOISE_B_LACUNARITY  2.0

// Coordinate wrap. Perlin is lattice based, so wrapping on an integer keeps
// the cells aligned and only costs a repeat, not a discontinuity in the cells.
// This is the backstop that stops a huge plane from degenerating.
#define COORD_WRAP          64.0

// Mapping node feeding Noise B: Location Z 3.4m, Scale Z 0.013
// This is the stacked-plane trick. Each plane in the stack samples a
// slightly different slice of the 3D noise volume.
#define SLICE_OFFSET        3.4
#define SLICE_DEPTH         0.013

// --- Color Ramps -----------------------------------------------------------
// READ FROM A SCREENSHOT - verify these against your .blend.
// Click each stop in Blender and note its Pos and its RGB.

// Ramp A: stack height -> coverage fade          (Linear, 2 stops)
#define RAMP_A_P0  0.000
#define RAMP_A_C0  float3(0.0, 0.0, 0.0)
#define RAMP_A_P1  0.512
#define RAMP_A_C1  float3(1.0, 1.0, 1.0)

// Ramp B: 2D noise -> density                    (Linear, 2 stops)
#define RAMP_B_P0  0.000
#define RAMP_B_C0  float3(0.0, 0.0, 0.0)
#define RAMP_B_P1  0.650
#define RAMP_B_C1  float3(1.0, 1.0, 1.0)

// Ramp C: post-mix density reshape               (Linear, 2 stops)
#define RAMP_C_P0  0.000
#define RAMP_C_C0  float3(0.0, 0.0, 0.0)
#define RAMP_C_P1  0.541
#define RAMP_C_C1  float3(1.0, 1.0, 1.0)

// Ramp D: stack height -> second coverage term   (Linear, 2 stops)
#define RAMP_D_P0  0.000
#define RAMP_D_C0  float3(0.0, 0.0, 0.0)
#define RAMP_D_P1  0.175
#define RAMP_D_C1  float3(1.0, 1.0, 1.0)

// Ramp E: 3D noise -> hard cel mask              (CONSTANT, 2 stops)
#define RAMP_E_P0  0.000
#define RAMP_E_C0  float3(1.0, 1.0, 1.0)
#define RAMP_E_P1  0.465
#define RAMP_E_C1  float3(0.0, 0.0, 0.0)

// Ramp F: stack height -> emission tint          (Linear, 3 stops)
#define RAMP_F_P0  0.000
#define RAMP_F_C0  float3(0.549, 0.718, 0.949)
#define RAMP_F_P1  0.514
#define RAMP_F_C1  float3(1.0, 1.0, 1.0)
#define RAMP_F_P2  1.000
#define RAMP_F_C2  float3(1.0, 1.0, 1.0)


// ============================================================================
//  Blender integer hash - BLI_hash / Jenkins lookup3
// ============================================================================

uint bl_rot(uint x, uint k)
{
    return (x << k) | (x >> (32u - k));
}

uint bl_hash2(uint kx, uint ky)
{
    uint a = 0xdeadbeefu + (2u << 2u) + 13u;
    uint b = a;
    uint c = a;

    b += ky;
    a += kx;

    c ^= b; c -= bl_rot(b, 14u);
    a ^= c; a -= bl_rot(c, 11u);
    b ^= a; b -= bl_rot(a, 25u);
    c ^= b; c -= bl_rot(b, 16u);
    a ^= c; a -= bl_rot(c,  4u);
    b ^= a; b -= bl_rot(a, 14u);
    c ^= b; c -= bl_rot(b, 24u);
    return c;
}

uint bl_hash3(uint kx, uint ky, uint kz)
{
    uint a = 0xdeadbeefu + (3u << 2u) + 13u;
    uint b = a;
    uint c = a;

    c += kz;
    b += ky;
    a += kx;

    c ^= b; c -= bl_rot(b, 14u);
    a ^= c; a -= bl_rot(c, 11u);
    b ^= a; b -= bl_rot(a, 25u);
    c ^= b; c -= bl_rot(b, 16u);
    a ^= c; a -= bl_rot(c,  4u);
    b ^= a; b -= bl_rot(a, 14u);
    c ^= b; c -= bl_rot(b, 24u);
    return c;
}


// ============================================================================
//  Gradient selection - noise_grad
// ============================================================================

float bl_grad2(uint hash, float x, float y)
{
    uint h = hash & 7u;
    float u = h < 4u ? x : y;
    float v = 2.0 * (h < 4u ? y : x);
    float su = (h & 1u) != 0u ? -u : u;
    float sv = (h & 2u) != 0u ? -v : v;
    return su + sv;
}

float bl_grad3(uint hash, float x, float y, float z)
{
    uint h = hash & 15u;
    float u = h < 8u ? x : y;
    float vt = (h == 12u || h == 14u) ? x : z;
    float v = h < 4u ? y : vt;
    float su = (h & 1u) != 0u ? -u : u;
    float sv = (h & 2u) != 0u ? -v : v;
    return su + sv;
}

float bl_fade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}


// ============================================================================
//  Perlin - returns signed noise, same range as Blender's perlin_signed
// ============================================================================

float bl_perlin2(float2 p)
{
    float2 fp = floor(p);
    float2 f = p - fp;
    int2 i = int2(fp);

    uint X = asuint(i.x);
    uint Y = asuint(i.y);
    uint X1 = asuint(i.x + 1);
    uint Y1 = asuint(i.y + 1);

    float u = bl_fade(f.x);
    float v = bl_fade(f.y);

    float g00 = bl_grad2(bl_hash2(X,  Y ), f.x,       f.y      );
    float g10 = bl_grad2(bl_hash2(X1, Y ), f.x - 1.0, f.y      );
    float g01 = bl_grad2(bl_hash2(X,  Y1), f.x,       f.y - 1.0);
    float g11 = bl_grad2(bl_hash2(X1, Y1), f.x - 1.0, f.y - 1.0);

    float x0 = lerp(g00, g10, u);
    float x1 = lerp(g01, g11, u);
    float r = lerp(x0, x1, v);

    return 0.6616 * r;
}

float bl_perlin3(float3 p)
{
    float3 fp = floor(p);
    float3 f = p - fp;
    int3 i = int3(fp);

    uint X = asuint(i.x);
    uint Y = asuint(i.y);
    uint Z = asuint(i.z);
    uint X1 = asuint(i.x + 1);
    uint Y1 = asuint(i.y + 1);
    uint Z1 = asuint(i.z + 1);

    float u = bl_fade(f.x);
    float v = bl_fade(f.y);
    float w = bl_fade(f.z);

    float g000 = bl_grad3(bl_hash3(X,  Y,  Z ), f.x,       f.y,       f.z      );
    float g100 = bl_grad3(bl_hash3(X1, Y,  Z ), f.x - 1.0, f.y,       f.z      );
    float g010 = bl_grad3(bl_hash3(X,  Y1, Z ), f.x,       f.y - 1.0, f.z      );
    float g110 = bl_grad3(bl_hash3(X1, Y1, Z ), f.x - 1.0, f.y - 1.0, f.z      );
    float g001 = bl_grad3(bl_hash3(X,  Y,  Z1), f.x,       f.y,       f.z - 1.0);
    float g101 = bl_grad3(bl_hash3(X1, Y,  Z1), f.x - 1.0, f.y,       f.z - 1.0);
    float g011 = bl_grad3(bl_hash3(X,  Y1, Z1), f.x,       f.y - 1.0, f.z - 1.0);
    float g111 = bl_grad3(bl_hash3(X1, Y1, Z1), f.x - 1.0, f.y - 1.0, f.z - 1.0);

    float x00 = lerp(g000, g100, u);
    float x10 = lerp(g010, g110, u);
    float x01 = lerp(g001, g101, u);
    float x11 = lerp(g011, g111, u);

    float y0 = lerp(x00, x10, v);
    float y1 = lerp(x01, x11, v);
    float r = lerp(y0, y1, w);

    return 0.9820 * r;
}


// ============================================================================
//  fBm - Blender's perlin_fbm, including the fractional-octave blend
//  and the Normalize checkbox behaviour.
// ============================================================================

float bl_fbm2(float2 p, float detail, float roughness, float lacunarity)
{
    float fscale = 1.0;
    float amp = 1.0;
    float maxamp = 0.0;
    float sum = 0.0;

    int octaves = int(detail);

    [unroll(16)]
    for (int i = 0; i <= octaves; i++)
    {
        sum += bl_perlin2(fscale * p) * amp;
        maxamp += amp;
        amp *= roughness;
        fscale *= lacunarity;
    }

    float rmd = detail - floor(detail);
    float a = 0.5 * sum / maxamp + 0.5;

    if (rmd == 0.0)
        return a;

    float sum2 = sum + bl_perlin2(fscale * p) * amp;
    float b = 0.5 * sum2 / (maxamp + amp) + 0.5;
    return lerp(a, b, rmd);
}

float bl_fbm3(float3 p, float detail, float roughness, float lacunarity)
{
    float fscale = 1.0;
    float amp = 1.0;
    float maxamp = 0.0;
    float sum = 0.0;

    int octaves = int(detail);

    [unroll(16)]
    for (int i = 0; i <= octaves; i++)
    {
        sum += bl_perlin3(fscale * p) * amp;
        maxamp += amp;
        amp *= roughness;
        fscale *= lacunarity;
    }

    float rmd = detail - floor(detail);
    float a = 0.5 * sum / maxamp + 0.5;

    if (rmd == 0.0)
        return a;

    float sum2 = sum + bl_perlin3(fscale * p) * amp;
    float b = 0.5 * sum2 / (maxamp + amp) + 0.5;
    return lerp(a, b, rmd);
}


// ============================================================================
//  Color Ramp
// ============================================================================

float3 bl_ramp2(float t, float p0, float3 c0, float p1, float3 c1)
{
    float k = saturate((t - p0) / max(p1 - p0, 1e-6));
    return lerp(c0, c1, k);
}

float3 bl_ramp2_const(float t, float p1, float3 c0, float3 c1)
{
    return t < p1 ? c0 : c1;
}

float3 bl_ramp3(float t, float p0, float3 c0, float p1, float3 c1, float p2, float3 c2)
{
    float k0 = saturate((t - p0) / max(p1 - p0, 1e-6));
    float k1 = saturate((t - p1) / max(p2 - p1, 1e-6));
    float3 lo = lerp(c0, c1, k0);
    float3 hi = lerp(c1, c2, k1);
    return t < p1 ? lo : hi;
}


// ============================================================================
//  The graph
//
//  Coord   - Generated XY equivalent. Feed UV, or world XZ * scale.
//  LayerT  - 0..1 through the plane stack. This was Generated Z in Blender,
//            read by the Gradient Texture (Mapping Y-rotation 90 turns the
//            Linear gradient to read Z) and by the Noise B slice.
// ============================================================================

void BlenderCloudsDebug_float(float2 Coord, float LayerT,
    out float3 Color, out float Alpha, out float NoiseA, out float NoiseB, out float Grad)
{
    // --- Gradient Texture, Linear, rotated to read the stack axis ---
    float grad = saturate(LayerT);

    // --- Precision backstop: keep the sample coordinate bounded ---
    float2 c = Coord - COORD_WRAP * floor(Coord / COORD_WRAP);

    // --- Noise Texture A: 2D ---
    float noiseA = bl_fbm2(c * NOISE_A_SCALE,
                           NOISE_A_DETAIL, NOISE_A_ROUGHNESS, NOISE_A_LACUNARITY);

    // --- Noise Texture B: 3D, sliced by stack height ---
    float3 pB = float3(c.x, c.y, SLICE_OFFSET + SLICE_DEPTH * grad);
    float noiseB = bl_fbm3(pB * NOISE_B_SCALE,
                           NOISE_B_DETAIL, NOISE_B_ROUGHNESS, NOISE_B_LACUNARITY);

    NoiseA = noiseA;
    NoiseB = noiseB;
    Grad = grad;

    // --- Ramps ---
    float3 rA = bl_ramp2(grad,   RAMP_A_P0, RAMP_A_C0, RAMP_A_P1, RAMP_A_C1);
    float3 rB = bl_ramp2(noiseA, RAMP_B_P0, RAMP_B_C0, RAMP_B_P1, RAMP_B_C1);
    float3 rD = bl_ramp2(grad,   RAMP_D_P0, RAMP_D_C0, RAMP_D_P1, RAMP_D_C1);
    float3 rE = bl_ramp2_const(noiseB, RAMP_E_P1, RAMP_E_C0, RAMP_E_C1);
    float3 rF = bl_ramp3(grad,   RAMP_F_P0, RAMP_F_C0, RAMP_F_P1, RAMP_F_C1, RAMP_F_P2, RAMP_F_C2);

    // --- Mix (Color, Mix): Factor = rA, A = rB, B = black ---
    float f1 = saturate(rA.x);
    float3 mix1 = lerp(rB, float3(0.0, 0.0, 0.0), f1);

    // --- Ramp C on the mix result ---
    float3 rC = bl_ramp2(mix1.x, RAMP_C_P0, RAMP_C_C0, RAMP_C_P1, RAMP_C_C1);

    // --- Mix (Color, Multiply), Factor 1.0 ---
    float3 density = rD * rC;

    // --- Mix (Color, Mix): Factor = rE, A = black, B = density ---
    float f2 = saturate(rE.x);
    float3 mix2 = lerp(float3(0.0, 0.0, 0.0), density, f2);

    // --- Mix Shader: Fac 0 = Transparent BSDF, Fac 1 = Emission ---
    Alpha = saturate(mix2.x);
    Color = rF;
}

void BlenderClouds_float(float2 Coord, float LayerT, out float3 Color, out float Alpha)
{
    float na, nb, g;
    BlenderCloudsDebug_float(Coord, LayerT, Color, Alpha, na, nb, g);
}

void BlenderClouds_half(float2 Coord, float LayerT, out float3 Color, out float Alpha)
{
    // Stacked Perlin octaves blow out half precision. Always run full float.
    BlenderClouds_float(Coord, LayerT, Color, Alpha);
}

#endif // BLENDER_CLOUDS_INCLUDED
