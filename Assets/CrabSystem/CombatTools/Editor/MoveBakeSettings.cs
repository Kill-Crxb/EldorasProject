using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

// Which animator controllers the move bake reads, in priority order: the first controller whose
// state for an ability's trigger carries a Strike and Unlocked wins. When CF5's
// per-style override controllers arrive, they're listed here too.
[CreateAssetMenu(fileName = "MoveBakeSettings", menuName = "CrabSystem/Combat/Move Bake Settings")]
public class MoveBakeSettings : ScriptableObject
{
    [Tooltip("Controllers to read clips from, highest priority first.")]
    public List<AnimatorController> controllers = new List<AnimatorController>();

    [Tooltip("Re-bake whenever an animation clip, model or controller is imported, so an edited event moves the " +
             "frame data with it. Untick only to stop it while importing large batches.")]
    public bool bakeOnImport = true;
}
