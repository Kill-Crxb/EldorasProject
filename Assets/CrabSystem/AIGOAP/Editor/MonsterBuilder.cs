using System.Collections.Generic;
using NinjaGame.Animation;
using RPG.Factions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Tools → AI → Build Monsters
//
// Builds the level-1 monster roster from the Level 1 Monster Pack, one folder per monster under
// Database/Characters/Monsters. For each: animation events on the FBX's attack clip, an animator
// controller, a model prefab (registered in the model database), a natural weapon, an archetype,
// a movement profile, and the monster itself as a Base_PC variant driven by GOAP.
//
// Data is made once and then left alone: specs, abilities, goals. Edit those and rebuild.
// Everything else is regenerated from the spec on every run. Scene instances keep their link.
// Monster_Build.md has the design and the test list.
public static class MonsterBuilder
{
    const string Root = "Assets/Database/Characters/Monsters";
    const string PackModels = "Assets/Level 1 Monster Pack/Models/";
    const string PackPrefabs = "Assets/Level 1 Monster Pack/Prefabs/";
    const string BasePath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    const string HumanoidController = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string ModelDatabasePath = "Assets/Database/Characters/EldoraModelDatabase.asset";
    const string BakeSettingsPath = "Assets/Database/GameData/Combat/MoveBakeSettings.asset";
    const string TemplateAbility = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities/BasicAttack1.asset";
    const string TemplateProjectile = "Assets/Database/Resources/ItemDatabase/Weapons/Spells/PD_Spellproj.asset";
    const string TemplateItem = "Assets/Database/Resources/ItemDatabase/Weapons/Bladed/Item_SteelKatana.asset";
    const string TemplateMovement = "Assets/CrabSystem/MovementLocomotion/MovementProfile.asset";
    const string FactionPath = "Assets/CrabSystem/IdentityFaction/FactionS/Fac_Nature(Hostile).asset";
    const string MainWeaponSlot = "Assets/CrabSystem/ItemInventoryEquipment/Inventory/Items/Data/EquipmentSlot/Slot_MainWeapon.asset";
    const string AbilityFolder = "Assets/Database/Resources/AbilityDatabase/Monsters";
    const string ItemFolder = "Assets/Database/Resources/ItemDatabase/Weapons/Natural";
    const string GoalFolder = "Assets/Database/AI/Goals";

    const int HitboxLayer = 11;
    const int HitLayers = (1 << 11) | (1 << 12);

    static readonly string[] Roster = { "Slime", "Rabbit", "Bat", "Ghost" };
    static readonly string[] CoreStats = { "core.mind", "core.body", "core.spirit", "core.resilience", "core.endurance", "core.insight" };

    class Goals
    {
        public GOAPGoal wander, chase, lunge, hitAndRun, keepDistance, evade, surround, flee;
    }

