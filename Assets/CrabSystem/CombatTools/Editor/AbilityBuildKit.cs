using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The steps the ability builders share (Dash Thrust, Mirror moves): an animator state that enters and leaves like an
// existing one, an ability copied once from a template, an ability handed to Base_PC. Each step logs under the
// builder's tag. Rerunnable: an existing state only gets its clip refreshed; an existing ability keeps its tuning.
public static class AbilityBuildKit
{
    public const string ControllerPath = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string PlayerPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";

    // A state named `name` on `layerName`, playing `clip`, entered from Any State by the trigger `name` and left by
    // the same transitions as `templateName`. Synced layers get the clip as their override motion.
    public static bool AddStateLike(string layerName, string templateName, string name, AnimationClip clip, string tag)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || clip == null)
        {
            Debug.LogError($"[{tag}] Missing {(controller == null ? ControllerPath : "clip for " + name)}.");
            return false;
        }

        if (!controller.parameters.Any(p => p.name == name))
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

        AnimatorControllerLayer[] layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == layerName);
        if (index < 0)
        {
            Debug.LogError($"[{tag}] No {layerName} layer on the controller.");
            return false;
        }

        AnimatorStateMachine machine = layers[index].stateMachine;
        AnimatorState template = FindState(machine, templateName);
        if (template == null)
        {
            Debug.LogError($"[{tag}] No {templateName} state on {layerName} to copy.");
            return false;
        }

        AnimatorState state = FindState(machine, name);
        bool created = state == null;
        if (created) state = machine.AddState(name);

        state.motion = clip;
        state.speed = 1f;
        state.tag = template.tag;
        state.writeDefaultValues = template.writeDefaultValues;

        if (created)
        {
            CopyBehaviours(template, state);
            AddEntry(machine, template, state, name);
            AddExits(template, state);
        }

        foreach (AnimatorControllerLayer layer in layers.Where(l => l.syncedLayerIndex == index))
            layer.SetOverrideMotion(state, clip);

        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        Debug.Log($"[{tag}] animator: {name} state {(created ? "added" : "updated")} on {layerName}, trigger {name}.");
        return true;
    }

    // Copies `templatePath` to `path` and lets `setup` author it — once. Later runs return the existing asset.
    public static AbilityDefinition CopyOnce(string templatePath, string path, Action<SerializedObject> setup, string tag)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        if (existing != null)
        {
            Debug.Log($"[{tag}] ability: {existing.abilityId} exists; tuned values kept.");
            return existing;
        }

        if (!AssetDatabase.CopyAsset(templatePath, path))
        {
            Debug.LogError($"[{tag}] Couldn't copy {templatePath}.");
            return null;
        }

        var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        var so = new SerializedObject(ability);
        so.FindProperty("routes").arraySize = 0;
        so.FindProperty("bakedClip").objectReferenceValue = null;
        setup(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[{tag}] ability: {ability.abilityId} made from {templatePath}.");
        return ability;
    }

    // Base_PC's starter abilities; the Mirror, a Base_PC variant, starts with them too.
    public static void GiveToPlayer(AbilityDefinition ability, string tag)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        var manager = root.GetComponentInChildren<RuntimeAbilityManager>(true);
        if (manager == null)
        {
            Debug.LogError($"[{tag}] No RuntimeAbilityManager on Base_PC.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var so = new SerializedObject(manager);
        SerializedProperty starters = so.FindProperty("starterAbilities");
        bool has = Enumerable.Range(0, starters.arraySize).Any(i => starters.GetArrayElementAtIndex(i).objectReferenceValue == ability);
        if (!has)
        {
            starters.arraySize++;
            starters.GetArrayElementAtIndex(starters.arraySize - 1).objectReferenceValue = ability;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[{tag}] player: {ability.abilityId} {(has ? "already in" : "added to")} Base_PC's starter abilities.");
    }

    static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        return machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
    }

    static void CopyBehaviours(AnimatorState from, AnimatorState to)
    {
        foreach (StateMachineBehaviour behaviour in from.behaviours)
            EditorUtility.CopySerialized(behaviour, to.AddStateMachineBehaviour(behaviour.GetType()));
    }

    static void AddEntry(AnimatorStateMachine machine, AnimatorState template, AnimatorState state, string trigger)
    {
        AnimatorStateTransition source = machine.anyStateTransitions.FirstOrDefault(t => t.destinationState == template);
        AnimatorStateTransition entry = machine.AddAnyStateTransition(state);
        if (source != null) CopyTiming(source, entry);
        entry.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void AddExits(AnimatorState template, AnimatorState state)
    {
        foreach (AnimatorStateTransition source in template.transitions)
        {
            AnimatorStateTransition exit = source.isExit ? state.AddExitTransition() : state.AddTransition(source.destinationState);
            CopyTiming(source, exit);
            foreach (AnimatorCondition condition in source.conditions)
                exit.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }

    static void CopyTiming(AnimatorStateTransition from, AnimatorStateTransition to)
    {
        to.duration = from.duration;
        to.offset = from.offset;
        to.exitTime = from.exitTime;
        to.hasExitTime = from.hasExitTime;
        to.hasFixedDuration = from.hasFixedDuration;
        to.interruptionSource = from.interruptionSource;
        to.orderedInterruption = from.orderedInterruption;
        to.canTransitionToSelf = from.canTransitionToSelf;
    }
}
