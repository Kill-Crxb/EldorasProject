using System.Collections.Generic;
using System.Linq;
using System.Text;
using NinjaGame.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Read-only report of every ability's real frame data, taken from the animation it actually plays.
//
// For each AbilityDefinition: its animationTrigger → the animator state(s) that trigger enters → the
// clip → the clip's animation events, converted to frames at 60 fps. Startup / active / recovery are
// read off HitboxStart, HitboxEnd and AnimUnlocked. It also flags wiring that silently breaks an
// ability: a trigger the controller doesn't have, an effectTrigger event the clip never raises, a
// state speed that isn't 1, or a blend that shifts every event.
//
// Abilities with move data (AbilityDefinition.Move.cs) are also checked: the data itself (windows
// inside the move, routes that can fire) and the clip's events against it — drift is listed per
// event. The frame data is the source; the clip is what's wrong when they disagree.
//
// Move_Block_Build.md's validator. It never edits anything.
public class MoveReport : EditorWindow
{
    const float Fps = 60f;
    const string DefaultController = "Assets/Database/3d/Humanoid/Animations/HumanoidAnimator.controller";

    class Target
    {
        public string layer;
        public AnimatorState state;
        public float blend;
        public bool fixedBlend;
    }

    class Row
    {
        public AbilityDefinition ability;
        public string path;
        public readonly List<string> lines = new List<string>();
        public readonly List<string> problems = new List<string>();
        public readonly List<string> drift = new List<string>();
    }

    AnimatorController controller;
    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    bool onlyProblems;
    bool onlyAttacks = true;
    bool onlyMoves;

