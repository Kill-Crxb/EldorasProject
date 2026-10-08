using UnityEditor;
using UnityEngine;

// Tools → Combat → Build Dash Thrust (8 Oct). The Katana Girl's K_Sp_Skill_3, renamed DashThrust: a wind-up, a
// dash (~4.4 m in 0.15 s, marked by Travel(1)/Travel(0) for a later teleport) and a thrust. Adds its Actions state
// and trigger to HumanoidAnimator_v2 (copying BasicAttack3's entry and exit), makes the ability from BasicAttack3
// once, gives it to Base_PC and bakes. Rerunnable: an existing ability keeps its tuned values.
public static class DashThrustBuilder
{
    const string Tag = "DashThrustBuilder";
    const string ClipPath = "Assets/CombatGirlsAnimations/Katana_Girl/Special/DashThrust.anim";
    const string TemplatePath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/BasicAttack3.asset";
    const string AbilityPath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/DashThrust.asset";
    const string Trigger = "DashThrust";

    [MenuItem("Tools/Combat/Build Dash Thrust")]
    public static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (!AbilityBuildKit.AddStateLike(AnimationLayerNames.Actions, "BasicAttack3", Trigger, clip, Tag)) return;

        AbilityDefinition ability = AbilityBuildKit.CopyOnce(TemplatePath, AbilityPath, Author, Tag);
        if (ability == null) return;

        AbilityBuildKit.GiveToPlayer(ability, Tag);
        AssetDatabase.SaveAssets();
        MoveFrameBaker.Bake(true);
        Debug.Log($"[{Tag}] Done. Check Move Report, then try it from the spellbook / hotbar against the Mirror.");
    }

    // From BasicAttack3: katana dice, Stagger on hit, rooted with root motion.
    static void Author(SerializedObject so)
    {
        so.FindProperty("abilityId").stringValue = Trigger;
        so.FindProperty("abilityName").stringValue = "Dash Thrust";
        so.FindProperty("description").stringValue = "A wind-up, a blinding dash, then a thrust.";
        so.FindProperty("animationTrigger").stringValue = Trigger;
        so.FindProperty("speedClass").enumValueIndex = (int)SpeedClass.Technique;
        so.FindProperty("castWhileMoving").boolValue = false;
        so.FindProperty("useRootMotion").boolValue = true;
        so.FindProperty("rootMotionScale").floatValue = 2f;
        so.FindProperty("maxDuration").floatValue = 5f;
        so.FindProperty("cooldown").floatValue = 5f;

        SerializedProperty strikes = so.FindProperty("strikes");
        strikes.arraySize = 1;
        SerializedProperty thrust = strikes.GetArrayElementAtIndex(0);
        thrust.FindPropertyRelative("reachBonus").floatValue = 0.5f;
        thrust.FindPropertyRelative("arc").floatValue = 30f;
        thrust.FindPropertyRelative("heightMin").floatValue = 0f;
        thrust.FindPropertyRelative("heightMax").floatValue = 2f;
        thrust.FindPropertyRelative("lineOfSight").boolValue = true;
    }
}
