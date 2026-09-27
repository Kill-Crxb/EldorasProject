# FX Mesh Generator Pro - Unity Edition Changelog

## v1.0.0 RC1 Asset Store Submission
- Promoted the stable `v0.32.7` UV Flow build to `v1.0.0 RC1` release-candidate branding.
- Simplified Basic-mode Export/Save UI so normal users see only core save, folder, and summary actions.
- Moved regression QA, finalizer, validation suite, and publisher tools behind Advanced Mode.
- Added Quick QA copy and Quick Start document ping from the user-facing Save panel.
- Kept the final UV basis behavior: Scene View helper arrows, on-mesh material arrows, and manual Flow Offset all follow the final generated UV basis.
- Kept preview-only Flow Offset behavior; saved mesh UVs are not modified by manual preview offsets.

## v0.32.7 Final UV Basis Flow Sync
- Fixed UV flow preview basis so Scene View helper arrows, on-mesh material arrows, and manual Flow Offset use the same final generated UV basis.
- Removed double Reverse/Flip application from preview material properties. Reverse and Flip are baked into mesh UVs; preview arrows read the resulting final UV axis.
- Manual Flow Offset visually moves the texture in the same direction as the arrows.
- Offset value `1.0` wraps to the same visual frame because UV preview is tiled 0-1; use `0.25` or `0.5` for direction checks.