    [MenuItem("Tools/Combat/Move Report")]
    static void Open()
    {
        var window = GetWindow<MoveReport>("Move Report");
        window.controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DefaultController);
        window.Scan();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        controller = (AnimatorController)EditorGUILayout.ObjectField("Controller", controller, typeof(AnimatorController), false);
        if (GUILayout.Button("Scan", GUILayout.Width(60))) Scan();
        if (GUILayout.Button("Copy Markdown", GUILayout.Width(110))) EditorGUIUtility.systemCopyBuffer = Markdown();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        onlyProblems = GUILayout.Toggle(onlyProblems, "Only problems");
        onlyAttacks = GUILayout.Toggle(onlyAttacks, "Only abilities with a trigger");
        onlyMoves = GUILayout.Toggle(onlyMoves, "Only abilities with move data");
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row row in rows)
        {
            if (onlyProblems && row.problems.Count == 0) continue;
            if (onlyAttacks && string.IsNullOrEmpty(row.ability.animationTrigger)) continue;
            if (onlyMoves && !row.ability.HasMoveData) continue;
            DrawRow(row);
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawRow(Row row)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{row.ability.abilityName}  ({row.ability.abilityId})", EditorStyles.boldLabel);
        if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = row.ability;
        EditorGUILayout.EndHorizontal();

        foreach (string line in row.lines) EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
        foreach (string problem in row.problems) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        foreach (string drift in row.drift) EditorGUILayout.HelpBox(drift, MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    void Scan()
    {
        rows.Clear();
        if (controller == null) return;

        Dictionary<string, List<Target>> byTrigger = MapTriggers(controller);
        HashSet<string> parameters = new HashSet<string>(controller.parameters.Select(p => p.name));

        foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Backup/")) continue;

            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability == null) continue;

            rows.Add(Inspect(ability, path, byTrigger, parameters));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.ability.abilityName, b.ability.abilityName));

        int broken = rows.Count(r => r.problems.Count > 0);
        int moves = rows.Count(r => r.ability.HasMoveData);
        int drifting = rows.Count(r => r.drift.Count > 0);
        Debug.Log($"[MoveReport] {rows.Count} abilities scanned against {controller.name}, {broken} with problems, " +
                  $"{moves} with move data, {drifting} drifting from their clip.");
    }

    static Row Inspect(AbilityDefinition ability, string path, Dictionary<string, List<Target>> byTrigger, HashSet<string> parameters)
    {
        var row = new Row { ability = ability, path = path };
        string trigger = ability.animationTrigger;
        CheckMoveData(ability, row);

        if (string.IsNullOrEmpty(trigger))
        {
            row.lines.Add("no animationTrigger — effects run without an animation");
            return row;
        }

        if (!parameters.Contains(trigger))
        {
            row.problems.Add($"Trigger '{trigger}' is not a parameter on the controller — no animation plays, " +
                             "so no animation events fire and the ability ends on its safety timeout.");
            return row;
        }

        if (!byTrigger.TryGetValue(trigger, out List<Target> targets) || targets.Count == 0)
        {
            row.problems.Add($"Trigger '{trigger}' exists but no transition uses it.");
            return row;
        }

        foreach (Target target in targets) InspectState(ability, target, row);
        return row;
    }

    static void InspectState(AbilityDefinition ability, Target target, Row row)
    {
        var clip = target.state.motion as AnimationClip;
        if (clip == null)
        {
            row.problems.Add($"[{target.layer}] {target.state.name}: motion is not a single clip (blend tree or empty).");
            return;
        }

        if (!Mathf.Approximately(target.state.speed, 1f))
            row.problems.Add($"[{target.layer}] {target.state.name}: state speed {target.state.speed} — clip frames are not real frames.");
        if (target.blend > 0f)
        {
            string amount = target.fixedBlend ? $"{target.blend * Fps:0}f" : $"{target.blend:P0} of the previous state";
            row.problems.Add($"[{target.layer}] {target.state.name}: a {amount} blend into the state shifts every event.");
        }

        var events = AnimationUtility.GetAnimationEvents(clip);
        var frames = new Dictionary<string, int>();
        var order = new StringBuilder();
        foreach (AnimationEvent e in events)
        {
            string name = e.functionName.StartsWith("On") ? e.functionName.Substring(2) : e.functionName;
            int frame = Mathf.RoundToInt(e.time * Fps);
            if (!frames.ContainsKey(name)) frames[name] = frame;
            order.Append($"{name}@{frame}  ");
        }

        int length = Mathf.RoundToInt(clip.length * Fps);
        row.lines.Add($"[{target.layer}] {target.state.name} → {clip.name} ({length}f)");
        row.lines.Add("   " + order.ToString().TrimEnd());

        bool hasStart = frames.TryGetValue("HitboxStart", out int start);
        bool hasEnd = frames.TryGetValue("HitboxEnd", out int end);
        bool hasUnlock = frames.TryGetValue("AnimUnlocked", out int unlock);

        if (hasStart && hasEnd && hasUnlock)
            row.lines.Add($"   startup {start}f · active {end - start}f · recovery {unlock - end}f · tail after unlock {length - unlock}f");

        if (ability.HasMoveData)
            CompareFrames(ability, clip.name, length, frames, row);

        if (ability.abilityType == AbilityType.Offensive && ability.projectileData == null && (!hasStart || !hasEnd))
            row.problems.Add($"{clip.name}: offensive melee ability without HitboxStart/HitboxEnd — its hitbox never opens.");
        if (ability.waitForAnimUnlock && !hasUnlock)
            row.problems.Add($"{clip.name}: waitForAnimUnlock is on but the clip has no AnimUnlocked — ends on its {ability.maxDuration}s timeout.");

        string effect = ability.effectTrigger.ToString();
        bool effectIsEvent = ability.effectTrigger == AnimationEventType.Effect1 ||
                             ability.effectTrigger == AnimationEventType.Effect2 ||
                             ability.effectTrigger == AnimationEventType.Effect3;
        if (effectIsEvent && !frames.ContainsKey(effect))
            row.problems.Add($"{clip.name}: effectTrigger is {effect} but the clip never raises it — the ability's effects never run.");
    }

    static void CheckMoveData(AbilityDefinition ability, Row row)
    {
        if (!ability.HasMoveData)
        {
            row.lines.Add("no move data");
            return;
        }

        MoveFrames f = ability.frames;
        HitProperties hit = ability.hit;
        row.lines.Add($"move data: {ability.speedClass} · startup {f.startup}f · active {f.active}f · recovery {f.recovery}f · " +
                      $"total {ability.TotalFrames}f · on block {hit.blockAdvantage:+0;-0;0} · hit-stop {hit.hitStop}f · {hit.onHit}");

        if (f.active <= 0) row.problems.Add("Move data has no active frames — it can never hit.");
        if (f.recovery <= 0) row.problems.Add("Move data has no recovery — nothing about it is punishable.");

        CheckWindow("Cancel", f.cancelFrom, f.cancelTo, ability.TotalFrames, row);
        CheckWindow("I-frame", f.iframeFrom, f.iframeTo, ability.TotalFrames, row);
        CheckWindow("Armour", f.armorFrom, f.armorTo, ability.TotalFrames, row);

        bool hasWindow = f.cancelTo > f.cancelFrom;
        if (ability.routes.Count > 0 && !hasWindow)
            row.problems.Add("Has cancel routes but no cancel window — the routes can never fire.");

        foreach (CancelRoute route in ability.routes) CheckRoute(route, row);
    }

    static void CheckWindow(string label, int from, int to, int total, Row row)
    {
        if (from == 0 && to == 0) return;
        if (to <= from)
        {
            row.problems.Add($"{label} window {from}–{to}f is empty or reversed.");
            return;
        }
        if (to > total) row.problems.Add($"{label} window ends at {to}f, after the move ends at {total}f.");
    }

    static void CheckRoute(CancelRoute route, Row row)
    {
        if (route.into == null && route.intoCategory == MoveCategory.None)
            row.problems.Add("A cancel route has neither a move nor a category.");
        if (route.into != null && !route.into.HasMoveData)
            row.problems.Add($"Cancel route into {route.into.abilityName}, which has no move data.");
        if (route.into != null)
            row.lines.Add($"   route → {route.into.abilityName} ({route.when})");
    }

    static void CompareFrames(AbilityDefinition ability, string clip, int length, Dictionary<string, int> frames, Row row)
    {
        int before = row.drift.Count;
        AddDrift(clip, "HitboxStart", ability.ActiveStart, frames, row);
        AddDrift(clip, "HitboxEnd", ability.RecoveryStart, frames, row);
        AddDrift(clip, "AnimUnlocked", ability.TotalFrames, frames, row);

        if (length < ability.TotalFrames)
            row.problems.Add($"{clip}: clip is {length}f, shorter than the move's {ability.TotalFrames}f.");
        if (row.drift.Count == before)
            row.lines.Add("   ✔ clip events match the move data");
    }

    static void AddDrift(string clip, string evt, int expected, Dictionary<string, int> frames, Row row)
    {
        if (!frames.TryGetValue(evt, out int actual)) return;
        int delta = actual - expected;
        if (delta == 0) return;
        row.drift.Add($"{clip}: {evt} @{actual}f, move data says {expected}f ({delta:+0;-0}f)");
    }

    static Dictionary<string, List<Target>> MapTriggers(AnimatorController controller)
    {
        var map = new Dictionary<string, List<Target>>();
        foreach (AnimatorControllerLayer layer in controller.layers)
            CollectMachine(layer.name, layer.stateMachine, map);
        return map;
    }

    static void CollectMachine(string layer, AnimatorStateMachine machine, Dictionary<string, List<Target>> map)
    {
        foreach (AnimatorStateTransition t in machine.anyStateTransitions) Record(layer, t, map);
        foreach (ChildAnimatorState child in machine.states)
            foreach (AnimatorStateTransition t in child.state.transitions) Record(layer, t, map);
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            CollectMachine(layer, sub.stateMachine, map);
    }

    static void Record(string layer, AnimatorStateTransition transition, Dictionary<string, List<Target>> map)
    {
        if (transition.destinationState == null) return;

        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.mode != AnimatorConditionMode.If) continue;

            if (!map.TryGetValue(condition.parameter, out List<Target> list))
            {
                list = new List<Target>();
                map[condition.parameter] = list;
            }

            if (list.Any(x => x.state == transition.destinationState && x.layer == layer)) continue;

            list.Add(new Target
            {
                layer = layer,
                state = transition.destinationState,
                blend = transition.duration,
                fixedBlend = transition.hasFixedDuration,
            });
        }
    }

    string Markdown()
    {
        var md = new StringBuilder();
        md.AppendLine($"# Move Report — {controller.name}").AppendLine();
        foreach (Row row in rows)
        {
            if (onlyAttacks && string.IsNullOrEmpty(row.ability.animationTrigger)) continue;
            md.AppendLine($"## {row.ability.abilityName} (`{row.ability.abilityId}`)");
            foreach (string line in row.lines) md.AppendLine($"    {line}");
            foreach (string problem in row.problems) md.AppendLine($"- ⚠ {problem}");
            foreach (string drift in row.drift) md.AppendLine($"- ↔ {drift}");
            md.AppendLine();
        }
        return md.ToString();
    }
}
