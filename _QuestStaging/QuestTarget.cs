using UnityEngine;

/// <summary>
/// Names what this entity counts as for Kill objectives ("bandit", "wolf_alpha").
/// Put it on the prefab root. Entities without one never count for a kill.
///
/// Exists because the NPC archetype isn't reachable from the brain at runtime (AISystem
/// holds it privately) and target dummies have no archetype at all.
/// </summary>
public class QuestTarget : MonoBehaviour
{
    [SerializeField] private string targetId;

    public string TargetId => targetId;

    public static string IdOf(ControllerBrain brain)
    {
        if (brain == null) return null;

        var target = brain.GetComponentInChildren<QuestTarget>();
        return target != null ? target.targetId : null;
    }
}
