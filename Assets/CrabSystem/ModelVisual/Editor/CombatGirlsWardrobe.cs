using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One wardrobe for both CombatGirls (Shield_Girl_Build.md). Each girl's model carries her own outfits and
// hair and the other girl's, grafted onto her skeleton by bone name; bones she lacks (the school uniform's
// ribbons, the other hair's chain) are copied across under the same parent. Hair style and hair colour are
// chosen at creation.
//
// Outfits come apart into armour pieces (SG2, 8 Oct): body (top, plus armband or pauldrons), legs and feet,
// each an item in its slot that works on either girl. A piece exists only where the pack has a mesh for it,
// and each outfit is one armour tier (Sportswear light, School Uniform medium, Knight Armour heavy); the
// Squire is the Shield Girl's no-armour look and has no item. Each outfit's skin is trimmed to sit under it,
// so a matched set shows that skin and any mix shows the whole body (ModelAppearance skins).
//
// A grafted part brings the other girl's Magica Cloth with it (the pack's Biperworks Prefab Replace, step 3, done
// here so a rebuild keeps it): the cloth object is copied, its renderers, bones and colliders are pointed at the
// target's (colliders the target lacks are copied onto the same-named bone). Every cloth, her own included, is
// shown and hidden with the outfit or hair it moves, so a hidden skirt doesn't simulate.
public static class CombatGirlsWardrobe
{
    public const string OutfitTag = "combatgirls_outfit";
    const string NoneOption = "none";
    const string FullSkin = "Body_Full";
    const string ArmourSets = "Assets/Database/Resources/ItemDatabase/Armor/Sets/";
    const string Light = "armour_light";
    const string Medium = "armour_medium";
    const string Heavy = "armour_heavy";
    const int HairColours = 5;

    // The equipment slots an outfit comes apart into, each an appearance group of the same id.
    static readonly (string group, string name, string label)[] Slots =
    {
        ("body", "Body", "Top"),
        ("legs", "Legs", "Legs"),
        ("feet", "Feet", "Footwear"),
    };

    public class Outfit
    {
        public string groupObject;   // the pack object holding the outfit and its trimmed skin
        public string optionId;
        public string displayName;
        public string description;
        public string[] body = new string[0];
        public string[] legs = new string[0];
        public string[] feet = new string[0];
        public string skin;          // the body mesh trimmed to sit under this outfit
        public string tier = "";     // armour tier its pieces copy; empty = no items (a no-armour look)

        public string[] Pieces(string slot) => slot == "body" ? body : slot == "legs" ? legs : feet;
    }

    public class Girl
    {
        public string id;
        public string hairName;
        public string sourcePrefab;
        public string hairObject;
        public string hairFormat;    // pack hair material name, {0} = colour number
        public string shownFace;
        public string glassesObject; // empty = none
        public string defaultOutfit; // shown with no body armour equipped
        public string itemsFolder;
        public Outfit[] outfits;
    }

    public static readonly Girl Katana = new Girl
    {
        id = "kg",
        hairName = "Katana",
        sourcePrefab = "Assets/CombatGirlsCharacterPack/Katana_Girl/Prefab/KatanaGirl_FullBody.prefab",
        hairObject = "Katana_Hair",
        hairFormat = "Katana_Hair {0}",
        shownFace = "Face_02",
        glassesObject = "Acc_Glass",
        defaultOutfit = "sportswear",
        itemsFolder = "Assets/Database/Resources/ItemDatabase/Armor/KatanaGirl",
        outfits = new[]
        {
            new Outfit
            {
                groupObject = "SportsWear", optionId = "sportswear", displayName = "Sportswear", description = "Light training clothes.",
                body = new[] { "Sportswear_Top" }, legs = new[] { "Sportswear_Pants" }, feet = new[] { "Sportswear_Shoes" },
                skin = "Body_Sportswear", tier = Light,
            },
            new Outfit
            {
                groupObject = "SchoolUniform", optionId = "uniform", displayName = "School Uniform", description = "A school uniform cut for fighting in.",
                body = new[] { "Cloth_School_uniform_Top", "Colth_School_uniform_Armband" }, legs = new[] { "Cloth_School_uniform_Skirt" },
                feet = new[] { "Cloth_School_uniform_Shoes" }, skin = "Body_School_uniform", tier = Medium,
            },
        },
    };

