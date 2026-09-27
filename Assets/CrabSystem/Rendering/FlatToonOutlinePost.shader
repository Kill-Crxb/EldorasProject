// Screen-space outline for URP 17 / Unity 6.
// Roberts-cross edge detect over the depth and normals prepass textures.
// Driven by ToonOutlineFeature.cs - do not assign this to a material yourself.

Shader "Crab/FlatToonOutlinePost"
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
            float  _IDThreshold;
            float  _IDStrength;
            float  _FadeStart;
            float  _FadeEnd;

            float SampleEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                float2 texel = _Thickness / _ScreenParams.xy;

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
                float depthEdge = step(depthThreshold, depthGradient) * _DepthStrength;

                float normalGradient = length(abs(n1 - n0) + abs(n3 - n2));
                float normalEdge = step(_NormalThreshold, normalGradient) * _NormalStrength;

                float idGap = max(abs(i1 - i0), abs(i3 - i2));
                float idEdge = step(_IDThreshold, idGap) * _IDStrength;

                float edge = max(max(depthEdge, normalEdge), idEdge);

                // Fades the whole effect, not just one term. Distant characters stop
                // collecting fussy linework instead of turning into a scribble.
                float fade = 1.0 - saturate((centerDepth - _FadeStart) / max(_FadeEnd - _FadeStart, 1e-4));
                edge *= fade;

                return half4(lerp(source.rgb, _OutlineColor.rgb, edge * _OutlineColor.a), source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
