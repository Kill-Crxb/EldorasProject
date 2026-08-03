using System.Collections.Generic;
using UnityEngine;

public class AnimationDebugger : MonoBehaviour
{
    [SerializeField] private ControllerBrain brain;
    [SerializeField] private bool traceMovement = true;
    [SerializeField] private bool traceAbilities = true;
    [SerializeField] private bool traceAnimationEvents = true;
    [SerializeField] private bool traceParameters = true;
    [SerializeField] private bool logStateChanges = true;

    private MovementSystem movementSystem;
    private AbilitySystem abilitySystem;
    private AnimationSystem animationSystem;
    private AnimationEventForwarder eventForwarder;
    private Animator animator;

    private string currentState = "";
    private string lastLoggedState = "";
    private float stateCheckInterval = 0.25f;
    private float nextStateCheck = 0f;

    private Dictionary<string, object> lastParameterValues = new Dictionary<string, object>();

    void Start()
    {
        if (brain == null)
            brain = GetComponent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("[AnimationDebugger] No ControllerBrain found!");
            return;
        }

        ResolveReferences();
        HookIntoSystems();
        LogStartup();
    }

    void Update()
    {
        if (brain == null || animator == null) return;

        if (Time.time >= nextStateCheck)
        {
            nextStateCheck = Time.time + stateCheckInterval;
            CheckAnimatorState();
            CheckAnimatorParameters();
        }
    }

    private void ResolveReferences()
    {
        movementSystem = brain.Movement;
        abilitySystem = brain.Abilities;
        animationSystem = brain.Animation;
        animator = brain.EntityAnimator;

        eventForwarder = brain.GetComponentInChildren<AnimationEventForwarder>();

        Log($"[AnimationDebugger] Resolved references:", true);
        Log($"  MovementSystem: {(movementSystem != null ? "✓" : "✗ NULL")}");
        Log($"  AbilitySystem: {(abilitySystem != null ? "✓" : "✗ NULL")}");
        Log($"  AnimationSystem: {(animationSystem != null ? "✓" : "✗ NULL")}");
        Log($"  EventForwarder: {(eventForwarder != null ? "✓" : "✗ NULL")}");
        Log($"  Animator: {(animator != null ? "✓ on " + animator.gameObject.name : "✗ NULL")}");
    }

    private void HookIntoSystems()
    {
        if (movementSystem != null && traceMovement)
        {
            Log("[AnimationDebugger] Hooking into MovementSystem...", true);
        }

        if (abilitySystem != null && traceAbilities)
        {
            Log("[AnimationDebugger] Hooking into AbilitySystem...", true);
        }

        if (eventForwarder != null && traceAnimationEvents)
        {
            Log("[AnimationDebugger] Hooking into AnimationEventForwarder...", true);
        }
    }

    private void LogStartup()
    {
        Log("\n╔════════════════════════════════════════════╗", true);
        Log("║    ANIMATION DEBUGGER INITIALIZED        ║", true);
        Log("╚════════════════════════════════════════════╝", true);
        Log($"Entity: {brain.name}", true);
        Log($"Entity Type: {brain.EntityType}", true);
        Log($"Trace Movement: {traceMovement}", true);
        Log($"Trace Abilities: {traceAbilities}", true);
        Log($"Trace Animation Events: {traceAnimationEvents}", true);
        Log($"Trace Parameters: {traceParameters}", true);
        Log($"", true);

        if (animator != null)
        {
            Log($"Animator Parameters ({animator.parameterCount}):", true);
            foreach (var param in animator.parameters)
            {
                Log($"  • {param.name} ({param.type})", true);
            }
        }
        else
        {
            Log("⚠️  WARNING: No Animator found!", true);
        }

        Log($"", true);
    }

    private void CheckAnimatorState()
    {
        if (animator == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        currentState = stateInfo.IsName("Idle") ? "Idle" : stateInfo.shortNameHash.ToString();

        if (logStateChanges && currentState != lastLoggedState)
        {
            Log($"[AnimatorState] {lastLoggedState} → {currentState}", true);
            lastLoggedState = currentState;
        }
    }

    private void CheckAnimatorParameters()
    {
        if (animator == null) return;

        foreach (var param in animator.parameters)
        {
            object currentValue = GetParameterValue(param);
            bool hasChanged = !lastParameterValues.ContainsKey(param.name) ||
                             !AreValuesEqual(lastParameterValues[param.name], currentValue);

            if (hasChanged && traceParameters)
            {
                object oldValue = lastParameterValues.ContainsKey(param.name)
                    ? lastParameterValues[param.name]
                    : "unset";

                Log($"[Parameter] {param.name}: {oldValue} → {currentValue}");

                lastParameterValues[param.name] = currentValue;
            }
        }
    }

    private object GetParameterValue(AnimatorControllerParameter param)
    {
        switch (param.type)
        {
            case AnimatorControllerParameterType.Bool:
                return animator.GetBool(param.name);
            case AnimatorControllerParameterType.Float:
                return animator.GetFloat(param.name).ToString("F2");
            case AnimatorControllerParameterType.Int:
                return animator.GetInteger(param.name);
            case AnimatorControllerParameterType.Trigger:
                return "Trigger";
            default:
                return "Unknown";
        }
    }

    private bool AreValuesEqual(object a, object b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Equals(b);
    }

    public void TraceMovementInput(string input)
    {
        if (!traceMovement) return;
        Log($"[Movement] Input: {input}");
    }

    public void TraceMovementParameter(string paramName, object value)
    {
        if (!traceMovement) return;
        Log($"[Movement→Animator] SetParameter: {paramName} = {value}");
    }

    public void TraceAbilityAttempt(string abilityId)
    {
        if (!traceAbilities) return;
        Log($"[Ability] Attempting to use: {abilityId}");
    }

    public void TraceAbilityAnimation(string abilityId, string animationTrigger)
    {
        if (!traceAbilities) return;
        Log($"[Ability→Animation] {abilityId} → Trigger: {animationTrigger}");
    }

    public void TraceAbilityStarted(string abilityId)
    {
        if (!traceAbilities) return;
        Log($"[Ability] STARTED: {abilityId}", true);
    }

    public void TraceAbilityCompleted(string abilityId)
    {
        if (!traceAbilities) return;
        Log($"[Ability] COMPLETED: {abilityId}", true);
    }

    public void TraceAnimationEvent(string eventName)
    {
        if (!traceAnimationEvents) return;
        Log($"[AnimationEvent] Fired: {eventName}", true);
    }

    public void TraceAnimationEventListener(string eventName, string listener)
    {
        if (!traceAnimationEvents) return;
        Log($"[AnimationEvent] Listener registered: {eventName} → {listener}");
    }

    public void TraceParameterSet(string paramName, object value)
    {
        if (!traceParameters) return;
        Log($"[Animator.SetParameter] {paramName} = {value}");
    }

    public void TraceParameterMissing(string paramName)
    {
        if (!traceParameters) return;
        Log($"⚠️  [Parameter] NOT FOUND in Animator: {paramName}", true);
    }

    public void Log(string message, bool important = false)
    {
        if (important)
            Debug.Log($"<color=cyan>{message}</color>");
        else
            Debug.Log(message);
    }

    [ContextMenu("Debug/Print Current Animator State")]
    public void DebugPrintCurrentState()
    {
        if (animator == null)
        {
            Debug.LogError("[AnimationDebugger] No animator!");
            return;
        }

        Log("\n╔════════════════════════════════════════════╗", true);
        Log("║        CURRENT ANIMATOR STATE             ║", true);
        Log("╚════════════════════════════════════════════╝", true);

        for (int i = 0; i < animator.layerCount; i++)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(i);
            string layerName = animator.GetLayerName(i);
            float layerWeight = animator.GetLayerWeight(i);

            Log($"Layer {i}: {layerName} [Weight: {layerWeight:F2}]", true);
            Log($"  State Hash: {stateInfo.shortNameHash}");
            Log($"  Normalized Time: {stateInfo.normalizedTime:F2}");
            Log($"  In Transition: {animator.IsInTransition(i)}");
        }

        Log($"\nAll Parameters:", true);
        foreach (var param in animator.parameters)
        {
            object value = GetParameterValue(param);
            Log($"  {param.name} ({param.type}): {value}");
        }

        Log($"", true);
    }

    [ContextMenu("Debug/Validate Animation Setup")]
    public void DebugValidateSetup()
    {
        ResolveReferences();

        Log("\n╔════════════════════════════════════════════╗", true);
        Log("║      ANIMATION SETUP VALIDATION           ║", true);
        Log("╚════════════════════════════════════════════╝", true);

        bool allGood = true;

        if (movementSystem == null)
        {
            Log("✗ MovementSystem is NULL", true);
            allGood = false;
        }
        else
        {
            Log("✓ MovementSystem found", true);
        }

        if (abilitySystem == null)
        {
            Log("✗ AbilitySystem is NULL", true);
            allGood = false;
        }
        else
        {
            Log("✓ AbilitySystem found", true);
        }

        if (animationSystem == null)
        {
            Log("✗ AnimationSystem is NULL", true);
            allGood = false;
        }
        else
        {
            Log("✓ AnimationSystem found", true);
            if (animationSystem.GetAnimator() == null)
            {
                Log("  ⚠️  AnimationSystem.GetAnimator() returns NULL", true);
                allGood = false;
            }
            else
            {
                Log("  ✓ AnimationSystem has valid Animator", true);
            }
        }

        if (animator == null)
        {
            Log("✗ Brain.EntityAnimator is NULL", true);
            allGood = false;
        }
        else
        {
            Log("✓ Brain.EntityAnimator found", true);
            Log($"  Animator on: {animator.gameObject.name}");
            Log($"  Parameters: {animator.parameterCount}");
        }

        if (eventForwarder == null)
        {
            Log("⚠️  AnimationEventForwarder not found (optional)", true);
        }
        else
        {
            Log("✓ AnimationEventForwarder found", true);
        }

        Log($"", true);
        if (allGood)
            Log("✓ ALL CHECKS PASSED", true);
        else
            Log("✗ SETUP HAS ISSUES", true);

        Log($"", true);
    }

    [ContextMenu("Debug/Test Animation Parameter")]
    public void DebugTestParameter()
    {
        if (animator == null)
        {
            Debug.LogError("[AnimationDebugger] No animator!");
            return;
        }

        Log($"\n[Test] Setting test trigger 'BasicAttack1'", true);
        animator.SetTrigger("BasicAttack1");

        Log($"[Test] Parameter should have fired. Check console above.", true);
    }
}