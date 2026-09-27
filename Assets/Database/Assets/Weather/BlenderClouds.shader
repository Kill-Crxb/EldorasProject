// ============================================================================
//  Blender cloud material, ported to URP.
//  Drop this next to BlenderClouds.hlsl. Unlit, transparent, no depth write -
//  the same render state the Mix Shader + Transparent BSDF gave you in Blender.
// ============================================================================

Shader "Crab/BlenderClouds"
{
    Properties
    {
        _CloudScale   ("Cloud Scale", Float) = 0.01
        _CloudOffsetX ("Cloud Offset X", Float) = 0.0
        _CloudOffsetY ("Cloud Offset Y", Float) = 0.0

        _ObjectSpace  ("Object Space Coords (0 = world XZ)", Range(0, 1)) = 0.0

        _StackMinY    ("Stack Min Y (object space)", Float) = -0.5
        _StackMaxY    ("Stack Max Y (object space)", Float) =  0.5

        _WindX        ("Wind X", Float) = 0.0
        _WindY        ("Wind Y", Float) = 0.0

        _Brightness   ("Brightness", Float) = 1.0
        _AlphaScale   ("Alpha Scale", Range(0, 2)) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1

        // 0 final, 1 LayerT, 2 Coord, 3 NoiseA, 4 NoiseB, 5 Alpha
        [Enum(Final,0,LayerT,1,Coord,2,NoiseA,3,NoiseB,4,Alpha,5)] _Debug ("Debug View", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BlenderClouds.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CloudScale;
                float _CloudOffsetX;
                float _CloudOffsetY;
                float _ObjectSpace;
                float _StackMinY;
                float _StackMaxY;
                float _WindX;
                float _WindY;
                float _Brightness;
                float _AlphaScale;
                float _Cull;
                float _Debug;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 offset = float2(_CloudOffsetX, _CloudOffsetY);
                float2 wind = float2(_WindX, _WindY) * _Time.y;

                float2 coordWS = i.positionWS.xz * _CloudScale;
                float2 coordOS = i.positionOS.xz * _CloudScale;
                float2 coord = lerp(coordWS, coordOS, _ObjectSpace) + offset + wind;

                float span = max(_StackMaxY - _StackMinY, 1e-5);
                float layerT = saturate((i.positionOS.y - _StackMinY) / span);

                float3 color;
                float alpha, noiseA, noiseB, grad;
                BlenderCloudsDebug_float(coord, layerT, color, alpha, noiseA, noiseB, grad);

                alpha = saturate(alpha * _AlphaScale);
                color *= _Brightness;

                // Debug views are opaque so you can actually see them.
                if (_Debug > 0.5 && _Debug < 1.5) return float4(grad.xxx, 1);
                if (_Debug > 1.5 && _Debug < 2.5) return float4(frac(coord), 0, 1);
                if (_Debug > 2.5 && _Debug < 3.5) return float4(noiseA.xxx, 1);
                if (_Debug > 3.5 && _Debug < 4.5) return float4(noiseB.xxx, 1);
                if (_Debug > 4.5)                 return float4(alpha.xxx, 1);

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
