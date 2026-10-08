using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

// Tools → AI → Build Goal Test Variants
//
// Makes the new goals' assets, the extra moves they need, and one test monster per goal, as variants of
// the built monsters (run Build Monsters first). A variant changes only its goals, a move or two and a few
// AI settings, so a rebuild of the base monster flows through. Goals and moves are made once and then
// left alone — edit them directly. Monster_Goal_Tests.md has the layout and what to look for.
//
//   Rabbit_Ambush    sits still until you are within 5 m, then lunges      Ambush, Lunge, Flee
//   Slime_Leader     walks a 6 m square; pack leader                      Patrol, Chase & Melee, Surround
//   Slime_Follower   keeps near the nearest leader, runs to it in a fight  Follow Leader, Wander, Chase & Melee, Surround
//   Slime_Guard      guards, blocks your swings, punishes after them      Wander, Guard & Counter
//   Slime_Charger    hops in from up to 7 m, then slams                   Wander, Gap Close, Chase & Melee, Leash
//   Rabbit_Charger   1.2 s wind-up, then a straight rush you can sidestep  Wander, Charge, Lunge, Flee, Leash
//   Bat_Screecher    2 s cast that calls every idle AI within 25 m        Wander, Call For Help, Hit & Run, Leash
//   Ghost_Kiter      backs away while it keeps shooting                   Wander, Kite, Leash
public static class GoalTestBuilder
{
    const string GoalFolder = "Assets/Database/AI/Goals";
    const string MonsterRoot = "Assets/Database/Characters/Monsters";
    const string OutFolder = "Assets/Database/Characters/Monsters/Tests";
    const string AbilityFolder = "Assets/Database/Resources/AbilityDatabase/Monsters";
    const string BlockTemplate = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/KatanaBlock.asset";

    class Goals
    {
        public GOAPGoal ambush, guardCounter, follow, patrol, gapClose, leash, call, charge, kite, react;
        public GOAPGoal wander, chase, surround, lunge, flee, hitAndRun;
    }

    [MenuItem("Tools/AI/Build Goal Test Variants")]
    static void Build()
    {
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(MonsterRoot, "Tests");

        Goals g = LoadGoals();
        var square = new List<Vector3> { new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 6f), new Vector3(0f, 0f, 6f), Vector3.zero };

        Variant("Rabbit", "Rabbit_Ambush", new[] { g.ambush, g.lunge, g.flee }, false, null, null, AIRole.Melee);
        Variant("Slime", "Slime_Leader", new[] { g.patrol, g.chase, g.surround }, true, square, null, AIRole.Melee);
        Variant("Slime", "Slime_Follower", new[] { g.follow, g.wander, g.chase, g.surround }, false, null, null, AIRole.Melee);
        Variant("Slime", "Slime_Guard", new[] { g.wander, g.guardCounter }, false, null, Harden(), AIRole.Guard);
        Variant("Slime", "Slime_Charger", new[] { g.wander, g.gapClose, g.chase, g.leash }, false, null, Hop(), AIRole.Lunge);
        Variant("Rabbit", "Rabbit_Charger", new[] { g.wander, g.charge, g.lunge, g.flee, g.leash }, false, null, RabbitCharge(), AIRole.Charge);
        Variant("Bat", "Bat_Screecher", new[] { g.wander, g.call, g.hitAndRun, g.leash }, false, null, Screech(), AIRole.Call);
        Variant("Ghost", "Ghost_Kiter", new[] { g.wander, g.kite, g.leash }, false, null, MovingBolt(), AIRole.Ranged);

