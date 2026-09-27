using UnityEngine;

/// <summary>
/// Reads a stat value for a blackboard condition.
/// Stats are addressed by string id because a schema can hold dozens of them.
/// </summary>
[CreateAssetMenu(fileName = "StatValue", menuName = "NinjaGame/Blackboard/Value Sources/Stat")]
public class StatValueSource : ValueSourceDefinition
{
    [Header("Stat Query")]
    [Tooltip("Stat id to read, e.g. 'core.body'")]
    [SerializeField] private string statId;

    public override float GetValue(ControllerBrain brain)
    {
        if (brain == null) return 0f;
        if (string.IsNullOrEmpty(statId)) return 0f;

        var stats = brain.Stats;
        if (stats == null) return 0f;

        return stats.GetValue(statId, 0f);
    }

    public override string GetDisplayName()
    {
        return string.IsNullOrEmpty(statId) ? "None" : statId;
    }

    public override bool Validate(out string error)
    {
        if (string.IsNullOrEmpty(statId))
        {
            error = "Stat ID cannot be empty";
            return false;
        }

        error = null;
        return true;
    }
}
