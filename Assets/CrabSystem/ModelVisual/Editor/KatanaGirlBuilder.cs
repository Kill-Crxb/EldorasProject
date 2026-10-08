using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

// Tools → Characters → Build Katana Girl
//
// Puts the CombatGirls Katana Girl in as a playable model (Katana_Girl_Build.md). The pack lives in
// Assets/CombatGirlsCharacterPack, local only (git ignores it). From it this makes, in
// Database/Characters/KatanaGirl: FlatToon materials, the model prefab (sockets, socket relay,
// appearance groups) and her locomotion override controller; and the Blade A / B items. It writes the
// clip events into her FBX importers, puts her attacks and reactions into HumanoidAnimator_v2,
// registers the model as playable, dresses the Mirror in her with Blade A, gives the other humanoid
// models a socket relay so they can play her clips, adds a Nature Spirit option that hands over both
// blades, and makes the creation panel's row prefab if it is missing.
//
// Rerunnable. Generated assets are rewritten; the items, the row prefab and attack clips whose events
// you have tuned (any clip already carrying OnStrike) are left alone. Each step logs one line.
//
// Her look comes from CombatGirlsWardrobe, which also grafts the Shield Girl's outfits and hair onto her
// (Shield_Girl_Build.md). The internal helpers here are shared with ShieldGirlBuilder.
public static class KatanaGirlBuilder
{
    const string Pack = "Assets/CombatGirlsCharacterPack/Katana_Girl";
    const string SourcePrefab = Pack + "/Prefab/KatanaGirl_FullBody.prefab";
    const string WeaponPrefab = Pack + "/Prefab/Prefab_Parts/Weapon_Katana.prefab";
    const string PackMaterials = Pack + "/Materials";
    const string ClipFolder = Pack + "/Animations";

    const string Out = "Assets/Database/Characters/KatanaGirl";
    const string MaterialsOut = Out + "/Materials";
    const string ModelPath = Out + "/KatanaGirl_Model.prefab";
    const string OverridesPath = Out + "/KatanaGirl_Overrides.overrideController";
    const string ItemsOut = "Assets/Database/Resources/ItemDatabase/Weapons/Bladed";
    const string SteelKatanaPath = ItemsOut + "/Item_SteelKatana.asset";
    const string RowPrefabPath = "Assets/CrabSystem/Menu/AppearanceRow.prefab";

    const string ControllerPath = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string ModelDatabasePath = "Assets/Database/Characters/EldoraModelDatabase.asset";
    const string PorphiPath = "Assets/Database/Characters/MC/Porphi.prefab";
    const string SkinTemplatePath = "Assets/Database/Characters/PC-Messy-Porphi/PorphiRigtest/CharacterSkinBase 10.mat";
    const string ClothTemplatePath = "Assets/Database/Characters/PC-Messy-Porphi/PorphiRigtest/training_dummy 10.mat";
    const string MirrorArchetypePath = "Assets/Database/Characters/Mirror/Mirror-Archetype.asset";
    const string MirrorPrefabPath = "Assets/Database/Characters/Mirror/Mirror_NPC.prefab";

    const string ModelId = "katana_girl_v1";
    const string ModelName = "Katana Girl";
    const string FemaleBaseId = "female_base_v1";
    const string MainWeaponSlot = "mainwep";
    const string OffWeaponSlot = "offwep";
    const string DashClip = "K_QuickShift_F";


    // Dodge.anim's effect events, normalised: the dash abilities move on Effect1.
    const float DashCueTime = 0.125f;

    // The relay's socket events at time 0 must land after OpenSockets in the same frame.
    const float FirstSocketTime = 0.002f;

    static readonly string[] PackScripts =
    {
        "Character_Weapon_Controller", "Dummy_Event", "Animator_State_Shortcut", "SDFFaceShadowController"
    };

    static readonly string[] SocketEvents = { "SwitchSocket", "ResetSockets", "OpenSockets" };

    // First guesses, normalised clip time, read from where the clips move the sword. Tune them by eye
    // in the importer; the builder won't overwrite a clip that already has OnStrike. The values ride
    // with the events: Strike(0) is the move's first strike entry, Cue(1) its effectCue.
    static readonly string[] AttackEventNames = { "OnTell", "OnStrike", "OnCue", "OnUnlocked" };
    static readonly int[] AttackEventValues = { 0, 0, 1, 0 };

