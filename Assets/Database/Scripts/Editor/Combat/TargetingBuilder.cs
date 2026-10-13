using UnityEditor;
using UnityEngine;

// Tools → Combat → Add Targeting to Player (8 Oct). Puts a TargetingModule on Base_PC next to its CameraModule.
// The HUD marks (TargetMarker, Crosshair) are placed by hand: an Image under the HUD canvas with the component.
public static class TargetingBuilder
{
    const string Tag = "TargetingBuilder";
    const string PlayerPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";

    [MenuItem("Tools/Combat/Add Targeting to Player")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        CameraModule cameraModule = root.GetComponentInChildren<CameraModule>(true);
        bool has = root.GetComponentInChildren<TargetingModule>(true) != null;

        if (cameraModule == null) Debug.LogError($"[{Tag}] No CameraModule on Base_PC.");
        if (cameraModule != null && !has)
        {
            cameraModule.gameObject.AddComponent<TargetingModule>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[{Tag}] {(has ? "Base_PC already has a TargetingModule." : "TargetingModule added to Base_PC.")}");
    }
}
