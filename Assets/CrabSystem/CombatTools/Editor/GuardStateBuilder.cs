using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-shot builder for the Guard sub-state machine in Upper Body Combat (Combat_Framework §3).
//
//   Any State ─Block─► BlockStart ─► BlockLoop ─Parry──────► Parry ─┐
//                          │            │  └─BlockedHit─► BlockHit ─┤
//                          └────────────┴──── IsBlocking false ─────┴─► Exit
//
// The animator only shows the guard. Whether a block, parry or release is allowed is decided in
// code on the frame clock; code fires the triggers only when they are legal.
//
// Also sets the old Upper Body Block layer's weight to 0 and points KatanaBlock at the Block
// trigger. Refuses to run twice — delete the Guard sub-state machine to rebuild it.
// Backups: _Staging/_Backup/Guard_v1/.
public static class GuardStateBuilder
{
    const string ControllerPath = "Assets/Database/3d/Humanoid/Animations/HumanoidAnimator.controller";
    const string BlockAbilityPath = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/KatanaBlock.asset";
    const string LoopClipPath = "Assets/Database/3d/Humanoid/Animations/HumanM@Parry1H01_R - Loop.anim";
    const string HitClipPath = "Assets/Database/3d/Humanoid/Animations/HumanM@Parry1H01_R - Hit.anim";
    const string ParryClipPath = "Assets/Database/3d/Anims/HighBlockFront.anim";

    const string CombatLayer = "Upper Body Combat";
    const string OldBlockLayer = "Upper Body Block";
    const string GuardName = "Guard";

    // BlockStart borrows the 80f loop clip until a raise clip exists: leave after 6f.
    // Set to 1 once BlockStart has its own clip.
    const float BlockStartExit = 6f / 80f;
    const float Blend = 0.1f;

    [MenuItem("Tools/Combat/Build Guard States")]
    static void Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(LoopClipPath);
        var hit = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClipPath);
        var parry = AssetDatabase.LoadAssetAtPath<AnimationClip>(ParryClipPath);

        if (controller == null || loop == null || hit == null || parry == null)
        {
            Debug.LogError("[GuardStateBuilder] Controller or a guard clip is missing — check the paths at the top of the script.");
            return;
        }

        int layerIndex = FindLayer(controller, CombatLayer);
        if (layerIndex < 0)
        {
            Debug.LogError($"[GuardStateBuilder] No '{CombatLayer}' layer on {controller.name}.");
            return;
        }

        AnimatorStateMachine root = controller.layers[layerIndex].stateMachine;
        if (FindChildMachine(root, GuardName) != null)
        {
            Debug.LogWarning($"[GuardStateBuilder] '{GuardName}' already exists in {CombatLayer}. Delete it to rebuild.");
            return;
        }

        EnsureParameter(controller, "Block", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsBlocking", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Parry", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "BlockedHit", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine guard = root.AddStateMachine(GuardName, new Vector3(500f, 300f, 0f));
        AnimatorState start = AddState(guard, "BlockStart", loop, new Vector3(250f, 0f, 0f));
        AnimatorState hold = AddState(guard, "BlockLoop", loop, new Vector3(250f, 100f, 0f));
        AnimatorState blockHit = AddState(guard, "BlockHit", hit, new Vector3(500f, 50f, 0f));
        AnimatorState parryState = AddState(guard, "Parry", parry, new Vector3(500f, 150f, 0f));

        AnimatorStateTransition enter = root.AddAnyStateTransition(start);
        Configure(enter, false, 0f, Blend);
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "Block");

        Configure(start.AddTransition(hold), true, BlockStartExit, 0f);
        Release(start.AddExitTransition(), false);

        Configure(hold.AddTransition(parryState), false, 0f, 0f).AddCondition(AnimatorConditionMode.If, 0f, "Parry");
        Configure(hold.AddTransition(blockHit), false, 0f, 0f).AddCondition(AnimatorConditionMode.If, 0f, "BlockedHit");
        Release(hold.AddExitTransition(), false);

        Configure(blockHit.AddTransition(blockHit), false, 0f, 0f).AddCondition(AnimatorConditionMode.If, 0f, "BlockedHit");
        Resume(blockHit.AddTransition(hold));
        Release(blockHit.AddExitTransition(), true);

        Resume(parryState.AddTransition(hold));
        Release(parryState.AddExitTransition(), true);

        SetLayerWeight(controller, OldBlockLayer, 0f);
        PointBlockAbility();

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GuardStateBuilder] Built '{GuardName}' in {CombatLayer}; '{OldBlockLayer}' weight set to 0; KatanaBlock → Block.");
    }

    static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;   // matches every other state in the controller
        return state;
    }

    static AnimatorStateTransition Configure(AnimatorStateTransition t, bool hasExitTime, float exitTime, float duration)
    {
        t.hasExitTime = hasExitTime;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.interruptionSource = TransitionInterruptionSource.None;
        return t;
    }

    // Back to the held guard once a one-shot finishes, if the button is still down.
    static void Resume(AnimatorStateTransition t)
    {
        Configure(t, true, 1f, Blend);
        t.AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");
    }

    // Guard lowered. One-shots (blockstun, parry) finish first; held states leave at once.
    static void Release(AnimatorStateTransition t, bool waitForClip)
    {
        Configure(t, waitForClip, 1f, Blend);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    static int FindLayer(AnimatorController controller, string name)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == name) return i;
        return -1;
    }

    static AnimatorStateMachine FindChildMachine(AnimatorStateMachine root, string name)
    {
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
            if (child.stateMachine.name == name) return child.stateMachine;
        return null;
    }

    static void SetLayerWeight(AnimatorController controller, string name, float weight)
    {
        int index = FindLayer(controller, name);
        if (index < 0) return;

        AnimatorControllerLayer[] layers = controller.layers;   // a copy — must be assigned back
        layers[index].defaultWeight = weight;
        controller.layers = layers;
    }

    static void PointBlockAbility()
    {
        var block = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(BlockAbilityPath);
        if (block == null)
        {
            Debug.LogWarning("[GuardStateBuilder] KatanaBlock not found — set its animationTrigger to 'Block' by hand.");
            return;
        }
        block.animationTrigger = "Block";
        EditorUtility.SetDirty(block);
    }
}
