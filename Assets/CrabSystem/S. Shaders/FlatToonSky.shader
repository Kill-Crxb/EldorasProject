// Anime-styled skybox. Takes an imported panoramic or cubemap sky, applies image
// adjustments, then remaps it through a colour ramp so the photographic colours are
// replaced by authored ones.
//
// The ramp is the point. A photo sky has hundreds of values; an anime sky has three or
// four. Posterising the luminance and using it to look up a painted gradient throws away
// the source's colour entirely and keeps only its shapes - which is what makes an
// imported sky read as painted rather than photographed.
//
// Assign in Window > Rendering > Lighting > Environment > Skybox Material.
//
// Ramp import settings differ from the project's other ramps: sRGB **on**, because this
// one holds authored COLOUR, not a weight. Clamp, no mips.
//
// Unity 6 / URP 17.

Shader "Crab/FlatToonSky"
{
    Properties
    {
        [KeywordEnum(Panoramic, Cubemap)] _SkySource ("Source", Float) = 0
        _MainTex ("Panoramic (lat-long)", 2D) = "grey" {}
        [NoScaleOffset] _Cubemap ("Cubemap", Cube) = "grey" {}
        _Rotation ("Rotation", Range(0, 360)) = 0

        [Header(Image)][Space(4)]
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Contrast ("Contrast", Range(0, 3)) = 1
        _ContrastPivot ("Contrast Pivot", Range(0.01, 1)) = 0.2
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Tint ("Tint", Color) = (1,1,1,1)

        [Header(Colour Ramp)][Space(4)]
        [Toggle(_SKY_RAMP)] _UseRamp ("Enable Colour Ramp", Float) = 0
        _SkyRamp ("Sky Ramp (RGB)", 2D) = "white" {}
        _RampStrength ("Ramp Strength", Range(0,1)) = 1
        _Bands ("Posterise Bands (0 = off)", Range(0, 16)) = 0

        [Header(Horizon)][Space(4)]
        _HorizonColor ("Horizon Color", Color) = (0.74, 0.83, 0.91, 1)
        _HorizonHeight ("Horizon Height", Range(0.001, 1)) = 0.25
        _HorizonStrength ("Horizon Strength", Range(0,1)) = 0.5

        [Header(Sun)][Space(4)]
        [Toggle(_SKY_SUN)] _UseSun ("Enable Sun", Float) = 0
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.9, 1)
        _SunSize ("Sun Size", Range(0.0, 0.2)) = 0.02
        _SunSoftness ("Sun Edge Softness", Range(0.0, 0.1)) = 0.003
        _GlowColor ("Glow Color", Color) = (1, 0.95, 0.85, 1)
        _GlowPower ("Glow Tightness", Range(1, 256)) = 24
        _GlowStrength ("Glow Strength", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment
            #pragma target 3.0

            #pragma shader_feature_local _SKYSOURCE_PANORAMIC _SKYSOURCE_CUBEMAP
            #pragma shader_feature_local_fragment _SKY_RAMP
            #pragma shader_feature_local_fragment _SKY_SUN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURECUBE(_Cubemap);   SAMPLER(sampler_Cubemap);
            TEXTURE2D(_SkyRamp);     SAMPLER(sampler_SkyRamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SkyRamp_ST;
                float  _Rotation;
                half   _Exposure;
                half   _Contrast;
                half   _ContrastPivot;
                half   _Saturation;
                half4  _Tint;
                half   _RampStrength;
                half   _Bands;
                half4  _HorizonColor;
                half   _HorizonHeight;
                half   _HorizonStrength;
                half4  _SunColor;
                half   _SunSize;
                half   _SunSoftness;
                half4  _GlowColor;
                half   _GlowPower;
                half   _GlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Own constant: PI lives in SRP core's Macros.hlsl and UNITY_PI only exists
            // in the built-in pipeline's UnityCG.cginc. Declaring it here costs nothing
            // and this file then depends on neither.
            #define SKY_PI 3.14159265358979323846

            half Luma(half3 color)
            {
                return dot(color, half3(0.2126h, 0.7152h, 0.0722h));
            }

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float angle = degrees * (SKY_PI / 180.0);
                float s = sin(angle);
                float c = cos(angle);
                return float3(c * direction.x + s * direction.z, direction.y, c * direction.z - s * direction.x);
            }

            // Latitude-longitude mapping, matching Unity's Skybox/Panoramic so an existing
            // panoramic texture keeps the orientation it already had.
            float2 PanoramicUV(float3 direction)
            {
                float latitude = acos(direction.y);
                float longitude = atan2(direction.z, -direction.x);
                float2 sphere = float2(longitude, latitude) * float2(0.5 / SKY_PI, 1.0 / SKY_PI);
                return float2(0.5, 1.0) - sphere;
            }

            Varyings SkyVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.direction = RotateAroundY(IN.positionOS.xyz, _Rotation);
                return OUT;
            }

            half4 SkyFragment(Varyings IN) : SV_Target
            {
                float3 direction = normalize(IN.direction);

                #ifdef _SKYSOURCE_CUBEMAP
                    half3 color = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, direction).rgb;
                #else
                    half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, PanoramicUV(direction)).rgb;
                #endif

                color *= _Exposure * _Tint.rgb;

                // Contrast around a linear pivot, not 0.5: linear mid-grey sits near 0.2,
                // and pivoting at 0.5 crushes everything a sky actually contains.
                color = (color - _ContrastPivot) * _Contrast + _ContrastPivot;
                color = max(color, 0.0h);

                half luma = Luma(color);
                color = lerp(half3(luma, luma, luma), color, _Saturation);

                // Gradient map. The source's luminance becomes a lookup into an authored
                // gradient, so the sky keeps its shapes and loses its photographic colour.
                #ifdef _SKY_RAMP
                    half rampU = saturate(Luma(color));

                    half bands = floor(_Bands);
                    half quantized = (floor(min(rampU, 0.999h) * bands) + 0.5h) / max(bands, 1.0h);
                    rampU = bands < 1.0h ? rampU : quantized;

                    half3 ramped = SAMPLE_TEXTURE2D(_SkyRamp, sampler_SkyRamp, float2(rampU, 0.5)).rgb;
                    color = lerp(color, ramped, _RampStrength);
                #endif

                // The sky is drawn behind everything and receives no fog, so the horizon
                // band is what marries it to the fog colour. Match this to the scene's fog
                // or distant terrain will end against a visible seam.
                half horizon = 1.0h - saturate(abs(direction.y) / _HorizonHeight);
                color = lerp(color, _HorizonColor.rgb, horizon * _HorizonStrength);

                #ifdef _SKY_SUN
                    half towardSun = dot(direction, _MainLightPosition.xyz);
                    half edge = 1.0h - _SunSize;

                    half disc = smoothstep(edge, edge + _SunSoftness + 1e-4h, towardSun);
                    half glow = pow(saturate(towardSun), _GlowPower) * _GlowStrength;

                    color += glow * _GlowColor.rgb;
                    color = lerp(color, _SunColor.rgb, disc);
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    // No FallBack: a silent fallback would render a plausible sky and hide a compile error.
}
