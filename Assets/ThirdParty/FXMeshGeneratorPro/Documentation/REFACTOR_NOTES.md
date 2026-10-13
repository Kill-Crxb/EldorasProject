# Refactor Notes - v1.0.0 RC1 Asset Store Submission

This build keeps the refactored `partial` editor-window structure and separated UI helper file introduced during the v0.32 stabilization pass.

## Current Structure
- `FXMeshGeneratorProUnityEdition.cs` contains the main workflow, mesh generation UI, QA panels, export actions, and scene helpers.
- `FXMeshGeneratorProUnityEditionWindow.Ui.cs` contains shared IMGUI helper functions for consistent buttons, spacing, separators, and delayed editor operations.

## Stability Rules
- Do not call scene creation or destructive operations directly inside `OnGUI`; schedule them through delayed editor operations.
- Keep Publisher/Store tools hidden behind Advanced Mode and session unlock.
- Treat Flow Offset as preview-only. It must not modify saved mesh UV data.
- Keep helper foldouts independent from Scene View helper visibility.
