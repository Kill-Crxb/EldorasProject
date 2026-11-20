// Screen-space outline for URP 17 / Unity 6.
// Roberts-cross edge detect over the depth and normals prepass textures.
// Driven by ToonOutlineFeature.cs - do not assign this to a material yourself.
// Staged copy of Crab/FlatToonOutlinePost: soft edges, character targeting, surface-tinted lines.

Shader "NinjaGame/ToonOutlinePost"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ToonOutlinePost"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineColor;
            float  _Thickness;
            float  _DepthStrength;
            float  _NormalStrength;
            float  _DepthThreshold;
            float  _DepthGrazingBias;
            float  _NormalThreshold;
            float  _CharacterNormalStrength;
            float  _CharacterNormalThreshold;
            float  _IDThreshold;
            float  _IDStrength;
            float  _FadeStart;
            float  _FadeEnd;
            float  _EdgeSoftness;
            float  _CharacterIdMin;
            float4 _TargetWeights;
            float  _CharacterThickness;
            float  _ColorFromSurface;
            float  _SurfaceDarkness;

            float SoftEdge(float threshold, float gradient)
            {
                return smoothstep(threshold, threshold * (1.0 + _EdgeSoftness) + 1e-5, gradient);
            }

            float SampleEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                half ic = SAMPLE_TEXTURE2D_X_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv, 0).a;
                float centerIsCharacter = step(_CharacterIdMin, ic);
                float2 texel = lerp(_Thickness, _CharacterThickness, centerIsCharacter) / _ScreenParams.xy;

                float2 uv0 = uv + float2(-texel.x, -texel.y);
                float2 uv1 = uv + float2( texel.x,  texel.y);
                float2 uv2 = uv + float2(-texel.x,  texel.y);
                float2 uv3 = uv + float2( texel.x, -texel.y);

                float d0 = SampleEyeDepth(uv0);
                float d1 = SampleEyeDepth(uv1);
                float d2 = SampleEyeDepth(uv2);
                float d3 = SampleEyeDepth(uv3);

                float centerRaw = SampleSceneDepth(uv);
                float centerDepth = LinearEyeDepth(centerRaw, _ZBufferParams);

                // Object IDs ride in the alpha of the normals buffer. An ID either
                // matches its neighbour or it does not, so this needs no threshold tuning
                // and produces no false lines on curved surfaces - unlike depth and normal
                // edges, which are always a compromise.
                half i0 = SAMPLE_TEXTURE2D_X_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv0, 0).a;
                half i1 = SAMPLE_TEXTURE2D_X_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv1, 0).a;
                half i2 = SAMPLE_TEXTURE2D_X_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv2, 0).a;
                half i3 = SAMPLE_TEXTURE2D_X_LOD(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv3, 0).a;

                float3 n0 = SampleSceneNormals(uv0);
                float3 n1 = SampleSceneNormals(uv1);
                float3 n2 = SampleSceneNormals(uv2);
                float3 n3 = SampleSceneNormals(uv3);

                // A surface seen edge-on has a huge depth gradient without being a real
                // edge, which is what fills floors and walls with false lines. Raising
                // the threshold as the view ray grazes the surface suppresses those.
                float3 centerNormal = normalize(SampleSceneNormals(uv));
                float3 positionWS = ComputeWorldSpacePosition(uv, centerRaw, UNITY_MATRIX_I_VP);
                float3 viewDirWS = normalize(GetCurrentViewPosition() - positionWS);
                float grazing = 1.0 - saturate(abs(dot(centerNormal, viewDirWS)));
                float depthThreshold = _DepthThreshold * (1.0 + grazing * _DepthGrazingBias);

                float depthGradient = length(float2(d1 - d0, d3 - d2)) / max(centerDepth, 1e-4);
                float depthEdge = SoftEdge(depthThreshold, depthGradient) * _DepthStrength;

                // Creases use the centre pixel's side, so a character's own folds take the
                // character settings while its silhouette is still a depth and ID edge.
                float normalGradient = length(abs(n1 - n0) + abs(n3 - n2));
                float normalThreshold = lerp(_NormalThreshold, _CharacterNormalThreshold, centerIsCharacter);
                float normalStrength = lerp(_NormalStrength, _CharacterNormalStrength, centerIsCharacter);
                float normalEdge = SoftEdge(normalThreshold, normalGradient) * normalStrength;

                float idGap = max(abs(i1 - i0), abs(i3 - i2));
                float idEdge = SoftEdge(_IDThreshold, idGap) * _IDStrength;

                float edge = max(max(depthEdge, normalEdge), idEdge);

                // A footprint touching any character pixel counts as character, so silhouettes
                // against the environment belong to the character side.
                float maxId = max(max(max(i0, i1), max(i2, i3)), ic);
                float isCharacter = step(_CharacterIdMin, maxId);
                edge *= lerp(_TargetWeights.x, _TargetWeights.y, isCharacter);

                // Fades the whole effect, not just one term. Distant characters stop
                // collecting fussy linework instead of turning into a scribble.
                float fade = 1.0 - saturate((centerDepth - _FadeStart) / max(_FadeEnd - _FadeStart, 1e-4));
                edge *= fade;

                half3 lineColor = lerp(_OutlineColor.rgb, source.rgb * _SurfaceDarkness, _ColorFromSurface);
                return half4(lerp(source.rgb, lineColor, edge * _OutlineColor.a), source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
