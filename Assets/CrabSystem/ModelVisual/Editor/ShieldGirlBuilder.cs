using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools → Characters → Build Shield Girl
//
// Puts the CombatGirls Sword & Shield girl in as a second playable model (Shield_Girl_Build.md), the way
// KatanaGirlBuilder did the Katana Girl, and reusing its helpers. From the local-only pack this makes, in
// Database/Characters/ShieldGirl: FlatToon materials, the model prefab (sockets, socket and root-motion
// relays, the shared wardrobe) and her locomotion override controller; and four weapon items (sword and axe
// for the main hand, on the katana moveset for now; shield and axe shield for the off hand). Registers her
// as playable and puts her weapons and outfits on the Nature Spirit's shelf.
//
// Her attacks stay the shared ones (BasicAttack1–3 on her model); her own SS_Attack_1–4 string is next.
// Rerunnable; weapon prefabs and items are made once, then left alone apart from id, name and visual.
public static class ShieldGirlBuilder
{
    const string Pack = "Assets/CombatGirlsCharacterPack/CombatGirl_Shield";
    const string SourcePrefab = Pack + "/Prefab/SwordShieldGirl_FullBody.prefab";
    const string SwordPrefab = Pack + "/Prefab/Weapon/Weapon_SS_Sword.prefab";
    const string ShieldPrefab = Pack + "/Prefab/Weapon/Weapon_SS_Shiled.prefab";
    const string PackMaterials = Pack + "/Materials";
    const string ClipFolder = Pack + "/Animations";

    const string Out = "Assets/Database/Characters/ShieldGirl";
    const string MaterialsOut = Out + "/Materials";
    const string ModelPath = Out + "/ShieldGirl_Model.prefab";
    const string OverridesPath = Out + "/ShieldGirl_Overrides.overrideController";
    const string ControllerPath = "Assets/Database/3d/Humanoid/HumanoidAnimator_v2.controller";
    const string WeaponsOut = "Assets/Database/Resources/ItemDatabase/Weapons/ShieldGirl";
    const string SteelKatanaPath = "Assets/Database/Resources/ItemDatabase/Weapons/Bladed/Item_SteelKatana.asset";

    const string ModelId = "shield_girl_v1";
    const string ModelName = "Shield Girl";
    const string MainWeaponSlot = "mainwep";
    const string OffWeaponSlot = "offwep";

    class WeaponSpec
    {
        public string prefab;
        public string mesh;
        public string itemId;
        public string displayName;
        public string description;
        public string subType;
        public string category;
        public bool hits;
    }

    static readonly WeaponSpec[] Weapons =
    {
        new WeaponSpec { prefab = SwordPrefab, mesh = "Weapon_SS_Sword", itemId = "ss_sword", displayName = "Knight's Sword", description = "A straight one-handed sword.", subType = "SubType_Sword", category = "Catagory_MainWep", hits = true },
        new WeaponSpec { prefab = SwordPrefab, mesh = "Weapon_SS_Axe", itemId = "ss_axe", displayName = "Knight's Axe", description = "A one-handed axe.", subType = "SubType_Sword", category = "Catagory_MainWep", hits = true },
        new WeaponSpec { prefab = ShieldPrefab, mesh = "Weapon_SS_Shiled", itemId = "ss_shield", displayName = "Knight's Shield", description = "A kite shield for the off hand.", subType = "SubType_Offhand", category = "Catagory_OffWep", hits = false },
        new WeaponSpec { prefab = ShieldPrefab, mesh = "Weapon_SS_Axe_Shield", itemId = "ss_axe_shield", displayName = "Axe-Knight's Shield", description = "A heavier shield, made to match the axe.", subType = "SubType_Offhand", category = "Catagory_OffWep", hits = false },
    };

