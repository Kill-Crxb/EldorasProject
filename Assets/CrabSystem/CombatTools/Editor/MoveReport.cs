using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Read-only report of every ability's real frame data, taken from the animation it actually plays.
//
// For each AbilityDefinition: its animationTrigger → the animator state(s) that trigger enters → the
// clip → the clip's animation events, converted to frames at 60 fps. Startup / active / recovery are
// read off the first and last Strike and Unlocked. It also flags wiring that silently breaks an
// ability: a trigger the controller doesn't have, an effectCue the clip never raises, a missing Tell,
// a state speed that isn't 1, or a blend that shifts every event.
//
// Abilities with move data (AbilityDefinition.Move.cs) are also checked: the data itself (windows
// inside the move, routes that can fire), whether the baked frames still match the clip, and the clip
// against the move's speed-class target. The clip is the authority (CrabSystem_Standard §9); the
// target is what the animator aims for. "Bake" writes the clip's frames into the abilities
// (MoveFrameBaker); everything else here is read-only.
public class MoveReport : EditorWindow
{
    const float Fps = MoveClipReader.Fps;
    const string DefaultController = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";

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
        if (GUILayout.Button("Bake", GUILayout.Width(60))) BakeAndScan();
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

    void BakeAndScan()
    {
        MoveFrameBaker.Bake(true);
        Scan();
    }

    void Scan()
    {
        rows.Clear();
        if (controller == null) return;

        Dictionary<string, List<MoveClipReader.Target>> byTrigger = MoveClipReader.MapTriggers(controller);
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
                  $"{moves} with move data, {drifting} off their speed-class target.");
    }

    static Row Inspect(AbilityDefinition ability, string path, Dictionary<string, List<MoveClipReader.Target>> byTrigger, HashSet<string> parameters)
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

        if (!byTrigger.TryGetValue(trigger, out List<MoveClipReader.Target> targets) || targets.Count == 0)
        {
            row.problems.Add($"Trigger '{trigger}' exists but no transition uses it.");
            return row;
        }

        foreach (MoveClipReader.Target target in targets) InspectState(ability, target, row);
        return row;
    }

    static void InspectState(AbilityDefinition ability, MoveClipReader.Target target, Row row)
    {
        var clip = target.state.motion as AnimationClip;
        if (clip == null)
        {
            row.problems.Add($"[{target.layer}] {target.state.name}: motion is not a single clip (blend tree or empty).");
            return;
        }

        if (!Mathf.Approximately(target.state.speed, 1f))
            row.problems.Add($"[{target.layer}] {target.state.name}: state speed {target.state.speed} — the frames below are scaled by it.");
        if (target.blend > 0f)
        {
            string amount = target.fixedBlend ? $"{target.blend * Fps:0}f" : $"{target.blend:P0} of the previous state";
            row.problems.Add($"[{target.layer}] {target.state.name}: a {amount} blend into the state shifts every event.");
        }

        List<MoveClipReader.ClipEvent> events = MoveClipReader.ReadEvents(clip, target);
        var frames = new Dictionary<string, int>();
        var order = new StringBuilder();
        foreach (MoveClipReader.ClipEvent e in events)
        {
            if (!frames.ContainsKey(e.name)) frames[e.name] = e.frame;
            order.Append(e.value != 0 ? $"{e.name}({e.value})@{e.frame}  " : $"{e.name}@{e.frame}  ");
        }
        int lastStrike = MoveClipReader.Last(events, "Strike");
        if (lastStrike >= 0) frames[LastStrike] = lastStrike;

        int length = MoveClipReader.LengthInFrames(clip, target);
        row.lines.Add($"[{target.layer}] {target.state.name} → {clip.name} ({length}f)");
        row.lines.Add("   " + order.ToString().TrimEnd());

        bool hasStrike = frames.TryGetValue("Strike", out int start);
        bool hasUnlock = frames.TryGetValue("Unlocked", out int unlock);

        if (hasStrike && hasUnlock)
            row.lines.Add($"   startup {start}f · active {lastStrike - start + 1}f · recovery {unlock - lastStrike - 1}f · tail after unlock {length - unlock}f");

        if (ability.HasMoveData)
            CompareFrames(ability, clip.name, length, frames, row);

        bool melee = ability.abilityType == AbilityType.Offensive && !ability.IsParry && ability.projectileData == null;
        if (melee && !hasStrike)
            row.problems.Add($"{clip.name}: offensive melee ability without a Strike — it can never hit.");
        if (melee && hasStrike && !frames.ContainsKey("Tell"))
            row.problems.Add($"{clip.name}: no Tell — the move tells as it starts, so defenders read it early.");
        if (ability.waitForAnimUnlock && !hasUnlock)
            row.problems.Add($"{clip.name}: waitForAnimUnlock is on but the clip has no Unlocked — ends on its {ability.maxDuration}s timeout.");
        if (ability.effectCue > 0 && MoveClipReader.First(events, "Cue", ability.effectCue) < 0)
            row.problems.Add($"{clip.name}: effectCue is {ability.effectCue} but the clip never raises Cue({ability.effectCue}) — the ability's effects never run.");
    }

    const string LastStrike = "Strike (last)";

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

    // The baked frames should equal the clip's (stale means the bake hasn't run since the clip changed).
    // Drift is the clip against the speed-class target: the animator's to-do list, not an error.
    static void CompareFrames(AbilityDefinition ability, string clip, int length, Dictionary<string, int> frames, Row row)
    {
        bool stale = Differs("Strike", ability.ActiveStart, frames) ||
                     Differs(LastStrike, ability.RecoveryStart - 1, frames) ||
                     Differs("Unlocked", ability.TotalFrames, frames);
        if (stale)
            row.problems.Add($"{clip}: the move's frames don't match its clip — press Bake.");

        if (ability.speedClass == SpeedClass.None) return;

        int before = row.drift.Count;
        int startup = AbilityDefinition.ClassStartup(ability.speedClass);
        int active = AbilityDefinition.ClassActive(ability.speedClass);
        int recovery = AbilityDefinition.ClassRecovery(ability.speedClass);
        AddDrift(clip, "Strike", startup, frames, row);
        AddDrift(clip, LastStrike, startup + active - 1, frames, row);
        AddDrift(clip, "Unlocked", startup + active + recovery, frames, row);

        if (row.drift.Count == before)
            row.lines.Add($"   ✔ clip hits its {ability.speedClass} target");
    }

    static bool Differs(string evt, int expected, Dictionary<string, int> frames)
        => frames.TryGetValue(evt, out int actual) && actual != expected;

    static void AddDrift(string clip, string evt, int target, Dictionary<string, int> frames, Row row)
    {
        if (!frames.TryGetValue(evt, out int actual)) return;
        int delta = actual - target;
        if (delta == 0) return;
        row.drift.Add($"{clip}: {evt} @{actual}f, target {target}f ({delta:+0;-0}f)");
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
