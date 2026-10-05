using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// The move bake (CrabSystem_Standard §9, Move_Block_Build.md). Animation events are the authority for
// move timing; a move's frame data is a reading of its clip, written here so nobody keeps it in step by
// hand. For every ability whose clip carries HitboxStart, HitboxEnd and AnimUnlocked:
//
//   startup  = first HitboxStart
//   active   = last HitboxEnd − first HitboxStart
//   recovery = AnimUnlocked − last HitboxEnd
//
// Runs from Tools → Combat → Bake Move Frames, and on its own whenever a clip, model or controller
// is imported (MoveBakeSettings.bakeOnImport). Only frames.startup / active / recovery, bakedClip and
// the state the move plays (bakedState, bakedLayer, bakedFade, bakedStartTime) are written; hit
// properties, windows and routes stay authored. AbilitySystem starts that state directly when the
// move starts, so the clip and the move always begin on the same frame.
public static class MoveFrameBaker
{
    [MenuItem("Tools/Combat/Bake Move Frames")]
    public static void BakeFromMenu() => Bake(true);

    public static void Bake(bool logEvenIfUnchanged)
    {
        MoveBakeSettings settings = FindSettings();
        if (settings == null || settings.controllers.Count == 0)
        {
            Debug.LogError("[MoveFrameBaker] No MoveBakeSettings with a controller. Create one " +
                           "(Create → CrabSystem → Combat → Move Bake Settings) and list the characters' animator controllers.");
            return;
        }

        var maps = new List<Dictionary<string, List<MoveClipReader.Target>>>();
        foreach (var controller in settings.controllers)
            if (controller != null) maps.Add(MoveClipReader.MapTriggers(controller));

        var report = new StringBuilder();
        int changed = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/_Backup/")) continue;

            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability == null || string.IsNullOrEmpty(ability.animationTrigger)) continue;

            if (BakeOne(ability, maps, report)) changed++;
        }

        if (changed > 0) AssetDatabase.SaveAssets();
        if (changed == 0 && !logEvenIfUnchanged && report.Length == 0) return;

        Debug.Log($"[MoveFrameBaker] {changed} move(s) re-baked.\n{report}");
    }

    // True when the ability's frames changed.
    static bool BakeOne(AbilityDefinition ability, List<Dictionary<string, List<MoveClipReader.Target>>> maps, StringBuilder report)
    {
        if (!TryRead(ability.animationTrigger, maps, out AnimationClip clip, out MoveClipReader.Target target, out MoveFrames read))
        {
            if (ability.HasMoveData && ability.bakedClip != null)
                report.AppendLine($"  ⚠ {ability.abilityId}: its clip no longer has HitboxStart / HitboxEnd / AnimUnlocked — frames left as they were.");
            return false;
        }

        int state = Animator.StringToHash(target.path);
        float fade = FadeSeconds(target, clip);
        float startTime = target.offset * clip.length;

        MoveFrames f = ability.frames;
        bool same = f.startup == read.startup && f.active == read.active && f.recovery == read.recovery &&
                    ability.bakedClip == clip && ability.bakedState == state && ability.bakedLayer == target.layerIndex &&
                    Mathf.Approximately(ability.bakedFade, fade) && Mathf.Approximately(ability.bakedStartTime, startTime);
        if (same) return false;

        Undo.RecordObject(ability, "Bake Move Frames");
        report.AppendLine($"  {ability.abilityId}: {f.startup}/{f.active}/{f.recovery} → {read.startup}/{read.active}/{read.recovery}  ({clip.name})");

        f.startup = read.startup;
        f.active = read.active;
        f.recovery = read.recovery;
        ability.frames = f;
        ability.bakedClip = clip;
        ability.bakedState = state;
        ability.bakedLayer = target.layerIndex;
        ability.bakedFade = fade;
        ability.bakedStartTime = startTime;
        EditorUtility.SetDirty(ability);
        return true;
    }

    // A normalised transition duration is a fraction of the state it leaves, which the bake can't know;
    // the move's own clip stands in for it.
    static float FadeSeconds(MoveClipReader.Target target, AnimationClip clip)
        => target.fixedBlend ? target.blend : target.blend * clip.length;

    // The first controller (in settings order) whose state for this trigger has all three events.
    static bool TryRead(string trigger, List<Dictionary<string, List<MoveClipReader.Target>>> maps,
                        out AnimationClip clip, out MoveClipReader.Target found, out MoveFrames frames)
    {
        clip = null;
        found = null;
        frames = default;

        foreach (var map in maps)
        {
            if (!map.TryGetValue(trigger, out List<MoveClipReader.Target> targets)) continue;

            foreach (MoveClipReader.Target target in targets)
            {
                var candidate = target.state.motion as AnimationClip;
                if (candidate == null) continue;

                List<MoveClipReader.ClipEvent> events = MoveClipReader.ReadEvents(candidate, target);
                int start = MoveClipReader.First(events, "HitboxStart");
                int end = MoveClipReader.Last(events, "HitboxEnd");
                int unlock = MoveClipReader.First(events, "AnimUnlocked");
                if (start < 0 || end <= start || unlock < end) continue;

                clip = candidate;
                found = target;
                frames.startup = Mathf.Max(1, start);
                frames.active = end - start;
                frames.recovery = unlock - end;
                return true;
            }
        }

        return false;
    }

    static MoveBakeSettings FindSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:MoveBakeSettings");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<MoveBakeSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public static bool BakeOnImport()
    {
        MoveBakeSettings settings = FindSettings();
        return settings != null && settings.bakeOnImport;
    }
}

// An edited event in a clip (or a clip inside an FBX, or a controller) re-bakes on import, so the frame
// data can never go stale. Deferred, because assets mustn't be edited while the import is running.
public class MoveBakeOnImport : AssetPostprocessor
{
    static bool queued;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (queued || !TouchesAnimation(imported)) return;

        queued = true;
        EditorApplication.delayCall += RunQueued;
    }

    static void RunQueued()
    {
        queued = false;
        if (MoveFrameBaker.BakeOnImport()) MoveFrameBaker.Bake(false);
    }

    static bool TouchesAnimation(string[] paths)
    {
        foreach (string path in paths)
        {
            string lower = path.ToLowerInvariant();
            if (lower.EndsWith(".anim") || lower.EndsWith(".fbx") || lower.EndsWith(".controller")) return true;
        }
        return false;
    }
}