    [MenuItem("Tools/Characters/Build Shield Girl")]
    static void BuildAll()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab) == null)
        {
            Debug.LogError($"[ShieldGirlBuilder] {SourcePrefab} isn't in the project. The CombatGirls pack is local only; import it first.");
            return;
        }

        Dictionary<Material, Material> toon = BuildShieldMaterials();
        Dictionary<Material, Material> katanaToon = KatanaGirlBuilder.BuildKatanaMaterials();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorOverrideController overrides = BuildOverrides(controller);
        GameObject model = BuildModel(toon, katanaToon, overrides);
        List<ItemDefinition> weapons = BuildWeapons(toon);
        List<ItemDefinition> outfits = CombatGirlsWardrobe.BuildOutfitItems(CombatGirlsWardrobe.Shield);
        KatanaGirlBuilder.RegisterModel(model, ModelId, ModelName);
        KatanaGirlBuilder.StockVendor(weapons.Concat(outfits).ToList(), "the Shield Girl's weapons and armour pieces");

        AssetDatabase.SaveAssets();
        Log("Done. Rebuild the Katana Girl too (Tools → Characters → Build Katana Girl) so she carries this girl's outfits and hair.");
    }

    /// <summary>Her FlatToon materials; empty when the pack isn't imported.</summary>
    internal static Dictionary<Material, Material> BuildShieldMaterials()
    {
        if (!AssetDatabase.IsValidFolder(PackMaterials)) return new Dictionary<Material, Material>();
        return KatanaGirlBuilder.BuildMaterials(PackMaterials, MaterialsOut, "SS");
    }

    // Her idle, walk, run and sprint over the shared controller; attacks and reactions stay shared.
    static AnimatorOverrideController BuildOverrides(AnimatorController controller)
    {
        var swaps = new Dictionary<string, AnimationClip>
        {
            ["M_Walk_Forward"] = Clip("Normal/SS_Walk"),
            ["M_Run_Forward"] = Clip("Normal/SS_Run"),
            ["M_Sprint_Forward"] = Clip("Normal/SS_Sprint"),
            ["HumanF@Walk01_Forward"] = Clip("Normal/SS_Walk"),
            ["SprintForward"] = Clip("Normal/SS_Run"),
        };

        return KatanaGirlBuilder.BuildOverrides(controller, OverridesPath, Clip("Normal/SS_Idle"), swaps);
    }

    static AnimationClip Clip(string file) => KatanaGirlBuilder.LoadPackClip(ClipFolder, file);

    // ── Model ─────────────────────────────────────────────────────────────

    static GameObject BuildModel(Dictionary<Material, Material> toon, Dictionary<Material, Material> katanaToon,
                                 RuntimeAnimatorController overrides)
    {
        KatanaGirlBuilder.EnsureFolder(Out);

        var root = new GameObject("ShieldGirl_Model");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab), root.transform);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = "Visual";

        KatanaGirlBuilder.StripPackParts(visual);
        KatanaGirlBuilder.RemapMaterials(visual, toon);
        KatanaGirlBuilder.SetUpAnimator(visual, overrides, null);
        SetRestSockets(visual);
        MapSockets(root, visual);
        CombatGirlsWardrobe.Dress(root, visual, CombatGirlsWardrobe.Shield, toon, CombatGirlsWardrobe.Katana, katanaToon);
        KatanaGirlBuilder.LogHeight(visual, ModelName);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
        Object.DestroyImmediate(root);
        Log($"model: {ModelPath}.");
        return prefab;
    }

    // She carries her sword in hand and stows it on her back; she has no sheath. Shared attack clips that
    // move a blade to a socket she lacks (Katana_Close) are ignored by the relay.
    static void SetRestSockets(GameObject visual)
    {
        var relay = visual.GetComponent<SocketEventRelay>();
        var so = new SerializedObject(relay);
        so.FindProperty("armedBladeSocket").stringValue = "Hand_R_Socket";
        so.FindProperty("unarmedBladeSocket").stringValue = "Put_Socket_Blade";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MapSockets(GameObject root, GameObject visual)
    {
        var provider = root.AddComponent<ModelSocketProvider>();
        Transform t = visual.transform;

        provider.slotSockets.Add(new ModelSocketProvider.SlotSocketMapping { slot = KatanaGirlBuilder.LoadSlot(MainWeaponSlot), socket = KatanaGirlBuilder.FindDeep(t, "Hand_R_Socket") });
        provider.slotSockets.Add(new ModelSocketProvider.SlotSocketMapping { slot = KatanaGirlBuilder.LoadSlot(OffWeaponSlot), socket = KatanaGirlBuilder.FindDeep(t, "Hand_L_Socket") });

        KatanaGirlBuilder.AddNamed(provider, "mainwep_sheathed", KatanaGirlBuilder.FindDeep(t, "Put_Socket_Blade"));
        KatanaGirlBuilder.AddNamed(provider, "offwep_sheathed", KatanaGirlBuilder.FindDeep(t, "Put_Socket_Shield"));
        foreach (string socket in new[] { "Hand_R_Socket", "Hand_L_Socket", "Put_Socket_Blade", "Put_Socket_Shield" })
            KatanaGirlBuilder.AddNamed(provider, socket, KatanaGirlBuilder.FindDeep(t, socket));
    }

    // ── Weapons ───────────────────────────────────────────────────────────

    static List<ItemDefinition> BuildWeapons(Dictionary<Material, Material> toon)
    {
        var items = new List<ItemDefinition>();
        var steel = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SteelKatanaPath);
        if (steel == null)
        {
            Debug.LogError($"[ShieldGirlBuilder] No Steel Katana at {SteelKatanaPath}; weapons not made.");
            return items;
        }

        KatanaGirlBuilder.EnsureFolder(WeaponsOut);
        foreach (WeaponSpec spec in Weapons)
        {
            GameObject prefab = BuildWeaponPrefab(spec, toon);
            if (prefab != null) items.Add(BuildWeaponItem(spec, steel, prefab));
        }

        Log($"weapons: {items.Count} items in {WeaponsOut}.");
        return items;
    }

    // Made once; after that the prefab is yours (grip offset), and a rebuild keeps it. The mesh keeps its
    // offset from the pack prefab's root, which is the grip the pack put on the hand socket. No collider:
    // strikes are a range and facing check (Combat_Framework §2.5).
    static GameObject BuildWeaponPrefab(WeaponSpec spec, Dictionary<Material, Material> toon)
    {
        string path = $"{Out}/{spec.mesh}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
        Transform source = packPrefab != null ? ChildMesh(packPrefab.transform, spec.mesh) : null;
        if (source == null)
        {
            Debug.LogError($"[ShieldGirlBuilder] No mesh '{spec.mesh}' in {spec.prefab}.");
            return null;
        }

        var root = new GameObject(spec.mesh);
        var mesh = new GameObject("Mesh");
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = source.localPosition;
        mesh.transform.localRotation = source.localRotation;
        mesh.transform.localScale = source.localScale;

        Mesh shape = source.GetComponent<MeshFilter>().sharedMesh;
        mesh.AddComponent<MeshFilter>().sharedMesh = shape;
        mesh.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials
            .Select(m => m != null && toon.TryGetValue(m, out Material mapped) ? mapped : m).ToArray();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Log($"weapon prefab: {path}.");
        return prefab;
    }

    // A child with a mesh; the pack prefab's root shares its first mesh's name.
    static Transform ChildMesh(Transform root, string name)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.transform != root && filter.name == name) return filter.transform;
        return null;
    }

    // Made once from the Steel Katana (moveset, dice, stats), then only id, name, description, visual and
    // subtype are rewritten. Shields drop the moveset: the main hand decides how LMB attacks.
    static ItemDefinition BuildWeaponItem(WeaponSpec spec, ItemDefinition steel, GameObject prefab)
    {
        string path = $"{WeaponsOut}/Item_{spec.mesh}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(steel), path);

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = spec.itemId;
        so.FindProperty("displayName").stringValue = spec.displayName;
        so.FindProperty("description").stringValue = spec.description;
        so.FindProperty("equippedPrefab").objectReferenceValue = prefab;
        so.FindProperty("subType").objectReferenceValue = LoadNamed<ItemSubType>(spec.subType);
        so.FindProperty("category").objectReferenceValue = LoadNamed<ItemCategory>(spec.category);
        if (!spec.hits) so.FindProperty("moveset").objectReferenceValue = null;
        if (!spec.hits) so.FindProperty("weaponData").objectReferenceValue = null; // a shield isn't a katana
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    static T LoadNamed<T>(string assetName) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == assetName) return asset;
        }

        Debug.LogError($"[ShieldGirlBuilder] No {typeof(T).Name} asset named '{assetName}'.");
        return null;
    }

    static void Log(string message) => Debug.Log("[ShieldGirlBuilder] " + message);
}
