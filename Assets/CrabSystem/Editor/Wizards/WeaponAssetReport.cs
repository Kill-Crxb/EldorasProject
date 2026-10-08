using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Inventory of every weapon-side asset, what references it, and what is already broken.
//
// Built for clearing out old weapons safely. It never deletes anything — it selects, so the actual
// removal goes through Unity's own delete, which still warns about scene usage.
public class WeaponAssetReport : EditorWindow
{
    class Row
    {
        public Object Asset;
        public string Path;
        public string Kind;
        public string Id;
        public readonly List<string> Referrers = new List<string>();
        public readonly List<string> Problems = new List<string>();

        public bool Orphan => Referrers.Count == 0;
    }

    readonly List<Row> rows = new List<Row>();
    Vector2 scroll;
    bool scanned;
    bool onlyProblems;

    // Unity's own serialized header keys, not fields on the class.
    static readonly HashSet<string> UnityKeys = new HashSet<string>
    {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
    };

    [MenuItem("Tools/Crab/Wizards/Weapon Asset Report")]
    static void Open()
    {
        WeaponAssetReport window = GetWindow<WeaponAssetReport>("Weapon Assets");
        window.minSize = new Vector2(520, 400);
    }

    void OnGUI()
    {
        CrabWizardGUI.Title("Weapon assets", "Every weapon-side asset, who references it, and what is already broken. Nothing is deleted here.");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan", GUILayout.Height(24), GUILayout.Width(90)))
            Scan();

        onlyProblems = GUILayout.Toggle(onlyProblems, "Only rows with problems", EditorStyles.miniButton, GUILayout.Height(24));
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!scanned))
        {
            if (GUILayout.Button("Select orphans", GUILayout.Height(24), GUILayout.Width(120)))
                SelectOrphans();
        }

        EditorGUILayout.EndHorizontal();

        if (!scanned)
        {
            EditorGUILayout.HelpBox("Scan reads every ItemDefinition and DiceProfile in the project, then walks every asset's dependencies to find what points at them.", MessageType.None);
            return;
        }

        CrabWizardGUI.Rule();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (Row row in rows)
        {
            if (onlyProblems && row.Problems.Count == 0) continue;
            DrawRow(row);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawRow(Row row)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{row.Kind}   {row.Asset.name}", EditorStyles.boldLabel);

        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            Selection.activeObject = row.Asset;
            EditorGUIUtility.PingObject(row.Asset);
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(row.Id))
            EditorGUILayout.LabelField("  id", row.Id, EditorStyles.miniLabel);

        EditorGUILayout.LabelField("  path", row.Path, EditorStyles.miniLabel);

        if (row.Referrers.Count == 0)
        {
            EditorGUILayout.LabelField("  referenced by", "nothing — safe to delete as far as assets go", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("  referenced by", row.Referrers.Count + ":", EditorStyles.miniLabel);

            foreach (string referrer in row.Referrers)
                EditorGUILayout.LabelField("     ", referrer, EditorStyles.miniLabel);
        }

        foreach (string problem in row.Problems)
            EditorGUILayout.HelpBox(problem, MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    // ── Scan ──────────────────────────────────────────────────────────────────────────────────

    void Scan()
    {
        rows.Clear();

        List<ItemDefinition> items = CrabWizardGUI.LoadAll<ItemDefinition>();
        List<DiceProfile> weapons = CrabWizardGUI.LoadAll<DiceProfile>();

        Dictionary<string, List<string>> referrers = BuildReferrerMap();
        Dictionary<string, List<string>> idOwners = new Dictionary<string, List<string>>();

        foreach (ItemDefinition item in items)
        {
            if (item.weaponData == null && !IsWeaponSlot(item)) continue;

            Row row = NewRow(item, "ItemDefinition", item.itemId, referrers);

            if (item.icon == null) row.Problems.Add("No icon.");
            if (item.equippedPrefab == null) row.Problems.Add("No equippedPrefab — nothing appears in hand when equipped.");
            if (item.weaponData != null && item.reach <= 0f) row.Problems.Add("reach is 0 — its strikes only land on a target touching the wielder.");
            if (item.category == null) row.Problems.Add("category is null.");
            if (item.subType == null) row.Problems.Add("subType is null — this item can never be equipped.");
            else if (item.subType.equipmentSlot == null) row.Problems.Add($"subType '{item.subType.name}' has no equipmentSlot.");

            if (item.weaponData == null) row.Problems.Add("Weapon slot item with no weaponData — it will roll no damage.");

            if (!string.IsNullOrEmpty(item.itemId))
            {
                if (!idOwners.TryGetValue(item.itemId, out List<string> owners))
                {
                    owners = new List<string>();
                    idOwners[item.itemId] = owners;
                }

                owners.Add(row.Path);
            }

            AddStaleFieldProblem(row, typeof(ItemDefinition));
            rows.Add(row);
        }

        foreach (DiceProfile weapon in weapons)
        {
            Row row = NewRow(weapon, "DiceProfile", weapon.weaponName, referrers);

            if (row.Orphan) row.Problems.Add("No ItemDefinition references this DiceProfile.");

            AddStaleFieldProblem(row, typeof(DiceProfile));
            rows.Add(row);
        }

        foreach (KeyValuePair<string, List<string>> pair in idOwners)
        {
            if (pair.Value.Count < 2) continue;

            foreach (Row row in rows)
            {
                if (row.Kind != "ItemDefinition" || row.Id != pair.Key) continue;
                row.Problems.Add($"itemId '{pair.Key}' is shared by {pair.Value.Count} assets. ItemManager loads by id — one of them wins and the rest are unreachable.");
            }
        }

        scanned = true;
        Debug.Log($"[WeaponAssetReport] {rows.Count} weapon-side assets scanned.");
    }

    Row NewRow(Object asset, string kind, string id, Dictionary<string, List<string>> referrers)
    {
        string path = AssetDatabase.GetAssetPath(asset);

        Row row = new Row { Asset = asset, Kind = kind, Id = id, Path = path };

        if (referrers.TryGetValue(path, out List<string> found))
            row.Referrers.AddRange(found);

        return row;
    }

    static bool IsWeaponSlot(ItemDefinition item)
    {
        if (item.subType == null || item.subType.equipmentSlot == null) return false;

        string slot = item.subType.equipmentSlot.slotId;
        return slot == "mainwep" || slot == "offwep";
    }

    // path -> the assets whose direct dependencies include it.
    static Dictionary<string, List<string>> BuildReferrerMap()
    {
        Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();

        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/")) continue;
            if (path.StartsWith("Assets/ImportedAssets/") || path.StartsWith("Assets/MagicaCloth2/")) continue;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".asset" && extension != ".prefab" && extension != ".unity" && extension != ".controller") continue;

            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (dependency == path) continue;

                if (!map.TryGetValue(dependency, out List<string> list))
                {
                    list = new List<string>();
                    map[dependency] = list;
                }

                list.Add(path);
            }
        }

        return map;
    }

    // Serialized keys in the file that no longer exist on the class. This is how Katana.asset still
    // carries attackSpeed, reach, combo and socket fields that were removed from DiceProfile — Unity
    // drops them silently on load, so the inspector looks fine while the data is gone.
    static void AddStaleFieldProblem(Row row, System.Type type)
    {
        if (!File.Exists(row.Path)) return;

        HashSet<string> known = new HashSet<string>();

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            known.Add(field.Name);

        List<string> stale = new List<string>();

        foreach (string line in File.ReadAllLines(row.Path))
        {
            Match match = Regex.Match(line, @"^  ([A-Za-z_][A-Za-z0-9_]*):");
            if (!match.Success) continue;

            string key = match.Groups[1].Value;
            if (UnityKeys.Contains(key) || known.Contains(key) || stale.Contains(key)) continue;

            stale.Add(key);
        }

        if (stale.Count == 0) return;

        row.Problems.Add($"Serialized against an older script — {stale.Count} field(s) no longer on {type.Name} and silently dropped on load: {string.Join(", ", stale)}");
    }

    void SelectOrphans()
    {
        List<Object> orphans = new List<Object>();

        foreach (Row row in rows)
            if (row.Orphan) orphans.Add(row.Asset);

        Selection.objects = orphans.ToArray();
        Debug.Log($"[WeaponAssetReport] Selected {orphans.Count} unreferenced asset(s). Delete them from the Project window so Unity's own checks still run.");
    }
}
