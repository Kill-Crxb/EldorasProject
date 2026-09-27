using System;
using System.Collections.Generic;
using UnityEngine;

// Optional. Without one, every claimed layer is driven with the controller's default fade times
// and returns to whatever weight the Animator Controller authored. Add a profile when a specific
// layer needs different fades, a different rest weight, or to be excluded from arbitration.
[CreateAssetMenu(menuName = "Crab/Animation Layer Profile", fileName = "AnimationLayerProfile")]
public class AnimationLayerProfile : ScriptableObject
{
    [Serializable]
    public class LayerBinding
    {
        public string layerName;

        [Tooltip("Other spellings that should bind to this layer. Tried before the fuzzy match.")]
        public string[] aliases;

        [Tooltip("Off leaves this layer entirely alone, even when something claims it.")]
        public bool driveAtRuntime = true;

        [Tooltip("On, the layer returns to restWeight with no claims. Off, it returns to the " +
                 "weight the Animator Controller authored.")]
        public bool overrideRestWeight;

        [Range(0f, 1f)] public float restWeight;

        public float fadeIn = 0.05f;
        public float fadeOut = 0.15f;
    }

    public List<LayerBinding> layers = new List<LayerBinding>();
}
