using UnityEngine;

/// <summary>
/// Bridges GameEvents to UniversalWindowManager's dialogue canvas, the same way
/// CameraEventListener bridges GameEvents to CameraModule. DialogueSystem raises
/// GameEvents.DialogueStarted/DialogueEnded without needing a direct reference to the UI.
///
/// Scene-level, not per-brain — place once alongside UniversalWindowManager.
/// </summary>
public class DialogueUIListener : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnDialogueStarted += HandleDialogueStarted;
        GameEvents.OnDialogueEnded += HandleDialogueEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnDialogueStarted -= HandleDialogueStarted;
        GameEvents.OnDialogueEnded -= HandleDialogueEnded;
    }

    private void HandleDialogueStarted(ControllerBrain npc, ControllerBrain actor)
    {
        if (UniversalWindowManager.Instance == null) return;
        UniversalWindowManager.Instance.OpenDialogueWindow(actor, npc);
    }

    private void HandleDialogueEnded(ControllerBrain npc)
    {
        if (UniversalWindowManager.Instance == null) return;
        UniversalWindowManager.Instance.CloseDialogueWindow();
    }
}
