#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only. Drop one in the scene and fill the list: F5 applies the first status to the player,
// F6 the second, and so on. F12 clears every status on the player. For testing grants and denials
// (Status_Hasted, Status_Rooted, Status_Silenced) without authoring an ability for each.
public class StatusHotkeys : MonoBehaviour
{
    [SerializeField] private StatusDefinition[] statuses;

    private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10 };

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[Key.F12].wasPressedThisFrame)
            PlayerStatuses()?.RemoveAll();

        int count = Mathf.Min(statuses.Length, Keys.Length);
        for (int i = 0; i < count; i++)
        {
            if (!keyboard[Keys[i]].wasPressedThisFrame) continue;
            if (statuses[i] == null) continue;

            PlayerStatuses()?.Apply(statuses[i]);
        }
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
