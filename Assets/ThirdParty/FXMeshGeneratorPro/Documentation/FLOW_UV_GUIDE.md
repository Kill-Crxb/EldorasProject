# Flow UV Guide

Use these together for final validation:

- 2D Flow Preview
- On-Mesh Flow Texture
- Surface Flow Arrows
- U/V Basis Overlay

Checks:
- V Forward and V Reverse should oppose each other.
- U Forward and U Reverse should oppose each other.
- Circular CW and CCW should rotate opposite directions.
- Flip U / Flip V should update preview texture and arrows.


## Preview Offset / Animated Flow QA
`Preview Offset U/V` and `Animate Flow Preview` are preview-only controls. They move the on-mesh flow preview texture so artists can verify direction and continuity without changing saved UV data.


## v1.0.0 RC1 Manual Flow Offset Controller
Use `Flow Offset` to manually slide the preview texture along the selected flow direction. Use `Width Offset` to slide across the width/thickness side axis. These controls are preview-only and do not modify saved mesh UVs.


## v1.0.0 RC1 Unified flow basis
The Scene View helper arrows and preview material arrows now use the final generated UV basis as the shared source of truth. Use `Flow Offset` to manually slide the preview texture along the selected flow axis; this does not affect saved UVs.


## v1.0.0 RC1 True UV Axis Flow Debug
- `U Forward` follows the real material U axis.
- `V Forward` follows the real material V axis.
- `U Reverse` and `V Reverse` invert only their own axis.
- `Flip U` and `Flip V` invert the displayed helper/material arrows and preview offset on the matching axis.
- This avoids the previous behavior where U/V modes could look like the same physical direction after UV remapping.


## v1.0.0 RC1 Final UV Basis Flow Sync
- The generator now treats the generated mesh UV as the single source of truth for preview direction.
- Reverse and Flip are baked into the UV layout; helper arrows and material arrows no longer reverse a second time.
- `Flow Offset` is a tiled preview control. `1.0` equals a full UV cycle and visually returns to the same frame. Use `0.25` / `0.5` to verify movement direction.
