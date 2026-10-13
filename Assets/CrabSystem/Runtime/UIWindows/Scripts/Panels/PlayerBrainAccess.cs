using UnityEngine;

/// <summary>
/// Single way for UI to reach the player. SaveManager knows first; the scene search
/// is the fallback for scenes that start without going through character select.
/// </summary>
public static class PlayerBrainAccess
{
    public static ControllerBrain Find()
    {
        var saved = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        if (saved != null) return saved;

        foreach (var brain in Object.FindObjectsByType<ControllerBrain>())
        {
            if (brain.IsPlayer) return brain;
        }

        return null;
    }
}
