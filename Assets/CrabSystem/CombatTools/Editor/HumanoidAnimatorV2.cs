using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds HumanoidAnimator_v2 from HumanoidAnimator and installs it (Animator_Audit.md).
//
//   0 Head              base, IK pass            (kept)
//   1 Locomotion        blend trees              (kept)
//   2 Movement Actions  jump, fall, land, dash
//   3 Actions           every action clip, full body
//   4 Actions Upper     synced to Actions, upper mask — same states, arms only
//   5 Reactions         hits and death, override
//
// Rules: every action layer's default state is tagged Rest and AnimationLayerController holds the
// layer at 0 while it rests; every state has a clip; every trigger lives on exactly one layer and
// the state carries the trigger's name; states leave at exit time 1. The animator shows — code
// decides whether a trigger is legal.
//
// The v1 controller is copied, so Locomotion's blend trees come across untouched, then its four
// action layers are replaced. v1 itself is never edited. Clips are taken from v1's own states, so
// nothing here names a clip path except the guard clips.
public static class HumanoidAnimatorV2
{
    const string SourcePath = "Assets/Database/3d/Humanoid/HumanoidAnimator.controller";
    const string TargetPath = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string LayerControllerScript = "AnimationLayerController.cs";

    const string GuardLoopClip = "Assets/Database/3d/Humanoid/Animations/HumanM@Parry1H01_R - Loop.anim";
    const string GuardHitClip = "Assets/Database/3d/Humanoid/Animations/HumanM@Parry1H01_R - Hit.anim";
    const string ParryClip = "Assets/Database/3d/Anims/HighBlockFront.anim";

    const float ExitBlend = 0.15f;   // matches AnimationLayerController's default fade-out

    static readonly string[] ReplacedLayers = { "Full Body Actions", "Upper Body Combat", "Upper Body Block", "Reactions" };

    struct Source
    {
        public Motion motion;
        public float speed;
        public bool speedParameterActive;
        public string speedParameter;
        public float cycleOffset;
        public bool mirror;
        public bool footIK;
    }