    public static readonly Girl Shield = new Girl
    {
        id = "ss",
        hairName = "Knight",
        sourcePrefab = "Assets/CombatGirlsCharacterPack/CombatGirl_Shield/Prefab/SwordShieldGirl_FullBody.prefab",
        hairObject = "SwordAndShiled_Hair",
        hairFormat = "Shield_Sword_Hair_{0:00}",
        shownFace = "Face_04",
        glassesObject = "",
        defaultOutfit = "squire",
        itemsFolder = "Assets/Database/Resources/ItemDatabase/Armor/ShieldGirl",
        outfits = new[]
        {
            new Outfit
            {
                groupObject = "SwordAndShiled_Cloth", optionId = "armour", displayName = "Knight Armour", description = "Plate over cloth.",
                body = new[] { "SwordAndShiled_Cloth_Top", "SwordAndShiled_Clotth_Pauldrons" }, legs = new[] { "SwordAndShiled_Cloth_Skirt" },
                feet = new[] { "SwordAndShiled_Cloth_Boots" }, skin = "Body_Cloth", tier = Heavy,
            },
            new Outfit
            {
                groupObject = "Squire_Cloth", optionId = "squire", displayName = "Squire", description = "A squire's plain clothes.",
                body = new[] { "Squire_Cloth" }, skin = "Body_Squire",
            },
        },
    };

    // ── Dressing a model ──────────────────────────────────────────────────

    class OptionSpec
    {
        public string id;
        public string name;
        public GameObject[] show = new GameObject[0];
        public List<(Renderer renderer, int slot, Material material)> swaps = new List<(Renderer, int, Material)>();
    }

    class GroupSpec
    {
        public string id;
        public string name;
        public bool chosenAtCreation;
        public string unequippedOptionId = "";
        public List<OptionSpec> options = new List<OptionSpec>();
    }

    class SkinSpec
    {
        public Transform show;
        public List<(string group, string option)> whenWearing = new List<(string, string)>();
    }

    // Writes the model's ModelAppearance: the body, legs and feet pieces of her outfits and the other girl's,
    // their skins, both hairs, hair colour, and her glasses if she has them. Grafts the other girl's parts first.
    // Face fixed.
    public static void Dress(GameObject root, GameObject visual, Girl own, Dictionary<Material, Material> ownToon,
                             Girl other, Dictionary<Material, Material> otherToon)
    {
        Transform t = visual.transform;
        GameObject donor = SpawnDonor(other, otherToon);

        Outfit unequipped = own.outfits.First(o => o.optionId == own.defaultOutfit);
        List<GroupSpec> pieces = Slots.Select(slot => new GroupSpec
        {
            id = slot.group,
            name = slot.name,
            unequippedOptionId = unequipped.Pieces(slot.group).Length > 0 ? unequipped.optionId : NoneOption,
            options = { Show(NoneOption, "None") },
        }).ToList();
        var skins = new List<SkinSpec>();

        foreach (Outfit outfit in own.outfits)
            AddOutfit(pieces, skins, KatanaGirlBuilder.FindDeep(t, outfit.groupObject), outfit);
        foreach (Outfit outfit in other.outfits)
            AddOutfit(pieces, skins, Graft(donor, outfit.groupObject, t, otherToon), outfit);

        Transform ownHair = KatanaGirlBuilder.FindDeep(t, own.hairObject);
        Transform otherHair = Graft(donor, other.hairObject, t, otherToon);

        var groups = new List<GroupSpec>(pieces)
        {
            HairStyles(own, ownHair, other, otherHair),
            HairColourGroup(own, ownHair, ownToon, other, otherHair, otherToon),
        };

        Transform glasses = string.IsNullOrEmpty(own.glassesObject) ? null : KatanaGirlBuilder.FindDeep(t, own.glassesObject);
        if (glasses != null)
            groups.Add(new GroupSpec { id = "glasses", name = "Glasses", unequippedOptionId = "off", options = { Show("off", "None"), Show("on", "Glasses", glasses) } });

        LinkCloth(t, groups);
        ShowOnly(t, own.shownFace, "Face_0");
        if (donor != null) Object.DestroyImmediate(donor);

        var parts = root.AddComponent<ModelAppearance>();
        WriteGroups(parts, groups);
        WriteSkins(parts, skins, KatanaGirlBuilder.FindDeep(t, FullSkin));
        parts.ApplyDefaults();
        Log($"{root.name}: {string.Join(", ", pieces.Select(p => $"{p.options.Count - 1} {p.id}"))} pieces, {skins.Count} skins, hair {own.hairName} / {(otherHair != null ? other.hairName : "(other girl missing)")}, face {own.shownFace}.");
    }

