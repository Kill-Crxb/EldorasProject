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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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

                return grass;
            }

            ENDHLSL
        }
    }

    FallBack Off
}