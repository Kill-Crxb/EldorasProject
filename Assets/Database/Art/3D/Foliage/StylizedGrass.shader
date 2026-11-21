Shader "Custom/Stylized Terrain Grass"
{
    Properties
    {
        [MainTexture]
        _MainTex ("Grass Texture", 2D) = "white" {}

        [MainColor]
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Color Noise)]
        _NoiseColor ("Noise Color", Color) = (0.8, 0.9, 0.5, 1)
        _ColorNoiseScale ("Color Noise Scale", Float) = 0.05
        _ColorNoiseSpeed ("Color Noise Speed", Vector) = (0.1, 0.05, 0, 0)
        _ColorBands ("Color Bands", Range(2, 10)) = 5

        [Header(Vertical Gradient)]
        _RootDarkness ("Root Darkness Factor", Range(0, 1)) = 0.5
        _TipLightness ("Tip Lightness Factor", Range(1, 2)) = 1.2
        _GradientPower ("Gradient Power", Range(0.1, 4.0)) = 1.0

        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Terrain Root Blend)]
        [Toggle(_TERRAIN_ROOT_BLEND)] _UseRootBlend ("Blend Root Into Terrain", Float) = 0
        _RootBlendHeight ("Root Blend Height", Range(0.01, 1)) = 0.25
        _RootBlendStrength ("Root Blend Strength", Range(0,1)) = 1.0

        [Header(Lighting  match these to the terrain material)]
        _ShadeColor ("Shade Tint", Color) = (0.58, 0.6, 0.72, 1)
        [Toggle(_USE_ENV_RAMP)] _UseEnvRamp ("Use Environment Ramp", Float) = 0
        [NoScaleOffset] _EnvRamp ("Environment Ramp (RGBA, sRGB off, Clamp)", 2D) = "white" {}
        _ShadeThreshold ("Terminator Position (no ramp)", Range(-1, 1)) = 0.0
        _ShadeSoftness ("Terminator / Shadow Softness", Range(0.0, 0.5)) = 0.015
        _ShadowStrength ("Cast Shadow Strength", Range(0,1)) = 1.0
        _LightColorInfluence ("Light Color Influence", Range(0, 1)) = 0.0
        _IndirectMinColor ("Indirect Floor Color", Color) = (0.05, 0.05, 0.07, 1)
        _IndirectStrength ("Ambient Hue Pickup", Range(0, 1)) = 0.3

        [Header(Macro Tint  same map and values as the terrain)]
        [Toggle(_MACRO_TINT)] _UseMacro ("Enable Macro Tint", Float) = 0
        [NoScaleOffset] _MacroMap ("Macro Map (grey = neutral, sRGB off)", 2D) = "gray" {}
        _MacroScale ("Macro Tiling (per metre)", Float) = 0.02
        _MacroStrength ("Macro Strength", Range(0, 1)) = 0.5

        // Read by the shared lighting include, never by grass.
        [HideInInspector] _Bands ("Bands", Float) = 1
        [HideInInspector] _AdditionalLightScale ("Additional Light Scale", Float) = 0
        [HideInInspector] _IsFace ("Is Face", Float) = 0
        [HideInInspector] _FaceShadowLift ("Face Shadow Lift", Float) = 0.5
        [HideInInspector] _ShadowPosOffset ("Shadow Test Offset", Float) = 0
        [HideInInspector] _ShadowTexAmount ("Pattern Coverage", Float) = 0
        [HideInInspector] _ShadowTexSoftness ("Pattern Softness", Float) = 0
        [HideInInspector] _ShadowTexBlend ("Pattern Blend", Float) = 0

        [Header(Wind)]
        _WindDirection ("Wind Direction", Vector) = (1,0,0,0)
        _WindNoiseScale ("Wind Noise Scale", Float) = 0.15
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindStrength ("Wind Strength", Float) = 0.15

        [Header(Bending)]
        _GrassHeight ("Grass Height", Float) = 1.0
        _BendPower ("Bend Power", Range(0.1,4.0)) = 1.5

        [Header(Gusts)]
        _GustStrength ("Gust Strength", Range(0,1)) = 0.25
        _GustScale ("Gust Scale", Float) = 0.08
        _GustSpeed ("Gust Speed", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        LOD 100

        Cull Off
        ZWrite On

        Pass
        {
            Name "Grass"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing

            #pragma shader_feature_local_fragment _TERRAIN_ROOT_BLEND
            #pragma shader_feature_local_fragment _USE_ENV_RAMP
            #pragma shader_feature_local_fragment _MACRO_TINT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EnvRamp);  SAMPLER(sampler_EnvRamp);
            TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);

            // Set by a zone volume; alpha 0 (never set) keeps the material's Shade Tint.
            half4 _ZoneShadeTint;

            // Globals, pushed by TerrainColorGlobals on the Terrain object. Not in
            // UnityPerMaterial - globals in there break the SRP Batcher.
            TEXTURE2D(_TerrainControl);
            TEXTURE2D(_TerrainLayerTex0);
            TEXTURE2D(_TerrainLayerTex1);
            TEXTURE2D(_TerrainLayerTex2);
            TEXTURE2D(_TerrainLayerTex3);

            float4 _TerrainOriginSize;
            float4 _TerrainLayerTiling0;
            float4 _TerrainLayerTiling1;
            float4 _TerrainLayerTiling2;
            float4 _TerrainLayerTiling3;
            half4  _TerrainLayerTint0;
            half4  _TerrainLayerTint1;
            half4  _TerrainLayerTint2;
            half4  _TerrainLayerTint3;

            CBUFFER_START(UnityPerMaterial)

                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _NoiseColor;
                float _ColorNoiseScale;
                float4 _ColorNoiseSpeed;
                float _ColorBands;

                float _RootDarkness;
                float _TipLightness;
                float _GradientPower;

                float _Cutoff;

                half4 _ShadeColor;
                half _ShadeThreshold;
                half _ShadeSoftness;
                half _ShadowStrength;
                half _LightColorInfluence;
                half4 _IndirectMinColor;
                half _IndirectStrength;
                float _MacroScale;
                half _MacroStrength;
                half _Bands;
                half _AdditionalLightScale;
                half _IsFace;
                half _FaceShadowLift;
                float _ShadowPosOffset;
                half _ShadowTexAmount;
                half _ShadowTexSoftness;
                half _ShadowTexBlend;

                half _RootBlendHeight;
                half _RootBlendStrength;

                float4 _WindDirection;
                float _WindNoiseScale;
                float _WindSpeed;
                float _WindStrength;

                float _GrassHeight;
                float _BendPower;

                float _GustStrength;
                float _GustScale;
                float _GustSpeed;

            CBUFFER_END

            #define CRAB_TOON_ENV
            #include "Packages/com.crxb.crabsystem/Runtime/Rendering/Shaders/CrabToonLighting.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float heightFactor: TEXCOORD2;
                float3 windPositionWS : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);

                return frac(p.x * p.y);
            }


            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float ab = lerp(a, b, f.x);
                float cd = lerp(c, d, f.x);

                return lerp(ab, cd, f.y);
            }


            // The ground colour under a blade: the terrain's splat weights applied to its
            // layer textures, at the layer tiling the terrain itself uses. Shared inline
            // samplers rather than one per texture - five own samplers would eat the
            // D3D11 budget for no gain.
            half3 SampleTerrainLayer(TEXTURE2D_PARAM(tex, samp), float2 worldXZ, float4 tiling, half4 tint)
            {
                float2 uv = (worldXZ + tiling.zw) / max(tiling.xy, 1e-4);
                return SAMPLE_TEXTURE2D(tex, samp, uv).rgb * tint.rgb;
            }


            half3 SampleTerrainColor(float3 positionWS)
            {
                float2 localXZ = positionWS.xz - _TerrainOriginSize.xy;
                float2 controlUV = localXZ / max(_TerrainOriginSize.zw, 1e-4);

                half4 control = SAMPLE_TEXTURE2D(_TerrainControl, sampler_LinearClamp, controlUV);

                half3 ground = SampleTerrainLayer(TEXTURE2D_ARGS(_TerrainLayerTex0, sampler_TrilinearRepeat), localXZ, _TerrainLayerTiling0, _TerrainLayerTint0) * control.r;
                ground += SampleTerrainLayer(TEXTURE2D_ARGS(_TerrainLayerTex1, sampler_TrilinearRepeat), localXZ, _TerrainLayerTiling1, _TerrainLayerTint1) * control.g;
                ground += SampleTerrainLayer(TEXTURE2D_ARGS(_TerrainLayerTex2, sampler_TrilinearRepeat), localXZ, _TerrainLayerTiling2, _TerrainLayerTint2) * control.b;
                ground += SampleTerrainLayer(TEXTURE2D_ARGS(_TerrainLayerTex3, sampler_TrilinearRepeat), localXZ, _TerrainLayerTiling3, _TerrainLayerTint3) * control.a;

                half total = control.r + control.g + control.b + control.a;
                return ground / max(total, 1e-4);
            }


            float3 CalculateWind(
                float3 positionOS,
                float3 positionWS)
            {
                float heightMask;

                if (_GrassHeight > 0.001)
                {
                    heightMask = saturate(positionOS.y / _GrassHeight);
                }
                else
                {
                    heightMask = 1.0;
                }

                heightMask = pow(heightMask, _BendPower);

                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float2 worldXZ = positionWS.xz;

                // Simple single-sample noise scaled by _WindNoiseScale
                float2 windUV = worldXZ * _WindNoiseScale + windDirection * (_Time.y * _WindSpeed);
                float windNoise = ValueNoise(windUV) * 2.0 - 1.0;

                // Gust Layer
                float2 gustUV = worldXZ * _GustScale + windDirection * (_Time.y * _GustSpeed);
                float gustSample = ValueNoise(gustUV);

                float gustAmount = 1.0 + (gustSample * _GustStrength);

                float displacement = windNoise * _WindStrength * gustAmount * heightMask;

                float3 windOffset = float3(
                    windDirection.x * displacement,
                    0.0,
                    windDirection.y * displacement
                );

                return windOffset;
            }


            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionOS = IN.positionOS.xyz;
                float3 positionWS = TransformObjectToWorld(positionOS);

                float3 wind = CalculateWind(positionOS, positionWS);
                positionWS += wind;

                OUT.positionWS = TransformObjectToWorld(positionOS);

                float heightFactor = _GrassHeight > 0.001 ? saturate(positionOS.y / _GrassHeight) : IN.uv.y;
                OUT.heightFactor = heightFactor;

                OUT.positionCS = TransformWorldToHClip(positionWS);

                // The wind-displaced position, for the shadow lookup only. The colour
                // noise keeps reading the undisplaced one so it does not swim in the wind.
                OUT.windPositionWS = positionWS;

                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }


            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 grass = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                clip(grass.a - _Cutoff);

                float2 noiseUV = IN.positionWS.xz * _ColorNoiseScale;
                noiseUV += _Time.y * _ColorNoiseSpeed.xy;

                float colorNoise = ValueNoise(noiseUV);

                float steps = max(2.0, _ColorBands);
                colorNoise = floor(colorNoise * (steps - 1.0) + 0.5) / (steps - 1.0);

                half3 blendedColor = lerp(_BaseColor.rgb, _NoiseColor.rgb, saturate(colorNoise));

                float hFactor = pow(IN.heightFactor, _GradientPower);
                half3 verticalGradient = lerp(_BaseColor.rgb * _RootDarkness, blendedColor * _TipLightness, hFactor);

                grass.rgb *= verticalGradient;

                // The root takes the ground's colour, so the blade stops reading as a
                // separate object standing on the terrain. Undisplaced position: the root
                // is where the blade is planted, and wind must not slide the match.
                #ifdef _TERRAIN_ROOT_BLEND
                    half rootWeight = 1.0h - smoothstep(0.0h, _RootBlendHeight, IN.heightFactor);
                    grass.rgb = lerp(grass.rgb, SampleTerrainColor(IN.positionWS), rootWeight * _RootBlendStrength);
                #endif

                // Same macro tint as the terrain, at the same world scale, so a field
                // and the ground under it shift colour together. Undisplaced position, so
                // the tint does not swim in the wind.
                #ifdef _MACRO_TINT
                    half3 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, IN.positionWS.xz * _MacroScale).rgb;
                    grass.rgb *= lerp(half3(1, 1, 1), macro * 2.0h, _MacroStrength);
                #endif

                // Lit as the ground it stands on: the terrain's soft environment ramp with
                // the normal straight up, so a blade takes the same shade, zone tint and
                // ambient hue as the patch of terrain under it. Blade normals would shade
                // every card differently and the field would sparkle.
                //
                // Per fragment, not per vertex: TransformWorldToShadowCoord picks a
                // cascade, and interpolating a vertex-side choice across a blade samples
                // the wrong cascade near a boundary - concentric rings of wrong shade
                // around the camera.
                //
                // The three-argument GetMainLight is the one that applies the shadow
                // distance fade. GetMainLight(shadowCoord) does not, so past Shadow Max
                // Distance the coord runs off the atlas and the grass bands instead of
                // going unshadowed.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.windPositionWS);
                Light mainLight = GetMainLight(shadowCoord, IN.windPositionWS, half4(1, 1, 1, 1));
                half4 mainRamp = ToonEnvLightContribution(mainLight, float3(0, 1, 0), 1.0h);
                half3 shadeTint = lerp(_ShadeColor.rgb, _ZoneShadeTint.rgb, _ZoneShadeTint.a);
                grass.rgb = CompositeToonEnv(grass.rgb, shadeTint, mainRamp, half3(0, 0, 0), ToonAmbient());

                return grass;
            }

            ENDHLSL
        }
    }

    FallBack Off
}