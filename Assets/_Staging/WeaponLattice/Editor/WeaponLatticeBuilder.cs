using System.Collections.Generic;
using CrabSystem;
using CrabSystem.Progression;
using UnityEditor;
using UnityEngine;

// Tools > Crab > Weapons > Build Demo Nine. Staging: graduates with the weapon lattice.
//
// Writes the demo nine (Claude Doc "Weapon Lattice — Demo Nine", 13 Nov 2026) and the weapon modifiers they use
// (Weapon_Modifiers.md). Re-running overwrites the same assets, so the tables below are where the numbers live until
// they're tuned by hand. Four passes:
//   Statuses: Slowed, Sapped, Bleeding, Frightened, Ignited, Sworn Foe (Resources/Statuses/Weapon). Topple uses Prone.
//   Traits:   one HitTraitReward per modifier (ItemDatabase/Weapons/Traits) — masteries and the magic lines.
//   Weapons:  dice, reach, stats (Deflection, parry/riposte windows, strike arc, guard pressure, equip load), facts
//             (row, column, NoParry, StunThrough on Break) and trait rewards. Break sits ~15% over the katana's damage
//             per frame (Crxb, 13 Nov) and pays it back (Crxb, 13 Nov night): +2 stamina a swing, +1 equip load and
//             -4 frames on block. Its +6 recovery frames belong to the Break moveset's clips (frames are baked), so
//             they land when Break gets its own moveset. The Steel Katana and Knight's Axe are updated in place; every weapon gets its own
//             dice, because the Axe shared Dice_Katana. New weapons borrow the katana moveset and a stand-in mesh.
//   Modules:  WeaponTraitModule goes on every character prefab under Assets/Database that has a DamageSystem.

public static class WeaponLatticeBuilder
{
    private const string DatabaseRoot = "Assets/Database";
    private const string WeaponsRoot = "Assets/Database/Resources/ItemDatabase/Weapons";
    private const string Folder = WeaponsRoot + "/Lattice";
    private const string TraitFolder = WeaponsRoot + "/Traits";
    private const string StatusFolder = "Assets/Database/Resources/Statuses/Weapon";
    private const string PronePath = "Assets/Database/Resources/Statuses/HitStates/Status_Prone.asset";
    private const string GuardBrokenPath = "Assets/Database/Resources/Statuses/HitStates/Status_GuardBroken.asset";
    private const string KatanaPath = WeaponsRoot + "/Bladed/Item_SteelKatana.asset";
    private const string AxePath = WeaponsRoot + "/ShieldGirl/Item_Weapon_SS_Axe.asset";
    private const string EquipLoad = "equip.load";
    private const string StaminaPath = "Assets/Database/Config/ResourceDefinitions/StaminaDefinition.asset";

    private class Sheet
    {
        public string file;
        public string itemId;
        public string name;
        public string culture;
        public string row;
        public string column;
        public int count;
        public int faces;
        public float flat;
        public float reach;
        public float arc;
        public float deflection;
        public float parry;
        public float riposte;
        public float pressure;
        public float load;
        public float blockFrames;
        public float swingStamina;
        public bool noParry;
        public string[] traits = new string[0];
        public string existingPath;
    }

