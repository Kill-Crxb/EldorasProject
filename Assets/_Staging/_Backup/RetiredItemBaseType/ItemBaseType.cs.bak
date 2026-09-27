using UnityEngine;

/// <summary>
/// ItemBaseType - Shared identity and combat flags for a family of items (e.g. all katanas).
/// Referenced by ItemDefinition.baseType.
/// </summary>
[CreateAssetMenu(fileName = "New Item Base Type", menuName = "Items/Base Type")]
public class ItemBaseType : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier (e.g., 'katana_base', 'longsword_base')")]
    public string baseTypeId;

    [Tooltip("Display name for designer reference")]
    public string displayName;

    [Header("Behavior Flags")]
    [Tooltip("Can this weapon type block attacks?")]
    public bool canBlock = true;

    [Tooltip("Can this weapon type parry attacks?")]
    public bool canParry = true;

    [Tooltip("Is this a two-handed weapon?")]
    public bool isTwoHanded = false;

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(baseTypeId) && !string.IsNullOrEmpty(displayName);
    }
}