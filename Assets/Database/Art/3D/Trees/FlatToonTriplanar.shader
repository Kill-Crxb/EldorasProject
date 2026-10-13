// Triplanar flat / cel shader for static geometry - terrain, rocks, walls, props.
// Unity 6 / URP 17. Matches Crab/FlatToon's shading model so characters and environment
// read as one image.
//
// No UVs needed. Textures are projected from world space (or object space) so a mesh can
// be scaled, sculpted or reused without unwrapping, and tiling stays a constant real-world
// size regardless of how big the object is.
//
// Two layers: a top layer for whatever faces up (grass, moss, snow) and a side layer for
// everything else (rock, dirt). The split is driven by slope, not by painted masks.
//
// NOTE: the shading block below is duplicated from Crab/FlatToon rather than shared. If we
// keep both shaders long term this should move to a common .hlsl include so the terminator
// maths cannot drift between characters and environment.

Shader "Crab/FlatToonTriplanar"
{
    Properties
    {
        [Header(Projection)][Space(4)]
        [Toggle(_OBJECT_SPACE)] _ObjectSpace ("Project In Object Space", Float) = 0
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 4.0

        [Header(Side Layer)][Space(4)]
        _SideMap ("Side Map", 2D) = "white" {}
        _SideColor ("Side Color", Color) = (1,1,1,1)
        _SideScale ("Side Tiling (per metre)", Float) = 0.5

        [Header(Top Layer)][Space(4)]
        _TopMap ("Top Map", 2D) = "white" {}
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _TopScale ("Top Tiling (per metre)", Float) = 0.5
        _SlopeThreshold ("Slope Threshold", Range(-1,1)) = 0.5
        _SlopeSoftness ("Slope Softness", Range(0.001, 1)) = 0.15

        [Header(Anti Tiling)][Space(4)]
        [Toggle(_MACRO_VARIATION)] _UseMacro ("Enable Macro Variation", Float) = 0
        _MacroMap ("Macro Map", 2D) = "gray" {}
        _MacroScale ("Macro Tiling (per metre)", Float) = 0.02
        _MacroStrength ("Macro Strength", Range(0,1)) = 0.5

        [Header(Shading)][Space(4)]
        _ShadeColor ("Shade Tint (multiply)", Color) = (0.62, 0.62, 0.68, 1)
        [Toggle(_USE_RAMP)] _UseRamp ("Use Shade Ramp", Float) = 0
        _ShadeRamp ("Shade Ramp (R)", 2D) = "white" {}
        _ShadeThreshold ("Terminator Position", Range(-1,1)) = 0.0
        _ShadeSoftness ("Terminator Softness", Range(0.0, 0.5)) = 0.015
        _Bands ("Tone Count (1 = two-tone)", Range(1, 6)) = 1
        _LightColorInfluence ("Light Color Influence", Range(0,1)) = 0.0
        _ShadowStrength ("Cast Shadow Strength", Range(0,1)) = 1.0

        [Header(Outline)][Space(4)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (world units, 0 = off)", Range(0, 0.2)) = 0.0
        _OutlineSilhouetteBias ("Silhouette Bias (evenness)", Range(1, 16)) = 4.0
        [Toggle(_SMOOTHED_NORMALS)] _UseSmoothedNormals ("Extrude Along Baked Normals (UV3)", Float) = 0
        _OutlineFadeNear ("Outline Fade Near", Float) = 40.0
        _OutlineFadeFar ("Outline Fade Far", Float) = 150.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _SideMap_ST;
            float4 _TopMap_ST;
            float4 _MacroMap_ST;
            float4 _ShadeRamp_ST;
            half4  _SideColor;
            half4  _TopColor;
            float  _SideScale;
            float  _TopScale;
            float  _MacroScale;
            half   _MacroStrength;
            half   _BlendSharpness;
            half   _SlopeThreshold;
            half   _SlopeSoftness;
            half4  _ShadeColor;
            half   _ShadeThreshold;
            half   _ShadeSoftness;
            half   _Bands;
            half   _LightColorInfluence;
            half   _ShadowStrength;
            half4  _OutlineColor;
            half   _OutlineWidth;
            half   _OutlineSilhouetteBias;
            float  _OutlineFadeNear;
            float  _OutlineFadeFar;
        CBUFFER_END
        ENDHLSL

        // ---------------------------------------------------------------- lit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TriplanarVertex
            #pragma fragment TriplanarFragment

            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _USE_RAMP
            #pragma shader_feature_local_fragment _MACRO_VARIATION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SideMap);    SAMPLER(sampler_SideMap);
            TEXTURE2D(_TopMap);     SAMPLER(sampler_TopMap);
            TEXTURE2D(_MacroMap);   SAMPLER(sampler_MacroMap);
            TEXTURE2D(_ShadeRamp);  SAMPLER(sampler_ShadeRamp);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 projPos    : TEXCOORD2;
                float3 projNormal : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half ToonRamp(half ndotl, half attenuation)
            {
                half lit = ndotl * lerp(1.0h, attenuation, _ShadowStrength);

                #ifdef _USE_RAMP
                    half rampU = saturate(lit * 0.5h + 0.5h);
                    return SAMPLE_TEXTURE2D(_ShadeRamp, sampler_ShadeRamp, float2(rampU, 0.5)).r;
                #else
                    half edge = smoothstep(_ShadeThreshold, _ShadeThreshold + _ShadeSoftness + 1e-4h, lit);
                    half steps = floor(_Bands);
                    half quantized = saturate(floor(edge * steps) / max(steps - 1.0h, 1.0h));
                    return lerp(edge, quantized, step(1.5h, _Bands));
                #endif
            }

            half3 ToonDiffuse(Light light, float3 normalWS)
            {
                half ndotl = dot(normalWS, light.direction);
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half ramp = ToonRamp(ndotl, atten);
                return ramp * lerp(half3(1, 1, 1), light.color, _LightColorInfluence);
            }

            // Projects the texture down all three axes and blends by how much the surface
            // faces each one. No UVs, no seams to author, and tiling stays a fixed size in
            // metres however the mesh is scaled.
            half3 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 position, float3 blend, float scale)
            {
                half3 sampleX = SAMPLE_TEXTURE2D(tex, samp, position.zy * scale).rgb;
                half3 sampleY = SAMPLE_TEXTURE2D(tex, samp, position.xz * scale).rgb;
                half3 sampleZ = SAMPLE_TEXTURE2D(tex, samp, position.xy * scale).rgb;
                return sampleX * blend.x + sampleY * blend.y + sampleZ * blend.z;
            }

            Varyings TriplanarVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;

                // Object space keeps the texture locked to the mesh, so a rock prefab can
                // be rotated or moved without the surface swimming. World space keeps
                // neighbouring pieces of terrain continuous across their seams.
                #ifdef _OBJECT_SPACE
                    OUT.projPos = IN.positionOS.xyz;
                    OUT.projNormal = IN.normalOS;
                #else
                    OUT.projPos = pos.positionWS;
                    OUT.projNormal = nrm.normalWS;
                #endif

                OUT.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 TriplanarFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 normalWS = normalize(IN.normalWS);
                float3 projNormal = normalize(IN.projNormal);

                float3 blend = pow(abs(projNormal), _BlendSharpness);
                blend /= max(blend.x + blend.y + blend.z, 1e-4);

                // The side layer must not sample the up-facing plane or grass bleeds onto
                // cliff faces, so drop Y and renormalise across X and Z.
                float3 sideBlend = float3(blend.x, 0.0, blend.z);
                sideBlend /= max(sideBlend.x + sideBlend.z, 1e-4);

                half3 sideColor = SampleTriplanar(TEXTURE2D_ARGS(_SideMap, sampler_SideMap), IN.projPos, sideBlend, _SideScale) * _SideColor.rgb;
                half3 topColor = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, IN.projPos.xz * _TopScale).rgb * _TopColor.rgb;

                half slope = smoothstep(_SlopeThreshold - _SlopeSoftness, _SlopeThreshold + _SlopeSoftness, projNormal.y);
                half3 albedo = lerp(sideColor, topColor, slope);

                // One extra sample at a much larger scale, multiplied over the top. Breaks
                // up the obvious grid repeat that kills tiled terrain at distance.
                #ifdef _MACRO_VARIATION
                    half3 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, IN.projPos.xz * _MacroScale).rgb;
                    albedo *= lerp(half3(1, 1, 1), macro * 2.0h, _MacroStrength);
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = shadowCoord;
                inputData.normalizedScreenSpaceUV = screenUV;

                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(shadowCoord, IN.positionWS, shadowMask);

                half3 ramp = ToonDiffuse(mainLight, normalWS);

                uint lightCount = GetAdditionalLightsCount();

                #ifdef LIGHT_LOOP_BEGIN
                LIGHT_LOOP_BEGIN(lightCount)
                    Light addLight = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                    ramp += ToonDiffuse(addLight, normalWS);
                LIGHT_LOOP_END
                #else
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS, shadowMask);
                    ramp += ToonDiffuse(addLight, normalWS);
                }
                #endif

                ramp = saturate(ramp);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                    ramp *= ao.directAmbientOcclusion;
                #endif

                half3 color = albedo * lerp(_ShadeColor.rgb, half3(1, 1, 1), ramp);
                color = MixFog(color, IN.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------ outline
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #pragma shader_feature_local_vertex _SMOOTHED_NORMALS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  fogCoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 extrudeOS = IN.normalOS;

                #ifdef _SMOOTHED_NORMALS
                    extrudeOS = normalize(IN.smoothNormalOS);
                #endif

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(extrudeOS);
                float3 positionVS = TransformWorldToView(positionWS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);

                float viewDistance = length(GetCameraPositionWS() - positionWS);
                float fade = 1.0 - saturate((viewDistance - _OutlineFadeNear) / max(_OutlineFadeFar - _OutlineFadeNear, 1e-4));

                float extrudeLength = length(normalVS.xy);
                float2 extrudeDir = extrudeLength > 1e-5 ? normalVS.xy / extrudeLength : float2(0.0, 0.0);
                float2 offset = extrudeDir * saturate(extrudeLength * _OutlineSilhouetteBias) * fade;

                positionVS.xy += offset * _OutlineWidth;

                float4 positionCS = mul(UNITY_MATRIX_P, float4(positionVS, 1.0));

                OUT.positionCS = positionCS;
                OUT.fogCoord = ComputeFogFactor(positionCS.z);
                return OUT;
            }

            half4 OutlineFragment(Varyings IN) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, IN.fogCoord), 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------- shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------- depth only
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------- depth normals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFragment(Varyings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
