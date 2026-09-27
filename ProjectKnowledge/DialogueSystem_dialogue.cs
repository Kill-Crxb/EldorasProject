using UnityEngine;

public class DialogueSystem : MonoBehaviour, IBrainModule
{
    [Header("Module")]
    [SerializeField] private bool isEnabled = true;

    [Header("Camera")]
    [SerializeField] private string dialogueCameraMode = "Dialogue";
    [SerializeField] private string defaultCameraMode = "Default";

    [Header("Data")]
    [SerializeField] private DialogueDatabase database;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool IsInConversation { get; private set; }
    public ControllerBrain CurrentActor { get; private set; }
    public DialogueDatabase Database => database;

    private ControllerBrain brain;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    // Ending on range is handled by InteractionSystem.OnTriggerExit, which calls
    // EndConversation() directly when the actor leaves interactionCollider — no
    // per-frame distance polling needed here.
    public void UpdateModule() { }

    public bool BeginConversation(ControllerBrain actor)
    {
        if (!isEnabled || actor == null) return false;
        if (IsInConversation) return false;

        IsInConversation = true;
        CurrentActor = actor;

        // BrainState belongs to the initiator, not the NPC — an NPC can be talked to
        // by whoever approaches it, so "currently focused on dialogue" is a fact about
        // the actor's own attention/input, not something the NPC should carry.
        actor.GetModule<StateMachineModule>()?.TryTransitionBrain(BrainState.Dialogue);

        brain.Blackboard?.SetBool(BlackboardKey.IsInConversation, true);
        GameEvents.DialogueStarted(brain, actor);
        GameEvents.CameraModeChangeRequested(dialogueCameraMode);

        return true;
    }

    public void EndConversation()
    {
        if (!IsInConversation) return;

        IsInConversation = false;
        var actor = CurrentActor;
        CurrentActor = null;

        var actorStateMachine = actor?.GetModule<StateMachineModule>();
        if (actorStateMachine != null)
            actorStateMachine.TryTransitionBrain(actorStateMachine.GetPreviousBrainState());

        brain.Blackboard?.SetBool(BlackboardKey.IsInConversation, false);
        GameEvents.DialogueEnded(brain);
        GameEvents.CameraModeChangeRequested(defaultCameraMode);
    }

    // Options only ever reach here from an active conversation's own UI, so no
    // extra validation of "is this option actually mine" — the UI only ever shows
    // options pulled from this NPC's own database.
    public void SelectOption(DialogueOptionData option)
    {
        if (option == null || !IsInConversation) return;

        switch (option.actionType)
        {
            case DialogueActionType.GiveItem:
                GiveItem(option);
                break;
        }
    }

    private void GiveItem(DialogueOptionData option)
    {
        if (option.itemToGive == null || CurrentActor == null) return;

        var inventory = CurrentActor.GetProvider<IInventoryProvider>();
        if (inventory == null)
        {
            Debug.LogWarning($"[DialogueSystem] {CurrentActor.name} has no inventory to receive {option.itemToGive.displayName}.", this);
            return;
        }

        inventory.AddItem(option.itemToGive.itemId, option.itemQuantity);
    }
}
