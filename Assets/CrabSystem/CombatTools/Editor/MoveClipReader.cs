using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Reads what an ability's animation really does: its animationTrigger → the animator state(s) that
// trigger enters → the clip → the clip's events, as frames at 60 fps. Shared by Move Report (which
// shows it) and the move bake (which writes it into the ability). Animation events are the authority
// for move timing (CrabSystem_Standard §9), so this is where the numbers come from.
public static class MoveClipReader
{
    public const float Fps = 60f;

    public class Target
    {
        public string layer;
        public int layerIndex;
        public string path;
        public AnimatorState state;
        public float blend;
        public bool fixedBlend;
        public float offset;
    }

    public struct ClipEvent
    {
        public string name;
        public int frame;
    }

    // Every trigger in the controller → the states it sends a layer into.
    public static Dictionary<string, List<Target>> MapTriggers(AnimatorController controller)
    {
        var map = new Dictionary<string, List<Target>>();
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            var paths = new Dictionary<AnimatorState, string>();
            CollectPaths(layers[i].stateMachine, layers[i].name, paths);
            CollectMachine(layers[i].name, i, paths, layers[i].stateMachine, map);
        }
        return map;
    }

    // The clip's events in order, as frames from the moment the state starts. State speed and the
    // transition's start offset are applied, so the frames are the ones that play.
    public static List<ClipEvent> ReadEvents(AnimationClip clip, Target target)
    {
        var events = new List<ClipEvent>();
        float speed = target.state.speed > 0f ? target.state.speed : 1f;
        float start = target.offset * clip.length;

        foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
        {
            string name = e.functionName.StartsWith("On") ? e.functionName.Substring(2) : e.functionName;
            events.Add(new ClipEvent { name = name, frame = Mathf.RoundToInt((e.time - start) / speed * Fps) });
        }

        events.Sort((a, b) => a.frame.CompareTo(b.frame));
        return events;
    }

    public static int LengthInFrames(AnimationClip clip, Target target)
    {
        float speed = target.state.speed > 0f ? target.state.speed : 1f;
        return Mathf.RoundToInt(clip.length * (1f - target.offset) / speed * Fps);
    }

    // First occurrence, or -1.
    public static int First(List<ClipEvent> events, string name)
    {
        foreach (ClipEvent e in events)
            if (e.name == name) return e.frame;
        return -1;
    }

    // Last occurrence, or -1. A multi-hit move's active window runs to its last HitboxEnd.
    public static int Last(List<ClipEvent> events, string name)
    {
        int frame = -1;
        foreach (ClipEvent e in events)
            if (e.name == name) frame = e.frame;
        return frame;
    }

    // Every state's full path ("Layer.SubMachine.State"), the name Animator.StringToHash needs for
    // fullPathHash. A transition can point into another sub-machine, so paths are looked up, not
    // built from where the transition sits.
    static void CollectPaths(AnimatorStateMachine machine, string prefix, Dictionary<AnimatorState, string> paths)
    {
        foreach (ChildAnimatorState child in machine.states)
            paths[child.state] = prefix + "." + child.state.name;
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            CollectPaths(sub.stateMachine, prefix + "." + sub.stateMachine.name, paths);
    }

    static void CollectMachine(string layer, int layerIndex, Dictionary<AnimatorState, string> paths,
                               AnimatorStateMachine machine, Dictionary<string, List<Target>> map)
    {
        foreach (AnimatorStateTransition t in machine.anyStateTransitions) Record(layer, layerIndex, paths, t, map);
        foreach (ChildAnimatorState child in machine.states)
            foreach (AnimatorStateTransition t in child.state.transitions) Record(layer, layerIndex, paths, t, map);
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            CollectMachine(layer, layerIndex, paths, sub.stateMachine, map);
    }

    static void Record(string layer, int layerIndex, Dictionary<AnimatorState, string> paths,
                       AnimatorStateTransition transition, Dictionary<string, List<Target>> map)
    {
        if (transition.destinationState == null) return;
        if (!paths.TryGetValue(transition.destinationState, out string path)) return;

        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.mode != AnimatorConditionMode.If) continue;

            if (!map.TryGetValue(condition.parameter, out List<Target> list))
            {
                list = new List<Target>();
                map[condition.parameter] = list;
            }

            if (Contains(list, layer, transition.destinationState)) continue;

            list.Add(new Target
            {
                layer = layer,
                layerIndex = layerIndex,
                path = path,
                state = transition.destinationState,
                blend = transition.duration,
                fixedBlend = transition.hasFixedDuration,
                offset = transition.offset,
            });
        }
    }

    static bool Contains(List<Target> list, string layer, AnimatorState state)
    {
        foreach (Target t in list)
            if (t.state == state && t.layer == layer) return true;
        return false;
    }
}
