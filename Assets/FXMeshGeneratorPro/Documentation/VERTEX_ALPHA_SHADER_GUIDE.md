# Vertex Alpha Shader Guide

Generated meshes include Color32 data. Use vertex color alpha for opacity, dissolve, edge softness, or mask blending. RGB channels can be used for packed masks when needed.

Recommended shader inputs:
- UV0
- Vertex Color RGB
- Vertex Color A
