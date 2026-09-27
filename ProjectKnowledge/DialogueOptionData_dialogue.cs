using System;
using UnityEngine;

[Serializable]
public class DialogueOptionData
{
    public string optionText;
    public DialogueActionType actionType;

    [Header("Give Item")]
    public ItemDefinition itemToGive;
    public int itemQuantity = 1;
}
