using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Custom property drawer for EquipmentSlotData
/// Shows ItemManager items as dropdown list in inspector
/// </summary>
[CustomPropertyDrawer(typeof(EquipmentSlotData))]
public class EquipmentSlotDataDrawer : PropertyDrawer
{
    private const float LINE_HEIGHT = 18f;
    private const float SPACING = 2f;
    private const float LABEL_WIDTH = 100f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 3 lines: slotType, itemId, rarity
        return (LINE_HEIGHT + SPACING) * 3 + SPACING;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw box background
        GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

        // Indent the content
        position.x += 4;
        position.width -= 8;
        position.y += 4;

        var slotTypeProp = property.FindPropertyRelative("slotType");
        var itemIdProp = property.FindPropertyRelative("itemId");
        var rarityProp = property.FindPropertyRelative("rarity");

        // Line 1: Slot Type
        Rect slotTypeRect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);
        EditorGUI.PropertyField(slotTypeRect, slotTypeProp, new GUIContent("Slot"));

        // Line 2: Item ID Dropdown
        Rect itemIdRect = new Rect(position.x, position.y + LINE_HEIGHT + SPACING, position.width, LINE_HEIGHT);
        DrawItemIdDropdown(itemIdRect, itemIdProp);

        // Line 3: Rarity
        Rect rarityRect = new Rect(position.x, position.y + (LINE_HEIGHT + SPACING) * 2, position.width, LINE_HEIGHT);
        EditorGUI.PropertyField(rarityRect, rarityProp, new GUIContent("Rarity"));

        EditorGUI.EndProperty();
    }

    private void DrawItemIdDropdown(Rect position, SerializedProperty itemIdProp)
    {
        // Get all items from ItemManager
        string[] itemIds;
        string[] displayNames;
        GetItemLists(out itemIds, out displayNames);

        string currentItemId = itemIdProp.stringValue;

        // Find current index
        int currentIndex = System.Array.IndexOf(itemIds, currentItemId);
        if (currentIndex < 0) currentIndex = 0;

        // Draw label
        Rect labelRect = new Rect(position.x, position.y, LABEL_WIDTH, position.height);
        EditorGUI.LabelField(labelRect, "Item");

        // Draw dropdown
        Rect dropdownRect = new Rect(position.x + LABEL_WIDTH, position.y, position.width - LABEL_WIDTH, position.height);

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(dropdownRect, currentIndex, displayNames);

        if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < itemIds.Length)
        {
            itemIdProp.stringValue = itemIds[newIndex];
        }
    }

    private void GetItemLists(out string[] itemIds, out string[] displayNames)
    {
        // In edit mode, ItemManager.Instance may not be initialized
        // So we need to find the ItemManager component and read its serialized field directly

        ItemManager itemManager = null;

        // Try to get initialized instance first (runtime)
        if (Application.isPlaying && ItemManager.Instance != null && ItemManager.Instance.IsInitialized)
        {
            itemManager = ItemManager.Instance;
        }
        else
        {
            // Edit mode: find in scene (use older API for compatibility)
            itemManager = Object.FindObjectOfType<ItemManager>();
        }

        if (itemManager == null)
        {
            itemIds = new string[] { "" };
            displayNames = new string[] { "<No ItemManager in Scene>" };
            return;
        }

        // In edit mode, use SerializedObject to read the itemDefinitions field
        var so = new SerializedObject(itemManager);
        var itemDefinitionsProperty = so.FindProperty("itemDefinitions");

        if (itemDefinitionsProperty == null)
        {
            itemIds = new string[] { "" };
            displayNames = new string[] { "<itemDefinitions field not found>" };
            return;
        }

        if (itemDefinitionsProperty.arraySize == 0)
        {
            itemIds = new string[] { "" };
            displayNames = new string[] { $"<No Items in Array>" };
            return;
        }

        // Build lists from serialized properties
        var idList = new System.Collections.Generic.List<string>();
        var nameList = new System.Collections.Generic.List<string>();

        for (int i = 0; i < itemDefinitionsProperty.arraySize; i++)
        {
            var element = itemDefinitionsProperty.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != null)
            {
                var itemDef = element.objectReferenceValue as ItemDefinition;
                if (itemDef != null && !string.IsNullOrEmpty(itemDef.itemId))
                {
                    idList.Add(itemDef.itemId);
                    nameList.Add($"{itemDef.displayName} ({itemDef.itemId})");
                }
            }
        }

        if (idList.Count == 0)
        {
            itemIds = new string[] { "" };
            displayNames = new string[] { $"<No valid items found>" };
            return;
        }

        itemIds = idList.ToArray();
        displayNames = nameList.ToArray();
    }
}
#endif