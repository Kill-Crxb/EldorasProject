Shader "NinjaGame/Screen Speed Lines"
{
    // Anime speed lines as a POLAR pattern rather than as particles.
    //
    // Screen UV is converted into "angle around a focal point" and "distance from it", and the
    // streak texture is sampled with those as its UVs: angle walks along U so every band around
    // the circle is a separate line, distance runs along V and scrolls over time so the lines
    // rush outward. Distance also drives a mask that keeps the middle of the screen clear.
    //
    // Meant for a QUAD parented to the camera on an overlay-camera layer — not a Renderer
    // Feature. URP's fullscreen pass APIs have changed shape repeatedly (RTHandles, Blitter,
    // RenderGraph) and a feature written against the wrong version is a rewrite; a quad is a
    // quad in every version.
    //
    // ZTest Always because it must never be occluded by world geometry. Unlit because a lit
    // speed line would take shadows and change colour depending on where the player stands.

    Properties
    {
        [MainTexture] _BaseMap   ("Streak Texture (tiles along U)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint", Color) = (1,1,1,1)

        _Intensity   ("Intensity (drive from speed)", Range(0,1)) = 1

        [IntRange]
        _LineCount   ("Line Count", Range(4,64)) = 24
        _Stretch     ("Radial Stretch", Float) = 1
        _ScrollSpeed ("Scroll Speed", Float) = 2

        _InnerRadius ("Inner Radius (at full intensity)", Range(0,1.5)) = 0.15
        _OuterRadius ("Inner Radius (at zero intensity)", Range(0,1.5)) = 0.85
        _Softness    ("Edge Softness", Range(0.001,1)) = 0.25

        _FocalPoint  ("Focal Point (screen UV)", Vector) = (0.5, 0.5, 0, 0)

        // Defaults are premultiplied ADDITIVE — bright lines over a darker world.
        // For dark manga-style lines set Src = One, Dst = OneMinusSrcAlpha.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SpeedLines"

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Intensity;
                float  _LineCount;
                float  _Stretch;
                float  _ScrollSpeed;
                float  _InnerRadius;
                float  _OuterRadius;
                float  _Softness;
                float4 _FocalPoint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Aspect-correct the offset, or the pattern stretches into an ellipse on a wide
                // screen and the lines stop looking like they converge on a point.
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                float2 offset = IN.uv - _FocalPoint.xy;
                offset.x *= aspect;

                float dist = length(offset);

                // atan2 gives -PI..PI; scale to 0..1 so one turn is one texture repeat.
                // _LineCount is [IntRange] on purpose — a fractional count leaves the texture
                // mismatched across the atan2 seam and you get one visibly wrong line.
                float angle = atan2(offset.y, offset.x) * 0.15915494;

                float2 polar = float2(angle * _LineCount,
                                      dist * _Stretch - _Time.y * _ScrollSpeed);

                half4 streak = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, polar);

                // Intensity pulls the clear centre inward. At 0 the inner radius sits past the
                // screen corners so nothing shows; at 1 only a small core stays clear. Driving
                // the RADIUS rather than just fading alpha is what makes the lines feel like
                // they close in as you accelerate instead of simply getting brighter.
                float inner = lerp(_OuterRadius, _InnerRadius, _Intensity);
                float mask  = smoothstep(inner, inner + _Softness, dist);

                half4 col = streak * _BaseColor;
                col.a *= mask * _Intensity;

                // Premultiplied, so One/One is clean additive and One/OneMinusSrcAlpha is clean
                // normal blending — one shader, both looks, no keyword.
                col.rgb *= col.a;

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
