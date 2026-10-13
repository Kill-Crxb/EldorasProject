using System;
using UnityEngine;

[Serializable]
public class DialogueOptionData
{
    public string optionText;
    public DialogueActionType actionType;

    [Header("Give Item")]
    [Tooltip("Everything handed over when this option is picked.")]
    public DialogueItemGrant[] itemsToGive;

    [Header("Once Only")]
    [Tooltip("Once picked, this option never shows again for this character.")]
    public bool onceOnly;

    [Tooltip("What gets written to the save. Leave empty to key off the option text — " +
             "set it if you expect to reword the option later and want it to stay used.")]
    public string onceOnlyId;

    /// <summary>
    /// Keyed on the database, so two NPCs sharing one dialogue asset share the memory.
    /// Give them their own asset if they should be remembered separately.
    /// </summary>
    public string ResolveMemoryKey(DialogueDatabase database)
    {
        string id = string.IsNullOrEmpty(onceOnlyId) ? optionText : onceOnlyId;
        return database != null ? $"{database.name}/{id}" : id;
    }
}

[Serializable]
public class DialogueItemGrant
{
    public ItemDefinition item;
    public int quantity = 1;
}
