#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only. Drop one in the scene and fill the list: F5 applies the first status to the player,
// F6 the second, and so on. F12 clears every status on the player. For testing grants and denials
// (Status_Hasted, Status_Rooted, Status_Silenced) without authoring an ability for each.
// Hold Shift and the same keys act on the nearest non-player instead (the mirror NPC).
public class StatusHotkeys : MonoBehaviour
{
    [SerializeField] private StatusDefinition[] statuses;

    private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10 };

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool other = keyboard.shiftKey.isPressed;

        if (keyboard[Key.F12].wasPressedThisFrame)
            TargetStatuses(other)?.RemoveAll();

        int count = Mathf.Min(statuses.Length, Keys.Length);
        for (int i = 0; i < count; i++)
        {
            if (!keyboard[Keys[i]].wasPressedThisFrame) continue;
            if (statuses[i] == null) continue;

            TargetStatuses(other)?.Apply(statuses[i]);
        }
    }

    private static StatusSystem TargetStatuses(bool nearestOther)
    {
        return nearestOther ? NearestOtherStatuses() : PlayerStatuses();
    }

    private static StatusSystem NearestOtherStatuses()
    {
        ControllerBrain player = null;
        ControllerBrain[] brains = FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None);
        foreach (ControllerBrain brain in brains)
        {
            if (brain.IsPlayer) player = brain;
        }

        if (player == null)
        {
            Debug.LogWarning("[StatusHotkeys] No player brain in the scene.");
            return null;
        }

        StatusSystem nearest = null;
        float best = float.MaxValue;
        foreach (ControllerBrain brain in brains)
        {
            if (brain == player) continue;

            StatusSystem statuses = brain.GetComponentInChildren<StatusSystem>();
            if (statuses == null) continue;

            float distance = (brain.transform.position - player.transform.position).sqrMagnitude;
            if (distance >= best) continue;

            best = distance;
            nearest = statuses;
        }

        if (nearest == null) Debug.LogWarning("[StatusHotkeys] No other entity with a StatusSystem.");
        else Debug.Log($"[StatusHotkeys] Target: {nearest.transform.root.name}");
        return nearest;
    }

    private static StatusSystem PlayerStatuses()
    {
        foreach (ControllerBrain brain in FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None))
        {
            if (brain.IsPlayer) return brain.GetComponentInChildren<StatusSystem>();
        }

        Debug.LogWarning("[StatusHotkeys] No player brain in the scene.");
        return null;
    }
}
#endif
