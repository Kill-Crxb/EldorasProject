using System.Reflection;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot authoring for Juice.md §6. Tools → Juice → Build FX And Fill Players.
//
// 1. Builds placeholder particle prefabs into CrabSystem/Juice/FX — the project has no hit sparks
//    or dust since the Cartoon FX pack was removed. Swap them for real art any time; the players
//    only hold a reference.
// 2. Clears and refills every MMF_Player in JuiceSystem.prefab. World players get particles and
//    3D sound; view players get camera shake only.
// 3. Fills Juice_Step on Base_PC and wires FootstepEmitter.onStep → StepJuice.OnStep.
//
// Safe to re-run: every player is cleared before it is filled, the FX prefabs are overwritten in
// place, and the onStep listener is only added if missing. Any hand edits to the players are lost.
public static class JuiceSetupTool
{
    private const string FxFolder = "Assets/CrabSystem/Juice/FX";
    private const string SoundFolder = "Assets/Database/Assets/Sound/";
    private const string SystemPrefab = "Assets/Database/Characters/JuiceSystem.prefab";
    private const string PlayerPrefab = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    private const string ParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    private class Burst
    {
        public string name;
        public int count;
        public float lifeMin, lifeMax;
        public float speedMin, speedMax;
        public float sizeMin, sizeMax;
        public Color colorA, colorB;
        public float stretch;
        public bool flatRing;
        public float radius;
        public float gravity;
        public bool grow;
    }

