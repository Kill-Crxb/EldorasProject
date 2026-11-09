// STAGING: layered face decal for the face plane parented to the Head bone.
//
// Three atlases share one layout: a grid of equal cells, with the face plane's UVs authored
// inside cell 0 (top-left). Picking a cell shifts the UVs by whole cells, so every expression
// is painted in the same spot of its own cell.
//
// Layer order, back to front: eye white, iris, lines. The iris slides inside the eye white
// (its alpha is the mask) so look direction never draws iris over skin.
//
// Shading is a plain two-tone off the main light so the decal darkens with the face; set
// Shade Strength to 0 for unlit. Screen-space main light shadows are not picked up yet.
//
// Look direction: Iris Look is -1..1 per axis, scaled by Iris Travel. Flip an axis by making
// its travel negative.

Shader "Crab/Face Atlas (Staging)"
{
    Properties
    {
        [NoScaleOffset] _WhiteTex ("Eye White Atlas", 2D) = "black" {}
        [NoScaleOffset] _IrisTex ("Iris Atlas", 2D) = "black" {}
        [NoScaleOffset] _LineTex ("Line Atlas", 2D) = "black" {}
        _Grid ("Atlas Cells Per Side", Float) = 4

        [Header(Cells (driven at runtime))][Space(4)]
        _ExpressionCell ("Expression Cell (white + lines)", Float) = 0
        _IrisCell ("Iris Cell", Float) = 0

        [Header(Iris)][Space(4)]
        _IrisTint ("Iris Tint (multiply)", Color) = (1,1,1,1)
        _IrisLook ("Iris Look (xy -1..1, driven at runtime)", Vector) = (0,0,0,0)
        _IrisRange ("Iris Travel (UV, xy)", Vector) = (0.01, 0.005, 0, 0)

        [Header(Colour)][Space(4)]
        _WhiteTint ("Eye White Tint", Color) = (1,1,1,1)
        _LineTint ("Line Tint", Color) = (1,1,1,1)

        [Header(Shading)][Space(4)]
        _ShadeColor ("Shade Tint (multiply)", Color) = (0.62, 0.62, 0.68, 1)
        _ShadeStrength ("Shade Strength (0 = unlit)", Range(0,1)) = 1
        _ShadeThreshold ("Terminator Position", Range(-1,1)) = 0.0
        _ShadeSoftness ("Terminator Softness", Range(0.0, 0.5)) = 0.015

        _ZOffset ("Depth Offset (view units, toward camera)", Float) = 0.002
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardFace"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FaceVertex
            #pragma fragment FaceFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Grid;
                float  _ExpressionCell;
                float  _IrisCell;
                half4  _IrisTint;
                float4 _IrisLook;
                float4 _IrisRange;
                half4  _WhiteTint;
                half4  _LineTint;
                half4  _ShadeColor;
                half   _ShadeStrength;
                half   _ShadeThreshold;
                half   _ShadeSoftness;
                float  _ZOffset;
            CBUFFER_END

            TEXTURE2D(_WhiteTex); SAMPLER(sampler_WhiteTex);
            TEXTURE2D(_IrisTex);  SAMPLER(sampler_IrisTex);
            TEXTURE2D(_LineTex);  SAMPLER(sampler_LineTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Cell 0 is top-left; cells run left to right, then down. UV v grows upward, so rows subtract.
            float2 CellOffset(float cell)
            {
                float index = floor(cell + 0.5);
                float col = fmod(index, _Grid);
                float row = floor(index / _Grid);
                return float2(col, -row) / _Grid;
            }

            Varyings FaceVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Pulled toward the camera in view space so the plane never fights the head it sits on.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.z += _ZOffset;

                OUT.positionCS = TransformWViewToHClip(positionVS);
                OUT.positionWS = positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half3 FaceShade(float3 positionWS, float3 normalWS)
            {
                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half ndotl = dot(normalize(normalWS), light.direction);
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndotl);
                lit *= light.shadowAttenuation;
                half3 shade = lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                return lerp(half3(1, 1, 1), shade, _ShadeStrength);
            }

            half4 FaceFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 expressionUV = IN.uv + CellOffset(_ExpressionCell);
                float2 irisUV = IN.uv + CellOffset(_IrisCell) - _IrisLook.xy * _IrisRange.xy;

                half4 white = SAMPLE_TEXTURE2D(_WhiteTex, sampler_WhiteTex, expressionUV) * _WhiteTint;
                half4 iris = SAMPLE_TEXTURE2D(_IrisTex, sampler_IrisTex, irisUV) * _IrisTint;
                half4 lines = SAMPLE_TEXTURE2D(_LineTex, sampler_LineTex, expressionUV) * _LineTint;

                iris.a *= white.a;

                // Composite premultiplied so soft edges don't pick up the colour of transparent pixels.
                half4 col = half4(white.rgb * white.a, white.a);
                col = half4(iris.rgb * iris.a, iris.a) + col * (1.0h - iris.a);
                col = half4(lines.rgb * lines.a, lines.a) + col * (1.0h - lines.a);
                col.rgb /= max(col.a, 1e-4h);

                col.rgb *= FaceShade(IN.positionWS, IN.normalWS);
                col.rgb = MixFog(col.rgb, IN.fogCoord);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
