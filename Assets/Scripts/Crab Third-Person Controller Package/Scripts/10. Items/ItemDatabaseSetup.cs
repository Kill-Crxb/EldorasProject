using UnityEngine;

// ItemDatabaseSetup.cs - Helper component to setup and test ItemDatabase
public class ItemDatabaseSetup : MonoBehaviour
{
    [Header("Database Setup Instructions")]
    [TextArea(5, 10)]
    public string instructions =
        "1. Create ItemDefinition assets (Right-click → Create → Items → Item Definition)\n" +
        "2. Set their itemId values: headguard_common, headband_common, circlet_common, sword_common\n" +
        "3. Drag them into the ItemDatabase component's 'Item Definitions' array\n" +
        "4. Use the test methods below to validate setup\n" +
        "5. This component should be on the same GameObject as ItemDatabase";

    [Header("Required Components")]
    [SerializeField] private ItemDatabase database;

    void Awake()
    {
        // Auto-find ItemDatabase on this GameObject
        if (database == null)
            database = GetComponent<ItemDatabase>();
    }

    void Start()
    {
        // Validate setup on start
        if (database == null)
        {
            Debug.LogError("ItemDatabase component not found! Add ItemDatabase component to this GameObject.");
            return;
        }

        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("ItemDatabase.Instance is null! Make sure ItemDatabase is properly initialized.");
            return;
        }

        Debug.Log("ItemDatabase setup validation complete. Use context menu to test specific items.");
    }

    [ContextMenu("Test Database Loading")]
    public void TestDatabaseLoading()
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("ItemDatabase.Instance is null! Make sure ItemDatabase component exists and is initialized.");
            return;
        }

        Debug.Log("=== TESTING DATABASE LOADING ===");

        var headguard = ItemDatabase.GetDefinition("headguard_common");
        var headband = ItemDatabase.GetDefinition("headband_common");
        var circlet = ItemDatabase.GetDefinition("circlet_common");
        var sword = ItemDatabase.GetDefinition("sword_common");

        Debug.Log($"Headguard found: {headguard?.displayName ?? "NOT FOUND"}");
        Debug.Log($"Headband found: {headband?.displayName ?? "NOT FOUND"}");
        Debug.Log($"Circlet found: {circlet?.displayName ?? "NOT FOUND"}");
        Debug.Log($"Sword found: {sword?.displayName ?? "NOT FOUND"}");

        // Check how many of our test items were found
        int foundCount = 0;
        if (headguard != null) foundCount++;
        if (headband != null) foundCount++;
        if (circlet != null) foundCount++;
        if (sword != null) foundCount++;

        Debug.Log($"Found {foundCount}/4 expected test items in database");
    }

    [ContextMenu("Test Item Creation")]
    public void TestItemCreation()
    {
        if (ItemDatabase.Instance == null)
        {
            Debug.LogError("ItemDatabase.Instance is null!");
            return;
        }

        Debug.Log("=== TESTING ITEM CREATION ===");

        var commonHeadguard = ItemDatabase.CreateItem("headguard_common", ItemRarity.Common);
        var rareHeadband = ItemDatabase.CreateItem("headband_common", ItemRarity.Rare);
        var epicCirclet = ItemDatabase.CreateItem("circlet_common", ItemRarity.Epic);

        if (commonHeadguard != null)
        {
            Debug.Log($"✓ Created: {commonHeadguard.definitionId} with {commonHeadguard.calculatedModifiers.Length} modifiers");
            foreach (var mod in commonHeadguard.calculatedModifiers)
            {
                Debug.Log($"  +{mod.value:F1} {mod.statName} ({mod.source})");
            }
        }
        else
        {
            Debug.LogError("✗ Failed to create common headguard - check if 'headguard_common' ItemDefinition exists");
        }

        if (rareHeadband != null)
        {
            Debug.Log($"✓ Created: {rareHeadband.definitionId} (Rare) with {rareHeadband.calculatedModifiers.Length} modifiers");
            foreach (var mod in rareHeadband.calculatedModifiers)
            {
                Debug.Log($"  +{mod.value:F1} {mod.statName} ({mod.source})");
            }
        }
        else
        {
            Debug.LogError("✗ Failed to create rare headband - check if 'headband_common' ItemDefinition exists");
        }

        if (epicCirclet != null)
        {
            Debug.Log($"✓ Created: {epicCirclet.definitionId} (Epic) with {epicCirclet.calculatedModifiers.Length} modifiers");
            foreach (var mod in epicCirclet.calculatedModifiers)
            {
                Debug.Log($"  +{mod.value:F1} {mod.statName} ({mod.source})");
            }
        }
        else
        {
            Debug.LogError("✗ Failed to create epic circlet - check if 'circlet_common' ItemDefinition exists");
        }
    }

    [ContextMenu("List All Available Items")]
    public void ListAllAvailableItems()
    {
        if (ItemDatabase.Instance == null || database == null)
        {
            Debug.LogError("ItemDatabase not available!");
            return;
        }

        Debug.Log("=== ALL AVAILABLE ITEM DEFINITIONS ===");

        // This would require adding a method to ItemDatabase to get all definitions
        // For now, just test the known ones
        string[] knownItems = { "headguard_common", "headband_common", "circlet_common", "sword_common" };

        foreach (string itemId in knownItems)
        {
            var definition = ItemDatabase.GetDefinition(itemId);
            if (definition != null)
            {
                Debug.Log($"✓ {itemId}: {definition.displayName} ({definition.archetype} {definition.category})");
            }
            else
            {
                Debug.Log($"✗ {itemId}: NOT FOUND");
            }
        }
    }

    [ContextMenu("Validate ItemDefinition Setup")]
    public void ValidateItemDefinitionSetup()
    {
        Debug.Log("=== VALIDATING ITEM DEFINITION SETUP ===");

        // Check if we have the required ItemDefinitions
        string[] requiredItems = { "headguard_common", "headband_common", "circlet_common", "sword_common" };
        bool allFound = true;

        foreach (string itemId in requiredItems)
        {
            var definition = ItemDatabase.GetDefinition(itemId);
            if (definition == null)
            {
                Debug.LogError($"✗ Missing required ItemDefinition: {itemId}");
                allFound = false;
            }
            else
            {
                // Validate the definition has required fields
                if (string.IsNullOrEmpty(definition.displayName))
                {
                    Debug.LogWarning($"⚠ {itemId} missing displayName");
                }
                if (definition.icon == null)
                {
                    Debug.LogWarning($"⚠ {itemId} missing icon");
                }
                if (definition.baseStats == null)
                {
                    Debug.LogWarning($"⚠ {itemId} missing baseStats");
                }
            }
        }

        if (allFound)
        {
            Debug.Log("✓ All required ItemDefinitions found! Setup is complete.");
        }
        else
        {
            Debug.LogError("✗ Some ItemDefinitions are missing. Create them and assign to ItemDatabase.");
        }
    }
}