    [MenuItem("Tools/AI/Build Monsters")]
    static void BuildAll()
    {
        EnsureFolder(Root);
        EnsureFolder(AbilityFolder);
        EnsureFolder(ItemFolder);
        EnsureFolder(GoalFolder);

        Goals goals = LoadGoals();
        var controllers = new List<AnimatorController>();

        foreach (string name in Roster)
        {
            MonsterSpec spec = LoadSpec(name, goals);
            AnimatorController controller = Build(spec);
            if (controller != null) controllers.Add(controller);
        }

        RegisterForBake(controllers);
        AssetDatabase.SaveAssets();
        MoveFrameBaker.Bake(true);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MonsterBuilder] Built {controllers.Count} monsters in {Root}. Drag them into a scene near the player.");
    }

    static AnimatorController Build(MonsterSpec s)
    {
        if (s.sourceModel == null || s.sourcePrefab == null)
        {
            Debug.LogError($"[MonsterBuilder] {s.name} has no source model or prefab.", s);
            return null;
        }

        EnsureFolder(Folder(s));
        WriteAttackEvents(s);

        AnimatorController controller = BuildController(s);
        GameObject model = BuildModel(s, controller, out Vector3 size);
        RegisterModel(s, model);

        string weaponId = NaturalWeapon(s);
        NPCArchetype archetype = Archetype(s);
        MovementProfile movement = Movement(s);
        BuildVariant(s, weaponId, size, archetype, movement);
        return controller;
    }

    // ── Specs (made once) ────────────────────────────────────────────────

    static MonsterSpec LoadSpec(string name, Goals goals)
    {
        string folder = $"{Root}/{name}";
        EnsureFolder(folder);

        string path = $"{folder}/{name}_Spec.asset";
        var spec = Load<MonsterSpec>(path);
        if (spec != null) return spec;

        spec = ScriptableObject.CreateInstance<MonsterSpec>();
        spec.monsterId = name.ToLowerInvariant();
        spec.displayName = name;
        spec.modelId = spec.monsterId + "_lv1";
        spec.clipPrefix = spec.monsterId + "_";
        spec.attackTrigger = name + "Attack";

        if (name == "Slime") Slime(spec, goals);
        if (name == "Rabbit") Rabbit(spec, goals);
        if (name == "Bat") Bat(spec, goals);
        if (name == "Ghost") Ghost(spec, goals);

        AssetDatabase.CreateAsset(spec, path);
        return spec;
    }

    // Slow, tough, hits hard and shoves. In a group the closest one fights and the rest ring the target.
    static void Slime(MonsterSpec s, Goals g)
    {
        s.sourceModel = Load<GameObject>(PackModels + "Slime_Level_1.fbx");
        s.sourcePrefab = Load<GameObject>(PackPrefabs + "Slime/Slime_Green.prefab");
        s.height = 0.7f;
        s.attackSpeed = 0.8f;
        SetEvents(s, -1f, 0.45f, 0.7f, 0.95f);
        SetHitbox(s, "SlimeRootJoint", "SlimeBody", 0.45f, 0.3f);
        SetBody(s, 45f, 2.4f, 10f);
        SetDice(s, 1, 6, 0);

        AbilityDefinition slam = NewAbility("Slime_Slam", out SerializedObject so);
        if (so != null)
        {
            Melee(so, s, 1.4f, false);
            Knockback(so, 6f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        s.abilities.Add(Role(AIRole.Melee, slam));
        s.goals.AddRange(new[] { g.wander, g.chase, g.surround });
    }

    // Fast and fragile. Circles at leaping distance, leaps in, and runs when it's hurt.
    static void Rabbit(MonsterSpec s, Goals g)
    {
        s.sourceModel = Load<GameObject>(PackModels + "Rabbit_Level_1.fbx");
        s.sourcePrefab = Load<GameObject>(PackPrefabs + "Rabbit/Rabbit_Cyan.prefab");
        s.height = 0.6f;
        SetEvents(s, 0.2f, 0.25f, 0.55f, 0.9f);
        SetHitbox(s, "HeadJoint", "RabbitKick", 0.35f, 0.15f);
        SetBody(s, 20f, 6.5f, 14f);
        SetDice(s, 1, 4, 1);

        AbilityDefinition lunge = NewAbility("Rabbit_Lunge", out SerializedObject so);
        if (so != null)
        {
            Melee(so, s, 4.5f, true);
            Push(so, MovementEffect.MovementType.Impulse, Vector3.forward, 9f, 0.25f, 0f);
            Set(so, "cooldown", 2.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        s.abilities.Add(Role(AIRole.Lunge, lunge));
        s.goals.AddRange(new[] { g.wander, g.lunge, g.flee });
    }

    // A flyer that never stands and trades: swoops through, bites on the way, peels off, comes round.
    static void Bat(MonsterSpec s, Goals g)
    {
        s.sourceModel = Load<GameObject>(PackModels + "Bat_Level_1.fbx");
        s.sourcePrefab = Load<GameObject>(PackPrefabs + "Bat/Bat_Violet.prefab");
        s.height = 0.5f;
        s.lift = 1f;
        SetEvents(s, 0.15f, 0.2f, 0.6f, 0.9f);
        SetHitbox(s, "HeadJoint", "BatBite", 0.35f, 0.2f);
        SetBody(s, 25f, 5.5f, 16f);
        SetDice(s, 1, 4, 0);

        AbilityDefinition swoop = NewAbility("Bat_Swoop", out SerializedObject so);
        if (so != null)
        {
            Melee(so, s, 2.6f, true);
            Push(so, MovementEffect.MovementType.Impulse, Vector3.forward, 8f, 0.3f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        s.abilities.Add(Role(AIRole.Melee, swoop));
        s.goals.AddRange(new[] { g.wander, g.hitAndRun });
    }

    // Keeps its distance and shoots slow spirit bolts — magic, so they go past the armour shield.
    // Get close and it blinks away.
    static void Ghost(MonsterSpec s, Goals g)
    {
        s.sourceModel = Load<GameObject>(PackModels + "Ghost_Lv1.fbx");
        s.sourcePrefab = Load<GameObject>(PackPrefabs + "Ghost/Ghost_White.prefab");
        s.height = 1.2f;
        s.lift = 0.2f;
        SetEvents(s, 0.55f, -1f, -1f, 0.95f);
        SetBody(s, 30f, 3.2f, 18f);

        AbilityDefinition bolt = NewAbility("Ghost_Bolt", out SerializedObject so);
        if (so != null)
        {
            Set(so, "animationTrigger", s.attackTrigger);
            Set(so, "abilityCategory", (int)AbilityCategory.Spell);
            Set(so, "range", 12f);
            Set(so, "castWhileMoving", false);
            Set(so, "effectTrigger", (int)AnimationEventType.Effect1);
            Set(so, "projectileData", SpiritBolt());
            Damage(so, false, 4f, DamageType.Magical);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AbilityDefinition blink = NewAbility("Ghost_Blink", out so);
        if (so != null)
        {
            Set(so, "animationTrigger", "");
            Set(so, "abilityCategory", (int)AbilityCategory.Movement);
            Set(so, "range", 3f);
            Set(so, "castWhileMoving", true);
            Set(so, "effectTrigger", (int)AnimationEventType.PlayEffect);
            Set(so, "waitForAnimUnlock", false);
            Set(so, "maxDuration", 0.5f);
            Set(so, "cooldown", 5f);
            so.FindProperty("damageEffects").arraySize = 0;
            Push(so, MovementEffect.MovementType.Teleport, Vector3.back, 0f, 0f, 5f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        s.abilities.Add(Role(AIRole.Ranged, bolt));
        s.abilities.Add(Role(AIRole.Escape, blink));
        s.goals.AddRange(new[] { g.wander, g.keepDistance, g.evade });
    }

    static void SetEvents(MonsterSpec s, float effect1, float hitboxStart, float hitboxEnd, float unlock)
    {
        s.effect1 = effect1;
        s.hitboxStart = hitboxStart;
        s.hitboxEnd = hitboxEnd;
        s.unlock = unlock;
    }

    static void SetHitbox(MonsterSpec s, string bone, string tag, float radius, float forward)
    {
        s.hitboxBone = bone;
        s.hitboxTag = tag;
        s.hitboxRadius = radius;
        s.hitboxForward = forward;
    }

    static void SetBody(MonsterSpec s, float health, float runSpeed, float aggroRange)
    {
        s.health = health;
        s.runSpeed = runSpeed;
        s.aggroRange = aggroRange;
    }

    static void SetDice(MonsterSpec s, int count, int faces, int flat)
    {
        s.diceCount = count;
        s.diceFaces = faces;
        s.diceFlat = flat;
    }

    static AIRoleAbility Role(AIRole role, AbilityDefinition ability) => new AIRoleAbility { role = role, ability = ability };

    // ── Goals (made once) ────────────────────────────────────────────────

    static Goals LoadGoals()
    {
        var g = new Goals();
        g.wander = Goal<WanderGoal>("Goal_Wander", "Wander", 0.2f, false);
        g.chase = Goal<ChaseMeleeGoal>("Goal_ChaseMelee", "Chase And Melee", 1f, true);
        g.lunge = Goal<LungeGoal>("Goal_Lunge", "Lunge", 1f, true);
        g.hitAndRun = Goal<HitAndRunGoal>("Goal_HitAndRun", "Hit And Run", 1f, true);
        g.keepDistance = Goal<KeepDistanceGoal>("Goal_KeepDistance", "Keep Distance", 1f, true);
        g.evade = Goal<EvadeGoal>("Goal_Evade", "Evade", 5f, true);
        g.surround = Goal<SurroundGoal>("Goal_Surround", "Surround", 2f, true);
        g.flee = Goal<FleeGoal>("Goal_Flee", "Flee", 3f, true);
        return g;
    }

    static GOAPGoal Goal<T>(string file, string goalName, float weight, bool needsTarget) where T : GOAPGoal
    {
        string path = $"{GoalFolder}/{file}.asset";
        var goal = Load<T>(path);
        if (goal != null) return goal;

        goal = ScriptableObject.CreateInstance<T>();
        goal.goalName = goalName;
        goal.baseWeight = weight;
        goal.requiresTarget = needsTarget;
        AssetDatabase.CreateAsset(goal, path);
        return goal;
    }

    // ── Abilities (made once) ────────────────────────────────────────────

    // A copy of BasicAttack1 with its chain, bake and effects cleared. `so` is null when the ability
    // already exists — it's yours then, and the builder leaves it alone.
    static AbilityDefinition NewAbility(string id, out SerializedObject so)
    {
        string path = $"{AbilityFolder}/{id}.asset";
        so = null;

        var existing = Load<AbilityDefinition>(path);
        if (existing != null) return existing;

        var ability = Object.Instantiate(Load<AbilityDefinition>(TemplateAbility));
        ability.name = id;
        AssetDatabase.CreateAsset(ability, path);

        so = new SerializedObject(ability);
        Set(so, "abilityId", id);
        Set(so, "abilityName", id.Replace('_', ' '));
        Set(so, "description", "");
        Set(so, "requiresLineOfSight", false);
        Set(so, "cooldown", 0f);
        Set(so, "bakedClip", (Object)null);
        Set(so, "bakedState", 0);
        Set(so, "frames.startup", 0);
        Set(so, "frames.active", 0);
        Set(so, "frames.recovery", 0);
        Set(so, "frames.cancelFrom", 0);
        Set(so, "frames.cancelTo", 0);
        so.FindProperty("routes").arraySize = 0;
        so.FindProperty("hitboxTags").arraySize = 0;
        so.FindProperty("knockbackEffects").arraySize = 0;
        so.FindProperty("movementEffects").arraySize = 0;
        so.FindProperty("statusEffects").arraySize = 0;
        return ability;
    }

    // A natural-weapon hit: the monster's dice, its hitbox, its attack clip.
    static void Melee(SerializedObject so, MonsterSpec s, float range, bool castWhileMoving)
    {
        Set(so, "animationTrigger", s.attackTrigger);
        Set(so, "abilityCategory", (int)AbilityCategory.Natural);
        Set(so, "range", range);
        Set(so, "castWhileMoving", castWhileMoving);
        Set(so, "effectTrigger", (int)AnimationEventType.Effect1);
        Set(so, "waitForAnimUnlock", true);
        Set(so, "maxDuration", 2.5f);

        SerializedProperty tags = so.FindProperty("hitboxTags");
        tags.arraySize = 1;
        tags.GetArrayElementAtIndex(0).stringValue = s.hitboxTag;

        Damage(so, true, 0f, DamageType.Physical);
    }

    static void Damage(SerializedObject so, bool useWeapon, float baseDamage, DamageType type)
    {
        SerializedProperty list = so.FindProperty("damageEffects");
        list.arraySize = 1;
        SerializedProperty e = list.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("useWeaponDamage").boolValue = useWeapon;
        e.FindPropertyRelative("baseDamage").floatValue = baseDamage;
        e.FindPropertyRelative("weaponSlotId").stringValue = "mainwep";
        e.FindPropertyRelative("damageType").intValue = (int)type;
        e.FindPropertyRelative("baseDamageMultiplier").floatValue = 1f;
        e.FindPropertyRelative("finalDamageMultiplier").floatValue = 1f;
    }

    static void Knockback(SerializedObject so, float force)
    {
        SerializedProperty list = so.FindProperty("knockbackEffects");
        list.arraySize = 1;
        SerializedProperty e = list.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("force").floatValue = force;
        e.FindPropertyRelative("direction").vector3Value = Vector3.forward;
        e.FindPropertyRelative("useRelativeDirection").boolValue = true;
    }

    // One movement effect along the caster's facing. Speed is the impulse, holiday the seconds of
    // no ground friction after it, distance only for a teleport.
    static void Push(SerializedObject so, MovementEffect.MovementType type, Vector3 direction, float speed, float holiday, float distance)
    {
        SerializedProperty list = so.FindProperty("movementEffects");
        list.arraySize = 1;
        SerializedProperty e = list.GetArrayElementAtIndex(0);
        e.FindPropertyRelative("movementType").intValue = (int)type;
        e.FindPropertyRelative("directionSource").intValue = (int)MovementDirectionSource.CasterFacing;
        e.FindPropertyRelative("direction").vector3Value = direction;
        e.FindPropertyRelative("speed").floatValue = speed;
        e.FindPropertyRelative("duration").floatValue = 0.2f;
        e.FindPropertyRelative("speedCap").floatValue = speed;
        e.FindPropertyRelative("frictionHoliday").floatValue = holiday;
        e.FindPropertyRelative("teleportDistance").floatValue = distance;
    }

    // Slow, gently homing, aimed at the perceived target.
    static ProjectileData SpiritBolt()
    {
        string path = $"{Root}/Ghost/PD_SpiritBolt.asset";
        var existing = Load<ProjectileData>(path);
        if (existing != null) return existing;

        var data = Object.Instantiate(Load<ProjectileData>(TemplateProjectile));
        data.name = "PD_SpiritBolt";
        AssetDatabase.CreateAsset(data, path);

        var so = new SerializedObject(data);
        Set(so, "speed", 9f);
        Set(so, "lifetime", 4f);
        Set(so, "homingStrength", 0.04f);
        Set(so, "aim.directionMode", (int)AimDirectionMode.TowardTarget);
        Set(so, "aim.targetSources", (int)TargetSource.LockedTarget);
        so.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    // ── Animation ────────────────────────────────────────────────────────

    // The attack clip's events live in the FBX's import settings.
    static void WriteAttackEvents(MonsterSpec s)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.sourceModel)) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;

        string attack = s.clipPrefix + "attack";
        foreach (ModelImporterClipAnimation clip in clips)
            if (clip.name == attack) clip.events = AttackEvents(s);

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationEvent[] AttackEvents(MonsterSpec s)
    {
        var list = new List<AnimationEvent>();
        AddEvent(list, "OnEffect1", s.effect1);
        AddEvent(list, "OnHitboxStart", s.hitboxStart);
        AddEvent(list, "OnHitboxEnd", s.hitboxEnd);
        AddEvent(list, "OnAnimUnlocked", s.unlock);
        list.Sort((a, b) => a.time.CompareTo(b.time));
        return list.ToArray();
    }

    static void AddEvent(List<AnimationEvent> list, string function, float time)
    {
        if (time < 0f) return;
        list.Add(new AnimationEvent { functionName = function, time = time });
    }

    static AnimationClip Clip(MonsterSpec s, string suffix)
    {
        string name = s.clipPrefix + suffix;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(s.sourceModel)))
            if (asset is AnimationClip clip && clip.name == name) return clip;
        return null;
    }

    // Idle / Move on speed; Attack, Hit and Die from Any State. The humanoid controller's parameters
    // and layer names are copied (layers empty, resting) so the systems Base_PC carries find what
    // they look for and stay quiet.
    static AnimatorController BuildController(MonsterSpec s)
    {
        string path = $"{Folder(s)}/{s.displayName}.controller";
        var controller = Load<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        ClearController(controller);
        CopyHumanoidParameters(controller);
        EnsureParameter(controller, "MovementSpeed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "HitLight", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "HitHeavy", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Stagger", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, s.attackTrigger, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = State(sm, "Idle", Clip(s, "idle"));
        AnimatorState move = State(sm, "Move", Clip(s, "move"));
        sm.defaultState = idle;
        Link(idle, move).AddCondition(AnimatorConditionMode.Greater, 0.3f, "MovementSpeed");
        Link(move, idle).AddCondition(AnimatorConditionMode.Less, 0.2f, "MovementSpeed");

        AnimatorState attack = State(sm, s.attackTrigger, Clip(s, "attack"));
        attack.speed = s.attackSpeed;
        FromAny(sm, attack, s.attackTrigger, false);
        ExitTo(attack, idle);

        AnimationClip damage = Clip(s, "damage");
        if (damage != null)
        {
            AnimatorState hit = State(sm, "Hit", damage);
            FromAny(sm, hit, "HitLight", true);
            FromAny(sm, hit, "HitHeavy", true);
            FromAny(sm, hit, "Stagger", true);
            ExitTo(hit, idle);
        }

        AnimatorState die = State(sm, "Die", Clip(s, "die"));
        AnimatorStateTransition toDie = sm.AddAnyStateTransition(die);
        toDie.AddCondition(AnimatorConditionMode.If, 0f, "Death");
        toDie.duration = 0.1f;
        toDie.canTransitionToSelf = false;

        AddRestLayers(controller);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void ClearController(AnimatorController controller)
    {
        while (controller.layers.Length > 0) controller.RemoveLayer(0);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddLayer("Base Layer");
    }

    static void CopyHumanoidParameters(AnimatorController controller)
    {
        var humanoid = Load<AnimatorController>(HumanoidController);
        if (humanoid == null) return;

        foreach (AnimatorControllerParameter p in humanoid.parameters)
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = p.name,
                type = p.type,
                defaultBool = p.defaultBool,
                defaultFloat = p.defaultFloat,
                defaultInt = p.defaultInt,
            });
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    static void AddRestLayers(AnimatorController controller)
    {
        var humanoid = Load<AnimatorController>(HumanoidController);
        if (humanoid == null) return;

        foreach (AnimatorControllerLayer source in humanoid.layers)
        {
            controller.AddLayer(source.name);
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.defaultWeight = 0f;
            AnimatorState rest = layer.stateMachine.AddState("Rest");
            rest.tag = AnimationLayerController.RestTag;
            controller.layers = layers;
        }
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        AnimatorState state = sm.AddState(name);
        state.motion = clip;
        return state;
    }

    static AnimatorStateTransition Link(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        return t;
    }

    static void FromAny(AnimatorStateMachine sm, AnimatorState to, string trigger, bool canRestart)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = canRestart;
    }

    static void ExitTo(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.1f;
    }

    static void RegisterForBake(List<AnimatorController> controllers)
    {
        var settings = Load<MoveBakeSettings>(BakeSettingsPath);
        if (settings == null) return;

        foreach (AnimatorController controller in controllers)
            if (!settings.controllers.Contains(controller)) settings.controllers.Add(controller);

        EditorUtility.SetDirty(settings);
    }

    // ── Model ────────────────────────────────────────────────────────────

    // An empty root (what ModelModule swaps) holding the pack's model, scaled to the spec's height,
    // feet on the ground plus any lift. The Animator, its event forwarder and the hitbox sit on the
    // pack model. `size` is the model's world size after scaling.
    static GameObject BuildModel(MonsterSpec s, AnimatorController controller, out Vector3 size)
    {
        var root = new GameObject(s.displayName + "_Model");
        root.AddComponent<ModelSocketProvider>();

        var visual = (GameObject)Object.Instantiate(s.sourcePrefab, root.transform);
        visual.name = "Visual";
        visual.transform.localRotation = Quaternion.Euler(0f, s.yaw, 0f) * visual.transform.localRotation;

        float measured = Measure(visual).size.y;
        float scale = measured > 0.001f ? s.height / measured : 1f;
        visual.transform.localScale *= scale;

        Bounds bounds = Measure(visual);
        visual.transform.localPosition += new Vector3(0f, s.lift - bounds.min.y, 0f);
        size = bounds.size;

        Animator animator = visual.GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.gameObject.AddComponent<AnimationEventForwarder>();

        AddHitbox(s, visual);

        string path = $"{Folder(s)}/{s.displayName}_Model.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"[MonsterBuilder] {s.displayName}: model measured {measured:F2} m tall, scaled ×{scale:F3} to {size.y:F2} m, footprint {size.x:F2} × {size.z:F2} m.");
        return prefab;
    }

    static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static void AddHitbox(MonsterSpec s, GameObject visual)
    {
        if (string.IsNullOrEmpty(s.hitboxBone)) return;

        Transform bone = FindDeep(visual.transform, s.hitboxBone);
        if (bone == null)
        {
            Debug.LogWarning($"[MonsterBuilder] {s.displayName}: no bone '{s.hitboxBone}'; hitbox goes on the model root.");
            bone = visual.transform;
        }

        var go = new GameObject("Hitbox_" + s.hitboxTag);
        go.layer = HitboxLayer;
        go.transform.SetParent(bone, false);
        go.transform.position = bone.position + Vector3.forward * s.hitboxForward;
        go.transform.rotation = Quaternion.identity;

        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = s.hitboxRadius / Mathf.Max(0.0001f, go.transform.lossyScale.x);

        var hitbox = go.AddComponent<WeaponHitbox>();
        var so = new SerializedObject(hitbox);
        Set(so, "weaponName", s.displayName);
        Set(so, "hitboxTag", s.hitboxTag);
        Set(so, "hitboxCollider", sphere);
        Set(so, "hitLayers", HitLayers);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static void RegisterModel(MonsterSpec s, GameObject prefab)
    {
        var db = Load<ModelDatabase>(ModelDatabasePath);
        var so = new SerializedObject(db);
        SerializedProperty models = so.FindProperty("models");

        SerializedProperty entry = null;
        for (int i = 0; i < models.arraySize; i++)
            if (models.GetArrayElementAtIndex(i).FindPropertyRelative("modelId").stringValue == s.modelId)
                entry = models.GetArrayElementAtIndex(i);

        if (entry == null)
        {
            models.arraySize++;
            entry = models.GetArrayElementAtIndex(models.arraySize - 1);
        }

        entry.FindPropertyRelative("modelId").stringValue = s.modelId;
        entry.FindPropertyRelative("displayName").stringValue = s.displayName;
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("allowColorCustomization").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Data from the spec (rewritten every build) ───────────────────────

    // The monster's natural weapon: an item in mainwep whose dice its Natural attacks roll.
    static string NaturalWeapon(MonsterSpec s)
    {
        if (s.diceCount <= 0) return null;

        string dicePath = $"{ItemFolder}/Dice_{s.displayName}.asset";
        var dice = Load<DiceProfile>(dicePath);
        if (dice == null)
        {
            dice = ScriptableObject.CreateInstance<DiceProfile>();
            AssetDatabase.CreateAsset(dice, dicePath);
        }

        var diceSo = new SerializedObject(dice);
        Set(diceSo, "weaponName", s.displayName);
        Set(diceSo, "damageDice.diceCount", s.diceCount);
        Set(diceSo, "damageDice.diceFaces", s.diceFaces);
        Set(diceSo, "flatBonus", s.diceFlat);
        Set(diceSo, "isNaturalWeapon", true);
        diceSo.ApplyModifiedPropertiesWithoutUndo();

        string itemId = s.monsterId + "_natural";
        string itemPath = $"{ItemFolder}/Item_{s.displayName}Natural.asset";
        var item = Load<ItemDefinition>(itemPath);
        if (item == null)
        {
            item = Object.Instantiate(Load<ItemDefinition>(TemplateItem));
            item.name = $"Item_{s.displayName}Natural";
            AssetDatabase.CreateAsset(item, itemPath);
        }

        var itemSo = new SerializedObject(item);
        Set(itemSo, "itemId", itemId);
        Set(itemSo, "displayName", s.displayName + " (natural)");
        Set(itemSo, "description", "A monster's own body. Never drops.");
        Set(itemSo, "equippedPrefab", (Object)null);
        Set(itemSo, "moveset", (Object)null);
        Set(itemSo, "weaponData", dice);
        Set(itemSo, "baseValue", 0);
        Set(itemSo, "dropsOnDeath", false);
        Set(itemSo, "isTradeable", false);
        itemSo.FindProperty("tags").arraySize = 0;
        itemSo.ApplyModifiedPropertiesWithoutUndo();
        return itemId;
    }

    static NPCArchetype Archetype(MonsterSpec s)
    {
        string path = $"{Folder(s)}/{s.displayName}-Archetype.asset";
        var archetype = Load<NPCArchetype>(path);
        if (archetype == null)
        {
            archetype = ScriptableObject.CreateInstance<NPCArchetype>();
            AssetDatabase.CreateAsset(archetype, path);
        }

        archetype.archetypeId = s.monsterId;
        archetype.archetypeName = s.displayName;
        archetype.faction = Load<FactionDefinition>(FactionPath);
        archetype.baseLevel = 1;
        archetype.modelPool = new List<string> { s.modelId };
        archetype.randomizeModel = false;
        archetype.useGenericName = false;
        archetype.aiSystemType = AISystemType.GOAP;
        archetype.goapGoals = new List<GOAPGoal>(s.goals);
        archetype.goalSelectionMode = GoalSelectionMode.HighestWeight;
        archetype.visionRange = s.aggroRange;
        archetype.visionAngle = 360f;
        archetype.requireLineOfSight = false;

        var stats = new List<StatBaseOverride>();
        foreach (string stat in CoreStats) stats.Add(new StatBaseOverride { statId = stat, baseValue = 10f });
        stats.Add(new StatBaseOverride { statId = "character.max_health", baseValue = s.health });
        archetype.baseStatOverrides = stats.ToArray();

        EditorUtility.SetDirty(archetype);
        return archetype;
    }

    static MovementProfile Movement(MonsterSpec s)
    {
        string path = $"{Folder(s)}/{s.displayName}_Movement.asset";
        var profile = Load<MovementProfile>(path);
        if (profile == null)
        {
            profile = Object.Instantiate(Load<MovementProfile>(TemplateMovement));
            profile.name = $"{s.displayName}_Movement";
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.runSpeed = s.runSpeed;
        profile.walkSpeed = s.runSpeed * 0.5f;
        profile.sprintSpeed = s.runSpeed * 1.3f;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ── The monster ──────────────────────────────────────────────────────

    static void BuildVariant(MonsterSpec s, string weaponId, Vector3 size, NPCArchetype archetype, MovementProfile movement)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BasePath));
        root.name = s.displayName;

        var brain = root.GetComponentInChildren<ControllerBrain>(true);
        Write(brain, "entityType", p => p.intValue = (int)EntityType.NPC);

        DisablePlayerView(root);
        DisableHumanoidFeel(root);
        SizeBody(root, size, s.lift);
        Equip(root, weaponId);

        var abilities = new SerializedObject(root.GetComponentInChildren<AbilitySystem>(true));
        SerializedProperty list = abilities.FindProperty("abilities");
        list.arraySize = s.abilities.Count;
        for (int i = 0; i < s.abilities.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = s.abilities[i].ability;
        abilities.ApplyModifiedPropertiesWithoutUndo();

        Write(root.GetComponentInChildren<ParkourLocomotionHandler>(true), "profile", p => p.objectReferenceValue = movement);

        AddAI(brain, s);

        var config = root.AddComponent<PersistentNPCConfigurator>();
        Write(config, "entityId", p => p.stringValue = s.monsterId + "_01");
        Write(config, "archetype", p => p.objectReferenceValue = archetype);
        Write(config, "brain", p => p.objectReferenceValue = brain);
        Write(config, "level", p => p.intValue = 1);

        string path = $"{Folder(s)}/{s.displayName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void AddAI(ControllerBrain brain, MonsterSpec s)
    {
        var ai = new GameObject("AI_System");
        ai.transform.SetParent(brain.transform, false);

        var control = ai.AddComponent<AIControlSource>();
        var controlSo = new SerializedObject(control);
        SerializedProperty roles = controlSo.FindProperty("roleAbilities");
        roles.arraySize = s.abilities.Count;
        for (int i = 0; i < s.abilities.Count; i++)
        {
            SerializedProperty e = roles.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("role").intValue = (int)s.abilities[i].role;
            e.FindPropertyRelative("ability").objectReferenceValue = s.abilities[i].ability;
        }
        controlSo.ApplyModifiedPropertiesWithoutUndo();

        var perception = ai.AddComponent<PerceptionModule>();
        Write(perception, "requireLineOfSight", p => p.boolValue = false);
        Write(perception, "visionAngle", p => p.floatValue = 360f);
        Write(perception, "visionRange", p => p.floatValue = s.aggroRange);

        var goap = ai.AddComponent<GOAPModule>();
        goap.goalPool.AddRange(s.goals);
        goap.SelectionMode = GoalSelectionMode.HighestWeight;
    }

    // Body capsule from the ground to the top of the model; the hurtbox only around the model, so a
    // flyer is hit where it is, not underneath.
    static void SizeBody(GameObject root, Vector3 size, float lift)
    {
        float radius = Mathf.Clamp(Mathf.Max(size.x, size.z) * 0.35f, 0.15f, 0.6f);
        float height = Mathf.Max(size.y + lift, radius * 2f + 0.05f);
        Vector3 center = new Vector3(0f, height * 0.5f, 0f);

        var body = root.GetComponent<CapsuleCollider>();
        body.radius = radius;
        body.height = height;
        body.center = center;

        var controller = root.GetComponent<CharacterController>();
        controller.radius = radius;
        controller.height = height;
        controller.center = center;
        controller.stepOffset = Mathf.Min(0.3f, height * 0.3f);

        var damage = root.GetComponentInChildren<DamageSystem>(true);
        var hurtbox = damage.GetComponent<CapsuleCollider>();
        hurtbox.radius = radius + 0.1f;
        hurtbox.height = Mathf.Max(size.y + 0.2f, hurtbox.radius * 2f);
        hurtbox.center = new Vector3(0f, lift + size.y * 0.5f, 0f);
    }

    // Base_PC starts with a katana in mainwep. A monster starts with its own body.
    static void Equip(GameObject root, string weaponId)
    {
        var so = new SerializedObject(root.GetComponentInChildren<EquipmentSystem>(true));
        so.FindProperty("equippedItems").arraySize = 0;
        Set(so, "hasNaturalWeapon", weaponId != null);
        Set(so, "naturalWeaponItemId", weaponId ?? "");
        Set(so, "naturalWeaponSlot", Load<EquipmentSlotDefinition>(MainWeaponSlot));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The player's camera rig, audio listener and post-process volumes come along with Base_PC.
    static void DisablePlayerView(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            cam.gameObject.SetActive(false);

        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.gameObject.SetActive(false);

        foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            volume.gameObject.SetActive(false);
    }

    // Foot IK, head look, lean and footsteps read humanoid bones. Off on a generic rig.
    static void DisableHumanoidFeel(GameObject root)
    {
        Disable<FootIKSystem>(root);
        Disable<FootIKDebugger>(root);
        Disable<HeadLookSystem>(root);
        Disable<LandingImpactSystem>(root);
        Disable<BodyLeanSystem>(root);
        Disable<FootstepEmitter>(root);
        Disable<StepJuice>(root);
    }

    static void Disable<T>(GameObject root) where T : Behaviour
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            component.enabled = false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    static string Folder(MonsterSpec s) => $"{Root}/{s.displayName}";

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void Write(Object target, string field, System.Action<SerializedProperty> write)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = Find(so, field);
        if (prop == null) return;
        write(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static SerializedProperty Find(SerializedObject so, string path)
    {
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null) Debug.LogWarning($"[MonsterBuilder] {so.targetObject.GetType().Name} has no field '{path}'");
        return prop;
    }

    // Numbers go in whether the field is a float, an int or an enum.
    static void Set(SerializedObject so, string path, float value)
    {
        SerializedProperty p = Find(so, path);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        else p.intValue = Mathf.RoundToInt(value);
    }

    static void Set(SerializedObject so, string path, int value) => Set(so, path, (float)value);

    static void Set(SerializedObject so, string path, bool value)
    {
        SerializedProperty p = Find(so, path);
        if (p != null) p.boolValue = value;
    }

    static void Set(SerializedObject so, string path, string value)
    {
        SerializedProperty p = Find(so, path);
        if (p != null) p.stringValue = value;
    }

    static void Set(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = Find(so, path);
        if (p != null) p.objectReferenceValue = value;
    }
}
