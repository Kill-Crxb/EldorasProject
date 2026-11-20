using System;
using System.Collections.Generic;
using UnityEngine;

// A shared list of colours a creation group offers, e.g. every hair colour. The Customisation tab writes it into each
// model's tint group as options, so ids are what saves keep: rename the display name freely, keep the id.
// Staging: graduates with the Customisation tab.

[CreateAssetMenu(menuName = "CrabSystem/Customisation/Colour Palette", fileName = "Palette_")]
public class ColourPalette : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        public Color colour = Color.white;
    }

    [Tooltip("The first colour is the default for anyone who hasn't picked one.")]
    public List<Entry> colours = new List<Entry>();
}
