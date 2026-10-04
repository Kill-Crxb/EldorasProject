using NinjaGame.Animation;
using UnityEngine;

/// <summary>
/// AbilityDefinition - Animation Partial
/// Animation triggers, VFX, and timing control
/// 
/// State Transitions: Use animation events to control state changes.
/// In your animator, add events like: OnStateTransition("MeleeSwing")
/// This replaces the old static setsUpperBodyState field.
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // ANIMATION CONTROL
    // ========================================

    [Header("Animation Control")]
    [Tooltip("Which animation event triggers ability effects (Effect1/2/3, or set to PlayEffect for immediate)")]
    public AnimationEventType effectTrigger = AnimationEventType.Effect1;

    [Tooltip("Wait for AnimUnlocked event before completing ability?")]
    public bool waitForAnimUnlock = true;

    [Tooltip("Safety timeout - force complete if AnimUnlocked never fires (0 = no timeout)")]
    public float maxDuration = 2.0f;

    [Tooltip("On: the caster keeps walking — the clip plays on the arms while moving and on the whole " +
             "body when standing still. Off: the caster is rooted for the move (MoveRooted) and the " +
             "clip plays full body. Heavies and anything meant to be punishable root.")]
    public bool castWhileMoving = true;

    // ========================================
    // ANIMATION & VFX
    // ========================================

    [Header("Animation & VFX")]
    [Tooltip("Animation trigger parameter name")]
    [IdRef(IdKind.AnimatorParam)] public string animationTrigger = "Ability";

    [Tooltip("VFX spawned on cast start")]
    public GameObject castEffectPrefab;

    [Tooltip("VFX spawned on hit/impact")]
    public GameObject hitEffectPrefab;

    [Tooltip("Projectile fired by this ability, or none for a melee ability.\n\n" +
             "Points at the DATA, not the prefab — the ProjectileData names its own archetype " +
             "prefab, so several abilities can fire different projectiles that share one " +
             "physical archetype and one pool.")]
    public ProjectileData projectileData;
}