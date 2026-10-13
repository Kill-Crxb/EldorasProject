using UnityEngine;

public class DialogueSystem : MonoBehaviour, IBrainModule
{
    public int InitOrder => 160;

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

        bool completed = true;

        switch (option.actionType)
        {
            case DialogueActionType.GiveItem:
                completed = GiveItems(option);
                break;
            case DialogueActionType.OpenShop:
                completed = OpenShop();
                break;
        }

        // Only spend a once-only option if it actually did its job. A full bag would
        // otherwise burn the reward for good.
        if (option.onceOnly && completed)
            Memory()?.MarkUsed(option.ResolveMemoryKey(database));
    }

    /// <summary>False once a one-shot option has been taken by this actor.</summary>
    public bool IsOptionAvailable(DialogueOptionData option)
    {
        if (option == null) return false;
        if (!option.onceOnly) return true;

        var memory = Memory();
        return memory == null || !memory.HasUsed(option.ResolveMemoryKey(database));
    }

    private DialogueMemory Memory() => CurrentActor != null ? CurrentActor.GetModule<DialogueMemory>() : null;

    // Ends the conversation first: the shop is a window, not a dialogue screen.
    private bool OpenShop()
    {
        var vendor = brain.GetModule<VendorSystem>();
        if (vendor == null)
        {
            Debug.LogWarning($"[DialogueSystem] {brain.name} has an OpenShop option but no VendorSystem.", this);
            return false;
        }

        ControllerBrain customer = CurrentActor;
        EndConversation();
        vendor.Open(customer);
        return true;
    }

    private bool GiveItems(DialogueOptionData option)
    {
        if (option.itemsToGive == null || option.itemsToGive.Length == 0) return true;
        if (CurrentActor == null) return false;

        var inventory = CurrentActor.GetProvider<IInventoryProvider>();
        if (inventory == null)
        {
            Debug.LogWarning($"[DialogueSystem] {CurrentActor.name} has no inventory to receive items.", this);
            return false;
        }

        bool allLanded = true;

        foreach (var grant in option.itemsToGive)
        {
            if (grant == null || grant.item == null) continue;

            if (inventory.AddItem(grant.item.itemId, grant.quantity)) continue;

            Debug.LogWarning($"[DialogueSystem] {CurrentActor.name} could not take {grant.item.displayName} — no room?", this);
            allLanded = false;
        }

        return allLanded;
    }
}
