using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools → Characters → Fill Reading Layer
//
// Puts every CombatGirls clip that nothing plays yet into HumanoidAnimator_v2's "ForReadingAnims" layer, one state
// each, so each can be played on a character and watched (Animation_Pool.md lists what they might be for). A clip
// counts as placed when any other layer's state, blend tree or synced override plays it, or Katana Girl's override
// controller swaps it in.
//
// States are named by pack and file ("KG K_Sp_Skill_3", "SS SS_Attack_4"), laid out in a grid, with no transitions.
// The layer starts on an empty state, so it shows nothing until a state is played. Rerunnable: states already there
// are kept, missing ones added. Delete the layer when the clips have homes.
public static class ReadingLayerBuilder
{
    const string ControllerPath = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string OverridesPath = "Assets/Database/Characters/KatanaGirl/KatanaGirl_Overrides.overrideController";
    const string LayerName = "ForReadingAnims";
    const string EmptyState = "Empty";
    const int Columns = 6;

    static readonly (string label, string folder)[] Packs =
    {
        ("KG", "Assets/CombatGirlsCharacterPack/Katana_Girl/Animations"),
        ("SS", "Assets/CombatGirlsCharacterPack/CombatGirl_Shield/Animations"),
    };

    [MenuItem("Tools/Characters/Fill Reading Layer")]
    static void Fill()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[ReadingLayerBuilder] No controller at {ControllerPath}.");
            return;
        }

        int layerIndex = FindOrAddLayer(controller);
        AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;

        HashSet<Motion> placed = PlacedMotions(controller, layerIndex);
        AddOverridden(placed);
        EnsureEmptyDefault(machine);

        var existing = new HashSet<string>(machine.states.Select(s => s.state.name));
        int added = 0;
        int skipped = 0;

        foreach ((string label, string folder) in Packs)
        {
            foreach (string path in ClipPaths(folder))
            {
                AnimationClip clip = LoadClip(path);
                string name = $"{label} {Path.GetFileNameWithoutExtension(path)}";

                if (clip == null || placed.Contains(clip)) { skipped++; continue; }
                if (existing.Contains(name)) continue;

                AnimatorState state = machine.AddState(name, GridPosition(machine.states.Length));
                state.motion = clip;
                state.writeDefaultValues = false;
                added++;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ReadingLayerBuilder] {LayerName}: {added} states added, {machine.states.Length - 1} unplaced clips in all; {skipped} clips already placed elsewhere and left out.");
    }

    // New layers start at weight 0 with no mask, so the reading layer shows nothing until it is used.
    static int FindOrAddLayer(AnimatorController controller)
    {
        int index = System.Array.FindIndex(controller.layers, l => l.name == LayerName);
        if (index >= 0) return index;

        controller.AddLayer(LayerName);
        Debug.Log($"[ReadingLayerBuilder] No '{LayerName}' layer found in the saved controller; added one.");
        return controller.layers.Length - 1;
    }

    static HashSet<Motion> PlacedMotions(AnimatorController controller, int readingLayer)
    {
        var placed = new HashSet<Motion>();
        AnimatorControllerLayer[] layers = controller.layers;

        for (int i = 0; i < layers.Length; i++)
        {
            if (i == readingLayer) continue;

            AnimatorControllerLayer source = layers[i].syncedLayerIndex >= 0 ? layers[layers[i].syncedLayerIndex] : layers[i];
            foreach (AnimatorState state in AllStates(source.stateMachine))
            {
                AddMotion(state.motion, placed);
                if (layers[i].syncedLayerIndex >= 0) AddMotion(layers[i].GetOverrideMotion(state), placed);
            }
        }

        return placed;
    }

    static IEnumerable<AnimatorState> AllStates(AnimatorStateMachine machine)
    {
        foreach (ChildAnimatorState child in machine.states)
            yield return child.state;

        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            foreach (AnimatorState state in AllStates(sub.stateMachine))
                yield return state;
    }

    static void AddMotion(Motion motion, HashSet<Motion> placed)
    {
        if (motion == null) return;
        placed.Add(motion);

        if (!(motion is BlendTree tree)) return;
        foreach (ChildMotion child in tree.children)
            AddMotion(child.motion, placed);
    }

    static void AddOverridden(HashSet<Motion> placed)
    {
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridesPath);
        if (overrides == null) return;

        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrides.GetOverrides(pairs);
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
            if (pair.Value != null) placed.Add(pair.Value);
    }

    static void EnsureEmptyDefault(AnimatorStateMachine machine)
    {
        AnimatorState empty = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == EmptyState);
        if (empty == null) empty = machine.AddState(EmptyState, new Vector3(0f, 0f, 0f));

        machine.defaultState = empty;
    }

    static IEnumerable<string> ClipPaths(string folder)
    {
        return AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p);
    }

    // The editable copy when the clips have been extracted, else the FBX's own clip.
    static AnimationClip LoadClip(string path)
    {
        AnimationClip extracted = CombatGirlsClips.ExtractedFor(path);
        if (extracted != null) return extracted;
        return CombatGirlsClips.FromFbx(path);
    }

    static Vector3 GridPosition(int index)
    {
        return new Vector3(300f + (index % Columns) * 240f, 60f * (index / Columns), 0f);
    }
}