    // The other girl, unpacked, with her FlatToon materials: the parts are copied out of her, then she goes.
    static GameObject SpawnDonor(Girl girl, Dictionary<Material, Material> toon)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(girl.sourcePrefab);
        if (prefab == null || toon.Count == 0)
        {
            Debug.LogWarning($"[CombatGirlsWardrobe] {girl.sourcePrefab} (or its materials) missing; her outfits and hair aren't shared.");
            return null;
        }

        var donor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(donor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return donor;
    }

    // Each slot the pack has meshes for becomes an option in that slot's group; the trimmed skin is shown only
    // while the whole outfit is worn, and a slot it has no piece for counts as worn when that slot is empty.
    static void AddOutfit(List<GroupSpec> pieces, List<SkinSpec> skins, Transform outfitObject, Outfit outfit)
    {
        if (outfitObject == null)
        {
            Debug.LogError($"[CombatGirlsWardrobe] Outfit object '{outfit.groupObject}' not found.");
            return;
        }

        // Some pack outfits ship with their meshes switched off (Squire); the pieces decide now.
        foreach (Transform child in outfitObject.GetComponentsInChildren<Transform>(true))
            child.gameObject.SetActive(true);

        var skin = new SkinSpec { show = FindPiece(outfitObject, outfit.skin) };
        foreach (GroupSpec group in pieces)
        {
            Transform[] meshes = outfit.Pieces(group.id).Select(n => FindPiece(outfitObject, n)).Where(m => m != null).ToArray();
            if (meshes.Length > 0) group.options.Add(Show(outfit.optionId, outfit.displayName, meshes));
            skin.whenWearing.Add((group.id, meshes.Length > 0 ? outfit.optionId : NoneOption));
        }

        if (skin.show != null) skins.Add(skin);
        else Debug.LogError($"[CombatGirlsWardrobe] Skin '{outfit.skin}' not found under '{outfit.groupObject}'.");
    }

    // A named mesh inside the outfit object, never the object itself (the Squire's mesh shares its name).
    static Transform FindPiece(Transform outfitObject, string name)
    {
        foreach (Transform t in outfitObject.GetComponentsInChildren<Transform>(true))
            if (t != outfitObject && t.name == name) return t;

        return null;
    }

    static GroupSpec HairStyles(Girl own, Transform ownHair, Girl other, Transform otherHair)
    {
        var group = new GroupSpec { id = "hair_style", name = "Hair style", chosenAtCreation = true };
        group.options.Add(Show(own.id, own.hairName, ownHair));
        if (otherHair != null) group.options.Add(Show(other.id, other.hairName, otherHair));
        return group;
    }

