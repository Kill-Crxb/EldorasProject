using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns a folder of icon sprites into ItemDefinition assets.
///
/// The slot is read from the filename: the first keyword found in the name wins, so
/// "Falmer_armored_boots_17.png" lands on boots rather than on the "armor" in body armour.
/// Everything else — category, subtype, grid size — follows from the slot, so adding a new
/// item type is a matter of authoring an ItemSubType asset, not editing this file.
///
/// Sprites whose name matches nothing are listed at the end and skipped. Rename and re-run.
/// Nothing is ever overwritten: an itemId that already exists is left alone.
/// </summary>
public class ItemDefinitionGenerator : EditorWindow
{
    private const string OutputRoot =
        "Assets/CrabSystem/ItemInventoryEquipment/Inventory/Items/Resources/ItemDatabase";

    // Order matters — specific words before generic ones.
    private static readonly (string slot, string[] words)[] SlotWords =
    {
        ("boots",     new[] { "boot", "shoe", "sandal", "sabaton", "greave", "feet" }),
        ("gloves",    new[] { "glove", "gaunt", "mitt", "bracer", "hands" }),
        ("legs",      new[] { "leg", "pant", "trouser", "kilt", "skirt", "chaps" }),
        ("belt",      new[] { "belt", "sash", "girdle" }),
        ("helmet",    new[] { "helm", "hat", "cap", "circlet", "crown", "hood", "headband", "headguard", "mask", "head" }),
        ("amulet",    new[] { "amulet", "necklace", "pendant", "talisman", "charm", "neck" }),
        ("ring",      new[] { "ring", "signet" }),
        ("pouch",     new[] { "pouch", "bag", "sack", "satchel", "pack" }),
        ("offwep",    new[] { "shield", "buckler", "offhand", "torch", "tome", "quiver" }),
        ("mainwep",   new[] { "sword", "katana", "blade", "axe", "mace", "spear", "dagger", "knife",
                              "hammer", "staff", "bow", "club", "scythe", "glaive", "sabre", "saber", "weapon" }),
        ("bodyarmor", new[] { "body", "chest", "cuirass", "robe", "tunic", "vest", "cloak",
                              "jerkin", "shirt", "plate", "armor", "armour" }),
    };

    private static readonly Dictionary<string, Vector2Int> SlotGrid = new Dictionary<string, Vector2Int>
    {
        { "mainwep",   new Vector2Int(1, 3) },
        { "offwep",    new Vector2Int(1, 2) },
        { "bodyarmor", new Vector2Int(2, 3) },
        { "legs",      new Vector2Int(2, 2) },
        { "helmet",    new Vector2Int(2, 2) },
        { "boots",     new Vector2Int(2, 2) },
        { "gloves",    new Vector2Int(2, 2) },
        { "belt",      new Vector2Int(2, 1) },
        { "pouch",     new Vector2Int(2, 2) },
        { "amulet",    new Vector2Int(1, 1) },
        { "ring",      new Vector2Int(1, 1) },
    };

    private static readonly Dictionary<string, string> SlotFolder = new Dictionary<string, string>
    {
        { "mainwep", "Weapons" }, { "offwep", "Weapons" },
        { "helmet", "Armor" }, { "bodyarmor", "Armor" }, { "legs", "Armor" },
        { "boots", "Armor" }, { "gloves", "Armor" }, { "belt", "Armor" },
        { "ring", "Trinkets" }, { "amulet", "Trinkets" },
        { "pouch", "Misc" },
    };

    private DefaultAsset spriteFolder;

    [MenuItem("Tools/Items/Generate Items From Sprites")]
    private static void Open() => GetWindow<ItemDefinitionGenerator>("Item Generator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sprite folder", EditorStyles.boldLabel);
        spriteFolder = (DefaultAsset)EditorGUILayout.ObjectField(spriteFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Every sprite in the folder becomes one ItemDefinition under " + OutputRoot +
            ".\nThe slot comes from the filename — helm, boot, sword, ring and so on.",
            MessageType.None);

