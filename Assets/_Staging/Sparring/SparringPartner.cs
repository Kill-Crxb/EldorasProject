using UnityEngine;

// A training partner that hits back, so block, parry, hit reactions and the player's side of the
// damage pipeline can be tested before real AI exists (roadmap A1).
//
// It does not animate an attack or swing a weapon hitbox — dummies have neither. On a fixed rhythm it
// turns to the player, telegraphs (optional animator trigger + hit flash), waits the wind-up, then
// delivers one Melee hit through the real pipeline: CalculateDamage on its own DamageSystem,
// TakeDamage on the player's. Hit roll, soak, block intercept, parry, Juice and HitReactionSystem
// all see an ordinary melee hit.
//
// Stands down while its own blackboard says IsStunned — so Cleave (Cripple) at 3 stacks silences it.
//
// Place on a child of Component_Brain on a dummy variant. Staging only; delete the folder to remove.
public class SparringPartner : MonoBehaviour, IBrainModule
{
    [SerializeField] private bool isEnabled = true;

    [Header("Rhythm")]
    [Tooltip("Seconds between the end of one strike and the start of the next wind-up.")]
    [SerializeField] private float interval = 2.5f;
    [Tooltip("Seconds of telegraph before the strike lands. The parry window is measured against this.")]
    [SerializeField] private float windup = 0.6f;

    [Header("Reach")]
    [Tooltip("Starts a wind-up only when the player is this close.")]
    [SerializeField] private float aggroRange = 6f;
    [Tooltip("The strike lands only if the player is still this close when the wind-up ends.")]
    [SerializeField] private float strikeRange = 2.5f;

    [Header("Strike")]
    [SerializeField] private DiceRoll damageDice = DiceRoll.D6();
    [SerializeField] private DamageType damageType = DamageType.Physical;

    [Header("Telegraph")]
    [Tooltip("Animator trigger fired at wind-up start. Leave empty for none; skipped if the animator lacks it.")]
    [SerializeField] private string windupTrigger = "";
    [Tooltip("Hit flash on this entity at wind-up start, so the telegraph is visible without an animation.")]
    [SerializeField] private float windupFlash = 0.15f;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private ControllerBrain brain;
    private DamageSystem damage;
    private AnimationSystem anim;
    private HitFlash flash;
    private ControllerBrain player;
    private DamageSystem playerDamage;

    private float nextWindup;
    private float strikeAt;
    private bool windingUp;

    private static readonly int IsStunnedKey = new BlackboardKey("IsStunned").hash;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        damage = brain.Damage;
        anim = brain.Animation;
        flash = brain.GetModule<HitFlash>();
        nextWindup = Time.time + interval;

        if (damage == null) Debug.LogError($"[SparringPartner] No DamageSystem on {brain.name}.", this);
    }

    public void UpdateModule()
    {
        if (!isEnabled || damage == null) return;
        if (!IsAlive(brain)) return;
        if (!FindPlayer()) return;

        if (windingUp)
        {
            if (Time.time >= strikeAt) Strike();
            return;
        }

        if (Time.time < nextWindup) return;
        if (Stunned()) return;
        if (Distance() > aggroRange) return;

        StartWindup();
    }

    private void StartWindup()
    {
        windingUp = true;
        strikeAt = Time.time + windup;

        FacePlayer();

        if (anim != null && !string.IsNullOrEmpty(windupTrigger) && anim.HasParameter(windupTrigger))
            anim.TriggerCombatAnimation(windupTrigger);

        if (flash != null && windupFlash > 0f) flash.Flash(windupFlash);
    }

    private void Strike()
    {
        windingUp = false;
        nextWindup = Time.time + interval;

        if (Stunned()) return;
        if (!IsAlive(player)) return;
        if (Distance() > strikeRange) return;

        FacePlayer();

        Vector3 point = ContactPoint();
        int baseRoll = damageDice.RollExploding(out int extra, out int explosions);

        var attack = new CombatAttackData
        {
            baseDamage = baseRoll,
            explosionDamage = extra,
            explosions = explosions,
            damageType = damageType,
            attackerTransform = damage.transform,
            hitPoint = point,
            hitNormal = (point - Root(brain).position).normalized,
            comboMultiplier = 1f,
            weaponDamageMultiplier = 1f,
            source = DamageSource.Melee,
        };

        CombatDamagePacket packet = damage.CalculateDamage(attack);
        float applied = playerDamage.TakeDamage(packet);
        DamageNumberManager.Spawn(applied, point + Vector3.up * 0.5f);
    }

    private bool FindPlayer()
    {
        if (player != null && playerDamage != null) return true;

        foreach (ControllerBrain candidate in FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None))
        {
            if (!candidate.IsPlayer) continue;
            player = candidate;
            playerDamage = candidate.Damage;
            return playerDamage != null;
        }
        return false;
    }

    private void FacePlayer()
    {
        Transform root = Root(brain);
        Vector3 to = Root(player).position - root.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        root.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    private Vector3 ContactPoint()
    {
        Vector3 from = Root(brain).position + Vector3.up;
        Collider hurtbox = playerDamage.Hurtbox;
        return hurtbox != null ? hurtbox.ClosestPoint(from) : Root(player).position + Vector3.up;
    }

    private float Distance()
    {
        Vector3 a = Root(brain).position;
        Vector3 b = Root(player).position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool Stunned()
    {
        Blackboard blackboard = brain.Blackboard;
        return blackboard != null && blackboard.GetBool(IsStunnedKey);
    }

    private static bool IsAlive(ControllerBrain target)
    {
        IHealthProvider health = target != null ? target.Health : null;
        return health == null || health.IsAlive();
    }

    private static Transform Root(ControllerBrain target)
    {
        return target.EntityRoot != null ? target.EntityRoot : target.transform.root;
    }
}