    // Colour n recolours both hairs, so it carries over when the style changes.
    static GroupSpec HairColourGroup(Girl own, Transform ownHair, Dictionary<Material, Material> ownToon,
                                     Girl other, Transform otherHair, Dictionary<Material, Material> otherToon)
    {
        var group = new GroupSpec { id = "hair_colour", name = "Hair colour", chosenAtCreation = true };
        List<(Renderer, int)> ownSlots = SlotsWearing(ownHair, ToonByName(ownToon, string.Format(own.hairFormat, 1)));
        List<(Renderer, int)> otherSlots = otherHair != null
            ? SlotsWearing(otherHair, ToonByName(otherToon, string.Format(other.hairFormat, 1)))
            : new List<(Renderer, int)>();

        for (int n = 1; n <= HairColours; n++)
        {
            var option = new OptionSpec { id = n.ToString(), name = $"Colour {n}" };
            AddSwaps(option, ownSlots, ToonByName(ownToon, string.Format(own.hairFormat, n)));
            if (otherHair != null) AddSwaps(option, otherSlots, ToonByName(otherToon, string.Format(other.hairFormat, n)));
            group.options.Add(option);
        }

        return group;
    }

    static void AddSwaps(OptionSpec option, List<(Renderer, int)> slots, Material material)
    {
        foreach ((Renderer renderer, int slot) in slots) option.swaps.Add((renderer, slot, material));
    }

