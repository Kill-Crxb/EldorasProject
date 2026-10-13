using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools → Combat → Build Directional Dash (10 Oct). The dash plays the QuickShift that matches where the dasher is
// travelling: DashBack, DashLeft and DashRight states on Movement Actions (copies of Dash), each clip given the
// push cue Dash's clip has, and Dash set to pick its trigger by direction. Rerunnable.
public static class DirectionalDashBuilder
{
    const string Tag = "DirectionalDashBuilder";
    const string DashPath = "Assets/Database/Resources/AbilityDatabase/Movement/Dash.asset";
    const string ClipFolder = "Assets/CombatGirlsAnimations/Katana_Girl/Special/";
    const string Layer = "Movement Actions";
    const float CueTime = 0.125f;

    static readonly (string state, string clip)[] Directions =
    {
        ("DashBack", "K_QuickShift_B"),
        ("DashLeft", "K_QuickShift_L"),
        ("DashRight", "K_QuickShift_R"),
    };

    [MenuItem("Tools/Combat/Build Directional Dash")]
    public static void Build()
    {
        foreach ((string state, string clipName) in Directions)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".anim");
            AddCue(clip);
            AbilityBuildKit.AddStateLike(Layer, "Dash", state, clip, Tag);
        }

        SetDirectional();
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Dash while holding back, left or right to see the matching QuickShift.");
    }

    static void AddCue(AnimationClip clip)
    {
        if (clip == null) return;

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        if (events.Any(e => e.functionName == "OnCue")) return;

        var cue = new AnimationEvent { time = CueTime, functionName = "OnCue", intParameter = 1 };
        AnimationUtility.SetAnimationEvents(clip, events.Append(cue).OrderBy(e => e.time).ToArray());
        EditorUtility.SetDirty(clip);
        Debug.Log($"[{Tag}] clip: OnCue(1) added to {clip.name} at {CueTime}s.");
    }

    static void SetDirectional()
    {
        var dash = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DashPath);
        if (dash == null)
        {
            Debug.LogError($"[{Tag}] Missing {DashPath}.");
            return;
        }

        dash.directionalTrigger = true;
        EditorUtility.SetDirty(dash);
        Debug.Log($"[{Tag}] ability: {dash.abilityId} picks its clip by travel direction.");
    }
}
