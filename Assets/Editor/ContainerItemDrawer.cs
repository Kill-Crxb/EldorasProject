using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Custom property drawer for ContainerItem
/// Shows ItemManager items as dropdown list in inspector
/// FIXED: Now properly saves the selected itemId
/// </summary>
[CustomPropertyDrawer(typeof(ContainerItem))]
public class ContainerItemDrawer : PropertyDrawer
{
    private const float LINE_HEIGHT = 18f;
    private const float SPACING = 2f;
    private const float LABEL_WIDTH = 80f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 3 lines: itemId, rarity, gridX/gridY (same line)
        return (LINE_HEIGHT + SPACING) * 3 + SPACING * 2;
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

        var itemIdProp = property.FindPropertyRelative("itemId");
        var rarityProp = property.FindPropertyRelative("rarity");
        var gridXProp = property.FindPropertyRelative("gridX");
        var gridYProp = property.FindPropertyRelative("gridY");

        // Line 1: Item ID Dropdown
        Rect itemIdRect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);
        DrawItemDropdown(itemIdRect, itemIdProp, property);

        // Line 2: Rarity
        Rect rarityRect = new Rect(position.x, position.y + LINE_HEIGHT + SPACING, position.width, LINE_HEIGHT);
        EditorGUI.PropertyField(rarityRect, rarityProp, new GUIContent("Rarity"));

        // Line 3: Grid Position (X and Y on same line)
        float halfWidth = (position.width - SPACING) / 2f;
        Rect gridXRect = new Rect(position.x, position.y + (LINE_HEIGHT + SPACING) * 2, halfWidth, LINE_HEIGHT);
        Rect gridYRect = new Rect(position.x + halfWidth + SPACING, position.y + (LINE_HEIGHT + SPACING) * 2, halfWidth, LINE_HEIGHT);

        EditorGUI.PropertyField(gridXRect, gridXProp, new GUIContent("Grid X"));
        EditorGUI.PropertyField(gridYRect, gridYProp, new GUIContent("Grid Y"));

        EditorGUI.EndProperty();
    }

    private void DrawItemDropdown(Rect position, SerializedProperty itemIdProp, SerializedProperty containerItemProp)
    {
        // Get all items from ItemManager
        string[] itemIds;
        string[] displayNames;
        GetItemLists(out itemIds, out displayNames);

        string currentItemId = itemIdProp.stringValue;

        // Find current index
        int currentIndex = System.Array.IndexOf(itemIds, currentItemId);

        // If not found and current is empty, default to first item
        if (currentIndex < 0 && string.IsNullOrEmpty(currentItemId) && itemIds.Length > 0)
        {
            currentIndex = 0;
        }

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

            // CRITICAL FIX: Apply modified properties immediately
            itemIdProp.serializedObject.ApplyModifiedProperties();

            // Mark the asset as dirty to ensure it saves
            EditorUtility.SetDirty(itemIdProp.serializedObject.targetObject);

            Debug.Log($"[ContainerItemDrawer] Set itemId to: '{itemIds[newIndex]}'");
        }

        // Show warning if no items
        if (itemIds.Length == 0 || (itemIds.Length > 0 && itemIds[0].StartsWith("<")))
        {
            EditorGUI.HelpBox(new Rect(position.x, position.y + LINE_HEIGHT + 2, position.width, LINE_HEIGHT),
                "No items in ItemManager!", MessageType.Warning);
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
            // Edit mode: find in scene
            itemManager = Object.FindFirstObjectByType<ItemManager>();
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

        if (itemDefinitionsProperty == null || itemDefinitionsProperty.arraySize == 0)
        {
            itemIds = new string[] { "" };
            displayNames = new string[] { "<No Items in ItemManager>" };
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
            displayNames = new string[] { "<No valid items found>" };
            return;
        }

        itemIds = idList.ToArray();
        displayNames = nameList.ToArray();
    }
}
#endif