    private static readonly Sheet[] Sheets =
    {
        new Sheet { file = "Tanto", itemId = "tanto", name = "Tanto", culture = "Elven", row = "Light", column = "Edge",
            count = 1, faces = 6, flat = 1, reach = 1f, deflection = -3, parry = 2, riposte = 0.25f },
        new Sheet { file = "Kusarigama", itemId = "kusarigama", name = "Kusarigama", culture = "Elven", row = "Light", column = "Reach",
            count = 1, faces = 6, flat = 1, reach = 3f, arc = -30, deflection = -3, parry = -4, traits = new[] { "Pull" } },
        new Sheet { file = "FlangedMace", itemId = "flanged_mace", name = "Flanged Mace", culture = "Human", row = "Light", column = "Break",
            count = 2, faces = 4, reach = 1f, deflection = -1, pressure = 3, load = 1, blockFrames = -4, swingStamina = 2, traits = new[] { "Adamantine" } },
        new Sheet { file = "SteelKatana", itemId = "steel_katana", name = "Steel Katana", culture = "Elven", row = "Medium", column = "Edge",
            count = 1, faces = 8, flat = 2, reach = 1.5f, deflection = -2, parry = 2, riposte = 0.25f, load = 1, existingPath = KatanaPath },
        new Sheet { file = "BoarSpear", itemId = "boar_spear", name = "Boar Spear", culture = "Human", row = "Medium", column = "Reach",
            count = 1, faces = 8, flat = 2, reach = 3.5f, arc = -30, deflection = -2, parry = -4, load = 1, traits = new[] { "Push" } },
        new Sheet { file = "KnightsAxe", itemId = "ss_axe", name = "Knight's Axe", culture = "Human", row = "Medium", column = "Break",
            count = 1, faces = 8, flat = 3, reach = 1.5f, pressure = 2, load = 2, blockFrames = -4, swingStamina = 2, traits = new[] { "Adamantine" }, existingPath = AxePath },
        new Sheet { file = "Odachi", itemId = "odachi", name = "Ōdachi", culture = "Elven", row = "Heavy", column = "Edge",
            count = 1, faces = 10, flat = 4, reach = 1.5f, arc = 30, parry = -2, riposte = 0.25f, load = 2, traits = new[] { "Topple" } },
        new Sheet { file = "Halberd", itemId = "halberd", name = "Halberd", culture = "Human", row = "Heavy", column = "Reach",
            count = 2, faces = 6, flat = 3, reach = 3.5f, arc = -30, load = 2, noParry = true, traits = new[] { "Push", "Topple" } },
        new Sheet { file = "Maul", itemId = "maul", name = "Maul", culture = "Human", row = "Heavy", column = "Break",
            count = 3, faces = 4, flat = 4, reach = 1.5f, deflection = 2, parry = -4, pressure = 2, load = 3, blockFrames = -4, swingStamina = 2, traits = new[] { "Adamantine", "Topple" } },
    };

    [MenuItem("Tools/Crab/Weapons/Build Demo Nine")]
    public static void Build()
    {
        ItemDefinition katana = AssetDatabase.LoadAssetAtPath<ItemDefinition>(KatanaPath);
        ItemDefinition axe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AxePath);
        StatusDefinition prone = AssetDatabase.LoadAssetAtPath<StatusDefinition>(PronePath);
        StatusDefinition guardBroken = AssetDatabase.LoadAssetAtPath<StatusDefinition>(GuardBrokenPath);
        if (katana == null || axe == null || prone == null || guardBroken == null)
        {
            Debug.LogError("[WeaponLatticeBuilder] Missing a template: the Steel Katana, Knight's Axe, Prone or Guard Broken asset.");
            return;
        }

        EnsureFolder(Folder);
        EnsureFolder(TraitFolder);
        EnsureFolder(StatusFolder);

        Dictionary<string, StatusDefinition> statuses = BuildStatuses();
        Dictionary<string, HitTraitReward> traits = BuildTraits(statuses, prone, guardBroken);

        foreach (Sheet sheet in Sheets)
        {
            ItemDefinition template = sheet.column == "Break" ? axe : katana;
            WriteWeapon(sheet, template, traits);
        }

