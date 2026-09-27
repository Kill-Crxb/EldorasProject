using UnityEngine;

public class AbilitySystemAnimationHooks : MonoBehaviour
{
    private AbilitySystem abilitySystem;
    private AnimationDebugger debugger;
    private Animator animator;

    void Start()
    {
        var brain = GetComponent<ControllerBrain>();
        if (brain == null) brain = GetComponentInParent<ControllerBrain>();

        abilitySystem = brain?.Abilities;
        animator = brain?.EntityAnimator;
        debugger = FindObjectOfType<AnimationDebugger>();

        if (abilitySystem == null)
            Debug.LogWarning("[AbilityHooks] AbilitySystem not found");

        if (animator == null)
            Debug.LogWarning("[AbilityHooks] No Animator found");

        if (debugger == null)
            Debug.LogWarning("[AbilityHooks] AnimationDebugger not found in scene");

        if (abilitySystem != null)
            HookAbilityEvents();
    }

    private void HookAbilityEvents()
    {
        debugger?.Log("[AbilityHooks] Hooking ability system events...", true);
    }

    void Update()
    {
        if (animator == null || debugger == null) return;

        TraceAnimatorState();
    }

    private void TraceAnimatorState()
    {
        if (animator == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        string stateName = GetStateName(animator, stateInfo.shortNameHash);

        foreach (var param in animator.parameters)
        {
            if (param.type != AnimatorControllerParameterType.Trigger) continue;

            if (animator.IsInTransition(0))
            {
                debugger?.Log($"[AbilityHooks] Transitioning: Trigger '{param.name}' may have fired");
            }
        }
    }

    private string GetStateName(Animator animator, int stateHash)
    {
        foreach (var state in animator.GetCurrentAnimatorClipInfo(0))
        {
            if (state.clip != null)
                return state.clip.name;
        }
        return stateHash.ToString();
    }

    public void NotifyAbilityTriggered(string abilityId)
    {
        debugger?.TraceAbilityAttempt(abilityId);
    }

    public void NotifyAbilityAnimation(string abilityId, string triggerName)
    {
        if (animator == null) return;

        if (!HasParameter(animator, triggerName))
        {
            debugger?.TraceParameterMissing(triggerName);
            return;
        }

        debugger?.TraceAbilityAnimation(abilityId, triggerName);
    }

    public void NotifyTriggerSet(string triggerName)
    {
        if (animator == null) return;

        if (!HasParameter(animator, triggerName))
        {
            debugger?.TraceParameterMissing(triggerName);
            return;
        }

        debugger?.TraceParameterSet(triggerName, "Trigger");
    }

    public void NotifyAbilityStarted(string abilityId)
    {
        debugger?.TraceAbilityStarted(abilityId);
    }

    public void NotifyAbilityCompleted(string abilityId)
    {
        debugger?.TraceAbilityCompleted(abilityId);
    }

    private bool HasParameter(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}