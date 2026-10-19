using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools → Loot → Set Up Test Loot (Loot_Build.md §7 step 5). Makes the first loot content and wires it:
// a Material category with Currency / Gem / Monster Part subtypes, a coin, four gems and a part per monster;
// the shared Gem Table and one table per monster; a placeholder loot glow; a LootModule on each monster prefab
// with its table and XP; the coin as the Nature Spirit's currency. Anything that already exists is left as it
// is, so a rerun keeps your edits. Staging only: retire it once the loot work graduates.

namespace CrabSystem
{
    public static class LootSetupTool
    {
        private const string ItemFolder = "Assets/Database/Resources/ItemDatabase/Loot";
        private const string TypeFolder = "Assets/Database/Items/Data";
        private const string TableFolder = "Assets/Database/Items/Loot";
        private const string GlowPath = "Assets/Database/VFX/Loot/VFX_LootGlow_Placeholder.prefab";
        private const string MonsterFolder = "Assets/Database/Characters/Monsters";
        private const string ParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        private const string Coin = "coin";

        [MenuItem("Tools/Loot/Set Up Test Loot")]
        private static void Run()
        {
            Folder(ItemFolder);
            Folder(TableFolder);
            Folder(Path.GetDirectoryName(GlowPath).Replace('\\', '/'));

            ItemCategory material = Category();
            ItemSubType currency = SubType("currency", "Currency", material);
            ItemSubType gem = SubType("gem", "Gem", material);
            ItemSubType part = SubType("monster_part", "Monster Part", material);

            Item(Coin, "Coin", "A small silver coin.", currency, 1000, 1);
            Item("sapphire", "Sapphire", "An uncut sapphire.", gem, 10, 25);
            Item("emerald", "Emerald", "An uncut emerald.", gem, 10, 50);
            Item("ruby", "Ruby", "An uncut ruby.", gem, 10, 100);
            Item("diamond", "Diamond", "An uncut diamond.", gem, 10, 200);
            Item("slime_goo", "Slime Goo", "Sticky. Smells of nothing.", part, 20, 2);
            Item("slime_pet", "Slime Pet", "It follows you with its eyes.", part, 1, 500);
            Item("rabbit_foot", "Rabbit Foot", "Lucky for someone.", part, 20, 2);
            Item("bat_wing", "Bat Wing", "Leathery and thin.", part, 20, 3);
            Item("ghost_essence", "Ghost Essence", "Cold to the touch.", part, 20, 5);
            IdSources.Invalidate();

            LootTable gems = Table("LootTable_Gems", t =>
            {
                t.mainRolls = 1;
                t.main.Add(Entry(32, "sapphire"));
                t.main.Add(Entry(16, "emerald"));
                t.main.Add(Entry(8, "ruby"));
                t.main.Add(Entry(2, "diamond"));
            });
            LootTable slime = MonsterTable("LootTable_Slime", "slime_goo", 1, gems, 64, 3, 20, 4, t => t.tertiary.Add(Chance(500, "slime_pet")));
            LootTable rabbit = MonsterTable("LootTable_Rabbit", "rabbit_foot", 1, gems, 60, 2, 14, 4, null);
            LootTable bat = MonsterTable("LootTable_Bat", "bat_wing", 1, gems, 64, 3, 8, 4, null);
            LootTable ghost = MonsterTable("LootTable_Ghost", "ghost_essence", 2, gems, 56, 4, 10, 6, null);

            GameObject glow = Glow();
            Wire("Slime", slime, 30, glow);
            Wire("Rabbit", rabbit, 25, glow);
            Wire("Bat", bat, 35, glow);
            Wire("Ghost", ghost, 50, glow);

            VendorCurrency();
            AssetDatabase.SaveAssets();
            Debug.Log("[Loot] Test loot set up: items, tables, loot glow, monster LootModules, coin currency.");
        }

