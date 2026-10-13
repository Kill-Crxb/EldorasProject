using UnityEditor;
using UnityEngine;

// Tools → AI → Build Mirror Moves (8 Oct). Points Fight And Guard at the Mirror's three reads: the Dash as a dodge
// (she backs off, then dashes along it), Dash Thrust as a gap closer, and the Dash in when the thrust is cooling
// down. Rerunnable.
public static class MirrorMovesBuilder
{
    const string Tag = "MirrorMovesBuilder";
    const string DashPath = "Assets/Database/Resources/AbilityDatabase/Movement/Dash.asset";
    const string DashThrustPath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/DashThrust.asset";
    const string GoalPath = "Assets/Database/AI/Goals/Goal_FightAndGuard.asset";

    [MenuItem("Tools/AI/Build Mirror Moves")]
    public static void Build()
    {
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

    static string Name(AbilityDefinition ability) => ability != null ? ability.abilityId : "MISSING";
}
