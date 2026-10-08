using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Creates a weapon: a DiceProfile and the ItemDefinition that ties it to a subtype, and so to an
// equipment slot.
//
// The chain an equippable item has to satisfy is:
//   ItemDefinition.subType -> ItemSubType.equipmentSlot -> EquipmentSlotDefinition.slotId
// and this refuses to write until that resolves, because a broken link there produces an asset
// that looks authored, loads fine, and simply never equips.
//
// There is no weapon type, two-handed flag or block/parry here because the code does not read any
// such thing — a weapon is dice, a reach and a mesh. See the dev insight note on that cleanup.
public class WeaponWizard : EditorWindow
{
    // The real database root. The bulk generator in CrabSystem/Editor still points at
    // Assets/CrabSystem/.../Resources/ItemDatabase, a folder that does not exist — see the README.
    const string DatabaseRoot = "Assets/Database/Resources/ItemDatabase";

    string displayName = "";
    string itemId = "";
    string description = "";
    string subFolder = "Weapons/Bladed";

    Sprite icon;
    GameObject equippedPrefab;

    string slotId = "mainwep";
    ItemSubType subType;

    int gridWidth = 1;
    int gridHeight = 3;

    int diceCount = 1;
    int diceFaces = 8;
    float flatBonus;
    float reach = 1.5f;

    ItemRarity rarity = ItemRarity.Common;
    int baseValue = 100;
    string tags = "";

    Vector2 scroll;
    bool idEdited;

    [MenuItem("Tools/Crab/Wizards/Weapon…")]
    static void Open()
    {
        WeaponWizard window = GetWindow<WeaponWizard>("Weapon Wizard");
        window.minSize = new Vector2(420, 500);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        CrabWizardGUI.Title("Weapon", "Creates a DiceProfile and the ItemDefinition that binds it to a slot.");

        DrawIdentity();
        DrawClassification();
        DrawDamage();
        DrawInventory();
        DrawOutput();

        CrabWizardGUI.Issues issues = Validate();

        EditorGUILayout.Space(8);
        CrabWizardGUI.Rule();
        DrawPlan();
        CrabWizardGUI.DrawIssues(issues);

        EditorGUILayout.Space(4);

        if (CrabWizardGUI.CreateButton("Create weapon", issues))
            Create();

        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();
    }

    // ── Sections ──────────────────────────────────────────────────────────────────────────────

    void DrawIdentity()
    {
        CrabWizardGUI.Section("Identity");

        EditorGUI.BeginChangeCheck();
        displayName = EditorGUILayout.TextField("Display name", displayName);
        if (EditorGUI.EndChangeCheck() && !idEdited)
            itemId = CrabWizardGUI.ToItemId(displayName);

        EditorGUI.BeginChangeCheck();
        itemId = EditorGUILayout.TextField("Item id", itemId);
        if (EditorGUI.EndChangeCheck()) idEdited = true;

        description = EditorGUILayout.TextField("Description", description);
        icon = (Sprite)EditorGUILayout.ObjectField("Icon", icon, typeof(Sprite), false);
        equippedPrefab = (GameObject)EditorGUILayout.ObjectField("Equipped prefab", equippedPrefab, typeof(GameObject), false);
        reach = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Reach (m)", "Edge to edge from the wielder's body. 1.5 m ≈ 5 ft, a reach weapon ≈ 3 m."), reach));
    }

    void DrawClassification()
    {
        CrabWizardGUI.Section("Slot");

        string[] slots = { "mainwep", "offwep" };
        int slotIndex = Mathf.Max(0, System.Array.IndexOf(slots, slotId));

        EditorGUI.BeginChangeCheck();
        slotIndex = EditorGUILayout.Popup("Slot", slotIndex, slots);
        if (EditorGUI.EndChangeCheck())
        {
            slotId = slots[slotIndex];
            subType = null;
            ApplyGridDefault();
        }

        List<ItemSubType> candidates = SubTypesForSlot(slotId);

        if (candidates.Count == 0)
        {
            EditorGUILayout.HelpBox($"No ItemSubType asset points at slot '{slotId}'. Author one first — it is what connects an item to an equipment slot.", MessageType.Error);
            return;
        }

        string[] names = new string[candidates.Count];
        for (int i = 0; i < candidates.Count; i++) names[i] = candidates[i].name;

        int current = candidates.IndexOf(subType);
        if (current < 0) current = 0;

        current = EditorGUILayout.Popup("Sub type", current, names);
        subType = candidates[current];

        if (subType.parentCategory != null)
            EditorGUILayout.LabelField(" ", $"Category: {subType.parentCategory.name}", EditorStyles.miniLabel);
    }