        // Main out of 128: Nothing, coins, extra parts, the Gem Table.
        private static LootTable MonsterTable(string name, string part, int rolls, LootTable gems, int nothing, int coinDice, int extraParts, int gemWeight, System.Action<LootTable> extra)
        {
            return Table(name, t =>
            {
                t.always.Add(new LootLine { itemId = part, quantity = Flat(1) });
                t.mainRolls = rolls;
                t.main.Add(new LootEntry { weight = nothing });
                t.main.Add(new LootEntry { weight = 128 - nothing - extraParts - gemWeight, itemId = Coin, quantity = new LootQuantity { dice = coinDice, faces = 6, flat = 0 } });
                t.main.Add(new LootEntry { weight = extraParts, itemId = part, quantity = Flat(2) });
                t.main.Add(new LootEntry { weight = gemWeight, subTable = gems });
                extra?.Invoke(t);
            });
        }

        private static LootEntry Entry(int weight, string itemId) => new LootEntry { weight = weight, itemId = itemId, quantity = Flat(1) };
        private static LootChance Chance(int oneIn, string itemId) => new LootChance { oneIn = oneIn, itemId = itemId, quantity = Flat(1) };
        private static LootQuantity Flat(int amount) => new LootQuantity { dice = 0, faces = 6, flat = amount };

        private static LootTable Table(string name, System.Action<LootTable> fill)
        {
            string path = $"{TableFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (existing != null) return existing;

            var table = ScriptableObject.CreateInstance<LootTable>();
            fill(table);
            AssetDatabase.CreateAsset(table, path);
            return table;
        }

        private static ItemCategory Category()
        {
            string path = $"{TypeFolder}/Catagories/Catagory_Material.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemCategory>(path);
            if (existing != null) return existing;

            var category = ScriptableObject.CreateInstance<ItemCategory>();
            category.categoryId = "material";
            category.displayName = "Material";
            category.description = "Loot, currency and crafting materials.";
            category.isStackable = true;
            AssetDatabase.CreateAsset(category, path);
            return category;
        }

        private static ItemSubType SubType(string id, string displayName, ItemCategory parent)
        {
            string path = $"{TypeFolder}/SubTypes/SubType_{displayName.Replace(" ", "")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemSubType>(path);
            if (existing != null) return existing;

            var subType = ScriptableObject.CreateInstance<ItemSubType>();
            subType.subTypeId = id;
            subType.displayName = displayName;
            subType.parentCategory = parent;
            AssetDatabase.CreateAsset(subType, path);
            return subType;
        }

        private static void Item(string id, string displayName, string description, ItemSubType subType, int maxStack, int value)
        {
            string path = $"{ItemFolder}/Item_{displayName.Replace(" ", "")}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null) return;

            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId = id;
            item.displayName = displayName;
            item.description = description;
            item.category = subType.parentCategory;
            item.subType = subType;
            item.maxStackSize = maxStack;
            item.baseValue = value;
            item.tags = new List<string> { subType.subTypeId };
            AssetDatabase.CreateAsset(item, path);
        }

        // A small gold shimmer with a soft light, until the real effect is bought or made (VFX_Shopping_List §8).
        private static GameObject Glow()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GlowPath);
            if (existing != null) return existing;

            var root = new GameObject("VFX_LootGlow_Placeholder");
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.25f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.85f, 0.45f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;

            var emission = particles.emission;
            emission.rateOverTime = 14f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterial);

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.4f);
            light.range = 2f;
            light.intensity = 0.8f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GlowPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Wire(string monster, LootTable table, int xp, GameObject glow)
        {
            string path = $"{MonsterFolder}/{monster}/{monster}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[Loot] No monster prefab at {path}.");
                return;
            }

            if (root.GetComponentInChildren<LootModule>(true) == null)
            {
                var child = new GameObject("Loot_Module");
                child.transform.SetParent(root.transform, false);
                var module = new SerializedObject(child.AddComponent<LootModule>());
                module.FindProperty("table").objectReferenceValue = table;
                module.FindProperty("xp").intValue = xp;
                module.FindProperty("lootMarker").objectReferenceValue = glow;
                module.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Only while it has no currency: once set, it's yours to change.
        private static void VendorCurrency()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VendorDefinition"))
            {
                var vendor = AssetDatabase.LoadAssetAtPath<VendorDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (vendor == null || vendor.vendorId != "nature_spirit" || !string.IsNullOrEmpty(vendor.currencyItemId)) continue;

                vendor.currencyItemId = Coin;
                EditorUtility.SetDirty(vendor);
            }
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
