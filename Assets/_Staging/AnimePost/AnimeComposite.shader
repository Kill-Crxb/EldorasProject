// Anime compositing passes, driven by AnimeCompositeFeature. Do not assign to a material.
// 0 prefilter, 1 blur H, 2 blur V, 3 composite (depth tint, diffusion screen, gradient soft light).

Shader "Hidden/NinjaGame/AnimeComposite"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    TEXTURE2D_X(_DiffusionTex);

    float  _DiffusionIntensity;
    float  _DiffusionThreshold;
    float  _DiffusionKnee;
    float4 _BlurTexel;
    float  _GradientIntensity;
    float4 _GradientTop;
    float4 _GradientBottom;
    float4 _GradientDirection;
    float  _DepthTintIntensity;
    float4 _DepthTintColor;
    float4 _DepthTintRange;

    half3 SampleSource(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
    }

    // 9-tap Gaussian on bilinear-filtered taps; the offset scales with spread.
    half3 Blur(float2 uv, float2 step)
    {
        half3 c = SampleSource(uv) * 0.2270270;
        c += SampleSource(uv + step * 1.3846154) * 0.3162162;
        c += SampleSource(uv - step * 1.3846154) * 0.3162162;
        c += SampleSource(uv + step * 3.2307692) * 0.0702703;
        c += SampleSource(uv - step * 3.2307692) * 0.0702703;
        return c;
    }

    half3 SoftLight(half3 a, half3 b)
    {
        half3 low = 2.0 * a * b + a * a * (1.0 - 2.0 * b);
        half3 high = 2.0 * a * (1.0 - b) + sqrt(max(a, 0.0)) * (2.0 * b - 1.0);
        return lerp(low, high, step(0.5, b));
    }

    half4 FragPrefilter(Varyings IN) : SV_Target
    {
        half3 c = SampleSource(IN.texcoord);
        half luma = Luminance(c);
        half knee = _DiffusionThreshold * _DiffusionKnee;
        half weight = smoothstep(_DiffusionThreshold - knee, _DiffusionThreshold + knee + 1e-4, luma);
        // Caps HDR emissives so one hot pixel cannot flood the glow.
        return half4(min(c * weight, 8.0), 1.0);
    }

    half4 FragBlurH(Varyings IN) : SV_Target
    {
        return half4(Blur(IN.texcoord, float2(_BlurTexel.x, 0.0)), 1.0);
    }

    half4 FragBlurV(Varyings IN) : SV_Target
    {
        return half4(Blur(IN.texcoord, float2(0.0, _BlurTexel.y)), 1.0);
    }

    half4 FragComposite(Varyings IN) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
        float2 uv = IN.texcoord;
        half4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
        half3 color = source.rgb;

        float rawDepth = SampleSceneDepth(uv);
        #if UNITY_REVERSED_Z
            float isSky = step(rawDepth, 1e-6);
        #else
            float isSky = step(1.0 - 1e-6, rawDepth);
        #endif
        float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        float range = max(_DepthTintRange.y - _DepthTintRange.x, 1e-4);
        float tint = saturate((eyeDepth - _DepthTintRange.x) / range) * _DepthTintIntensity * (1.0 - isSky);
        color = lerp(color, _DepthTintColor.rgb, tint);

        // Screen blend, HDR-safe: adds glow in proportion to how far the pixel is from white.
        half3 glow = SAMPLE_TEXTURE2D_X_LOD(_DiffusionTex, sampler_LinearClamp, uv, 0).rgb * _DiffusionIntensity;
        color += glow * (1.0 - saturate(color));

        float g = saturate(dot(uv - 0.5, _GradientDirection.xy) + 0.5);
        half3 gradient = lerp(_GradientBottom.rgb, _GradientTop.rgb, g);
        color = lerp(color, SoftLight(color, gradient), _GradientIntensity);

        return half4(color, source.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "AnimePrefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "AnimeBlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "AnimeBlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "AnimeComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }

    FallBack Off
}
