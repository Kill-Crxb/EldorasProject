using UnityEngine;
using System.Collections.Generic;

// ItemDatabase.cs - Runtime database for item definitions
public class ItemDatabase : MonoBehaviour
{
    private static ItemDatabase instance;
    public static ItemDatabase Instance => instance;

    [Header("Item Definitions")]
    [SerializeField] private ItemDefinition[] itemDefinitions;

    private Dictionary<string, ItemDefinition> definitionLookup;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDatabase()
    {
        definitionLookup = new Dictionary<string, ItemDefinition>();

        foreach (var definition in itemDefinitions)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.itemId))
            {
                definitionLookup[definition.itemId] = definition;
            }
        }
    }

    public static ItemDefinition GetDefinition(string itemId)
    {
        if (instance?.definitionLookup != null && instance.definitionLookup.ContainsKey(itemId))
        {
            return instance.definitionLookup[itemId];
        }
        return null;
    }

    public static ItemInstance CreateItem(string itemId, ItemRarity tier = ItemRarity.Common)
    {
        var definition = GetDefinition(itemId);
        if (definition == null)
        {
            Debug.LogError($"Item definition not found: {itemId}");
            return null;
        }

        return new ItemInstance(itemId, tier);
    }

    public static ItemDefinition[] GetItemsByArchetype(ItemArchetype archetype)
    {
        if (instance?.itemDefinitions == null) return new ItemDefinition[0];

        var results = new System.Collections.Generic.List<ItemDefinition>();
        foreach (var definition in instance.itemDefinitions)
        {
            if (definition.archetype == archetype)
            {
                results.Add(definition);
            }
        }
        return results.ToArray();
    }

    public static ItemDefinition[] GetItemsBySlot(EquipmentSlot slot)
    {
        if (instance?.itemDefinitions == null) return new ItemDefinition[0];

        var results = new System.Collections.Generic.List<ItemDefinition>();
        foreach (var definition in instance.itemDefinitions)
        {
            if (definition.equipmentSlot == slot)
            {
                results.Add(definition);
            }
        }
        return results.ToArray();
    }
}