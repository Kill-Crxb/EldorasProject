Shader "FX Mesh Generator Pro/Preview/Vertex Alpha Checker"
{
    Properties
    {
        _Tint ("Preview Tint", Color) = (0.25, 0.75, 1.0, 0.85)
        _CheckerScale ("Checker Scale", Range(1, 64)) = 8
        _CheckerStrength ("Checker Strength", Range(0, 1)) = 0.85
        _PreviewMode ("Preview Mode", Float) = 3
        _FlowDirection ("Flow Direction", Float) = 0
        _FlowFlipU ("Flow Flip U", Float) = 0
        _FlowFlipV ("Flow Flip V", Float) = 0
        _FlowPreviewOffset ("Flow Preview Offset", Vector) = (0, 0, 0, 0)
        _AlphaOpacity ("Alpha As Opacity", Float) = 0
        _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "FXM_VertexAlphaPreview"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _CheckerScale;
            float _CheckerStrength;
            float _PreviewMode;
            float _FlowDirection;
            float _FlowFlipU;
            float _FlowFlipV;
            float4 _FlowPreviewOffset;
            float _AlphaOpacity;

            float3 FXM_HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float FXM_DistanceToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-5));
                return length(p - (a + ab * t));
            }

            float FXM_FinalUvFlowAxis(float direction)
            {
                float d = round(direction);
                // U-axis family: U Forward/Reverse and Circular modes store visual progress on final U.
                if (d == 3.0 || d == 4.0 || d == 7.0 || d == 8.0)
                {
                    return 0.0;
                }
                // V-axis family: Auto, V Forward/Reverse, From Center, To Center store visual progress on final V.
                return 1.0;
            }

            void FXM_ResolveFlowArrow(float direction, float flipU, float flipV, out float2 a, out float2 b)
            {
                // v0.32.7: Final UV basis rule.
                // Reverse and Flip are already baked into mesh UVs by the generator, so the material arrow should
                // use the positive axis of the FINAL UV basis. The physical mesh direction changes because the UV
                // basis itself changes, not because this shader inverts a second time.
                if (FXM_FinalUvFlowAxis(direction) < 0.5)
                {
                    a = float2(0.18, 0.50);
                    b = float2(0.82, 0.50);
                }
                else
                {
                    a = float2(0.50, 0.18);
                    b = float2(0.50, 0.82);
                }
            }

            float2 FXM_ResolveFlowPreviewOffset(float direction, float flipU, float flipV, float2 semanticOffset)
            {
                // semanticOffset.x = width/side, semanticOffset.y = flow/progress.
                // This is a sampling offset. Since this shader samples (uv + offset), visible texture motion is
                // opposite the offset. Use negative flow so Flow Offset visually travels with the arrow direction.
                float sideOffset = frac(semanticOffset.x);
                float flowOffset = frac(semanticOffset.y);

                if (FXM_FinalUvFlowAxis(direction) < 0.5)
                {
                    return float2(-flowOffset, sideOffset);
                }

                return float2(sideOffset, -flowOffset);
            }

            float FXM_FlowArrowMask(float2 uv01, float direction, float flipU, float flipV)
            {
                float2 a, b;
                FXM_ResolveFlowArrow(direction, flipU, flipV, a, b);
                float2 dir = normalize(b - a + 1e-5);
                float2 side = float2(-dir.y, dir.x);

                float lineMask = 1.0 - smoothstep(0.010, 0.026, FXM_DistanceToSegment(uv01, a, b));

                float2 p = uv01 - b;
                float along = dot(p, -dir);
                float across = abs(dot(p, side));
                float head = step(0.0, along) * step(along, 0.12) * step(across, along * 0.75 + 0.012);

                float2 tail = uv01 - a;
                float tailDot = 1.0 - smoothstep(0.018, 0.045, length(tail));

                return saturate(max(max(lineMask, head), tailDot * 0.75));
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float vertexAlpha = saturate(i.color.a);

                float2 scaledUv = i.uv * max(_CheckerScale, 1.0);
                float2 checkerCell = floor(scaledUv);
                float checker = fmod(checkerCell.x + checkerCell.y, 2.0);

                float3 checkerDark = float3(0.12, 0.12, 0.12);
                float3 checkerBright = float3(0.88, 0.88, 0.88);
                float3 checkerColor = lerp(checkerDark, checkerBright, checker);
                checkerColor = lerp(float3(0.5, 0.5, 0.5), checkerColor, saturate(_CheckerStrength));

                float3 solidColor = _Tint.rgb;
                // v0.28.3: true alpha-matte black. A small grey floor made artists read 0 alpha as semi-visible.
                float3 alphaColor = lerp(float3(0.0, 0.0, 0.0), _Tint.rgb, vertexAlpha);

                float mode = round(_PreviewMode);
                float3 finalColor = solidColor;

                if (mode == 1.0)
                {
                    finalColor = checkerColor;
                }
                else if (mode == 2.0)
                {
                    finalColor = alphaColor;
                }
                else if (mode == 3.0)
                {
                    finalColor = lerp(checkerColor, alphaColor, 0.58);
                }
                else if (mode == 4.0)
                {
                    finalColor = saturate(i.color.rgb);
                }
                else if (mode == 5.0)
                {
                    // v0.30.4: On-mesh flow preview texture. Matches the editor UV Gradient / Flow Preview:
                    // 24x10 quantized UV cells, HSV ramp driven by final UV, grid lines, and a repeated white flow arrow.
                    float2 uv01 = frac(i.uv + FXM_ResolveFlowPreviewOffset(_FlowDirection, _FlowFlipU, _FlowFlipV, _FlowPreviewOffset.xy));
                    float2 cells = float2(24.0, 10.0);
                    float2 cellUv = (floor(uv01 * cells) + 0.5) / cells;

                    float hue = frac(cellUv.x * 0.78 + cellUv.y * 0.22);
                    float value = lerp(0.32, 0.95, saturate(cellUv.y));
                    float3 flowColor = FXM_HsvToRgb(float3(hue, 0.75, value));

                    float2 grid = abs(frac(uv01 * cells) - 0.5);
                    float gridLine = 1.0 - smoothstep(0.455, 0.495, max(grid.x, grid.y));
                    flowColor = lerp(flowColor, float3(0.04, 0.08, 0.08), gridLine * 0.35);

                    float arrow = FXM_FlowArrowMask(uv01, _FlowDirection, _FlowFlipU, _FlowFlipV);
                    finalColor = lerp(flowColor, float3(1.0, 1.0, 1.0), arrow * 0.86);
                }

                float finalAlpha = saturate(_Tint.a);
                if (_AlphaOpacity > 0.5)
                {
                    // v0.28.3: no opacity floor. Vertex Alpha 0 must become fully transparent.
                    finalAlpha *= vertexAlpha;
                }

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
