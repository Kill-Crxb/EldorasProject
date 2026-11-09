using System;
using System.Collections.Generic;
using UnityEngine;

// The player's HUD layout, saved once per account (Saves/<account>/ui_layout.json).

namespace CrabSystem
{
    [Serializable]
    public class UILayoutData
    {
        public int version = 1;
        public List<UIFrameRecord> frames = new();
    }

    [Serializable]
    public class UIFrameRecord
    {
        public string id;
        public Vector2 position;
        public float scale = 1f;
        public Vector2 size;
    }
}
