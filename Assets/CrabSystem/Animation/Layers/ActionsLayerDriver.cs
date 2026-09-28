using UnityEngine;

// WoW-style free movement for the Actions layers (Animator_Audit.md).
//
// Every action clip lives in one state machine shown through two layers: Actions (full body) and
// Actions Upper (upper mask, synced to Actions). Actions Upper always shows while an action plays.
// This module sets how much of the FULL body shows: all of it standing still, none of it while
// moving, so the legs come from locomotion and only the arms play the clip.
//
// A rooted ability (castWhileMoving off) needs no special case — MovementSystem stops the input,
// speed falls, and the full body fades in on its own. While the Actions state machine rests, the
// rest rule holds both layers at 0 whatever this claims.
public class ActionsLayerDriver : MonoBehaviour, IBrainModule
{
    [Tooltip("At or below this speed (m/s) the whole body plays the action.")]
    [SerializeField] private float fullBodyBelow = 0.3f;

    [Tooltip("At or above this speed (m/s) only the arms play it; the legs keep walking.")]
    [SerializeField] private float armsOnlyAbove = 1.5f;

    private ControllerBrain brain;
    private AnimationLayerController layers;
    private MovementSystem movement;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        layers = brain?.GetModule<AnimationLayerController>();
        movement = brain?.Movement;

        if (layers == null)
            Debug.LogError($"[ActionsLayerDriver] No AnimationLayerController on '{name}'.", this);
    }

    public void UpdateModule()
    {
        if (layers == null) return;

        float speed = movement != null ? movement.Speed : 0f;
        float fullBody = 1f - Mathf.InverseLerp(fullBodyBelow, armsOnlyAbove, speed);

        layers.Claim(this, AnimationLayerNames.Actions, fullBody, AnimationLayerController.PriorityAbility);
    }

    void OnDisable()
    {
        if (layers != null) layers.ReleaseAll(this);
    }
}