    void DrawDamage()
    {
        CrabWizardGUI.Section("Damage");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Dice");
        diceCount = Mathf.Max(1, EditorGUILayout.IntField(diceCount, GUILayout.Width(40)));
        EditorGUILayout.LabelField("d", GUILayout.Width(12));
        diceFaces = Mathf.Max(2, EditorGUILayout.IntField(diceFaces, GUILayout.Width(40)));

        if (GUILayout.Button("d4", EditorStyles.miniButtonLeft)) diceFaces = 4;
        if (GUILayout.Button("d6", EditorStyles.miniButtonMid)) diceFaces = 6;
        if (GUILayout.Button("d8", EditorStyles.miniButtonMid)) diceFaces = 8;
        if (GUILayout.Button("d10", EditorStyles.miniButtonMid)) diceFaces = 10;
        if (GUILayout.Button("d12", EditorStyles.miniButtonRight)) diceFaces = 12;
        EditorGUILayout.EndHorizontal();

        flatBonus = EditorGUILayout.FloatField("Flat bonus", flatBonus);

        float min = diceCount + flatBonus;
        float max = diceCount * diceFaces + flatBonus;
        float average = diceCount * (diceFaces + 1) * 0.5f + flatBonus;
        string bonus = flatBonus == 0f ? "" : (flatBonus > 0f ? " +" : " ") + flatBonus;

        EditorGUILayout.LabelField(" ", $"{diceCount}d{diceFaces}{bonus}   →   {min} to {max}, avg {average:0.#}", EditorStyles.miniLabel);
    }

    void DrawInventory()
    {
        CrabWizardGUI.Section("Inventory");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Grid size");
        gridWidth = Mathf.Max(1, EditorGUILayout.IntField(gridWidth, GUILayout.Width(40)));
        EditorGUILayout.LabelField("x", GUILayout.Width(12));
        gridHeight = Mathf.Max(1, EditorGUILayout.IntField(gridHeight, GUILayout.Width(40)));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", rarity);
        baseValue = EditorGUILayout.IntField("Base value", baseValue);
        tags = EditorGUILayout.TextField("Tags", tags);
        EditorGUILayout.LabelField(" ", "Comma separated. Slot and rarity are added automatically.", EditorStyles.miniLabel);
    }

    void DrawOutput()
    {
        CrabWizardGUI.Section("Output");
        subFolder = EditorGUILayout.TextField("Folder under database", subFolder);
        EditorGUILayout.LabelField(" ", DatabaseRoot + "/" + subFolder, EditorStyles.miniLabel);
    }

    void DrawPlan()
    {
        string assetName = CrabWizardGUI.ToAssetName(displayName);
        string folder = TargetFolder();

        string plan = "Will create:\n" +
                      $"  {folder}/Dice_{assetName}.asset   (DiceProfile)\n" +
                      $"  {folder}/Item_{assetName}.asset   (ItemDefinition)";

        EditorGUILayout.HelpBox(plan, MessageType.None);
    }

    // ── Validation ────────────────────────────────────────────────────────────────────────────

