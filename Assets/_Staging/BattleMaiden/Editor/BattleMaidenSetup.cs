using UnityEditor;
using UnityEngine;

// Tools → Characters → Battle Maiden → Open Clothing Setup. Staging, one-off: delete this Editor folder after use.
//
// Opens Add Clothing To Model already filled in for the Battle Maiden Raiment, then you press Add. The prefab
// is the clothing source rather than the FBX, so every piece (plates, pauldrons, vambraces, knees, skirts) keeps
// the material set on it. Equipping Item_Medium_BattleMaidenRaiment shows the option battle_maiden in the body
// group, with the manabond sheer and overshirt drawn over the body, sheer first.

public static class BattleMaidenSetup
{
    private const string Folder = "Assets/_Staging/BattleMaiden/";
    private const string ModelPath = "Assets/_Staging/HEv2/HighElfBase.prefab";

    [MenuItem("Tools/Characters/Battle Maiden/Open Clothing Setup")]
    private static void Open()
    {
        var clothing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BattleMaidenRainment.prefab");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var sheer = AssetDatabase.LoadAssetAtPath<Material>(Folder + "BattleMaidenSheerLayer.mat");
        var overshirt = AssetDatabase.LoadAssetAtPath<Material>(Folder + "BattleMaidenOvershirtLayer.mat");

        if (clothing == null || model == null || sheer == null || overshirt == null)
        {
            Debug.LogError("[BattleMaidenSetup] Missing the raiment prefab, HighElfBase or a layer material.");
            return;
        }

        var window = EditorWindow.GetWindow<ClothingSetup>("Add Clothing");
        var form = new SerializedObject(window);
        form.FindProperty("clothing").objectReferenceValue = clothing;
        form.FindProperty("model").objectReferenceValue = model;
        form.FindProperty("material").objectReferenceValue = null;
        form.FindProperty("groupId").stringValue = "body";
        form.FindProperty("optionId").stringValue = "battle_maiden";
        form.FindProperty("fitsGroup").stringValue = "";
        form.FindProperty("fitsSteps").ClearArray();

        var layers = form.FindProperty("layers");
        layers.arraySize = 2;
        layers.GetArrayElementAtIndex(0).objectReferenceValue = sheer;
        layers.GetArrayElementAtIndex(1).objectReferenceValue = overshirt;

        form.ApplyModifiedProperties();
    }
}
