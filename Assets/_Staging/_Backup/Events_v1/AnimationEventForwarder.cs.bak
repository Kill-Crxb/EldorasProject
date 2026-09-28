// AnimationEventForwarder.cs - Broadcasts animation events to subscribed systems
using System;
using UnityEngine;
using NinjaGame.Animation;

public class AnimationEventForwarder : MonoBehaviour
{
    private ControllerBrain brain;

    public event Action<AnimationEventType> OnAnimationEvent;
    public event Action<UpperBodyState> OnStateTransitionEvent;

    /// <summary>
    /// Called by AbilitySystem after it locates this component via GetComponentInChildren.
    /// Avoids the timing problem where Start() fires before the model is parented.
    /// </summary>
    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    // ============================================================================
    // ANIMATION EVENT SYSTEM
    // These methods are called by Unity's Animation Event system
    // ============================================================================

    public void OnHitboxStart() => BroadcastEvent(AnimationEventType.HitboxStart);
    public void OnHitboxEnd() => BroadcastEvent(AnimationEventType.HitboxEnd);
    public void OnAnimLocked() => BroadcastEvent(AnimationEventType.AnimLocked);
    public void OnAnimUnlocked() => BroadcastEvent(AnimationEventType.AnimUnlocked);
    public void OnParryStart() => BroadcastEvent(AnimationEventType.ParryStart);
    public void OnParryMax() => BroadcastEvent(AnimationEventType.ParryMax);
    public void OnMovementLocked() => BroadcastEvent(AnimationEventType.MovementLocked);
    public void OnMovementUnlocked() => BroadcastEvent(AnimationEventType.MovementUnlocked);
    public void OnRootMotionStart() => BroadcastEvent(AnimationEventType.RootMotionStart);
    public void OnRootMotionEnd() => BroadcastEvent(AnimationEventType.RootMotionEnd);
    public void OnPlayEffect() => BroadcastEvent(AnimationEventType.PlayEffect);
    public void OnWeaponTrailStart() => BroadcastEvent(AnimationEventType.WeaponTrailStart);
    public void OnWeaponTrailEnd() => BroadcastEvent(AnimationEventType.WeaponTrailEnd);
    public void OnTeleportFrame() => BroadcastEvent(AnimationEventType.TeleportFrame);
    public void OnIFrameStart() => BroadcastEvent(AnimationEventType.IFrameStart);
    public void OnIFrameEnd() => BroadcastEvent(AnimationEventType.IFrameEnd);
    public void OnFeint() => BroadcastEvent(AnimationEventType.Feint);
    public void OnEffect1() => BroadcastEvent(AnimationEventType.Effect1);
    public void OnEffect2() => BroadcastEvent(AnimationEventType.Effect2);
    public void OnEffect3() => BroadcastEvent(AnimationEventType.Effect3);

    /// <summary>
    /// Called by Unity Animation Events to trigger state transitions.
    /// Animator passes state name as string parameter: OnStateTransition("MeleeSwing")
    /// </summary>
    public void OnStateTransition(string stateName)
    {
        if (System.Enum.TryParse<UpperBodyState>(stateName, true, out var state))
            BroadcastStateTransition(state);
        else
            Debug.LogWarning($"[AnimationEventForwarder] Invalid state name: '{stateName}'. Must match UpperBodyState enum.");
    }

    private void BroadcastEvent(AnimationEventType eventType)
    {
        OnAnimationEvent?.Invoke(eventType);
    }

    private void BroadcastStateTransition(UpperBodyState state)
    {
        OnStateTransitionEvent?.Invoke(state);
    }
}