        EditorGUILayout.Space();
        GUI.enabled = spriteFolder != null;
        if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
        GUI.enabled = true;
    }

    private void Generate()
    {
        string folder = AssetDatabase.GetAssetPath(spriteFolder);
        var subTypes = CollectSubTypes();

        var report = new StringBuilder();
        var unmatched = new List<string>();
        int created = 0;

        foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta")) continue;

            string assetPath = path.Replace('\\', '/');
            var sprite = LoadAsSprite(assetPath);
            if (sprite == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string slot = MatchSlot(fileName);

            if (slot == null)
            {
                unmatched.Add(fileName);
                continue;
            }

            if (!subTypes.TryGetValue(slot, out var subType))
            {
                report.AppendLine($"  {fileName}: no ItemSubType bound to slot '{slot}'");
                continue;
            }

            string itemId = ToItemId(fileName);
            if (Exists(itemId))
            {
                report.AppendLine($"  {fileName}: itemId '{itemId}' already exists, left alone");
                continue;
            }

            Create(itemId, fileName, sprite, slot, subType);
            report.AppendLine($"  {fileName}  ->  {slot}  ({SlotGrid[slot].x}x{SlotGrid[slot].y})");
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var summary = new StringBuilder($"[ItemGenerator] Created {created} items\n");
        summary.Append(report);

        if (unmatched.Count > 0)
        {
            summary.AppendLine($"\nNo slot keyword in {unmatched.Count} filename(s) — rename and re-run:");
            foreach (string name in unmatched) summary.AppendLine("  " + name);
        }

        Debug.Log(summary.ToString());
    }

    private void Create(string itemId, string fileName, Sprite sprite, string slot, ItemSubType subType)
    {
        var item = CreateInstance<ItemDefinition>();
        item.itemId = itemId;
        item.displayName = ToDisplayName(fileName);
        item.description = "";
        item.icon = sprite;
        item.subType = subType;
        item.category = subType.parentCategory;
        item.gridWidth = SlotGrid[slot].x;
        item.gridHeight = SlotGrid[slot].y;
        item.rarity = ItemRarity.Common;
        item.maxStackSize = 1;
        item.tags = new List<string> { slot };

        string dir = $"{OutputRoot}/{SlotFolder[slot]}";
        if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);

        AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath($"{dir}/Item_{ToAssetName(fileName)}.asset"));
    }

    /// <summary>slotId -> the subtype bound to it. Built from the assets, so no ids live in this file.</summary>
    private static Dictionary<string, ItemSubType> CollectSubTypes()
    {
        var result = new Dictionary<string, ItemSubType>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemSubType"))
        {
            var subType = AssetDatabase.LoadAssetAtPath<ItemSubType>(AssetDatabase.GUIDToAssetPath(guid));
            if (subType == null || subType.equipmentSlot == null) continue;

            string slot = subType.equipmentSlot.slotId;
            if (string.IsNullOrEmpty(slot) || result.ContainsKey(slot)) continue;

            result[slot] = subType;
        }

        return result;
    }

    /// <summary>Textures import as plain textures in a 3D project, so make sure it is a sprite first.</summary>
    private static Sprite LoadAsSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return null;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static string MatchSlot(string fileName)
    {
        string name = fileName.ToLowerInvariant();

        foreach (var (slot, words) in SlotWords)
            foreach (string word in words)
                if (name.Contains(word)) return slot;

        return null;
    }

    private static bool Exists(string itemId)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null && def.itemId == itemId) return true;
        }

        return false;
    }

    private static string ToItemId(string fileName)
    {
        var builder = new StringBuilder();

        foreach (char c in fileName.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        return builder.ToString().Trim('_');
    }

    private static string ToDisplayName(string fileName)
    {
        string spaced = fileName.Replace('_', ' ').Replace('-', ' ').Trim();
        var words = spaced.Split(' ');
        var builder = new StringBuilder();

        foreach (string word in words)
        {
            if (word.Length == 0) continue;
            builder.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1)).Append(' ');
        }

        return builder.ToString().Trim();
    }

    private static string ToAssetName(string fileName) => ToDisplayName(fileName).Replace(" ", "");
}