    // Every material slot under scope wearing this material.
    static List<(Renderer, int)> SlotsWearing(Transform scope, Material material)
    {
        var slots = new List<(Renderer, int)>();
        if (scope == null || material == null) return slots;

        foreach (Renderer renderer in scope.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] == material) slots.Add((renderer, i));
        }

        return slots;
    }

    // ── Grafting ──────────────────────────────────────────────────────────

    /// <summary>Copies the donor's object onto the target model: skinned to the target's bones by name,
    /// with any bone the target lacks copied across under the same-named parent.</summary>
    static Transform Graft(GameObject donor, string objectName, Transform target, Dictionary<Material, Material> toon)
    {
        if (donor == null) return null;

        Transform source = KatanaGirlBuilder.FindDeep(donor.transform, objectName);
        Transform targetRoot = KatanaGirlBuilder.FindDeep(target, "root");
        if (source == null || targetRoot == null)
        {
            Debug.LogError($"[CombatGirlsWardrobe] Graft: '{objectName}' on the donor or 'root' on {target.name} not found.");
            return null;
        }

        GameObject copy = Object.Instantiate(source.gameObject, target, false);
        copy.name = source.name;

        // Everything on the target by name, the copy included, so the cloth finds the grafted renderer.
        Dictionary<string, Transform> bones = target.GetComponentsInChildren<Transform>(true)
            .GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
        int before = bones.Count;

        foreach (SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.bones = skin.bones.Select(b => MatchBone(b, bones, donor.transform, targetRoot)).ToArray();
            skin.rootBone = MatchBone(skin.rootBone, bones, donor.transform, targetRoot);
            skin.sharedMaterials = skin.sharedMaterials.Select(m => m != null && toon.TryGetValue(m, out Material mapped) ? mapped : m).ToArray();
        }

        foreach (MonoBehaviour cloth in ClothsOf(donor.transform).Where(c => Owns(c, source)))
            CarryCloth(cloth, objectName, bones, donor.transform, targetRoot);

        if (bones.Count > before) Log($"graft: {objectName} onto {target.root.name}, {bones.Count - before} bone(s) or collider(s) copied across.");
        return copy.transform;
    }

    // The donor's cloth object, copied beside the target's own and pointed at the target's renderers, bones and
    // colliders. Magica's saved start pose is dropped so it reads the pose from the model instead.
    static void CarryCloth(MonoBehaviour cloth, string partName, Dictionary<string, Transform> bones, Transform donorTop, Transform targetRoot)
    {
        Transform holder = MatchBone(cloth.transform.parent, bones, donorTop, targetRoot);
        GameObject copy = Object.Instantiate(cloth.gameObject, holder, false);
        copy.name = $"{cloth.name} [{partName}]";

        var so = new SerializedObject(copy.GetComponent(cloth.GetType()));
        SerializedProperty property = so.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            property.objectReferenceValue = Moved(property.objectReferenceValue, bones, donorTop, targetRoot);
        }

        SerializedProperty initHash = so.FindProperty("serializeData2.initData.localHash");
        if (initHash != null) initHash.intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A reference into the donor, moved to the target's object of the same name (copied across if missing).
    static Object Moved(Object value, Dictionary<string, Transform> bones, Transform donorTop, Transform targetRoot)
    {
        if (value is GameObject go && go.transform.IsChildOf(donorTop)) return MatchBone(go.transform, bones, donorTop, targetRoot).gameObject;
        if (value is Component c && c.transform.IsChildOf(donorTop)) return MatchBone(c.transform, bones, donorTop, targetRoot).GetComponent(c.GetType());
        return value;
    }

    static IEnumerable<MonoBehaviour> ClothsOf(Transform scope)
    {
        return scope.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null && b.GetType().Name == "MagicaCloth");
    }

    // A mesh cloth moves a renderer under the part; a bone cloth moves bones the part's meshes are skinned to.
    static bool Owns(MonoBehaviour cloth, Transform part)
    {
        var so = new SerializedObject(cloth);
        if (Refs(so.FindProperty("serializeData.sourceRenderers")).Any(r => r.IsChildOf(part))) return true;

        Transform[] skinBones = part.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .SelectMany(s => s.bones).Where(b => b != null).ToArray();
        return Refs(so.FindProperty("serializeData.rootBones")).Any(root => skinBones.Any(b => b.IsChildOf(root)));
    }

    static IEnumerable<Transform> Refs(SerializedProperty list)
    {
        if (list == null || !list.isArray) yield break;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is Component c) yield return c.transform;
    }

    static bool IsMagicaCollider(Transform t)
    {
        return t.GetComponents<MonoBehaviour>().Any(b => b != null && b.GetType().Name.StartsWith("Magica") && b.GetType().Name.EndsWith("Collider"));
    }

    // Each cloth joins the show list of every option showing what it moves.
    static void LinkCloth(Transform t, List<GroupSpec> groups)
    {
        foreach (MonoBehaviour cloth in ClothsOf(t))
            foreach (OptionSpec option in groups.SelectMany(g => g.options).Where(o => o.show.Any(s => Owns(cloth, s.transform))))
                option.show = option.show.Append(cloth.gameObject).ToArray();
    }

    // The target's bone of the same name, or a copy of the donor's placed under its matched parent.
    static Transform MatchBone(Transform bone, Dictionary<string, Transform> bones, Transform donorTop, Transform targetRoot)
    {
        if (bone == null) return null;
        if (bones.TryGetValue(bone.name, out Transform found)) return found;
        if (bone.parent == null || bone.parent == donorTop) return targetRoot;

        Transform parent = MatchBone(bone.parent, bones, donorTop, targetRoot);
        Transform clone = IsMagicaCollider(bone) ? Object.Instantiate(bone.gameObject).transform : new GameObject().transform;
        clone.name = bone.name;
        clone.SetParent(parent, false);
        clone.localPosition = bone.localPosition;
        clone.localRotation = bone.localRotation;
        clone.localScale = bone.localScale;

        bones[bone.name] = clone;
        return clone;
    }

    // ── Outfit items ──────────────────────────────────────────────────────

    // Her outfits as armour pieces, one item per slot the outfit has a mesh for. Made from that tier's set piece
    // (slot, stats, load) once, then only id, name, description and look are rewritten, so tuned stats survive a
    // rebuild. Light footwear has no set piece of its own: it copies the Medium Boots' slot with no stats.
    // Items in her folder that are no longer built (the old whole-outfit and colour items) are deleted.
    public static List<ItemDefinition> BuildOutfitItems(Girl girl)
    {
        var items = new List<ItemDefinition>();
        KatanaGirlBuilder.EnsureFolder(girl.itemsFolder);

        foreach (Outfit outfit in girl.outfits.Where(o => o.tier != ""))
            foreach (var slot in Slots.Where(s => outfit.Pieces(s.group).Length > 0))
                items.Add(BuildPieceItem(girl, outfit, slot.group, slot.label));

        items.RemoveAll(i => i == null);
        int deleted = DeleteStaleItems(girl.itemsFolder, items);
        Log($"outfits: {items.Count} armour pieces in {girl.itemsFolder}; {deleted} old outfit items deleted.");
        return items;
    }

    static ItemDefinition BuildPieceItem(Girl girl, Outfit outfit, string slot, string label)
    {
        string templatePath = ArmourSets + SetPiece(outfit.tier, slot);
        var template = AssetDatabase.LoadAssetAtPath<ItemDefinition>(templatePath);
        if (template == null)
        {
            Debug.LogError($"[CombatGirlsWardrobe] No set piece at {templatePath}; {outfit.displayName} {label} not made.");
            return null;
        }

        string path = $"{girl.itemsFolder}/Item_{girl.id.ToUpperInvariant()}_{outfit.displayName.Replace(" ", "")}_{label}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null)
            AssetDatabase.CopyAsset(templatePath, path);

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (!item.HasTag(outfit.tier)) Retier(item, template, outfit.tier, path);

        item.itemId = $"{girl.id}_{outfit.optionId}_{slot}";
        item.displayName = $"{outfit.displayName} {label}";
        item.description = outfit.description;
        item.equippedPrefab = null;
        item.appearanceGroup = slot;
        item.appearanceOption = outfit.optionId;
        if (!item.HasTag(OutfitTag)) item.tags.Add(OutfitTag);
        EditorUtility.SetDirty(item);
        return item;
    }

    // Copied from the set piece when new or when its tier no longer matches. A piece copied from another tier
    // (light footwear) keeps the slot only.
    static void Retier(ItemDefinition item, ItemDefinition template, string tier, string path)
    {
        EditorUtility.CopySerialized(template, item);
        item.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (TierOf(template) == tier) return;

        item.stats.Clear();
        item.tags.Remove(TierOf(template));
        item.tags.Add(tier);
    }

    static string SetPiece(string tier, string slot) => (tier, slot) switch
    {
        (Light, "body") => "Item_Light_Garb.asset",
        (Light, "legs") => "Item_Light_Hakama.asset",
        (Light, "feet") => "Item_Medium_Boots.asset",
        (Medium, "body") => "Item_Medium_Cuirass.asset",
        (Medium, "legs") => "Item_Medium_Greaves.asset",
        (Medium, "feet") => "Item_Medium_Boots.asset",
        (Heavy, "body") => "Item_Heavy_Cuirass.asset",
        (Heavy, "legs") => "Item_Heavy_Greaves.asset",
        (Heavy, "feet") => "Item_Heavy_Sabatons.asset",
        _ => "",
    };

    static int DeleteStaleItems(string folder, List<ItemDefinition> keep)
    {
        var keepPaths = new HashSet<string>(keep.Select(AssetDatabase.GetAssetPath));
        string[] stale = AssetDatabase.FindAssets("t:ItemDefinition", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !keepPaths.Contains(p))
            .ToArray();

        foreach (string path in stale) AssetDatabase.DeleteAsset(path);
        return stale.Length;
    }

    static string TierOf(ItemDefinition armour)
    {
        return armour.tags.FirstOrDefault(t => t.StartsWith("armour_")) ?? "";
    }

    // ── Writing ModelAppearance ───────────────────────────────────────────

    static OptionSpec Show(string id, string name, params Transform[] show)
    {
        return new OptionSpec { id = id, name = name, show = show.Where(s => s != null).Select(s => s.gameObject).ToArray() };
    }

    // Exactly one of the objects named `prefix…` stays on.
    static void ShowOnly(Transform t, string shown, string prefix)
    {
        foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith(prefix)) child.gameObject.SetActive(child.name == shown);
    }

    static Material ToonByName(Dictionary<Material, Material> toon, string sourceName)
    {
        foreach (KeyValuePair<Material, Material> pair in toon)
            if (pair.Key.name == sourceName) return pair.Value;

        Debug.LogError($"[CombatGirlsWardrobe] No pack material named '{sourceName}'.");
        return null;
    }

    static void WriteGroups(ModelAppearance parts, List<GroupSpec> groups)
    {
        var so = new SerializedObject(parts);
        SerializedProperty groupsProp = so.FindProperty("groups");
        groupsProp.arraySize = groups.Count;

        for (int g = 0; g < groups.Count; g++)
        {
            SerializedProperty groupProp = groupsProp.GetArrayElementAtIndex(g);
            groupProp.FindPropertyRelative("groupId").stringValue = groups[g].id;
            groupProp.FindPropertyRelative("displayName").stringValue = groups[g].name;
            groupProp.FindPropertyRelative("chosenAtCreation").boolValue = groups[g].chosenAtCreation;
            groupProp.FindPropertyRelative("unequippedOptionId").stringValue = groups[g].unequippedOptionId;

            SerializedProperty optionsProp = groupProp.FindPropertyRelative("options");
            optionsProp.arraySize = groups[g].options.Count;

            for (int o = 0; o < groups[g].options.Count; o++)
                WriteOption(optionsProp.GetArrayElementAtIndex(o), groups[g].options[o]);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WriteSkins(ModelAppearance parts, List<SkinSpec> skins, Transform fallback)
    {
        var so = new SerializedObject(parts);
        so.FindProperty("fallbackSkin").objectReferenceValue = fallback != null ? fallback.gameObject : null;

        SerializedProperty skinsProp = so.FindProperty("skins");
        skinsProp.arraySize = skins.Count;
        for (int i = 0; i < skins.Count; i++)
        {
            SerializedProperty skinProp = skinsProp.GetArrayElementAtIndex(i);
            skinProp.FindPropertyRelative("show").objectReferenceValue = skins[i].show.gameObject;

            SerializedProperty worn = skinProp.FindPropertyRelative("whenWearing");
            worn.arraySize = skins[i].whenWearing.Count;
            for (int w = 0; w < skins[i].whenWearing.Count; w++)
            {
                worn.GetArrayElementAtIndex(w).FindPropertyRelative("groupId").stringValue = skins[i].whenWearing[w].group;
                worn.GetArrayElementAtIndex(w).FindPropertyRelative("optionId").stringValue = skins[i].whenWearing[w].option;
            }
        }

        if (fallback == null) Debug.LogError($"[CombatGirlsWardrobe] No '{FullSkin}' on {parts.name}; a mixed outfit will show no skin.");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WriteOption(SerializedProperty optionProp, OptionSpec option)
    {
        optionProp.FindPropertyRelative("optionId").stringValue = option.id;
        optionProp.FindPropertyRelative("displayName").stringValue = option.name;

        SerializedProperty show = optionProp.FindPropertyRelative("show");
        show.arraySize = option.show.Length;
        for (int i = 0; i < option.show.Length; i++)
            show.GetArrayElementAtIndex(i).objectReferenceValue = option.show[i];

        SerializedProperty swaps = optionProp.FindPropertyRelative("swaps");
        swaps.arraySize = option.swaps.Count;
        for (int i = 0; i < option.swaps.Count; i++)
        {
            SerializedProperty swap = swaps.GetArrayElementAtIndex(i);
            swap.FindPropertyRelative("renderer").objectReferenceValue = option.swaps[i].renderer;
            swap.FindPropertyRelative("slot").intValue = option.swaps[i].slot;
            swap.FindPropertyRelative("material").objectReferenceValue = option.swaps[i].material;
        }
    }

    static void Log(string message) => Debug.Log("[CombatGirlsWardrobe] " + message);
}
