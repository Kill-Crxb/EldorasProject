using UnityEngine;

/// <summary>
/// AnimationSystem - Provides animation interface to other modules
/// Wraps Unity's Animator component with IAnimationProvider interface
/// Used by LocomotionModule, AbilityModule, etc. to trigger animations
/// </summary>
public class AnimationSystem : MonoBehaviour, IBrainModule, IAnimationProvider
{
    [Header("Debug")]
    [SerializeField] private bool debugAnimations = false;
    [SerializeField] private bool onlyLogChanges = true;

    private ControllerBrain brain;
    private Animator animator => brain?.EntityAnimator; // Dynamic property

    // Cache for change detection
    private System.Collections.Generic.Dictionary<string, object> parameterCache = new System.Collections.Generic.Dictionary<string, object>();

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Don't assign animator - it's now a dynamic property that gets current animator

        if (brain?.EntityAnimator == null)
        {
            Debug.LogWarning($"[AnimationSystem] No Animator found yet on {gameObject.name} - will use when available");
            return;
        }

        if (debugAnimations)
        {
            Debug.Log($"[AnimationSystem] Initialized with Animator on {brain.EntityAnimator.gameObject.name}");
            Debug.Log($"[AnimationSystem] Animator has {brain.EntityAnimator.parameterCount} parameters");
        }
    }

    public void UpdateModule()
    {
        // Animator updates itself, we just provide the interface
    }

    #region IAnimationProvider Implementation

    public void SetTrigger(string parameterName)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
            Debug.Log($"[AnimationSystem] SetTrigger: {parameterName}");

        animator.SetTrigger(parameterName);
    }

    public void ResetTrigger(string parameterName)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
            Debug.Log($"[AnimationSystem] ResetTrigger: {parameterName}");

        animator.ResetTrigger(parameterName);
    }

    public void SetBool(string parameterName, bool value)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
        {
            if (!onlyLogChanges || !parameterCache.ContainsKey(parameterName) || !(bool)parameterCache[parameterName] == value)
            {
                Debug.Log($"[AnimationSystem] SetBool: {parameterName} = {value}");
                parameterCache[parameterName] = value;
            }
        }

        animator.SetBool(parameterName, value);
    }

    public void SetFloat(string parameterName, float value)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
        {
            // Use larger thresholds for parameters that change frequently
            float threshold = 0.01f; // Default for strafe params
            if (parameterName == "VerticalVelocity")
                threshold = 0.5f;  // Large threshold for physics
            else if (parameterName == "MovementSpeed")
                threshold = 0.1f;  // Medium threshold for speed ramping

            bool shouldLog = !onlyLogChanges ||
                             !parameterCache.ContainsKey(parameterName) ||
                             Mathf.Abs((float)parameterCache[parameterName] - value) > threshold;

            if (shouldLog)
            {
                Debug.Log($"[AnimationSystem] SetFloat: {parameterName} = {value:F3}");
                parameterCache[parameterName] = value;
            }
        }

        animator.SetFloat(parameterName, value);
    }

    public void SetInteger(string parameterName, int value)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
        {
            if (!onlyLogChanges || !parameterCache.ContainsKey(parameterName) || (int)parameterCache[parameterName] != value)
            {
                Debug.Log($"[AnimationSystem] SetInteger: {parameterName} = {value}");
                parameterCache[parameterName] = value;
            }
        }

        animator.SetInteger(parameterName, value);
    }

    public bool GetBool(string parameterName)
    {
        if (animator == null) return false;
        return animator.GetBool(parameterName);
    }

    public float GetFloat(string parameterName)
    {
        if (animator == null) return 0f;
        return animator.GetFloat(parameterName);
    }

    public int GetInteger(string parameterName)
    {
        if (animator == null) return 0;
        return animator.GetInteger(parameterName);
    }

    public AnimatorStateInfo GetCurrentStateInfo(int layerIndex = 0)
    {
        if (animator == null) return default;
        return animator.GetCurrentAnimatorStateInfo(layerIndex);
    }

    public bool IsInTransition(int layerIndex = 0)
    {
        if (animator == null) return false;
        return animator.IsInTransition(layerIndex);
    }

    public void Play(string stateName, int layerIndex = 0)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
            Debug.Log($"[AnimationSystem] Play: {stateName} (Layer {layerIndex})");

        animator.Play(stateName, layerIndex);
    }

    public void CrossFade(string stateName, float transitionDuration, int layerIndex = 0)
    {
        if (!IsEnabled || animator == null) return;

        if (debugAnimations)
            Debug.Log($"[AnimationSystem] CrossFade: {stateName} (Duration: {transitionDuration:F2}, Layer {layerIndex})");

        animator.CrossFade(stateName, transitionDuration, layerIndex);
    }

    #endregion

    #region Additional Utilities

    public void TriggerCombatAnimation(string triggerName)
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// Check if a parameter exists in the animator
    /// </summary>
    public bool HasParameter(string parameterName)
    {
        if (animator == null) return false;

        foreach (var param in animator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if animator is currently in a specific state
    /// </summary>
    public bool IsInState(string stateName, int layerIndex = 0)
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.IsName(stateName);
    }

    /// <summary>
    /// Get normalized time in current state (0-1+)
    /// </summary>
    public float GetCurrentStateNormalizedTime(int layerIndex = 0)
    {
        if (animator == null) return 0f;

        return animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
    }

    /// <summary>
    /// Get the layer index by name
    /// </summary>
    public int GetLayerIndex(string layerName)
    {
        if (animator == null) return -1;
        return animator.GetLayerIndex(layerName);
    }

    /// <summary>
    /// Get layer weight (0-1)
    /// </summary>
    public float GetLayerWeight(int layerIndex)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
            return 0f;

        return animator.GetLayerWeight(layerIndex);
    }

    /// <summary>
    /// Set layer weight (0-1)
    /// </summary>
    public void SetLayerWeight(int layerIndex, float weight)
    {
        if (!IsEnabled || animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
            return;

        animator.SetLayerWeight(layerIndex, weight);

        if (debugAnimations)
            Debug.Log($"[AnimationSystem] SetLayerWeight: Layer {layerIndex} = {weight:F2}");
    }

    /// <summary>
    /// Direct access to Animator for advanced use cases
    /// </summary>
    public Animator GetAnimator() => animator;

    #endregion

    #region Debug Utilities

    /// <summary>
    /// Log all current parameter values (useful for debugging)
    /// </summary>
    [ContextMenu("Debug/Log All Parameters")]
    public void DebugLogAllParameters()
    {
        if (animator == null)
        {
            Debug.LogWarning("[AnimationSystem] No animator assigned!");
            return;
        }

        Debug.Log("=== Animator Parameters ===");
        foreach (var param in animator.parameters)
        {
            string value = param.type switch
            {
                AnimatorControllerParameterType.Float => animator.GetFloat(param.name).ToString("F3"),
                AnimatorControllerParameterType.Int => animator.GetInteger(param.name).ToString(),
                AnimatorControllerParameterType.Bool => animator.GetBool(param.name).ToString(),
                AnimatorControllerParameterType.Trigger => "Trigger",
                _ => "Unknown"
            };

            Debug.Log($"  {param.name} ({param.type}): {value}");
        }
    }

    /// <summary>
    /// Log current animation state for all layers
    /// </summary>
    [ContextMenu("Debug/Log Current States")]
    public void DebugLogCurrentStates()
    {
        if (animator == null)
        {
            Debug.LogWarning("[AnimationSystem] No animator assigned!");
            return;
        }

        Debug.Log("=== Current Animation States ===");
        for (int i = 0; i < animator.layerCount; i++)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(i);
            string layerName = animator.GetLayerName(i);
            float layerWeight = animator.GetLayerWeight(i);

            Debug.Log($"  Layer {i} ({layerName}) [Weight: {layerWeight:F2}]");
            Debug.Log($"    State Hash: {stateInfo.shortNameHash}");
            Debug.Log($"    Normalized Time: {stateInfo.normalizedTime:F2}");
            Debug.Log($"    In Transition: {animator.IsInTransition(i)}");
        }
    }

    /// <summary>
    /// Validate that required parameters exist
    /// </summary>
    [ContextMenu("Debug/Validate Locomotion Parameters")]
    public void ValidateLocomotionParameters()
    {
        if (animator == null)
        {
            Debug.LogWarning("[AnimationSystem] No animator assigned!");
            return;
        }

        Debug.Log("=== Validating Locomotion Parameters ===");

        string[] requiredParams = new string[]
        {
            "MovementState",
            "IsLockedOn",
            "StrafeX",
            "StrafeY",
            "MovementSpeed",
            "IsGrounded",
            "VerticalVelocity",
            "JumpTrigger",
            "DashTrigger",
            "IsDashing"
        };

        int foundCount = 0;
        foreach (string paramName in requiredParams)
        {
            bool exists = HasParameter(paramName);
            string status = exists ? "✓" : "✗ MISSING";
            Debug.Log($"  {status} {paramName}");
            if (exists) foundCount++;
        }

        Debug.Log($"Found {foundCount}/{requiredParams.Length} required locomotion parameters");
    }

    /// <summary>
    /// Validate that required combat parameters exist
    /// </summary>
    [ContextMenu("Debug/Validate Combat Parameters")]
    public void ValidateCombatParameters()
    {
        if (animator == null)
        {
            Debug.LogWarning("[AnimationSystem] No animator assigned!");
            return;
        }

        Debug.Log("=== Validating Combat Parameters ===");

        string[] combatParams = new string[]
        {
            "BasicAttack1",
            "BasicAttack2",
            "BasicAttack3",
            "Cleave",
            "Whirlwind",
            "Thrust",
            "Slam",
            "HitLight",
            "HitHeavy",
            "Stagger",
            "Death",
            "IsDead"
        };

        int foundCount = 0;
        foreach (string paramName in combatParams)
        {
            bool exists = HasParameter(paramName);
            string status = exists ? "✓" : "✗ MISSING";
            Debug.Log($"  {status} {paramName}");
            if (exists) foundCount++;
        }

        Debug.Log($"Found {foundCount}/{combatParams.Length} combat parameters");
    }

    #endregion
}