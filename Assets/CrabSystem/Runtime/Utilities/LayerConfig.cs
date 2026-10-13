using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// LayerConfig — ScriptableObject defining expected layers and physics collision matrix.
///
/// Used by LayerManagerWindow to validate and apply project layer settings.
/// Create via: Assets → Create → CrabSystem → Layer Config
///
/// Data-driven: swap this SO per game/project for different layer setups.
/// Version controlled alongside the project — no code changes needed to reconfigure.
/// </summary>
[CreateAssetMenu(menuName = "CrabSystem/Layer Config", fileName = "LayerConfig")]
public class LayerConfig : ScriptableObject
{
    [Header("Layer Definitions")]
    [Tooltip("Expected layers. Index must match Unity layer index exactly.")]
    public List<LayerDefinition> layers = new List<LayerDefinition>();

    [Header("Collision Matrix")]
    [Tooltip("Define which layer pairs should collide. Unlisted pairs default to Unity's current setting.")]
    public List<LayerCollisionRule> collisionRules = new List<LayerCollisionRule>();

    // ── Convenience accessors ─────────────────────────────────────────────

    public LayerDefinition GetLayer(int index)
    {
        return layers.Find(l => l.index == index);
    }

    public LayerDefinition GetLayer(string name)
    {
        return layers.Find(l => l.name == name);
    }

    public bool TryGetCollisionRule(int layerA, int layerB, out bool shouldCollide)
    {
        foreach (var rule in collisionRules)
        {
            if ((rule.layerIndexA == layerA && rule.layerIndexB == layerB) ||
                (rule.layerIndexA == layerB && rule.layerIndexB == layerA))
            {
                shouldCollide = rule.collides;
                return true;
            }
        }
        shouldCollide = false;
        return false;
    }
}

[System.Serializable]
public class LayerDefinition
{
    [Tooltip("Unity layer index (0-31)")]
    public int index;

    [Tooltip("Expected layer name")]
    public string name;

    [Tooltip("Description of what this layer is used for")]
    [TextArea(1, 3)]
    public string description;

    [Tooltip("If true, this layer is a Unity built-in and cannot be renamed")]
    public bool isBuiltIn;
}

[System.Serializable]
public class LayerCollisionRule
{
    public int layerIndexA;
    public int layerIndexB;

    [Tooltip("Should these two layers collide?")]
    public bool collides;

    [Tooltip("Optional note explaining why this rule exists")]
    public string note;
}