# Import Check

## v1.0.0 RC1 Asset Store Submission
After importing into a clean Unity project:

1. Open `Tools > FX Mesh Generator Pro`.
2. Confirm the header shows `v1.0.0 RC1 Asset Store Submission`.
3. In **Basic Mode**, confirm Developer / Publisher tools are hidden.
4. Press `Create / Update Scene Preview Object`.
5. Change Mesh Type through Slash, Beam, Ring, Disc, Dome, Half Dome, Helix, and Cross Helix.
6. Enable `Use Flow Preview` and `UV Surface Flow Arrows`.
7. Test `V Forward`, `V Reverse`, `U Forward`, `U Reverse`, `Flip U`, and `Flip V` with `Flow Offset` values `0`, `0.25`, and `0.5`.
8. Save a Mesh Asset and save the current Preview as Prefab.
9. Switch to Advanced Mode and run Mesh Validation Suite and Full Regression QA.
10. Build the Demo Scene and confirm no IMGUI layout errors appear.

Expected result: Basic mode remains clean, Flow preview and helper arrows agree, and saved assets are generated under `Assets/FXMeshGeneratorPro/Generated`.
