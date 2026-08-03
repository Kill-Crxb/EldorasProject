using UnityEngine;

public class AnimationSystem : MonoBehaviour, IBrainModule, IAnimationProvider
{
    private ControllerBrain brain;
    private Animator animator => brain?.EntityAnimator;

    private static readonly string[] LocomotionParams = { "MovementState", "IsLockedOn", "StrafeX", "StrafeY", "MovementSpeed", "IsGrounded", "VerticalVelocity", "JumpTrigger", "DashTrigger", "IsDashing" };
    private static readonly string[] CombatParams = { "BasicAttack1", "BasicAttack2", "BasicAttack3", "Cleave", "Whirlwind", "Thrust", "Slam", "HitLight", "HitHeavy", "Stagger", "Death", "IsDead" };

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void UpdateModule() { }

    public void LateInitialize() { }

    public void SetTrigger(string parameterName)
    {
        if (!IsEnabled || animator == null) return;
        animator.SetTrigger(parameterName);
    }

    public void ResetTrigger(string parameterName)
    {
        if (!IsEnabled || animator == null) return;
        animator.ResetTrigger(parameterName);
    }

    public void SetBool(string parameterName, bool value)
    {
        if (!IsEnabled || animator == null) return;
        animator.SetBool(parameterName, value);
    }

    public void SetFloat(string parameterName, float value)
    {
        if (!IsEnabled || animator == null) return;
        animator.SetFloat(parameterName, value);
    }

    public void SetInteger(string parameterName, int value)
    {
        if (!IsEnabled || animator == null) return;
        animator.SetInteger(parameterName, value);
    }

    public bool GetBool(string parameterName) => animator != null && animator.GetBool(parameterName);
    public float GetFloat(string parameterName) => animator != null ? animator.GetFloat(parameterName) : 0f;
    public int GetInteger(string parameterName) => animator != null ? animator.GetInteger(parameterName) : 0;

    public AnimatorStateInfo GetCurrentStateInfo(int layerIndex = 0) => animator != null ? animator.GetCurrentAnimatorStateInfo(layerIndex) : default;
    public bool IsInTransition(int layerIndex = 0) => animator != null && animator.IsInTransition(layerIndex);

    public void Play(string stateName, int layerIndex = 0)
    {
        if (!IsEnabled || animator == null) return;
        animator.Play(stateName, layerIndex);
    }

    public void CrossFade(string stateName, float transitionDuration, int layerIndex = 0)
    {
        if (!IsEnabled || animator == null) return;
        animator.CrossFade(stateName, transitionDuration, layerIndex);
    }

    public void TriggerCombatAnimation(string triggerName)
    {
        if (animator != null) animator.SetTrigger(triggerName);
    }

    public bool HasParameter(string parameterName)
    {
        if (animator == null) return false;
        foreach (var param in animator.parameters)
            if (param.name == parameterName) return true;
        return false;
    }

    public bool IsInState(string stateName, int layerIndex = 0) => animator != null && animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName);
    public float GetCurrentStateNormalizedTime(int layerIndex = 0) => animator != null ? animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime : 0f;
    public int GetLayerIndex(string layerName) => animator != null ? animator.GetLayerIndex(layerName) : -1;
    public float GetLayerWeight(int layerIndex) => (animator != null && layerIndex >= 0 && layerIndex < animator.layerCount) ? animator.GetLayerWeight(layerIndex) : 0f;

    public void SetLayerWeight(int layerIndex, float weight)
    {
        if (!IsEnabled || animator == null || layerIndex < 0 || layerIndex >= animator.layerCount) return;
        animator.SetLayerWeight(layerIndex, weight);
    }

    public Animator GetAnimator() => animator;
}