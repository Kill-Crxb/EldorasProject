using UnityEngine;

// AbilityDefinition — animation, timing and VFX. The clip's events (MoveEvent) time the move; the upper-body
// states follow from its phases, so a clip names no states.
public partial class AbilityDefinition
{
    // ========================================
    // ANIMATION CONTROL
    // ========================================

    [Header("Animation Control")]
    [Tooltip("The clip's Cue(n) that releases this ability's effects, spell or projectile. 0 = as the ability " +
             "starts (or its cast ends), with no clip event.")]
    [Min(0)] public int effectCue = 1;

    [Tooltip("Wait for the clip's Unlocked event before completing the ability?")]
    public bool waitForAnimUnlock = true;

    [Tooltip("Safety timeout - force complete if Unlocked never fires (0 = no timeout)")]
    public float maxDuration = 2.0f;

    [Tooltip("I-frames on a timer from the moment the move's effects fire, in seconds. For moves that end before " +
             "their clip, like a dash, whose Invuln events would arrive after the move is over. 0 = none.")]
    public float invulnSeconds = 0f;

    [Tooltip("On: the caster keeps walking — the clip plays on the arms while moving and on the whole " +
             "body when standing still. Off: the caster is rooted for the move (MoveRooted) and the " +
             "clip plays full body. Heavies and anything meant to be punishable root.")]
    public bool castWhileMoving = true;

    [Tooltip("On: the clip's own travel moves the caster for the move — root motion, handed to the movement " +
             "handler by the model's RootMotionRelay, so walls and steps still apply. Off: the clip plays in " +
             "place. Pairs with castWhileMoving off: a rooted move that travels the way it was animated.\n\n" +
             "Attacks: on by default, turned off case by case. Never with castWhileMoving on — root motion " +
             "replaces walking.")]
    public bool useRootMotion = false;

    [Tooltip("How much of the clip's travel the move keeps: 1 as animated, 0.5 half as far, 0 in place. Tunes a " +
             "lunge's distance without editing the clip. A move still stops at the body it travels into " +
             "(MovementProfile.rootMotionContactGap).")]
    [Min(0f)] public float rootMotionScale = 1f;

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