    // ── 1. Build ──────────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Combat/Animator v2/1. Build Controller")]
    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetPath) != null)
        {
            Debug.LogWarning($"[AnimatorV2] {TargetPath} already exists. Delete it to rebuild.");
            return;
        }

        var v1 = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourcePath);
        if (v1 == null)
        {
            Debug.LogError($"[AnimatorV2] No controller at {SourcePath}.");
            return;
        }

        Dictionary<string, Source> fullBody = ReadStates(v1, "Full Body Actions");
        Dictionary<string, Source> upperBody = ReadStates(v1, "Upper Body Combat");
        Dictionary<string, Source> reactions = ReadStates(v1, "Reactions");
        AvatarMask upperMask = FindLayer(v1, "Upper Body Combat")?.avatarMask;

        var guardLoop = AssetDatabase.LoadAssetAtPath<AnimationClip>(GuardLoopClip);
        var guardHit = AssetDatabase.LoadAssetAtPath<AnimationClip>(GuardHitClip);
        var parry = AssetDatabase.LoadAssetAtPath<AnimationClip>(ParryClip);

        var missing = new List<string>();
        Require(fullBody, missing, "Combat Idle", "BasicAttack1", "BasicAttack2", "BasicAttack3", "Cleave", "Whirlwind",
                "Thrust", "Slam", "Punch1", "Punch2", "Kick1", "Kick2", "Uppercut", "Stomp",
                "Jump Start", "Jump Fall", "Jump Land", "Dash");
        Require(upperBody, missing, "HumanF@CastingDamage01", "QuickThrow", "DrawElement");
        Require(reactions, missing, "Hit Light", "Hit Heavy", "Stagger", "Death");
        if (upperMask == null) missing.Add("Upper Body Combat's avatar mask");
        if (guardLoop == null || guardHit == null || parry == null) missing.Add("a guard clip");

        if (missing.Count > 0)
        {
            Debug.LogError($"[AnimatorV2] Nothing built — missing from v1: {string.Join(", ", missing)}.");
            return;
        }

        if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            Debug.LogError($"[AnimatorV2] Could not copy {SourcePath}.");
            return;
        }

        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetPath);
        foreach (string layer in ReplacedLayers) RemoveLayer(c, layer);

        EnsureParameter(c, "Mantle", AnimatorControllerParameterType.Trigger);
        EnsureParameter(c, "IsBlocking", AnimatorControllerParameterType.Bool);
        EnsureParameter(c, "Parry", AnimatorControllerParameterType.Trigger);
        EnsureParameter(c, "BlockedHit", AnimatorControllerParameterType.Trigger);
        EnsureParameter(c, "IsDrawing", AnimatorControllerParameterType.Bool);

        Motion rest = fullBody["Combat Idle"].motion;

        BuildMovementActions(AddLayer(c, AnimationLayerNames.MovementActions, null), fullBody, rest);
        BuildActions(AddLayer(c, AnimationLayerNames.Actions, null), fullBody, upperBody, rest, guardLoop, guardHit, parry);
        AddSyncedLayer(c, AnimationLayerNames.ActionsUpper, AnimationLayerNames.Actions, upperMask);
        BuildReactions(AddLayer(c, AnimationLayerNames.Reactions, null), reactions, rest);

        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        Selection.activeObject = c;
        Debug.Log($"[AnimatorV2] Built {TargetPath}. Next: Tools → Combat → Animator v2 → 2. Install on Prefabs.");
    }

    static void BuildMovementActions(AnimatorStateMachine sm, Dictionary<string, Source> v1, Motion rest)
    {
        AnimatorState idle = AddRest(sm, rest);
        AnimatorState start = AddState(sm, "JumpStart", v1["Jump Start"], 1);
        AnimatorState fall = AddState(sm, "Fall", v1["Jump Fall"], 2);
        AnimatorState land = AddState(sm, "Land", v1["Jump Land"], 3);
        AnimatorState dash = AddState(sm, "Dash", v1["Dash"], 4);

        AnyTrigger(sm, start, "JumpTrigger", true, 0f);
        AnyTrigger(sm, start, "Mantle", true, 0f);          // no mantle clip yet — the jump stands in
        AnyTrigger(sm, dash, "Dash", true, 0f);

        // Off a ledge, knocked back, or a jump that skipped JumpStart: fall without a trigger.
        When(Link(idle.AddTransition(fall), false, 0f, 0.15f), AnimatorConditionMode.IfNot, "IsGrounded");

        When(Link(start.AddTransition(fall), true, 0.61f, 0.13f), AnimatorConditionMode.IfNot, "IsGrounded");
        When(Link(start.AddTransition(land), true, 0.61f, 0.05f), AnimatorConditionMode.If, "IsGrounded");
        When(Link(fall.AddTransition(land), false, 0f, 0.05f), AnimatorConditionMode.If, "IsGrounded");
        Link(land.AddTransition(idle), true, 0.4f, 0.1f);    // v1's tuned landing
        Link(dash.AddTransition(idle), true, 1f, ExitBlend);
    }

    static void BuildActions(AnimatorStateMachine sm, Dictionary<string, Source> fb, Dictionary<string, Source> ub,
                             Motion rest, AnimationClip guardLoop, AnimationClip guardHit, AnimationClip parry)
    {
        AnimatorState idle = AddRest(sm, rest);

        // Trigger name → v1 state it takes its clip from. State name = trigger name.
        AddOneShot(sm, idle, "BasicAttack1", fb["BasicAttack1"], 1);
        AddOneShot(sm, idle, "BasicAttack2", fb["BasicAttack2"], 2);
        AddOneShot(sm, idle, "BasicAttack3", fb["BasicAttack3"], 3);
        AddOneShot(sm, idle, "Cleave", fb["Cleave"], 4);
        AddOneShot(sm, idle, "Whirlwind", fb["Whirlwind"], 5);
        AddOneShot(sm, idle, "Thrust", fb["Thrust"], 6);
        AddOneShot(sm, idle, "Slam", fb["Slam"], 7);

        AddOneShot(sm, idle, "Punch1", fb["Punch1"], 9);
        AddOneShot(sm, idle, "UnarmedHeavy", fb["Punch2"], 10);
        AddOneShot(sm, idle, "Kick1", fb["Kick1"], 11);
        AddOneShot(sm, idle, "Kick2", fb["Kick2"], 12);
        AddOneShot(sm, idle, "SPunch", fb["Uppercut"], 13);
        AddOneShot(sm, idle, "SKick", fb["Stomp"], 14);

        // v1's cast states had no clip; they share the one casting clip until each gets its own.
        AddOneShot(sm, idle, "CastProjectile", ub["HumanF@CastingDamage01"], 16);
        AddOneShot(sm, idle, "CastBeam", ub["HumanF@CastingDamage01"], 17);
        AddOneShot(sm, idle, "CastBreath", ub["HumanF@CastingDamage01"], 18);
        AddOneShot(sm, idle, "CastWave", ub["HumanF@CastingDamage01"], 19);
        AddOneShot(sm, idle, "qThrow", ub["QuickThrow"], 20);

        BuildSigns(sm, idle, ub["DrawElement"]);
        BuildGuard(sm, idle, guardLoop, guardHit, parry);
    }

    // DrawElement plays per sign. Between signs the hands hold the raised pose (SealHold: the same
    // clip frozen near its end) for as long as IsDrawing — a sequence is being entered — is true.
    static void BuildSigns(AnimatorStateMachine sm, AnimatorState idle, Source draw)
    {
        AnimatorState sign = AddState(sm, "DrawElement", draw, 22, 1);
        Source held = draw;
        held.speed = 0f;
        held.speedParameterActive = false;
        held.cycleOffset = 0.95f;
        AnimatorState hold = AddState(sm, "SealHold", held, 22, 2);

        AnyTrigger(sm, sign, "DrawElement", true, 0f);
        When(Link(sign.AddTransition(hold), true, 1f, 0.1f), AnimatorConditionMode.If, "IsDrawing");
        When(Link(sign.AddTransition(idle), true, 1f, ExitBlend), AnimatorConditionMode.IfNot, "IsDrawing");
        When(Link(hold.AddTransition(idle), false, 0f, ExitBlend), AnimatorConditionMode.IfNot, "IsDrawing");
    }

    //   Rest ─IsBlocking─► Block ─Parry──────► Parry ─┐
    //                        └─BlockedHit─► BlockHit ─┤
    //                  IsBlocking false ──────────────┴─► Exit (→ Rest)
    //
    // The guard is a hold driven by the IsBlocking bool alone — no trigger, no events (29 Sep).
    // Entered from Rest rather than Any State, so it can't restart itself every frame while the
    // bool is true, and an attack thrown from guard drops back into it through Rest.
    static void BuildGuard(AnimatorStateMachine root, AnimatorState idle, AnimationClip loop, AnimationClip hit, AnimationClip parry)
    {
        AnimatorStateMachine guard = root.AddStateMachine("Guard", Grid(24, 0));
        AnimatorState hold = AddClip(guard, "Block", loop, new Vector3(250f, 0f, 0f));
        AnimatorState blocked = AddClip(guard, "BlockHit", hit, new Vector3(500f, 50f, 0f));
        AnimatorState deflect = AddClip(guard, "Parry", parry, new Vector3(500f, 150f, 0f));
        guard.defaultState = hold;

        AnimatorStateTransition enter = Link(idle.AddTransition(hold), false, 0f, 0.1f);
        enter.canTransitionToSelf = false;
        When(enter, AnimatorConditionMode.If, "IsBlocking");

        When(Link(hold.AddTransition(deflect), false, 0f, 0f), AnimatorConditionMode.If, "Parry");
        When(Link(hold.AddTransition(blocked), false, 0f, 0f), AnimatorConditionMode.If, "BlockedHit");
        When(Link(hold.AddExitTransition(), false, 0f, 0.1f), AnimatorConditionMode.IfNot, "IsBlocking");

        When(Link(blocked.AddTransition(blocked), false, 0f, 0f), AnimatorConditionMode.If, "BlockedHit");
        When(Link(blocked.AddTransition(hold), true, 1f, 0.1f), AnimatorConditionMode.If, "IsBlocking");
        When(Link(blocked.AddExitTransition(), true, 1f, 0.1f), AnimatorConditionMode.IfNot, "IsBlocking");

        When(Link(deflect.AddTransition(hold), true, 1f, 0.1f), AnimatorConditionMode.If, "IsBlocking");
        When(Link(deflect.AddExitTransition(), true, 1f, 0.1f), AnimatorConditionMode.IfNot, "IsBlocking");
    }

    static void BuildReactions(AnimatorStateMachine sm, Dictionary<string, Source> v1, Motion rest)
    {
        AnimatorState idle = AddRest(sm, rest);
        AnimatorState light = AddState(sm, "HitLight", v1["Hit Light"], 1);
        AnimatorState heavy = AddState(sm, "HitHeavy", v1["Hit Heavy"], 2);
        AnimatorState stagger = AddState(sm, "Stagger", v1["Stagger"], 3);
        AnimatorState death = AddState(sm, "Death", v1["Death"], 4);

        AnyTrigger(sm, light, "HitLight", true, 0f);
        AnyTrigger(sm, heavy, "HitHeavy", true, 0f);
        AnyTrigger(sm, stagger, "Stagger", true, 0f);
        AnyTrigger(sm, death, "Death", false, 0f);

        Link(light.AddTransition(idle), true, 1f, 0.1f);
        Link(heavy.AddTransition(idle), true, 1f, 0.1f);
        Link(stagger.AddTransition(idle), true, 1f, 0.1f);
        When(Link(death.AddTransition(idle), false, 0f, 0.2f), AnimatorConditionMode.IfNot, "IsDead");
    }

    // ── 2. Install ────────────────────────────────────────────────────────────────────────────

    // Model prefabs whose Animator uses v1 get v2. Entity prefabs (anything holding an
    // AnimationLayerController) gain AnimatorFactBridge and ActionsLayerDriver beside it and lose
    // AirborneLayerClaim, which v2's Movement Actions layer replaces. Components inherited from a
    // base or nested prefab are left to that prefab, so variants don't get duplicates.
    [MenuItem("Tools/Combat/Animator v2/2. Install on Prefabs")]
    static void Install()
    {
        var v1 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SourcePath);
        var v2 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TargetPath);
        if (v2 == null)
        {
            Debug.LogError("[AnimatorV2] Build the controller first (step 1).");
            return;
        }

        var report = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLowerInvariant().Contains("backup")) continue;

            string[] deps = AssetDatabase.GetDependencies(path, false);
            bool usesV1 = System.Array.IndexOf(deps, SourcePath) >= 0;
            bool isEntity = System.Array.Exists(deps, d => d.EndsWith("/" + LayerControllerScript));
            if (!usesV1 && !isEntity) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int swapped = usesV1 ? SwapControllers(root, v1, v2) : 0;
            int installed = isEntity ? InstallModules(root) : 0;

            if (swapped + installed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add($"{path}: {swapped} animator(s) → v2, {installed} brain(s) updated");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[AnimatorV2] Installed on {report.Count} prefab(s):\n{string.Join("\n", report)}");
    }

    static int SwapControllers(GameObject root, RuntimeAnimatorController v1, RuntimeAnimatorController v2)
    {
        int count = 0;
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            // Instances included: a model variant (Porphi) overrides its base's controller, and
            // that override is exactly what has to change. Only component ADDS need the variant guard.
            if (animator.runtimeAnimatorController != v1) continue;

            animator.runtimeAnimatorController = v2;
            count++;
        }
        return count;
    }

    static int InstallModules(GameObject root)
    {
        int count = 0;
        foreach (AnimationLayerController layers in root.GetComponentsInChildren<AnimationLayerController>(true))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(layers)) continue;

            GameObject host = layers.gameObject;
            if (host.GetComponent<AnimatorFactBridge>() == null) host.AddComponent<AnimatorFactBridge>();
            if (host.GetComponent<ActionsLayerDriver>() == null) host.AddComponent<ActionsLayerDriver>();
            count++;
        }

        foreach (AirborneLayerClaim old in root.GetComponentsInChildren<AirborneLayerClaim>(true))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(old)) continue;
            Object.DestroyImmediate(old, true);
        }

        return count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    static Dictionary<string, Source> ReadStates(AnimatorController c, string layerName)
    {
        var states = new Dictionary<string, Source>();
        AnimatorControllerLayer layer = FindLayer(c, layerName);
        if (layer != null) CollectStates(layer.stateMachine, states);
        return states;
    }

    static void CollectStates(AnimatorStateMachine sm, Dictionary<string, Source> into)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            AnimatorState s = child.state;
            into[s.name] = new Source
            {
                motion = s.motion,
                speed = s.speed,
                speedParameterActive = s.speedParameterActive,
                speedParameter = s.speedParameter,
                cycleOffset = s.cycleOffset,
                mirror = s.mirror,
                footIK = s.iKOnFeet,
            };
        }

        foreach (ChildAnimatorStateMachine sub in sm.stateMachines)
            CollectStates(sub.stateMachine, into);
    }

    // A state with no clip would write the bind pose on an override layer, so it counts as missing.
    static void Require(Dictionary<string, Source> states, List<string> missing, params string[] names)
    {
        foreach (string name in names)
            if (!states.TryGetValue(name, out Source s) || s.motion == null) missing.Add(name);
    }

    static AnimatorControllerLayer FindLayer(AnimatorController c, string name)
    {
        foreach (AnimatorControllerLayer layer in c.layers)
            if (layer.name == name) return layer;
        return null;
    }

    static void RemoveLayer(AnimatorController c, string name)
    {
        AnimatorControllerLayer[] layers = c.layers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == name) { c.RemoveLayer(i); return; }
    }

    static AnimatorStateMachine AddLayer(AnimatorController c, string name, AvatarMask mask)
    {
        c.AddLayer(name);
        AnimatorControllerLayer[] layers = c.layers;   // a copy — changes must be assigned back
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.defaultWeight = 1f;
        layer.avatarMask = mask;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        c.layers = layers;
        return layer.stateMachine;
    }

    static void AddSyncedLayer(AnimatorController c, string name, string sourceName, AvatarMask mask)
    {
        int source = System.Array.FindIndex(c.layers, l => l.name == sourceName);

        var machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(machine, c);

        c.AddLayer(new AnimatorControllerLayer
        {
            name = name,
            stateMachine = machine,
            avatarMask = mask,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            syncedLayerIndex = source,
            syncedLayerAffectsTiming = false,
        });

        FillSyncedMotions(c);
    }

    // A synced layer starts with no motion on any state, which on an override layer writes the bind
    // pose. Every state gets the source's own clip; a style can swap them later. Written through
    // SerializedObject because AnimatorControllerLayer.SetOverrideMotion on a layers-array copy
    // did not survive the save on the first build.
    [MenuItem("Tools/Combat/Animator v2/3. Refresh Actions Upper Clips")]
    static void RefreshSyncedMotions()
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetPath);
        if (c == null)
        {
            Debug.LogError("[AnimatorV2] Build the controller first (step 1).");
            return;
        }

        FillSyncedMotions(c);
        AssetDatabase.SaveAssets();
    }

    static void FillSyncedMotions(AnimatorController c)
    {
        AnimatorControllerLayer source = FindLayer(c, AnimationLayerNames.Actions);
        var states = new List<AnimatorState>();
        CollectStateObjects(source.stateMachine, states);

        var so = new SerializedObject(c);
        SerializedProperty layers = so.FindProperty("m_AnimatorLayers");
        SerializedProperty motions = FindSerializedLayer(layers, AnimationLayerNames.ActionsUpper)?.FindPropertyRelative("m_Motions");
        if (motions == null)
        {
            Debug.LogError("[AnimatorV2] No 'Actions Upper' layer to fill.");
            return;
        }

        motions.ClearArray();
        for (int i = 0; i < states.Count; i++)
        {
            motions.InsertArrayElementAtIndex(i);
            SerializedProperty pair = motions.GetArrayElementAtIndex(i);
            pair.FindPropertyRelative("m_State").objectReferenceValue = states[i];
            pair.FindPropertyRelative("m_Motion").objectReferenceValue = states[i].motion;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(c);
        Debug.Log($"[AnimatorV2] Actions Upper: {states.Count} state clips set from Actions.");
    }

    static SerializedProperty FindSerializedLayer(SerializedProperty layers, string name)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (layer.FindPropertyRelative("m_Name").stringValue == name) return layer;
        }
        return null;
    }

    static void CollectStateObjects(AnimatorStateMachine sm, List<AnimatorState> into)
    {
        foreach (ChildAnimatorState child in sm.states) into.Add(child.state);
        foreach (ChildAnimatorStateMachine sub in sm.stateMachines) CollectStateObjects(sub.stateMachine, into);
    }

    static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in c.parameters)
            if (p.name == name) return;
        c.AddParameter(name, type);
    }

    static AnimatorState AddRest(AnimatorStateMachine sm, Motion motion)
    {
        AnimatorState rest = sm.AddState("Rest", Grid(0, 0));
        rest.motion = motion;
        rest.tag = AnimationLayerController.RestTag;
        rest.writeDefaultValues = true;
        sm.defaultState = rest;
        return rest;
    }

    // An action entered by its own trigger that plays once and returns to Rest.
    static void AddOneShot(AnimatorStateMachine sm, AnimatorState rest, string trigger, Source source, int slot)
    {
        AnimatorState state = AddState(sm, trigger, source, slot);
        AnyTrigger(sm, state, trigger, false, 0f);
        Link(state.AddTransition(rest), true, 1f, ExitBlend);
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string name, Source source, int slot, int row = 1)
    {
        AnimatorState state = sm.AddState(name, Grid(slot, row));
        state.motion = source.motion;
        state.speed = source.speed;
        state.speedParameterActive = source.speedParameterActive;
        state.speedParameter = source.speedParameter;
        state.cycleOffset = source.cycleOffset;
        state.mirror = source.mirror;
        state.iKOnFeet = source.footIK;
        state.writeDefaultValues = true;   // matches every state in v1
        return state;
    }

    static AnimatorState AddClip(AnimatorStateMachine sm, string name, Motion clip, Vector3 position)
    {
        AnimatorState state = sm.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    static void AnyTrigger(AnimatorStateMachine sm, AnimatorState to, string trigger, bool canRestart, float duration)
    {
        AnimatorStateTransition t = Link(sm.AddAnyStateTransition(to), false, 0f, duration);
        t.canTransitionToSelf = canRestart;
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static AnimatorStateTransition Link(AnimatorStateTransition t, bool hasExitTime, float exitTime, float duration)
    {
        t.hasExitTime = hasExitTime;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.interruptionSource = TransitionInterruptionSource.None;
        return t;
    }

    static void When(AnimatorStateTransition t, AnimatorConditionMode mode, string parameter)
        => t.AddCondition(mode, 0f, parameter);

    static Vector3 Grid(int column, int row) => new Vector3(250f + column % 8 * 220f, row * 80f + column / 8 * 240f, 0f);
}