    [MenuItem("Tools/Juice/Build FX And Fill Players")]
    private static void Run()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterial);
        if (material == null)
        {
            Debug.LogError($"[JuiceSetupTool] Particle material not found at {ParticleMaterial}.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(FxFolder)) AssetDatabase.CreateFolder("Assets/CrabSystem/Juice", "FX");

        ParticleSystem spark = Save(Build(Spark(), material));
        ParticleSystem sparkHeavy = Save(Build(SparkHeavy(), material));
        ParticleSystem blockSpark = Save(Build(BlockSpark(), material));
        ParticleSystem parrySpark = Save(BuildParry(material));
        ParticleSystem dust = Save(Build(Dust(), material));
        ParticleSystem dustHeavy = Save(Build(DustHeavy(), material));
        ParticleSystem stepDust = Save(Build(StepDust(), material));
        ParticleSystem deathPuff = Save(Build(DeathPuff(), material));

        GameObject system = PrefabUtility.LoadPrefabContents(SystemPrefab);

        MMF_Player p = Find(system, "Juice_HitLight");
        if (p != null) { Clear(p); Particles(p, spark); Sound(p, "PUNCH_DESIGNED_LIGHT_78.wav", 0.8f); }

        p = Find(system, "Juice_HitHeavy");
        if (p != null) { Clear(p); Particles(p, sparkHeavy); Sound(p, "WEAPAxe_Long Two-Handed Axe Flesh Hit_JSE_MW.wav", 1f); }

        p = Find(system, "Juice_Blocked");
        if (p != null) { Clear(p); Particles(p, blockSpark); Sound(p, "WEAPArmr_Metal Shield Block Hits_JSE_MW.wav", 0.8f); }

        p = Find(system, "Juice_HitProjectile");
        if (p != null) { Clear(p); Particles(p, spark); Sound(p, "bullet_impact_body_thump_02.wav", 0.8f); }

        p = Find(system, "Juice_Parry");
        if (p != null) { Clear(p); Particles(p, parrySpark); Sound(p, "SWORD Hit Metal 02.wav", 1f); }

        p = Find(system, "Juice_Death");
        if (p != null) { Clear(p); Particles(p, deathPuff); Sound(p, "IMPACT_LOW_THUD_10.wav", 1f); }

        p = Find(system, "Juice_Connect");
        if (p != null) { Clear(p); Sound(p, "Bluezone_BC0248_098_whoosh_hit.wav", 0.4f); }

        p = Find(system, "Juice_Land");
        if (p != null) { Clear(p); Particles(p, dust); Sound(p, "Thwump.wav", 0.35f); }

        p = Find(system, "Juice_LandHard");
        if (p != null) { Clear(p); Particles(p, dustHeavy); Sound(p, "PUNCH_PERCUSSIVE_HEAVY_06.wav", 0.8f); }

        p = Find(system, "View_Hit");
        if (p != null) { Clear(p); Shake(p, 0.15f, 0.25f, 40f, Vector3.zero); }

        p = Find(system, "View_Connect");
        if (p != null) { Clear(p); Shake(p, 0.08f, 0.12f, 40f, Vector3.zero); }

        p = Find(system, "View_Parry");
        if (p != null) { Clear(p); Shake(p, 0.1f, 0.35f, 60f, Vector3.zero); }

        p = Find(system, "View_Kill");
        if (p != null) { Clear(p); Shake(p, 0.25f, 0.3f, 30f, Vector3.zero); }

        p = Find(system, "View_LandHard");
        if (p != null) { Clear(p); }

        PrefabUtility.SaveAsPrefabAsset(system, SystemPrefab);
        PrefabUtility.UnloadPrefabContents(system);

        FillStep(stepDust);

        AssetDatabase.SaveAssets();
        Debug.Log("[JuiceSetupTool] Done — JuiceSystem players filled, Juice_Step filled, FX in " + FxFolder);
    }

    // Separate from Run on purpose: it touches no MMF_Player, so it never wipes hand-tuned players.
    // FootstepEmitter owns step audio (surface sets, speed volume, no-repeat); it had no
    // AudioSource and no clips, so it never made a sound. Clips are cut from Steps.wav.
    [MenuItem("Tools/Juice/Wire Footstep Audio")]
    private static void WireFootstepAudio()
    {
        var clips = new AudioClip[8];
        for (int i = 0; i < clips.Length; i++)
        {
            string path = $"{SoundFolder}Footsteps/Step_{i + 1:00}.wav";
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clips[i] == null)
            {
                Debug.LogError($"[JuiceSetupTool] Missing footstep clip: {path}");
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        FootstepEmitter emitter = root.GetComponentInChildren<FootstepEmitter>(true);
        if (emitter == null)
        {
            Debug.LogError("[JuiceSetupTool] No FootstepEmitter on Base_PC.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        AudioSource source = emitter.GetComponent<AudioSource>();
        if (source == null) source = emitter.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 25f;

        var so = new SerializedObject(emitter);
        so.FindProperty("audioSource").objectReferenceValue = source;
        SerializedProperty list = so.FindProperty("defaultClips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[JuiceSetupTool] Footstep audio wired — 8 clips on FootstepEmitter, 3D AudioSource added.");
    }

    private static void FillStep(ParticleSystem stepDust)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);

        MMF_Player step = Find(root, "Juice_Step");
        if (step != null)
        {
            Clear(step);
            Particles(step, stepDust);
        }

        StepJuice stepJuice = root.GetComponentInChildren<StepJuice>(true);
        FootstepEmitter emitter = root.GetComponentInChildren<FootstepEmitter>(true);
        if (stepJuice == null || emitter == null)
        {
            Debug.LogError("[JuiceSetupTool] Base_PC needs both StepJuice and FootstepEmitter to wire footsteps.");
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        FieldInfo field = typeof(FootstepEmitter).GetField("onStep", BindingFlags.NonPublic | BindingFlags.Instance);
        UnityEvent<Vector3, Vector3> onStep = field != null ? field.GetValue(emitter) as UnityEvent<Vector3, Vector3> : null;
        if (onStep != null && !HasListener(onStep, stepJuice)) UnityEventTools.AddPersistentListener(onStep, stepJuice.OnStep);

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Additive only: builds what is missing and never refills an existing player, so hand tuning
    // survives a re-run. Juice.md §7.
    //   Assets   — FX_AirJumpBurst, SpeedLines.mat, SpeedFX / PulseFX volume profiles.
    //   Base_PC  — ScreenFX rig (speed + pulse volumes, Feel URP shakers), speed-line quad under
    //              PlayerCam, Sprinting camera mode FOV.
    //   JuiceSystem — the ten movement players, filled only when created, wired into JuiceModule;
    //              a chromatic pulse appended to View_Hit and View_LandHard if they lack one.
    // Camera shake is for combat only — hit taken, hit landed, parry, kill. Movement pulses use lens
    // and chromatic; a shake on every landing read as camera jitter.
    [MenuItem("Tools/Juice/Build Movement And Screen FX")]
    private static void BuildMovementAndScreenFx()
    {
        Material particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterial);
        if (particleMaterial == null)
        {
            Debug.LogError($"[JuiceSetupTool] Particle material not found at {ParticleMaterial}.");
            return;
        }

        ParticleSystem airBurst = LoadFx("FX_AirJumpBurst");
        if (airBurst == null) airBurst = Save(Build(AirJumpBurst(), particleMaterial));

        Material speedLines = SpeedLinesMaterial();
        VolumeProfile speedProfile = SpeedProfile();
        VolumeProfile pulseProfile = PulseProfile();
        if (speedLines == null || speedProfile == null || pulseProfile == null) return;

        BuildScreenRig(speedLines, speedProfile, pulseProfile);
        BuildMovementPlayers(airBurst);

        AssetDatabase.SaveAssets();
        Debug.Log("[JuiceSetupTool] Movement and screen FX built — see Juice.md §7 for the test list.");
    }

    private static void BuildScreenRig(Material speedLines, VolumeProfile speedProfile, VolumeProfile pulseProfile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);

        Transform cameraSystem = FindChild(root.transform, "Camera_System");
        Transform playerCam = FindChild(root.transform, "PlayerCam");
        Camera camera = playerCam != null ? playerCam.GetComponent<Camera>() : null;
        if (cameraSystem == null || camera == null)
        {
            Debug.LogError("[JuiceSetupTool] Base_PC needs Camera_System and a PlayerCam with a Camera.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Transform fx = Child(cameraSystem, "ScreenFX");

        Volume speed = GetOrAdd<Volume>(Child(fx, "SpeedVolume").gameObject);
        speed.isGlobal = true;
        speed.priority = 10f;
        speed.weight = 0f;
        speed.sharedProfile = speedProfile;

        GameObject pulseObject = Child(fx, "PulseVolume").gameObject;
        Volume pulse = GetOrAdd<Volume>(pulseObject);
        pulse.isGlobal = true;
        pulse.priority = 20f;
        pulse.weight = 1f;
        pulse.sharedProfile = pulseProfile;
        GetOrAdd<MMChromaticAberrationShaker_URP>(pulseObject);
        GetOrAdd<MMLensDistortionShaker_URP>(pulseObject);

        Transform lines = Child(playerCam, "SpeedLines");
        GetOrAdd<MeshFilter>(lines.gameObject).sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer linesRenderer = GetOrAdd<MeshRenderer>(lines.gameObject);
        linesRenderer.sharedMaterial = speedLines;
        linesRenderer.shadowCastingMode = ShadowCastingMode.Off;
        linesRenderer.receiveShadows = false;
        GetOrAdd<SpeedLineDriver>(lines.gameObject);

        var rig = new SerializedObject(GetOrAdd<ScreenFXRig>(fx.gameObject));
        rig.FindProperty("speedVolume").objectReferenceValue = speed;
        rig.FindProperty("pulseVolume").objectReferenceValue = pulse;
        rig.FindProperty("targetCamera").objectReferenceValue = camera;
        rig.FindProperty("speedLines").objectReferenceValue = lines;
        rig.ApplyModifiedPropertiesWithoutUndo();

        SetSprintFov(root, 8f);

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Only the FOV. Sprinting's distance is 1.6, closer than Default's 2 — that pulls the camera
    // in on sprint, and whether that is intended is a feel call left to the inspector.
    private static void SetSprintFov(GameObject root, float fovOffset)
    {
        CameraModule module = root.GetComponentInChildren<CameraModule>(true);
        if (module == null) return;

        var so = new SerializedObject(module);
        SerializedProperty modes = so.FindProperty("modes");
        for (int i = 0; i < modes.arraySize; i++)
        {
            SerializedProperty mode = modes.GetArrayElementAtIndex(i);
            if (mode.FindPropertyRelative("modeName").stringValue != "Sprinting") continue;
            mode.FindPropertyRelative("fovOffset").floatValue = fovOffset;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildMovementPlayers(ParticleSystem airBurst)
    {
        ParticleSystem dust = LoadFx("FX_Dust");
        ParticleSystem dustHeavy = LoadFx("FX_DustHeavy");

        GameObject system = PrefabUtility.LoadPrefabContents(SystemPrefab);
        Transform juice = FindChild(system.transform, "Juice");
        JuiceModule module = system.GetComponentInChildren<JuiceModule>(true);
        if (juice == null || module == null)
        {
            Debug.LogError("[JuiceSetupTool] JuiceSystem needs a Juice child and a JuiceModule.");
            PrefabUtility.UnloadPrefabContents(system);
            return;
        }

        var so = new SerializedObject(module);

        MMF_Player p;
        if (NewPlayer(juice, "Juice_Jump", out p)) { Particles(p, dust); Sound(p, "Jump.wav", 0.5f); }
        so.FindProperty("jump").objectReferenceValue = p;

        if (NewPlayer(juice, "Juice_AirJump", out p)) { Particles(p, airBurst); Sound(p, "Whoosh,Organic,Fabric,Glove,Airy,Breathy,Punchy.wav", 0.5f); }
        so.FindProperty("airJump").objectReferenceValue = p;

        if (NewPlayer(juice, "Juice_WallJump", out p)) { Particles(p, dust); Sound(p, "Whoosh_Cloth_Leather_Fight_174.wav", 0.6f); }
        so.FindProperty("wallJump").objectReferenceValue = p;

        if (NewPlayer(juice, "Juice_Mantle", out p)) { Sound(p, "Whoosh,Organic,Fabric,Strap,Zip,High,Stagger,Pushy.wav", 0.5f); }
        so.FindProperty("mantle").objectReferenceValue = p;

        if (NewPlayer(juice, "Juice_Dash", out p)) { Particles(p, dustHeavy); Sound(p, "Dash.wav", 0.7f); }
        so.FindProperty("dash").objectReferenceValue = p;

        if (NewPlayer(juice, "Juice_SprintStart", out p)) { Particles(p, dust); Sound(p, "Seq1.15 whoosh #1 96 HK1.wav", 0.35f); }
        so.FindProperty("sprintStart").objectReferenceValue = p;

        if (NewPlayer(juice, "View_Jump", out p)) { Lens(p, 0.25f, -0.08f); }
        so.FindProperty("viewJump").objectReferenceValue = p;

        NewPlayer(juice, "View_Land", out p);
        so.FindProperty("viewLand").objectReferenceValue = p;

        if (NewPlayer(juice, "View_WallJump", out p)) { Lens(p, 0.25f, -0.12f); }
        so.FindProperty("viewWallJump").objectReferenceValue = p;

        if (NewPlayer(juice, "View_Dash", out p)) { Lens(p, 0.35f, -0.3f); Chroma(p, 0.3f, 0.8f); }
        so.FindProperty("viewDash").objectReferenceValue = p;

        so.ApplyModifiedPropertiesWithoutUndo();

        AppendChroma(juice, "View_Hit", 0.2f, 0.6f);
        AppendChroma(juice, "View_LandHard", 0.25f, 0.5f);

        PrefabUtility.SaveAsPrefabAsset(system, SystemPrefab);
        PrefabUtility.UnloadPrefabContents(system);
    }

    // True only when the player was created now — an existing one is returned untouched.
    private static bool NewPlayer(Transform parent, string objectName, out MMF_Player player)
    {
        Transform existing = FindChild(parent, objectName);
        if (existing != null)
        {
            player = GetOrAdd<MMF_Player>(existing.gameObject);
            return false;
        }

        player = Child(parent, objectName).gameObject.AddComponent<MMF_Player>();
        return true;
    }

    private static void AppendChroma(Transform juice, string objectName, float duration, float intensity)
    {
        Transform t = FindChild(juice, objectName);
        MMF_Player player = t != null ? t.GetComponent<MMF_Player>() : null;
        if (player == null) return;

        if (player.FeedbacksList != null)
        {
            foreach (MMF_Feedback f in player.FeedbacksList)
            {
                if (f is MMF_ChromaticAberration_URP) return;
            }
        }
        Chroma(player, duration, intensity);
    }

    // Both pulse the PulseVolume through its Feel shakers on channel 0, the default for both ends.
    private static void Chroma(MMF_Player player, float duration, float intensity)
    {
        var f = (MMF_ChromaticAberration_URP)player.AddFeedback(typeof(MMF_ChromaticAberration_URP));
        f.Duration = duration;
        f.RemapIntensityZero = 0f;
        f.RemapIntensityOne = intensity;
    }

    private static void Lens(MMF_Player player, float duration, float intensity)
    {
        var f = (MMF_LensDistortion_URP)player.AddFeedback(typeof(MMF_LensDistortion_URP));
        f.Duration = duration;
        f.RemapIntensityZero = 0f;
        f.RemapIntensityOne = intensity;
    }

    private static Material SpeedLinesMaterial()
    {
        string path = FxFolder + "/SpeedLines.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("NinjaGame/Screen Speed Lines");
        Texture2D spike = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Database/Assets/VFX/Movement/spike.png");
        if (shader == null || spike == null)
        {
            Debug.LogError("[JuiceSetupTool] Speed lines need the 'NinjaGame/Screen Speed Lines' shader and spike.png.");
            return null;
        }

        var material = new Material(shader);
        material.SetTexture("_BaseMap", spike);
        material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f));
        material.SetFloat("_LineCount", 32f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Vignette only. Lens distortion and chromatic aberration belong to the pulse volume; one
    // override per writer, or the higher-priority volume would flatten the other.
    private static VolumeProfile SpeedProfile()
    {
        string path = FxFolder + "/SpeedFX.asset";
        VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (existing != null) return existing;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);
        AddToProfile(vignette, profile);
        return profile;
    }

    private static VolumeProfile PulseProfile()
    {
        string path = FxFolder + "/PulseFX.asset";
        VolumeProfile existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (existing != null) return existing;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        ChromaticAberration chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);
        AddToProfile(chroma, profile);

        LensDistortion lens = profile.Add<LensDistortion>(true);
        lens.intensity.Override(0f);
        AddToProfile(lens, profile);
        return profile;
    }

    // Volume components are sub-assets; without this they vanish on the next domain reload.
    private static void AddToProfile(VolumeComponent component, VolumeProfile profile)
    {
        component.name = component.GetType().Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
    }

    private static ParticleSystem LoadFx(string fxName)
    {
        return AssetDatabase.LoadAssetAtPath<ParticleSystem>($"{FxFolder}/{fxName}.prefab");
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == objectName) return t;
        }
        return null;
    }

    // Finds a direct-or-nested child by name, or creates it directly under the parent.
    private static Transform Child(Transform parent, string objectName)
    {
        Transform existing = FindChild(parent, objectName);
        if (existing != null) return existing;

        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static Burst AirJumpBurst() => new Burst
    {
        name = "FX_AirJumpBurst", count = 14, lifeMin = 0.18f, lifeMax = 0.3f, speedMin = 3f, speedMax = 5f,
        sizeMin = 0.1f, sizeMax = 0.18f, colorA = new Color(1f, 1f, 1f, 0.8f), colorB = new Color(0.7f, 0.9f, 1f, 0.7f),
        flatRing = true, radius = 0.15f
    };

    private static bool HasListener(UnityEventBase evt, Object target)
    {
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentTarget(i) == target && evt.GetPersistentMethodName(i) == "OnStep") return true;
        }
        return false;
    }

    private static MMF_Player Find(GameObject root, string objectName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != objectName) continue;
            MMF_Player player = t.GetComponent<MMF_Player>();
            if (player == null) Debug.LogError($"[JuiceSetupTool] {objectName} has no MMF_Player.");
            return player;
        }

        Debug.LogError($"[JuiceSetupTool] No object named {objectName} under {root.name}.");
        return null;
    }

    private static void Clear(MMF_Player player)
    {
        if (player.FeedbacksList != null) player.FeedbacksList.Clear();
    }

    // Script position: spawns at the point the code passes to PlayFeedbacks — the contact.
    private static void Particles(MMF_Player player, ParticleSystem prefab)
    {
        var f = (MMF_ParticlesInstantiation)player.AddFeedback(typeof(MMF_ParticlesInstantiation));
        f.ParticlesPrefab = prefab;
        f.Mode = MMF_ParticlesInstantiation.Modes.Pool;
        f.ObjectPoolSize = 6;
        f.PositionMode = MMF_ParticlesInstantiation.PositionModes.Script;
        f.NestParticles = false;
    }

    // OnDemand, because Event needs an MMSoundManager in the scene. Fully 3D, so other fights
    // sound where they are.
    private static void Sound(MMF_Player player, string file, float volume)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + file);
        if (clip == null) Debug.LogError($"[JuiceSetupTool] Sound not found: {SoundFolder}{file}");

        var f = (MMF_Sound)player.AddFeedback(typeof(MMF_Sound));
        f.Sfx = clip;
        f.PlayMethod = MMF_Sound.PlayMethods.OnDemand;
        f.MinVolume = volume * 0.9f;
        f.MaxVolume = volume;
        f.MinPitch = 0.93f;
        f.MaxPitch = 1.07f;
        f.SpatialBlend = 1f;
        f.MinDistance = 2f;
        f.MaxDistance = 40f;
    }

    // Any non-zero axis switches the shaker to per-axis amplitude, ignoring the overall one.
    private static void Shake(MMF_Player player, float duration, float amplitude, float frequency, Vector3 axes)
    {
        var f = (MMF_CameraShake)player.AddFeedback(typeof(MMF_CameraShake));
        f.CameraShakeProperties = new MMCameraShakeProperties(duration, amplitude, frequency, axes.x, axes.y, axes.z);
    }

    private static ParticleSystem Save(GameObject go)
    {
        string path = $"{FxFolder}/{go.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
    }

    private static GameObject Build(Burst s, Material material)
    {
        var go = new GameObject(s.name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        // Disable, not None: Feel's pool only reuses inactive objects. A system that stays
        // active after finishing is never handed out again, so the pool grows on every play.
        main.stopAction = ParticleSystemStopAction.Disable;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(s.lifeMin, s.lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(s.speedMin, s.speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(s.sizeMin, s.sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(s.colorA, s.colorB);
        main.gravityModifier = s.gravity;
        main.maxParticles = s.count * 2;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.count) });

        var shape = ps.shape;
        shape.radius = s.radius;
        shape.shapeType = s.flatRing ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
        if (s.flatRing) shape.rotation = new Vector3(90f, 0f, 0f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = s.grow
            ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f))
            : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = s.stretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = s.stretch;
        renderer.lengthScale = 1f;

        return go;
    }

    // A flash disc plus a spark burst. Playing the root plays the child.
    private static GameObject BuildParry(Material material)
    {
        GameObject root = Build(ParrySparks(), material);
        GameObject flash = Build(ParryFlash(), material);
        flash.transform.SetParent(root.transform, false);

        // Only the root disables. A child that switched itself off would stay off on the next
        // play — the root's Play() does not reactivate child GameObjects.
        var flashMain = flash.GetComponent<ParticleSystem>().main;
        flashMain.stopAction = ParticleSystemStopAction.None;
        return root;
    }

    private static Burst Spark() => new Burst
    {
        name = "FX_HitSpark", count = 12, lifeMin = 0.12f, lifeMax = 0.22f, speedMin = 6f, speedMax = 12f,
        sizeMin = 0.05f, sizeMax = 0.1f, colorA = Color.white, colorB = new Color(1f, 0.85f, 0.4f),
        stretch = 0.04f, radius = 0.05f
    };

    private static Burst SparkHeavy() => new Burst
    {
        name = "FX_HitSparkHeavy", count = 26, lifeMin = 0.15f, lifeMax = 0.3f, speedMin = 8f, speedMax = 16f,
        sizeMin = 0.08f, sizeMax = 0.16f, colorA = Color.white, colorB = new Color(1f, 0.7f, 0.3f),
        stretch = 0.05f, radius = 0.08f
    };

    private static Burst BlockSpark() => new Burst
    {
        name = "FX_BlockSpark", count = 8, lifeMin = 0.08f, lifeMax = 0.15f, speedMin = 3f, speedMax = 6f,
        sizeMin = 0.04f, sizeMax = 0.07f, colorA = new Color(1f, 0.75f, 0.45f), colorB = new Color(0.8f, 0.6f, 0.4f),
        stretch = 0.03f, radius = 0.05f
    };

    private static Burst ParrySparks() => new Burst
    {
        name = "FX_ParrySpark", count = 22, lifeMin = 0.15f, lifeMax = 0.28f, speedMin = 10f, speedMax = 18f,
        sizeMin = 0.06f, sizeMax = 0.12f, colorA = Color.white, colorB = new Color(0.6f, 0.9f, 1f),
        stretch = 0.05f, radius = 0.05f
    };

    private static Burst ParryFlash() => new Burst
    {
        name = "Flash", count = 1, lifeMin = 0.1f, lifeMax = 0.1f, speedMin = 0f, speedMax = 0f,
        sizeMin = 1.2f, sizeMax = 1.2f, colorA = Color.white, colorB = Color.white, radius = 0.01f
    };

    private static Burst Dust() => new Burst
    {
        name = "FX_Dust", count = 8, lifeMin = 0.3f, lifeMax = 0.5f, speedMin = 1f, speedMax = 2f,
        sizeMin = 0.25f, sizeMax = 0.4f, colorA = new Color(0.75f, 0.7f, 0.62f, 0.6f), colorB = new Color(0.6f, 0.56f, 0.5f, 0.5f),
        flatRing = true, radius = 0.2f, grow = true
    };

    private static Burst DustHeavy() => new Burst
    {
        name = "FX_DustHeavy", count = 18, lifeMin = 0.45f, lifeMax = 0.7f, speedMin = 2f, speedMax = 3.5f,
        sizeMin = 0.4f, sizeMax = 0.7f, colorA = new Color(0.75f, 0.7f, 0.62f, 0.7f), colorB = new Color(0.6f, 0.56f, 0.5f, 0.6f),
        flatRing = true, radius = 0.3f, grow = true
    };

    private static Burst StepDust() => new Burst
    {
        name = "FX_StepDust", count = 3, lifeMin = 0.25f, lifeMax = 0.4f, speedMin = 0.3f, speedMax = 0.7f,
        sizeMin = 0.12f, sizeMax = 0.2f, colorA = new Color(0.75f, 0.7f, 0.62f, 0.45f), colorB = new Color(0.6f, 0.56f, 0.5f, 0.35f),
        flatRing = true, radius = 0.08f, grow = true
    };

    private static Burst DeathPuff() => new Burst
    {
        name = "FX_DeathPuff", count = 30, lifeMin = 0.6f, lifeMax = 1f, speedMin = 1f, speedMax = 3f,
        sizeMin = 0.5f, sizeMax = 1f, colorA = new Color(0.35f, 0.35f, 0.38f, 0.8f), colorB = new Color(0.2f, 0.2f, 0.22f, 0.7f),
        radius = 0.5f, gravity = -0.05f, grow = true
    };
}
