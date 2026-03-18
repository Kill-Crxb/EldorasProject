using UnityEngine;
using System;

[CreateAssetMenu(fileName = "InteractionIconDatabase", menuName = "NinjaGame/Interaction/Icon Database")]
public class InteractionIconDatabase : ScriptableObject
{
    [SerializeField] private IconEntry[] icons;

    [Serializable]
    public struct IconEntry
    {
        public InteractionAction action;
        public Sprite icon;
    }

    public Sprite GetIcon(InteractionAction action)
    {
        foreach (var entry in icons)
        {
            if (entry.action == action)
                return entry.icon;
        }
        return null;
    }
}