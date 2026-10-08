using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools → AI → Build Mirror Moves (8 Oct). Points Fight And Guard at the Mirror's three reads: the Dash as a dodge
// (she backs off, then dashes along it), Dash Thrust as a gap closer, and the Dash in when the thrust is cooling
// down. Also clears the Back Dash the first version made (Crxb: use the existing dash): its asset, Base_PC
// starter entry, Movement Actions state and trigger, and the cue it added to K_QuickShift_B. Rerunnable.
public static class MirrorMovesBuilder
{
    const string Tag = "MirrorMovesBuilder";
    const string DashPath = "Assets/Database/Resources/AbilityDatabase/Movement/Dash.asset";
    const string DashThrustPath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/DashThrust.asset";
    const string GoalPath = "Assets/Database/AI/Goals/Goal_FightAndGuard.asset";
    const string PlayerPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    const string BackDashPath = "Assets/Database/Resources/AbilityDatabase/Movement/BackDash.asset";
    const string BackClipPath = "Assets/CombatGirlsAnimations/Katana_Girl/Special/K_QuickShift_B.anim";
    const string BackDash = "BackDash";

    [MenuItem("Tools/AI/Build Mirror Moves")]
    public static void Build()
    {
        RemoveBackDash();
        PointGoal();
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Fight the Mirror: she backs off and dashes from your swings, Dash Thrusts in from 4–8 m, dashes in otherwise.");
    }

    static void PointGoal()
    {
        var goal = AssetDatabase.LoadAssetAtPath<FightAndGuardGoal>(GoalPath);
        if (goal == null)
        {
            Debug.LogError($"[{Tag}] Missing {GoalPath}.");
            return;
        }

        var dash = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DashPath);
        goal.dodge = dash;
        goal.dashIn = dash;
        goal.gapCloser = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DashThrustPath);
        EditorUtility.SetDirty(goal);
        Debug.Log($"[{Tag}] goal: Fight And Guard dodge {Name(goal.dodge)}, gap closer {Name(goal.gapCloser)}, dash in {Name(goal.dashIn)}.");
    }

    static void RemoveBackDash()
    {
        var backDash = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(BackDashPath);
        if (backDash != null)
        {
            RemoveStarter(backDash);
            AssetDatabase.DeleteAsset(BackDashPath);
            Debug.Log($"[{Tag}] cleanup: Back Dash ability deleted.");
        }

        RemoveState();
        RemoveCue();
    }

    static void RemoveStarter(AbilityDefinition ability)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        var manager = root.GetComponentInChildren<RuntimeAbilityManager>(true);
        var so = manager != null ? new SerializedObject(manager) : null;
        SerializedProperty starters = so?.FindProperty("starterAbilities");

        int index = starters == null ? -1 : Enumerable.Range(0, starters.arraySize)
            .Where(i => starters.GetArrayElementAtIndex(i).objectReferenceValue == ability).DefaultIfEmpty(-1).First();
        if (index >= 0)
        {
            starters.GetArrayElementAtIndex(index).objectReferenceValue = null;
            starters.DeleteArrayElementAtIndex(index);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            Debug.Log($"[{Tag}] cleanup: Back Dash removed from Base_PC's starters.");
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    static void RemoveState()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AbilityBuildKit.ControllerPath);
        if (controller == null) return;

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == BackDash);
            if (state == null) continue;

            machine.RemoveState(state);
            Debug.Log($"[{Tag}] cleanup: {BackDash} state removed from {layer.name}.");
        }

        if (controller.parameters.Any(p => p.name == BackDash))
        {
            controller.RemoveParameter(controller.parameters.First(p => p.name == BackDash));
            Debug.Log($"[{Tag}] cleanup: {BackDash} trigger removed.");
        }

        EditorUtility.SetDirty(controller);
    }

    static void RemoveCue()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BackClipPath);
        if (clip == null) return;

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        if (!events.Any(e => e.functionName == "OnCue")) return;

        AnimationUtility.SetAnimationEvents(clip, events.Where(e => e.functionName != "OnCue").ToArray());
        EditorUtility.SetDirty(clip);
        Debug.Log($"[{Tag}] cleanup: OnCue removed from {clip.name}.");
    }

    static string Name(AbilityDefinition ability) => ability != null ? ability.abilityId : "MISSING";
}