        AssetDatabase.SaveAssets();
        int prefabs = AddTraitModules();
        Debug.Log($"[WeaponLatticeBuilder] {Sheets.Length} weapons, {traits.Count} traits, {statuses.Count} statuses; WeaponTraitModule added to {prefabs} prefabs.");
    }

    // ---- Statuses ----

    private static Dictionary<string, StatusDefinition> BuildStatuses()
    {
        var statuses = new Dictionary<string, StatusDefinition>();

        StatusDefinition slowed = Status("Slowed", "slowed", "Slowed", "Moves slower for a moment.", 1f, false);
        slowed.control = ControlCategory.Snare;
        slowed.contributions = new List<StatContribution>
        {
            new StatContribution { statId = DialIds.WalkSpeed, amountPerStack = -1f },
            new StatContribution { statId = DialIds.RunSpeed, amountPerStack = -2f },
            new StatContribution { statId = DialIds.SprintSpeed, amountPerStack = -3f },
        };
        statuses["Slowed"] = slowed;

        StatusDefinition sapped = Status("Sapped", "sapped", "Sapped", "Your attacks roll with disadvantage.", 3f, false);
        sapped.blackboardFlags = new List<StatusFlag> { new StatusFlag { fact = "Disadvantaged", minStacks = 1 } };
        statuses["Sapped"] = sapped;

        StatusDefinition bleeding = Status("Bleeding", "bleeding", "Bleeding", "Loses 1 health a second and can't regenerate.", 4f, false);
        bleeding.tickSeconds = 1f;
        bleeding.tickDealsDamage = true;
        bleeding.tickDamage = new DiceRoll { diceCount = 1, diceFaces = 1 };
        bleeding.tickDamageType = DamageTypeIds.Physical;
        bleeding.contributions = new List<StatContribution> { new StatContribution { statId = DialIds.HealthRegenDelay, amountPerStack = 999f } };
        statuses["Bleeding"] = bleeding;

        StatusDefinition frightened = Status("Frightened", "frightened", "Frightened", "Can't attack.", 2f, false);
        frightened.save = CrabSystem.SaveType.Alteration;
        frightened.control = ControlCategory.LossOfControl;
        frightened.blackboardFlags = new List<StatusFlag> { new StatusFlag { fact = "CannotAttack", minStacks = 1 } };
        statuses["Frightened"] = frightened;

        statuses["Ignited"] = Status("Ignited", "ignited", "Ignited", "Your hits add 1d4 fire.", 10f, true);
        statuses["SwornFoe"] = Status("SwornFoe", "sworn_foe", "Sworn Foe", "Marked: the one who swore has advantage and +1d4 against you.", 10f, false);

        foreach (StatusDefinition status in statuses.Values) EditorUtility.SetDirty(status);
        return statuses;
    }

    private static StatusDefinition Status(string file, string id, string displayName, string description, float seconds, bool beneficial)
    {
        StatusDefinition status = LoadOrCreate<StatusDefinition>($"{StatusFolder}/Status_{file}.asset");
        status.id = id;
        status.displayName = displayName;
        status.description = description;
        status.duration = seconds;
        status.durationFrames = 0;
        status.beneficial = beneficial;
        status.stacking = StatusStacking.Refresh;
        status.maxStacks = 1;
        return status;
    }

    // ---- Traits ----

    private static Dictionary<string, HitTraitReward> BuildTraits(Dictionary<string, StatusDefinition> statuses, StatusDefinition prone, StatusDefinition guardBroken)
    {
        var traits = new Dictionary<string, HitTraitReward>();

        // Masteries (§3) and lattice modifiers.
        Trait(traits, "Push", "Push: full hits knock back").knockback = 8f;
        Trait(traits, "Pull", "Pull: full hits draw the target in").knockback = -6f;
        Trait(traits, "Slow", "Slow: full hits Slow for 1 s").applyStatus = statuses["Slowed"];
        Trait(traits, "Sap", "Sap: full hits Sap for 3 s").applyStatus = statuses["Sapped"];
        Trait(traits, "Topple", "Topple: full hits knock down (Toughness save)").applyStatus = prone;
        Trait(traits, "Vex", "Vex: your next hit on them within 2 s has advantage").vexSeconds = 2f;

        HitTrait graze = Trait(traits, "Graze", "Graze: glancing hits add +1");
        graze.gate = HitTraitGate.GlancingHit;
        graze.extraFlat = 1f;

        HitTrait adamantine = Trait(traits, "Adamantine", "Adamantine: shields you hit refill worse");
        adamantine.gate = HitTraitGate.AnyHit;
        adamantine.onShield = true;
        adamantine.impairShield = true;

        // Named-item lines (§4).
        HitTrait rime = Trait(traits, "Rime", "Rime: full hits, 15+: +1d6 ice and Slow");
        rime.dc = 15;
        rime.extraDice = 1;
        rime.extraDieFaces = 6;
        rime.extraDamageType = "ice";
        rime.applyStatus = statuses["Slowed"];

        HitTrait highRoll = Trait(traits, "HighRoll", "High Roll: a natural 20 adds +2d6");
        highRoll.gate = HitTraitGate.AnyHit;
        highRoll.naturalTwenty = true;
        highRoll.extraDice = 2;
        highRoll.extraDieFaces = 6;

        HitTrait headsman = Trait(traits, "Headsman", "Headsman: +1d8 on a guard-broken target");
        headsman.targetStatus = guardBroken;
        headsman.bonusDice = 1;
        headsman.bonusDieFaces = 8;

        HitTrait wounding = Trait(traits, "Wounding", "Wounding: full hits, 11+: Bleed");
        wounding.dc = 11;
        wounding.applyStatus = statuses["Bleeding"];

        HitTrait lifeStealing = Trait(traits, "LifeStealing", "Life Stealing: full hits, 18+: heal 1d6");
        lifeStealing.dc = 18;
        lifeStealing.healDice = 1;
        lifeStealing.healDieFaces = 6;

        HitTrait ignite = Trait(traits, "Ignite", "Ignite: while Ignited, hits add +1d4 fire");
        ignite.gate = HitTraitGate.AnyHit;
        ignite.selfStatus = statuses["Ignited"];
        ignite.extraDice = 1;
        ignite.extraDieFaces = 4;
        ignite.extraDamageType = "fire";

        HitTrait disruption = Trait(traits, "Disruption", "Disruption: full hits Frighten (Alteration save)");
        disruption.dc = 15;
        disruption.applyStatus = statuses["Frightened"];

        HitTrait swornFoe = Trait(traits, "SwornFoe", "Sworn Foe: advantage and +1d4 against your mark");
        swornFoe.targetStatus = statuses["SwornFoe"];
        swornFoe.advantage = true;
        swornFoe.bonusDice = 1;
        swornFoe.bonusDieFaces = 4;

        foreach (HitTraitReward reward in traits.Values) EditorUtility.SetDirty(reward);
        return traits;
    }

    // Each trait starts from defaults, so a re-run never keeps a field the table no longer sets.
    private static HitTrait Trait(Dictionary<string, HitTraitReward> traits, string file, string displayName)
    {
        HitTraitReward reward = LoadOrCreate<HitTraitReward>($"{TraitFolder}/Trait_{file}.asset");
        var serialized = new SerializedObject(reward);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        traits[file] = reward;
        ResetTrait(reward.Trait);
        return reward.Trait;
    }

    private static void ResetTrait(HitTrait trait)
    {
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(new HitTrait()), trait);
    }

    // ---- Weapons ----

    private static void WriteWeapon(Sheet sheet, ItemDefinition template, Dictionary<string, HitTraitReward> traits)
    {
        ItemDefinition item = sheet.existingPath != null
            ? AssetDatabase.LoadAssetAtPath<ItemDefinition>(sheet.existingPath)
            : LoadOrCreate<ItemDefinition>($"{Folder}/Item_{sheet.file}.asset");

        if (sheet.existingPath == null) CopyFrom(template, item, sheet);

        item.weaponData = Dice(sheet);
        item.reach = sheet.reach;
        item.stats = Stats(sheet);
        item.facts = Facts(sheet);
        item.rewards = Rewards(sheet, traits);
        EditorUtility.SetDirty(item);
    }

    private static void CopyFrom(ItemDefinition template, ItemDefinition item, Sheet sheet)
    {
        item.itemId = sheet.itemId;
        item.displayName = sheet.name;
        item.description = $"{sheet.culture} {sheet.row} {sheet.column} weapon (demo nine).";
        item.icon = template.icon;
        item.equippedPrefab = template.equippedPrefab;
        item.category = template.category;
        item.subType = template.subType;
        item.moveset = template.moveset;
        item.gridWidth = template.gridWidth;
        item.gridHeight = template.gridHeight;
        item.baseValue = template.baseValue;
        item.tags = new List<string> { "Weapon", sheet.row, sheet.column, sheet.culture };
    }

    private static DiceProfile Dice(Sheet sheet)
    {
        DiceProfile dice = LoadOrCreate<DiceProfile>($"{Folder}/Dice_{sheet.file}.asset");
        dice.weaponName = sheet.name;
        dice.damageDice = new DiceRoll { diceCount = sheet.count, diceFaces = sheet.faces };
        dice.flatBonus = sheet.flat;
        EditorUtility.SetDirty(dice);
        return dice;
    }

    private static List<ItemStatBonus> Stats(Sheet sheet)
    {
        var stats = new List<ItemStatBonus>();
        AddStat(stats, StatIds.Deflection, sheet.deflection);
        AddStat(stats, DialIds.ParryWindow, sheet.parry);
        AddStat(stats, DialIds.RiposteWindow, sheet.riposte);
        AddStat(stats, DialIds.GuardPressure, sheet.pressure);
        AddStat(stats, DialIds.StrikeArc, sheet.arc);
        AddStat(stats, EquipLoad, sheet.load);
        AddStat(stats, DialIds.BlockAdvantage, sheet.blockFrames);
        return stats;
    }

    private static void AddStat(List<ItemStatBonus> stats, string statId, float amount)
    {
        if (amount == 0f) return;
        stats.Add(new ItemStatBonus { statId = statId, amount = amount });
    }

    private static List<string> Facts(Sheet sheet)
    {
        var facts = new List<string> { "Wielding" + sheet.row, "Wielding" + sheet.column };
        if (sheet.noParry) facts.Add("NoParry");
        if (sheet.column == "Break") facts.Add("StunThrough");
        return facts;
    }

    private static List<Reward> Rewards(Sheet sheet, Dictionary<string, HitTraitReward> traits)
    {
        var rewards = new List<Reward>();
        foreach (string name in sheet.traits) rewards.Add(traits[name]);
        if (sheet.swingStamina != 0f) rewards.Add(SwingStamina(sheet.swingStamina));
        return rewards;
    }

    // Physical attacks cost more stamina while the weapon is equipped (the same filter as the burden track's).
    private static AbilityModifierReward SwingStamina(float extra)
    {
        AbilityModifierReward reward = LoadOrCreate<AbilityModifierReward>($"{Folder}/Reward_SwingStamina_{extra}.asset");
        var serialized = new SerializedObject(reward);
        serialized.FindProperty("filter.matchType").boolValue = true;
        serialized.FindProperty("filter.type").enumValueIndex = (int)AbilityType.Offensive;
        serialized.FindProperty("filter.matchCategory").boolValue = true;
        serialized.FindProperty("filter.category").enumValueIndex = (int)AbilityCategory.Physical;
        serialized.FindProperty("resource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(StaminaPath);
        serialized.FindProperty("costDelta").floatValue = extra;
        serialized.FindProperty("perRank").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    // ---- Modules ----

    // Added where the DamageSystem is, on prefabs that own it; a variant inherits it from its base.
    private static int AddTraitModules()
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { DatabaseRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            DamageSystem damage = root.GetComponentInChildren<DamageSystem>(true);

            bool owns = damage != null && PrefabUtility.GetCorrespondingObjectFromSource(damage) == null;
            if (owns && damage.GetComponent<WeaponTraitModule>() == null)
            {
                damage.gameObject.AddComponent<WeaponTraitModule>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                added++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return added;
    }

    // ---- Assets ----

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
