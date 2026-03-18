// AnimationEventForwarder.cs - Broadcasts animation events to subscribed systems
using System;
using UnityEngine;
using NinjaGame.Animation;

public class AnimationEventForwarder : MonoBehaviour
{
    private ControllerBrain brain;

    // Events that systems can subscribe to
    public event Action<AnimationEventType> OnAnimationEvent;
    public event Action<UpperBodyState> OnStateTransitionEvent;  // NEW: For state transitions

    void Start()
    {
        // Find the ControllerBrain by going up the hierarchy
        brain = GetComponentInParent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("AnimationEventForwarder: No ControllerBrain found in parent hierarchy!");
            return;
        }
    }

    // ============================================================================
    // ANIMATION EVENT SYSTEM
    // These methods are called by Unity's Animation Event system
    // ============================================================================

    // Hitbox Control
    public void OnHitboxStart() => BroadcastEvent(AnimationEventType.HitboxStart);
    public void OnHitboxEnd() => BroadcastEvent(AnimationEventType.HitboxEnd);

    // Cancel Windows
    public void OnAnimLocked() => BroadcastEvent(AnimationEventType.AnimLocked);
    public void OnAnimUnlocked() => BroadcastEvent(AnimationEventType.AnimUnlocked);

    // Parry System
    public void OnParryStart() => BroadcastEvent(AnimationEventType.ParryStart);
    public void OnParryMax() => BroadcastEvent(AnimationEventType.ParryMax);

    // Movement Control
    public void OnMovementLocked() => BroadcastEvent(AnimationEventType.MovementLocked);
    public void OnMovementUnlocked() => BroadcastEvent(AnimationEventType.MovementUnlocked);
    public void OnRootMotionStart() => BroadcastEvent(AnimationEventType.RootMotionStart);
    public void OnRootMotionEnd() => BroadcastEvent(AnimationEventType.RootMotionEnd);

    // VFX/SFX
    public void OnPlayEffect() => BroadcastEvent(AnimationEventType.PlayEffect);
    public void OnWeaponTrailStart() => BroadcastEvent(AnimationEventType.WeaponTrailStart);
    public void OnWeaponTrailEnd() => BroadcastEvent(AnimationEventType.WeaponTrailEnd);

    // Special Movement
    public void OnTeleportFrame() => BroadcastEvent(AnimationEventType.TeleportFrame);

    // Invincibility Frames
    public void OnIFrameStart() => BroadcastEvent(AnimationEventType.IFrameStart);
    public void OnIFrameEnd() => BroadcastEvent(AnimationEventType.IFrameEnd);

    // AI Decision Points
    public void OnFeint() => BroadcastEvent(AnimationEventType.Feint);

    // Generic Effect Triggers
    public void OnEffect1() => BroadcastEvent(AnimationEventType.Effect1);
    public void OnEffect2() => BroadcastEvent(AnimationEventType.Effect2);
    public void OnEffect3() => BroadcastEvent(AnimationEventType.Effect3);

    // State Machine Integration (NEW - Animation-driven states)
    /// <summary>
    /// Called by Unity Animation Events to trigger state transitions
    /// Animator passes state name as string parameter: OnStateTransition("MeleeSwing")
    /// </summary>
    public void OnStateTransition(string stateName)
    {
        // Parse string to UpperBodyState enum
        if (System.Enum.TryParse<UpperBodyState>(stateName, true, out var state))
        {
            BroadcastStateTransition(state);
        }
        else
        {
            Debug.LogWarning($"[AnimationEventForwarder] Invalid state name: '{stateName}'. Must match UpperBodyState enum.");
        }
    }

    /// <summary>
    /// Broadcasts an animation event to all subscribed systems
    /// </summary>
    private void BroadcastEvent(AnimationEventType eventType)
    {
        OnAnimationEvent?.Invoke(eventType);
    }

    /// <summary>
    /// Broadcasts a state transition to all subscribed systems
    /// </summary>
    private void BroadcastStateTransition(UpperBodyState state)
    {
        OnStateTransitionEvent?.Invoke(state);
    }
}