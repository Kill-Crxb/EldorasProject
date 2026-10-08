using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Make Nature Spirit a Vendor.
// Everything her dialogue used to hand out goes on a free vendor shelf, the dialogue drops to one
// "show me" option, and her prefab gets a VendorSystem. Safe to run again: stock only grows, and
// the prefab keeps a single VendorSystem.
public static class NatureSpiritVendorBuilder
{
    public const string VendorPath = "Assets/Database/GameData/Vendors/Vendor_NatureSpirit.asset";
    const string DialoguePath = "Assets/CrabSystem/Dialogue/NatureSpiritDialogue.asset";
    const string PrefabPath = "Assets/Database/Characters/Nature Spirit/Nature_Spirit.prefab";
    const string ShopOptionText = "Show me what you have.";
    const int ShelfWidth = 12;
    const int MinShelfHeight = 6;
    const float ShelfSlack = 1.4f; // first-fit packing wastes space; leaves room to sell into too

    [MenuItem("Tools/Characters/Make Nature Spirit a Vendor")]
    static void Build()
    {
        VendorDefinition vendor = LoadOrCreateVendor();
        AddStock(vendor, ItemsFromDialogue());
        CutDialogue();
        AddToPrefab(vendor);

        AssetDatabase.SaveAssets();
        Log($"Done. {vendor.stock.Count} items on a {vendor.shelfWidth}×{vendor.shelfHeight} shelf, free trade until currencyItemId is set.");
    }

    /// <summary>Adds what isn't on the shelf yet and grows the shelf to fit. Other builders call this.</summary>
    public static void AddStock(VendorDefinition vendor, IEnumerable<ItemDefinition> items)
    {
        foreach (var item in items)
        {
            if (item == null || vendor.StockFor(item.itemId) != null) continue;
            vendor.stock.Add(new VendorStock { item = item, restocks = true });
        }

        vendor.shelfWidth = ShelfWidth;
        vendor.shelfHeight = Mathf.Max(MinShelfHeight, Mathf.CeilToInt(StockArea(vendor) * ShelfSlack / ShelfWidth));
        EditorUtility.SetDirty(vendor);
    }

    static int StockArea(VendorDefinition vendor)
    {
        int area = 0;
        foreach (var entry in vendor.stock)
            if (entry.item != null) area += entry.item.gridWidth * entry.item.gridHeight;
        return area;
    }

    static VendorDefinition LoadOrCreateVendor()
    {
        var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(VendorPath);
        if (vendor != null) return vendor;

        EnsureFolder(System.IO.Path.GetDirectoryName(VendorPath).Replace('\\', '/'));
        vendor = ScriptableObject.CreateInstance<VendorDefinition>();
        vendor.vendorId = "nature_spirit";
        vendor.displayName = "Nature Spirit";
        AssetDatabase.CreateAsset(vendor, VendorPath);
        Log($"created {VendorPath}.");
        return vendor;
    }

    static List<ItemDefinition> ItemsFromDialogue()
    {
        var items = new List<ItemDefinition>();
        var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DialoguePath);
        if (dialogue == null || dialogue.options == null) return items;

        foreach (var option in dialogue.options)
        {
            if (option.actionType != DialogueActionType.GiveItem || option.itemsToGive == null) continue;
            foreach (var grant in option.itemsToGive)
                if (grant != null && grant.item != null) items.Add(grant.item);
        }

        return items;
    }

    // Run after ItemsFromDialogue — this throws the give options away.
    static void CutDialogue()
    {
        var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DialoguePath);
        if (dialogue == null) { Log($"no dialogue at {DialoguePath}; skipped."); return; }

        dialogue.options = new[]
        {
            new DialogueOptionData { optionText = ShopOptionText, actionType = DialogueActionType.OpenShop }
        };
        EditorUtility.SetDirty(dialogue);
        Log("dialogue: one option left, which opens the shop.");
    }

    static void AddToPrefab(VendorDefinition vendor)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        VendorSystem module = root.GetComponentInChildren<VendorSystem>(true);

        if (module == null)
        {
            Transform parent = ModuleParent(root);
            var child = new GameObject("Vendor_System");
            child.transform.SetParent(parent, false);
            module = child.AddComponent<VendorSystem>();
        }

        var so = new SerializedObject(module);
        so.FindProperty("definition").objectReferenceValue = vendor;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Log($"prefab: VendorSystem on {PrefabPath}.");
    }

    // Beside the other module objects (Dialogue_System's parent), or under the root.
    static Transform ModuleParent(GameObject root)
    {
        var dialogue = root.GetComponentInChildren<DialogueSystem>(true);
        if (dialogue == null || dialogue.transform == root.transform) return root.transform;
        return dialogue.transform.parent;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void Log(string message) => Debug.Log($"[NatureSpiritVendor] {message}");
}
