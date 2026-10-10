using UnityEditor;
using UnityEngine;

// Tools → Combat → Build Perilous (10 Oct). Makes Dash Thrust Perilous — it can't be blocked or parried, only
// dodged — and gives Base_PC (so the Mirror too) the ring and red weapon glow that warn of one. Rerunnable.
public static class PerilousBuilder
{
    const string Tag = "PerilousBuilder";
    const string DashThrustPath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/DashThrust.asset";
    const string PlayerPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    const string ClipPath = "Assets/Database/Sound/Combat-PerilousTell.wav";

    [MenuItem("Tools/Combat/Build Perilous")]
    public static void Build()
    {
        MakePerilous(DashThrustPath);
        AddTell();
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Dash Thrust rings and the weapon glows red through its windup; block and parry let it through.");
    }

    static void MakePerilous(string path)
    {
        var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        if (ability == null)
        {
            Debug.LogError($"[{Tag}] Missing {path}.");
            return;
        }

        ability.hit.guard = GuardType.Perilous;
        EditorUtility.SetDirty(ability);
        Debug.Log($"[{Tag}] ability: {ability.abilityId} is Perilous.");
    }

    static void AddTell()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        var flash = root.GetComponentInChildren<HitFlash>(true);
        if (flash == null)
        {
            Debug.LogError($"[{Tag}] No HitFlash on Base_PC to sit beside.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var tell = root.GetComponentInChildren<PerilousTell>(true);
        bool created = tell == null;
        if (created)
        {
            var go = new GameObject("PerilousTell");
            go.transform.SetParent(flash.transform.parent, false);
            go.transform.localPosition = Vector3.up * 1.2f;
            tell = go.AddComponent<PerilousTell>();
        }

        AudioSource source = tell.GetComponent<AudioSource>();
        if (source == null) source = tell.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0.8f;
        source.minDistance = 4f;
        source.maxDistance = 40f;

        var so = new SerializedObject(tell);
        so.FindProperty("audioSource").objectReferenceValue = source;
        so.FindProperty("tellClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[{Tag}] player: Perilous tell {(created ? "added to" : "rewired on")} Base_PC.");
    }
}