    CrabWizardGUI.Issues Validate()
    {
        CrabWizardGUI.Issues issues = new CrabWizardGUI.Issues();

        issues.Require(!string.IsNullOrWhiteSpace(displayName), "Display name is empty.");
        issues.Require(!string.IsNullOrWhiteSpace(itemId), "Item id is empty.");
        issues.Require(subType != null, "No sub type selected — without it the item can never be equipped.");

        if (!string.IsNullOrWhiteSpace(itemId) && IdInUse(itemId, out string owner))
            issues.Error($"Item id '{itemId}' is already used by {owner}. Ids are the key ItemManager loads by; two assets sharing one means only one is ever reachable.");

        if (subType != null)
        {
            if (subType.equipmentSlot == null)
                issues.Error($"'{subType.name}' has no equipmentSlot, so nothing using it can resolve a slot.");
            else if (subType.equipmentSlot.slotId != slotId)
                issues.Error($"'{subType.name}' points at slot '{subType.equipmentSlot.slotId}', not '{slotId}'.");

            if (subType.parentCategory == null)
                issues.Error($"'{subType.name}' has no parentCategory — ItemDefinition.category would be left null.");
        }

        if (icon == null) issues.Warn("No icon. The item works but draws blank in the inventory grid.");

        if (equippedPrefab == null)
            issues.Warn("No equipped prefab. Nothing appears in the hand when this is equipped.");

        return issues;
    }

    static bool IdInUse(string id, out string owner)
    {
        foreach (ItemDefinition definition in CrabWizardGUI.LoadAll<ItemDefinition>())
        {
            if (definition.itemId != id) continue;

            owner = AssetDatabase.GetAssetPath(definition);
            return true;
        }

        owner = null;
        return false;
    }

    // ── Creation ──────────────────────────────────────────────────────────────────────────────

    void Create()
    {
        string assetName = CrabWizardGUI.ToAssetName(displayName);
        string folder = TargetFolder();
        CrabWizardGUI.EnsureFolder(folder);

        DiceProfile dice = CreateInstance<DiceProfile>();
        dice.weaponName = displayName;
        dice.damageDice = new DiceRoll { diceCount = diceCount, diceFaces = diceFaces };
        dice.flatBonus = flatBonus;
        AssetDatabase.CreateAsset(dice, AssetDatabase.GenerateUniqueAssetPath($"{folder}/Dice_{assetName}.asset"));

        ItemDefinition item = CreateInstance<ItemDefinition>();
        item.itemId = itemId;
        item.displayName = displayName;
        item.description = description;
        item.icon = icon;
        item.equippedPrefab = equippedPrefab;
        item.subType = subType;
        item.category = subType != null ? subType.parentCategory : null;
        item.gridWidth = gridWidth;
        item.gridHeight = gridHeight;
        item.weaponData = dice;
        item.reach = reach;
        item.rarity = rarity;
        item.maxStackSize = 1;
        item.baseValue = baseValue;
        item.tags = BuildTags();

        string itemPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Item_{assetName}.asset");
        AssetDatabase.CreateAsset(item, itemPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = item;
        EditorGUIUtility.PingObject(item);

        Debug.Log($"[WeaponWizard] Created '{displayName}'\n" +
                  $"  {itemPath}\n" +
                  $"  damage {diceCount}d{diceFaces}{(flatBonus != 0f ? " + " + flatBonus : "")}\n" +
                  $"  slot   {slotId} via {(subType != null ? subType.name : "?")}", item);
    }

    List<string> BuildTags()
    {
        List<string> result = new List<string>();

        foreach (string tag in tags.Split(','))
        {
            string trimmed = tag.Trim();
            if (trimmed.Length > 0 && !result.Contains(trimmed)) result.Add(trimmed);
        }

        if (!result.Contains(slotId)) result.Add(slotId);

        string rarityTag = rarity.ToString();
        if (!result.Contains(rarityTag)) result.Add(rarityTag);

        return result;
    }

    string TargetFolder()
    {
        string trimmed = (subFolder ?? "").Trim('/');
        return trimmed.Length == 0 ? DatabaseRoot : DatabaseRoot + "/" + trimmed;
    }

    void ApplyGridDefault()
    {
        gridWidth = 1;
        gridHeight = slotId == "offwep" ? 2 : 3;
    }

    static List<ItemSubType> SubTypesForSlot(string slotId)
    {
        List<ItemSubType> found = new List<ItemSubType>();

        foreach (ItemSubType subType in CrabWizardGUI.LoadAll<ItemSubType>())
        {
            if (subType.equipmentSlot == null || subType.equipmentSlot.slotId != slotId) continue;
            found.Add(subType);
        }

        return found;
    }
}