        AssetDatabase.SaveAssets();
        Debug.Log($"[GoalTestBuilder] Built 8 test monsters in {OutFolder}. Layout and checks: Monster_Goal_Tests.md.");
    }

    static Goals LoadGoals()
    {
        var g = new Goals();
        g.ambush = Goal<AmbushGoal>("Goal_Ambush", "Ambush", 3f, true);
        g.guardCounter = Goal<GuardCounterGoal>("Goal_GuardCounter", "Guard And Counter", 1f, true);
        g.follow = Goal<FollowLeaderGoal>("Goal_FollowLeader", "Follow Leader", 1f, false);
        g.patrol = Goal<PatrolGoal>("Goal_Patrol", "Patrol", 0.5f, false);
        g.gapClose = Goal<GapCloseGoal>("Goal_GapClose", "Gap Close", 1.5f, true);
        g.leash = Goal<LeashGoal>("Goal_Leash", "Leash", 10f, false);
        g.call = Goal<CallForHelpGoal>("Goal_CallForHelp", "Call For Help", 2f, true);
        g.charge = Goal<ChargeGoal>("Goal_Charge", "Charge", 1.5f, true);
        g.kite = Goal<KiteGoal>("Goal_Kite", "Kite", 1f, true);
        g.react = Goal<ReactGoal>("Goal_React", "React", 1f, true);

        g.wander = Existing("Goal_Wander");
        g.chase = Existing("Goal_ChaseMelee");
        g.surround = Existing("Goal_Surround");
        g.lunge = Existing("Goal_Lunge");
        g.flee = Existing("Goal_Flee");
        g.hitAndRun = Existing("Goal_HitAndRun");
        return g;
    }

    // ── Variants ─────────────────────────────────────────────────────────

    // A copy of the monster with its own goals. `extra`, when given, joins its abilities and takes `role`,
    // replacing whatever held that role before.
    static void Variant(string monster, string name, GOAPGoal[] goals, bool leader, List<Vector3> route, AbilityDefinition extra, AIRole role)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{MonsterRoot}/{monster}/{monster}.prefab");
        if (source == null)
        {
            Debug.LogError($"[GoalTestBuilder] No {monster}.prefab — run Tools → AI → Build Monsters first.");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        root.name = name;

        var goap = new SerializedObject(root.GetComponentInChildren<GOAPModule>(true));
        SetList(goap.FindProperty("goalPool"), goals);
        goap.ApplyModifiedPropertiesWithoutUndo();

        var control = new SerializedObject(root.GetComponentInChildren<AIControlSource>(true));
        control.FindProperty("isPackLeader").boolValue = leader;
        SetRoute(control.FindProperty("patrolOffsets"), route);
        if (extra != null) SetRole(control.FindProperty("roleAbilities"), role, extra);
        control.ApplyModifiedPropertiesWithoutUndo();

        if (extra != null) AddAbility(root, extra);

        var config = new SerializedObject(root.GetComponent<PersistentNPCConfigurator>());
        config.FindProperty("entityId").stringValue = name.ToLowerInvariant() + "_01";
        config.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{OutFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    static void SetList(SerializedProperty list, GOAPGoal[] goals)
    {
        list.arraySize = goals.Length;
        for (int i = 0; i < goals.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = goals[i];
    }

    static void SetRoute(SerializedProperty list, List<Vector3> route)
    {
        list.arraySize = route != null ? route.Count : 0;
        for (int i = 0; i < list.arraySize; i++)
            list.GetArrayElementAtIndex(i).vector3Value = route[i];
    }

    static void SetRole(SerializedProperty roles, AIRole role, AbilityDefinition ability)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < roles.arraySize; i++)
            if (roles.GetArrayElementAtIndex(i).FindPropertyRelative("role").intValue == (int)role)
                entry = roles.GetArrayElementAtIndex(i);

        if (entry == null)
        {
            roles.arraySize++;
            entry = roles.GetArrayElementAtIndex(roles.arraySize - 1);
        }

        entry.FindPropertyRelative("role").intValue = (int)role;
        entry.FindPropertyRelative("ability").objectReferenceValue = ability;
    }

    static void AddAbility(GameObject root, AbilityDefinition ability)
    {
        var so = new SerializedObject(root.GetComponentInChildren<AbilitySystem>(true));
        SerializedProperty list = so.FindProperty("abilities");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = ability;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Moves (made once) ────────────────────────────────────────────────

    // The Slime's guard: a copy of the katana block. No clip; GuardModule holds it while GuardHeld is down.
    static AbilityDefinition Harden()
    {
        AbilityDefinition ability = Clone(BlockTemplate, "Slime_Harden", out SerializedObject so);
        if (so != null) so.ApplyModifiedPropertiesWithoutUndo();
        return ability;
    }

    // The Slime's gap closer: a forward, slightly upward hop. No damage, no clip, 3 s cooldown.
    static AbilityDefinition Hop()
    {
        AbilityDefinition ability = Clone($"{AbilityFolder}/Slime_Slam.asset", "Slime_Hop", out SerializedObject so);
        if (so == null) return ability;

        Instant(so, AbilityCategory.Movement);
        so.FindProperty("range").floatValue = 7f;
        so.FindProperty("cooldown").floatValue = 3f;
        Push(so, new Vector3(0f, 0.35f, 1f), 10f, 0.35f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return ability;
    }

    // The Rabbit's charge: the rush only. A 1.2 s wind-up (cast time), then a big straight push — no clip,
    // no hit. The Charge goal strikes with the Rabbit's Lunge the moment it arrives, so the hit lands on
    // arrival rather than while the rush is still travelling. 9 m range, 5 s cooldown.
    static AbilityDefinition RabbitCharge()
    {
        AbilityDefinition ability = Clone($"{AbilityFolder}/Rabbit_Lunge.asset", "Rabbit_Charge", out SerializedObject so);
        if (so == null) return ability;

        Instant(so, AbilityCategory.Movement);
        so.FindProperty("castTime").floatValue = 1.2f;
        so.FindProperty("range").floatValue = 9f;
        so.FindProperty("cooldown").floatValue = 5f;
        Push(so, Vector3.forward, 14f, 0.5f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return ability;
    }

    // The Bat's screech: a 2 s cast that does nothing itself; Call For Help acts when it completes.
    // 15 s cooldown. No clip yet — the Bat just hangs still while it casts.
    static AbilityDefinition Screech()
    {
        AbilityDefinition ability = Clone($"{AbilityFolder}/Bat_Swoop.asset", "Bat_Screech", out SerializedObject so);
        if (so == null) return ability;

        Instant(so, AbilityCategory.Utility);
        so.FindProperty("castTime").floatValue = 2f;
        so.FindProperty("castWhileMoving").boolValue = false;
        so.FindProperty("range").floatValue = 25f;
        so.FindProperty("cooldown").floatValue = 15f;
        so.FindProperty("movementEffects").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ability;
    }

    // The Ghost's bolt, castable on the move, so a kiter can backpedal while it shoots.
    static AbilityDefinition MovingBolt()
    {
        AbilityDefinition ability = Clone($"{AbilityFolder}/Ghost_Bolt.asset", "Ghost_BoltMoving", out SerializedObject so);
        if (so == null) return ability;

        so.FindProperty("castWhileMoving").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ability;
    }

    // A copy of `templatePath` named `id`. `so` is null when it already exists — it's yours then.
    static AbilityDefinition Clone(string templatePath, string id, out SerializedObject so)
    {
        string path = $"{AbilityFolder}/{id}.asset";
        so = null;

        var existing = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        if (existing != null) return existing;

        var ability = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(templatePath));
        ability.name = id;
        AssetDatabase.CreateAsset(ability, path);

        so = new SerializedObject(ability);
        so.FindProperty("abilityId").stringValue = id;
        so.FindProperty("abilityName").stringValue = id.Replace('_', ' ');
        return ability;
    }

    // No clip, no hit: effects fire as the move starts (or as its cast ends) and it completes at once.
    static void Instant(SerializedObject so, AbilityCategory category)
    {
        so.FindProperty("animationTrigger").stringValue = "";
        so.FindProperty("abilityCategory").intValue = (int)category;
        so.FindProperty("castWhileMoving").boolValue = true;
        so.FindProperty("effectCue").intValue = 0;
        so.FindProperty("waitForAnimUnlock").boolValue = false;
        so.FindProperty("maxDuration").floatValue = 0.5f;
        so.FindProperty("bakedState").intValue = 0;
        so.FindProperty("bakedClip").objectReferenceValue = null;
        so.FindProperty("frames.startup").intValue = 0;
        so.FindProperty("frames.active").intValue = 0;
        so.FindProperty("frames.recovery").intValue = 0;
        so.FindProperty("damageEffects").arraySize = 0;
        so.FindProperty("knockbackEffects").arraySize = 0;
        so.FindProperty("strikes").arraySize = 0;
    }

    static void Push(SerializedObject so, Vector3 direction, float speed, float holiday)
    {
        SerializedProperty moves = so.FindProperty("movementEffects");
        moves.arraySize = 1;
        SerializedProperty e = moves.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("movementType").intValue = (int)MovementEffect.MovementType.Impulse;
        e.FindPropertyRelative("directionSource").intValue = (int)MovementDirectionSource.CasterFacing;
        e.FindPropertyRelative("direction").vector3Value = direction;
        e.FindPropertyRelative("speed").floatValue = speed;
        e.FindPropertyRelative("duration").floatValue = 0.2f;
        e.FindPropertyRelative("speedCap").floatValue = speed;
        e.FindPropertyRelative("frictionHoliday").floatValue = holiday;
    }

    // ── Goals (made once) ────────────────────────────────────────────────

    static GOAPGoal Goal<T>(string file, string goalName, float weight, bool needsTarget) where T : GOAPGoal
    {
        string path = $"{GoalFolder}/{file}.asset";
        var goal = AssetDatabase.LoadAssetAtPath<T>(path);
        if (goal != null) return goal;

        goal = ScriptableObject.CreateInstance<T>();
        goal.goalName = goalName;
        goal.baseWeight = weight;
        goal.requiresTarget = needsTarget;
        AssetDatabase.CreateAsset(goal, path);
        return goal;
    }

    static GOAPGoal Existing(string file)
    {
        var goal = AssetDatabase.LoadAssetAtPath<GOAPGoal>($"{GoalFolder}/{file}.asset");
        if (goal == null) Debug.LogError($"[GoalTestBuilder] Missing {file} — run Tools → AI → Build Monsters first.");
        return goal;
    }
}