    static readonly Dictionary<string, float[]> AttackEventTimes = new Dictionary<string, float[]>
    {
        ["K_Attack_1"] = new[] { 0.05f, 0.15f, 0.16f, 0.49f },
        ["K_Attack_2"] = new[] { 0.03f, 0.12f, 0.13f, 0.47f },
        ["K_Attack_3"] = new[] { 0.05f, 0.15f, 0.16f, 0.58f },
    };

    enum ClipKind { Attack, Action, Reset, Strip }

    [MenuItem("Tools/Characters/Build Katana Girl")]
    static void BuildAll()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab) == null)
        {
            Debug.LogError($"[KatanaGirlBuilder] {SourcePrefab} isn't in the project. The CombatGirls pack is local only; import it first.");
            return;
        }

        Dictionary<Material, Material> toon = BuildKatanaMaterials();
        Dictionary<Material, Material> shieldToon = ShieldGirlBuilder.BuildShieldMaterials();
        WriteClipEvents();
        AnimatorController controller = SwapSharedClips();
        AnimatorOverrideController overrides = BuildKatanaOverrides(controller);
        GameObject model = BuildModel(toon, shieldToon, overrides);
        ItemDefinition bladeA = BuildBlade("A", toon);
        ItemDefinition bladeB = BuildBlade("B", toon);
        List<ItemDefinition> outfits = CombatGirlsWardrobe.BuildOutfitItems(CombatGirlsWardrobe.Katana);
        RegisterModel(model, ModelId, ModelName);
        DressMirror(bladeA);
        AddRelayToHumanoids(controller);
        StockVendor(new List<ItemDefinition> { bladeA, bladeB }.Concat(outfits).ToList(), "Iai Katana A and B and her armour pieces");
        BuildRowPrefab();

        AssetDatabase.SaveAssets();
        Log("Done. Next: Tools → Combat → Bake Move Frames, wire the creation panel, then the play checks in Katana_Girl_Build.md §4.");
    }

    // ── Materials ─────────────────────────────────────────────────────────

    internal static Dictionary<Material, Material> BuildKatanaMaterials() => BuildMaterials(PackMaterials, MaterialsOut, "KG");

    // One FlatToon material per pack material, saved as <prefix>_<name> in outFolder.
    internal static Dictionary<Material, Material> BuildMaterials(string packMaterials, string outFolder, string prefix)
    {
        EnsureFolder(outFolder);
        var skin = AssetDatabase.LoadAssetAtPath<Material>(SkinTemplatePath);
        var cloth = AssetDatabase.LoadAssetAtPath<Material>(ClothTemplatePath);
        var map = new Dictionary<Material, Material>();

        if (skin == null || cloth == null)
        {
            Debug.LogError("[KatanaGirlBuilder] FlatToon template materials missing; check SkinTemplatePath / ClothTemplatePath.");
            return map;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { packMaterials }))
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string path = $"{outFolder}/{prefix}_{source.name.Replace(' ', '_')}.mat";
            map[source] = BuildMaterial(source, IsSkin(source.name) ? skin : cloth, path);
        }

        Log($"materials: {map.Count} FlatToon materials in {outFolder}.");
        return map;
    }

    static bool IsSkin(string name)
    {
        return name.StartsWith("Body") || name.StartsWith("Face") || name.StartsWith("Eye");
    }

    static Material BuildMaterial(Material source, Material template, string path)
    {
        var texture = source.GetTexture("_MainTex") as Texture2D;
        SetBC7(texture);

        var material = new Material(template);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_IsFace", source.name.StartsWith("Face") ? 1f : 0f);

        // Eyes and the glasses' alpha card are small overlays: an outline would ring them.
        if (source.name.StartsWith("Eye") || source.name.Contains("Alpha"))
            material.SetFloat("_OutlineWidth", 0f);

        if (source.name.Contains("Alpha"))
        {
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
        }

        return SaveMaterial(material, path);
    }

    static Material SaveMaterial(Material material, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        EditorUtility.CopySerialized(material, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // BC1 ruins hard-edged line art; cel-shaded textures use BC7.
    static void SetBC7(Texture2D texture)
    {
        if (texture == null) return;

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        if (importer == null) return;

        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
        if (settings.overridden && settings.format == TextureImporterFormat.BC7) return;

        settings.overridden = true;
        settings.format = TextureImporterFormat.BC7;
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // ── Clip events ───────────────────────────────────────────────────────

    static void WriteClipEvents()
    {
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ClipFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            // Extracted clips carry their own events now (Tools → Characters → Extract CombatGirls Clips).
            if (CombatGirlsClips.ExtractedFor(path) != null) continue;

            ClipKind kind = KindOf(Path.GetFileNameWithoutExtension(path));
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.events = EventsFor(Path.GetFileNameWithoutExtension(path), kind, clip.events);
                BakeRootIntoPose(clip);
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            count++;
        }

        Log($"clip events: {count} FBX clips rewritten, extracted ones skipped (attacks get combat events; reactions and dodges reset the sockets; locomotion and emotes carry none).");
    }

    // Root motion is off, so an unbaked root curve is thrown away. Rotation and height are baked into
    // the pose so a clip can't turn her off her facing and the death fall reaches the floor. Ground
    // motion stays unbaked: moves play in place and the motor owns travel.
    static void BakeRootIntoPose(ModelImporterClipAnimation clip)
    {
        clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        clip.lockRootPositionXZ = false;
    }

    static ClipKind KindOf(string file)
    {
        if (AttackEventTimes.ContainsKey(file)) return ClipKind.Attack;
        if (file.StartsWith("K_Sp_Skill") || file == "K_Take" || file == "K_Put") return ClipKind.Action;
        if (file.StartsWith("K_Hit") || file.StartsWith("K_QuickShift")) return ClipKind.Reset;
        if (file == "K_Stun" || file == "K_Die" || file == "K_Evade") return ClipKind.Reset;
        return ClipKind.Strip;
    }

    // Locomotion clips loop under every attack, so their socket events would yank the blade back
    // mid-swing: they lose them. Reactions and dodges interrupt moves, so they reset to the rest pose.
    static AnimationEvent[] EventsFor(string file, ClipKind kind, AnimationEvent[] existing)
    {
        var events = existing.Where(e => !SocketEvents.Contains(e.functionName)).ToList();

        if (kind == ClipKind.Reset)
            events.Add(NewEvent("ResetSockets", 0f));

        if (kind == ClipKind.Action || kind == ClipKind.Attack)
        {
            events.Add(NewEvent("OpenSockets", 0f));
            events.AddRange(existing.Where(e => e.functionName == "SwitchSocket").Select(ShiftedSocketEvent));
        }

        bool tuned = events.Any(e => e.functionName == "OnStrike");
        if (kind == ClipKind.Attack && !tuned)
            AddAttackEvents(events, AttackEventTimes[file]);

        bool dashTuned = events.Any(e => e.functionName == "OnCue");
        if (file == DashClip && !dashTuned)
            AddDashEvents(events);

        return events.OrderBy(e => e.time).ToArray();
    }

    static AnimationEvent ShiftedSocketEvent(AnimationEvent source)
    {
        return new AnimationEvent
        {
            functionName = source.functionName,
            stringParameter = source.stringParameter,
            time = Mathf.Max(source.time, FirstSocketTime)
        };
    }

    static void AddAttackEvents(List<AnimationEvent> events, float[] times)
    {
        for (int i = 0; i < AttackEventNames.Length; i++)
            events.Add(NewEvent(AttackEventNames[i], times[i], AttackEventValues[i]));
    }

    static void AddDashEvents(List<AnimationEvent> events)
    {
        events.Add(NewEvent("OnCue", DashCueTime, 1));
    }

    static AnimationEvent NewEvent(string function, float time, int value = 0)
    {
        return new AnimationEvent { functionName = function, time = time, intParameter = value };
    }

    // The editable copy when the clips have been extracted, else the FBX's own clip.
    static AnimationClip LoadClip(string file) => LoadPackClip(ClipFolder, file);

    internal static AnimationClip LoadPackClip(string clipFolder, string file)
    {
        string path = $"{clipFolder}/{file}.fbx";
        AnimationClip extracted = CombatGirlsClips.ExtractedFor(path);
        if (extracted != null) return extracted;

        AnimationClip clip = CombatGirlsClips.FromFbx(path);
        if (clip == null) Debug.LogError($"[KatanaGirlBuilder] No clip in {path}.");
        return clip;
    }

    // ── Controllers ───────────────────────────────────────────────────────

    // D1: moves are shared, so her attacks and reactions go into the one controller every humanoid
    // plays, and each move's frames are baked from one clip.
    static AnimatorController SwapSharedClips()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        SetStateMotion(controller, "Actions", "BasicAttack1", LoadClip("Normal/K_Attack_1"));
        SetStateMotion(controller, "Actions", "BasicAttack2", LoadClip("Normal/K_Attack_2"));
        SetStateMotion(controller, "Actions", "BasicAttack3", LoadClip("Normal/K_Attack_3"));
        SetStateMotion(controller, "Reactions", "HitLight", LoadClip("Normal/K_Hit_R"));
        SetStateMotion(controller, "Reactions", "HitHeavy", LoadClip("Normal/K_Hit_L"));
        SetStateMotion(controller, "Reactions", "Stagger", LoadClip("Normal/K_Stun"));
        SetStateMotion(controller, "Reactions", "Death", LoadClip("Normal/K_Die"));
        SetStateMotion(controller, "Movement Actions", "Dash", LoadClip("Special/" + DashClip));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void SetStateMotion(AnimatorController controller, string layerName, string stateName, AnimationClip clip)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        int layerIndex = System.Array.FindIndex(layers, l => l.name == layerName);
        AnimatorState state = layerIndex < 0 ? null : FindState(layers[layerIndex].stateMachine, stateName);

        if (state == null || clip == null)
        {
            Debug.LogError($"[KatanaGirlBuilder] {layerName}/{stateName} not found in {controller.name}, or its clip is missing.");
            return;
        }

        Log($"controller: {layerName}/{stateName} {(state.motion != null ? state.motion.name : "none")} → {clip.name} ({clip.length * clip.frameRate:F0} f at {clip.frameRate} fps).");
        state.motion = clip;

        // A synced layer (Actions Upper) keeps its own copy of each state's motion.
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].syncedLayerIndex == layerIndex) layers[i].SetOverrideMotion(state, clip);

        controller.layers = layers;
    }

    static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state.name == name) return child.state;

        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
        {
            AnimatorState found = FindState(sub.stateMachine, name);
            if (found != null) return found;
        }

        return null;
    }

    // Looks are per model: her idle, walk, run and stance run (as the sprint) over the shared controller,
    // in both the free and the strafe blends (camera-driven facing plays the strafe one). Her clips are
    // forward only, so the sideways and backward strafe walks stay on the shared clips.
    static AnimatorOverrideController BuildKatanaOverrides(AnimatorController controller)
    {
        var swaps = new Dictionary<string, AnimationClip>
        {
            ["M_Walk_Forward"] = LoadClip("Normal/K_Walk"),
            ["M_Run_Forward"] = LoadClip("Normal/K_Run"),
            ["M_Sprint_Forward"] = LoadClip("Special/K_Sp_Run"),
            ["HumanF@Walk01_Forward"] = LoadClip("Normal/K_Walk"),
            ["SprintForward"] = LoadClip("Normal/K_Run"),
        };

        return BuildOverrides(controller, OverridesPath, LoadClip("Normal/K_Idle"), swaps);
    }

    // Idles are found by position (each locomotion blend tree's first motion), so swapping a different
    // idle into the shared controller doesn't leave the model playing it. The rest go by clip name.
    internal static AnimatorOverrideController BuildOverrides(AnimatorController controller, string overridesPath,
                                                             AnimationClip idle, Dictionary<string, AnimationClip> swaps)
    {
        HashSet<AnimationClip> idles = LocomotionIdles(controller);

        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridesPath);
        if (overrides == null)
        {
            overrides = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(overrides, overridesPath);
        }

        overrides.runtimeAnimatorController = controller;

        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrides.GetOverrides(pairs);

        int found = 0;
        for (int i = 0; i < pairs.Count; i++)
        {
            AnimationClip original = pairs[i].Key;
            if (original == null) continue;

            AnimationClip replacement = idles.Contains(original) ? idle : null;
            if (replacement == null) swaps.TryGetValue(original.name, out replacement);

            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            if (replacement != null) found++;
        }

        overrides.ApplyOverrides(pairs);
        EditorUtility.SetDirty(overrides);

        int expected = swaps.Count + idles.Count;
        if (found < expected)
            Debug.LogError($"[KatanaGirlBuilder] Only {found} of {expected} locomotion clips found to override in {controller.name}.");

        Log($"overrides: {found} locomotion clips ({idles.Count} idles: {string.Join(", ", idles.Select(c => c.name))}) → {overridesPath}.");
        return overrides;
    }

    static HashSet<AnimationClip> LocomotionIdles(AnimatorController controller)
    {
        var idles = new HashSet<AnimationClip>();

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != "Locomotion") continue;

            foreach (ChildAnimatorState child in layer.stateMachine.states)
                AddIdles(child.state.motion as BlendTree, idles);
        }

        return idles;
    }

    static void AddIdles(BlendTree tree, HashSet<AnimationClip> idles)
    {
        if (tree == null || tree.children.Length == 0) return;

        if (tree.children[0].motion is AnimationClip first) idles.Add(first);

        foreach (ChildMotion child in tree.children)
            AddIdles(child.motion as BlendTree, idles);
    }

    // ── Model ─────────────────────────────────────────────────────────────

    static GameObject BuildModel(Dictionary<Material, Material> toon, Dictionary<Material, Material> shieldToon,
                                 RuntimeAnimatorController overrides)
    {
        var root = new GameObject("KatanaGirl_Model");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab), root.transform);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = "Visual";

        StripPackParts(visual);
        Transform sheath = MountSheath(visual);
        RemapMaterials(visual, toon);
        SetUpAnimator(visual, overrides, sheath);
        MapSockets(root, visual);
        CombatGirlsWardrobe.Dress(root, visual, CombatGirlsWardrobe.Katana, toon, CombatGirlsWardrobe.Shield, shieldToon);
        LogHeight(visual, ModelName);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
        Object.DestroyImmediate(root);
        Log($"model: {ModelPath}.");
        return prefab;
    }

    // Her weapon script, demo scripts and both weapon constraints go: our relay drives the sheath and
    // the item brings the blade. The skinned weapon copy (the "Weapon" group) goes too.
    internal static void StripPackParts(GameObject visual)
    {
        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && PackScripts.Contains(behaviour.GetType().Name)) Object.DestroyImmediate(behaviour);

        foreach (ParentConstraint constraint in visual.GetComponentsInChildren<ParentConstraint>(true))
            Object.DestroyImmediate(constraint);

        DestroyChild(visual.transform, "Weapon");
        DestroyDeep(visual.transform, "Wp_R_Root");
    }

    // The sheath (Wp_L_Root, holding both sheath meshes and Katana_Close) moves onto Hand_L_Socket: the
    // armed rest pose, and the pose a menu preview shows.
    static Transform MountSheath(GameObject visual)
    {
        Transform sheath = FindDeep(visual.transform, "Wp_L_Root");
        Transform hand = FindDeep(visual.transform, "Hand_L_Socket");
        if (sheath == null || hand == null)
        {
            Debug.LogError("[KatanaGirlBuilder] Wp_L_Root or Hand_L_Socket missing from the pack prefab.");
            return null;
        }

        Transform weaponRoot = sheath.parent;
        ModelModule.PlaceInSocket(sheath, hand);
        sheath.gameObject.SetActive(true);
        if (weaponRoot != null && weaponRoot.name == "Weapon_Katana") Object.DestroyImmediate(weaponRoot.gameObject);

        SetActive(visual.transform, "Weapon_Katana_sheath_A", true);
        SetActive(visual.transform, "Weapon_Katana_sheath_B", false);
        SetActive(visual.transform, "Katana_Close", true);
        return sheath;
    }

    internal static void RemapMaterials(GameObject visual, Dictionary<Material, Material> toon)
    {
        int missing = 0;

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && toon.TryGetValue(materials[i], out Material mapped)) materials[i] = mapped;
                else missing++;
            }
            renderer.sharedMaterials = materials;
        }

        if (missing > 0)
            Debug.LogError($"[KatanaGirlBuilder] {missing} material slots had no FlatToon version and kept the pack's.");
    }

    internal static void SetUpAnimator(GameObject visual, RuntimeAnimatorController overrides, Transform sheath)
    {
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = overrides;

        Animator porphi = LoadPorphiAnimator();
        if (porphi != null)
        {
            animator.cullingMode = porphi.cullingMode;
            animator.updateMode = porphi.updateMode;
        }

        // Off in the prefab: RootMotionRelay turns it on at runtime and hands the travel to the movement
        // handler, so the Visual child never walks off the capsule.
        animator.applyRootMotion = false;

        AddMissing<AnimationEventForwarder>(visual);
        AddMissing<RootMotionRelay>(visual);

        var relay = visual.GetComponent<SocketEventRelay>();
        if (relay == null) relay = visual.AddComponent<SocketEventRelay>();
        var so = new SerializedObject(relay);
        so.FindProperty("sheath").objectReferenceValue = sheath;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Animator LoadPorphiAnimator()
    {
        var porphi = AssetDatabase.LoadAssetAtPath<GameObject>(PorphiPath);
        return porphi != null ? porphi.GetComponentInChildren<Animator>(true) : null;
    }

    // The blade lives sheathed (Katana_Close), so the weapon slot and the stance's sheath socket are
    // the same place on her: the stance moves nothing and the relay does the drawing.
    static void MapSockets(GameObject root, GameObject visual)
    {
        var provider = root.AddComponent<ModelSocketProvider>();
        Transform close = FindDeep(visual.transform, "Katana_Close");

        provider.slotSockets.Add(new ModelSocketProvider.SlotSocketMapping { slot = LoadSlot(MainWeaponSlot), socket = close });
        // A shield shares her left hand with the sheath until she has an off-hand pose of her own.
        provider.slotSockets.Add(new ModelSocketProvider.SlotSocketMapping { slot = LoadSlot(OffWeaponSlot), socket = FindDeep(visual.transform, "Hand_L_Socket") });

        AddNamed(provider, "mainwep_sheathed", close);
        AddNamed(provider, "Katana_Close", close);
        AddNamed(provider, "Hand_R_Socket", FindDeep(visual.transform, "Hand_R_Socket"));
        AddNamed(provider, "Hand_L_Socket", FindDeep(visual.transform, "Hand_L_Socket"));
        AddNamed(provider, "Put_Socket_Katana", FindDeep(visual.transform, "Put_Socket_Katana"));
        AddNamed(provider, "add_weapon_r", FindDeep(visual.transform, "Katana_Weapon_Bone_Dummy_R"));
    }

    internal static void AddNamed(ModelSocketProvider provider, string id, Transform socket)
    {
        if (socket == null) Debug.LogError($"[KatanaGirlBuilder] Socket for '{id}' not found on the model.");
        provider.namedSockets.Add(new ModelSocketProvider.NamedSocketMapping { socketId = id, socket = socket });
    }

    internal static EquipmentSlotDefinition LoadSlot(string slotId)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EquipmentSlotDefinition"))
        {
            var slot = AssetDatabase.LoadAssetAtPath<EquipmentSlotDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (slot != null && slot.slotId == slotId) return slot;
        }

        Debug.LogError($"[KatanaGirlBuilder] No EquipmentSlotDefinition with id '{slotId}'.");
        return null;
    }

    internal static void LogHeight(GameObject visual, string modelName)
    {
        float hers = Measure(visual).size.y;

        var porphi = AssetDatabase.LoadAssetAtPath<GameObject>(PorphiPath);
        if (porphi == null)
        {
            Log($"height: {hers:F2} m.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(porphi);
        float theirs = Measure(instance).size.y;
        Object.DestroyImmediate(instance);
        Log($"height: {modelName} {hers:F2} m, Porphi {theirs:F2} m. Not scaled.");
    }

    static Bounds Measure(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    // ── Blades ────────────────────────────────────────────────────────────

    // D2: the item is the blade. Its prefab is the pack's blade mesh at its offset from Wp_R_Root (the
    // root the pack constrained to a socket).
    static ItemDefinition BuildBlade(string letter, Dictionary<Material, Material> toon)
    {
        var steel = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SteelKatanaPath);
        var weapon = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefab);
        Transform source = weapon != null ? FindDeep(weapon.transform, "Weapon_Katana_Blade_" + letter) : null;

        if (steel == null || source == null)
        {
            Debug.LogError($"[KatanaGirlBuilder] Blade {letter}: Item_SteelKatana or Weapon_Katana_Blade_{letter} missing.");
            return null;
        }

        GameObject prefab = BuildBladePrefab(letter, source, toon);
        return BuildBladeItem(letter, steel, prefab);
    }

    // Made once; after that the prefab is yours, and a rebuild keeps it. The blade carries no collider:
    // strikes are a range and facing check (Combat_Framework §2.5), so the mesh only has to look right.
    static GameObject BuildBladePrefab(string letter, Transform source, Dictionary<Material, Material> toon)
    {
        string path = $"{Out}/Weapon_KatanaGirl_Blade{letter}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            Log($"blade {letter}: kept {path}.");
            return existing;
        }

        var root = new GameObject("Weapon_KatanaGirl_Blade" + letter);
        var blade = new GameObject("Blade");
        blade.transform.SetParent(root.transform, false);
        blade.transform.localPosition = source.localPosition;
        blade.transform.localRotation = source.localRotation;
        blade.transform.localScale = source.localScale;

        Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
        blade.AddComponent<MeshFilter>().sharedMesh = mesh;
        blade.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials
            .Select(m => m != null && toon.TryGetValue(m, out Material mapped) ? mapped : m).ToArray();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Log($"blade {letter}: {path}.");
        return prefab;
    }

    // Made once from the Steel Katana (same moveset, dice and stats), then left alone apart from its
    // id, name and visual, so hand edits survive a rebuild.
    static ItemDefinition BuildBladeItem(string letter, ItemDefinition steel, GameObject prefab)
    {
        string path = $"{ItemsOut}/Item_KatanaGirlBlade{letter}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(steel), path);

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = "katana_girl_blade_" + letter.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = "Iai Katana " + letter;
        so.FindProperty("description").stringValue = "A katana made to live in its sheath. Drawn, cut, put away.";
        so.FindProperty("equippedPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        Log($"item: {path} ({item.itemId}).");
        return item;
    }

    // ── Registration ──────────────────────────────────────────────────────

    internal static void RegisterModel(GameObject prefab, string modelId, string modelName)
    {
        var db = AssetDatabase.LoadAssetAtPath<ModelDatabase>(ModelDatabasePath);
        var so = new SerializedObject(db);
        SerializedProperty models = so.FindProperty("models");

        SerializedProperty entry = FindModelEntry(models, modelId);
        if (entry == null)
        {
            models.arraySize++;
            entry = models.GetArrayElementAtIndex(models.arraySize - 1);
            entry.FindPropertyRelative("thumbnailSprite").objectReferenceValue = null;
        }

        entry.FindPropertyRelative("modelId").stringValue = modelId;
        entry.FindPropertyRelative("displayName").stringValue = modelName;
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("allowColorCustomization").boolValue = false;
        entry.FindPropertyRelative("playable").boolValue = true;

        // Female Base stays out of creation: the CombatGirls replace it.
        SerializedProperty femaleBase = FindModelEntry(models, FemaleBaseId);
        if (femaleBase != null) femaleBase.FindPropertyRelative("playable").boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
        Log($"registered '{modelId}' in {db.name} as playable ('{FemaleBaseId}' off).");
    }

    static SerializedProperty FindModelEntry(SerializedProperty models, string modelId)
    {
        for (int i = 0; i < models.arraySize; i++)
        {
            SerializedProperty entry = models.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("modelId").stringValue == modelId) return entry;
        }

        return null;
    }

    static void DressMirror(ItemDefinition blade)
    {
        var archetype = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MirrorArchetypePath);
        if (archetype != null)
        {
            var so = new SerializedObject(archetype);
            SerializedProperty pool = so.FindProperty("modelPool");
            pool.arraySize = 1;
            pool.GetArrayElementAtIndex(0).stringValue = ModelId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (blade == null) return;

        GameObject mirror = PrefabUtility.LoadPrefabContents(MirrorPrefabPath);
        var equipment = mirror.GetComponentInChildren<EquipmentSystem>(true);
        bool armed = equipment != null && SetEquipped(equipment, MainWeaponSlot, blade.itemId);

        if (armed) PrefabUtility.SaveAsPrefabAsset(mirror, MirrorPrefabPath);
        PrefabUtility.UnloadPrefabContents(mirror);

        Log(armed ? $"mirror: model '{ModelId}', {MainWeaponSlot} = {blade.itemId}." : "mirror: model set; no main weapon entry to change.");
    }

    static bool SetEquipped(EquipmentSystem equipment, string slotId, string itemId)
    {
        var so = new SerializedObject(equipment);
        SerializedProperty items = so.FindProperty("equippedItems");

        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty entry = items.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("slotId").stringValue != slotId) continue;

            entry.FindPropertyRelative("item.definitionId").stringValue = itemId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        return false;
    }

    // Every humanoid on the shared controller now plays her attacks: their clips call the socket relay
    // (a model without her sockets ignores them; without one, Unity logs "no receiver"), and their
    // travel needs the root-motion relay.
    static void AddRelayToHumanoids(AnimatorController controller)
    {
        var db = AssetDatabase.LoadAssetAtPath<ModelDatabase>(ModelDatabasePath);
        int added = 0;

        foreach (ModelDatabase.ModelVariant variant in db.AllModels)
        {
            if (variant == null || variant.prefab == null || variant.modelId == ModelId) continue;

            string path = AssetDatabase.GetAssetPath(variant.prefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            Animator animator = contents.GetComponentInChildren<Animator>(true);

            bool plays = animator != null && Plays(animator, controller);
            int parts = plays ? AddMissing<SocketEventRelay>(animator.gameObject) + AddMissing<RootMotionRelay>(animator.gameObject) : 0;
            if (parts > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                added++;
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        Log($"relays: added to {added} other humanoid model(s).");
    }

    // 1 when the component was added, 0 when it was already there.
    internal static int AddMissing<T>(GameObject target) where T : Component
    {
        if (target.GetComponent<T>() != null) return 0;
        target.AddComponent<T>();
        return 1;
    }

    static bool Plays(Animator animator, AnimatorController controller)
    {
        RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
        if (runtime == controller) return true;
        return runtime is AnimatorOverrideController overrides && overrides.runtimeAnimatorController == controller;
    }

    // Onto the Nature Spirit's shelf (Tools → Characters → Make Nature Spirit a Vendor).
    internal static void StockVendor(List<ItemDefinition> items, string what)
    {
        var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(NatureSpiritVendorBuilder.VendorPath);
        if (vendor == null)
        {
            Log("vendor: no Nature Spirit shelf yet. Run Tools → Characters → Make Nature Spirit a Vendor, then this again.");
            return;
        }

        // A test shelf for now: the CombatGirls armour pieces and weapons only.
        int dropped = vendor.stock.RemoveAll(s => s.item == null || !SoldOnShelf(s.item));
        NatureSpiritVendorBuilder.AddStock(vendor, items.Where(i => i != null && SoldOnShelf(i)));
        Log($"vendor: {what} on the Nature Spirit's shelf ({items.Count} items; {dropped} other items taken off).");
    }

    static readonly string[] ShelfSlots = { "mainwep", "offwep" };

    static bool SoldOnShelf(ItemDefinition item)
    {
        if (item.HasTag(CombatGirlsWardrobe.OutfitTag)) return true;
        return item.subType != null && item.subType.equipmentSlot != null && ShelfSlots.Contains(item.subType.equipmentSlot.slotId);
    }

    // ── Creation row ──────────────────────────────────────────────────────

    // Made once; restyle it freely.
    static void BuildRowPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath) != null) return;

        var row = new GameObject("AppearanceRow", typeof(RectTransform));
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        row.AddComponent<LayoutElement>().preferredHeight = 36f;

        TextMeshProUGUI label = MakeText(row.transform, "Label", 180f, TextAlignmentOptions.Left);
        Button previous = MakeButton(row.transform, "Previous", "<");
        TextMeshProUGUI value = MakeText(row.transform, "Value", 180f, TextAlignmentOptions.Center);
        Button next = MakeButton(row.transform, "Next", ">");

        var view = row.AddComponent<AppearanceRowView>();
        var so = new SerializedObject(view);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("value").objectReferenceValue = value;
        so.FindProperty("previousButton").objectReferenceValue = previous;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(row, RowPrefabPath);
        Object.DestroyImmediate(row);
        Log($"row prefab: {RowPrefabPath}.");
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, float width, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = name;
        text.fontSize = 22f;
        text.alignment = alignment;

        if (width > 0f) go.AddComponent<LayoutElement>().preferredWidth = width;
        return text;
    }

    static Button MakeButton(Transform parent, string name, string glyph)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredWidth = 36f;

        var image = go.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = MakeText(go.transform, "Glyph", 0f, TextAlignmentOptions.Center);
        text.text = glyph;
        text.color = Color.black;

        var rect = (RectTransform)text.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return button;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    internal static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;

        return null;
    }

    static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    static void DestroyDeep(Transform parent, string name)
    {
        Transform found = FindDeep(parent, name);
        if (found != null) Object.DestroyImmediate(found.gameObject);
    }

    internal static void SetActive(Transform parent, string name, bool active)
    {
        Transform found = FindDeep(parent, name);
        if (found != null) found.gameObject.SetActive(active);
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void Log(string message)
    {
        Debug.Log("[KatanaGirlBuilder] " + message);
    }
}
