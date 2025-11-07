using UnityEngine;

public class MeleeCombatBehavior : AICombatBehaviorModule
{
    [Header("Melee Settings")]
    [SerializeField] private bool stopMovementInCombat = true;
    [SerializeField] private bool faceTargetBeforeAttack = true;
    [SerializeField] private float facingThreshold = 0.9f;

    private MeleeModule melee;
    private AttackModule cachedAttackModule;

    protected override void OnInitialize()
    {
        melee = brain.GetModule<MeleeModule>();

        if (melee == null)
        {
            Debug.LogError($"[MeleeCombatBehavior] MeleeModule not found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        cachedAttackModule = melee.Attack;

        if (cachedAttackModule == null)
        {
            Debug.LogError($"[MeleeCombatBehavior] AttackModule not found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[MeleeCombatBehavior] Initialized successfully on {gameObject.name}");
        }
    }

    public override void UpdateCombat(Transform target)
    {
        if (!isEnabled || target == null) return;

        if (stopMovementInCombat && npcMovement != null)
        {
            npcMovement.Stop();
        }

        if (faceTargetBeforeAttack && npcMovement != null)
        {
            npcMovement.RotateTowards(target.position);
        }

        if (faceTargetBeforeAttack && !IsFacingTarget(target))
        {
            return;
        }

        if (IsAttackReady())
        {
            AttemptAttack();
        }
    }

    public override void ExecuteAttack()
    {
        if (!isEnabled || cachedAttackModule == null)
            return;

        if (!cachedAttackModule.CanAttack())
        {
            if (debugMode)
            {
                Debug.Log($"[MeleeCombatBehavior] Cannot attack - CanAttack() returned false");
            }
            return;
        }

        cachedAttackModule.StartLightAttack();
        RecordAttack();

        if (debugMode)
        {
            Debug.Log($"[MeleeCombatBehavior] {gameObject.name} executed attack via ExecuteAttack()");
        }
    }

    bool IsFacingTarget(Transform target)
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, directionToTarget);
        return dot >= facingThreshold;
    }

    public override bool CanEnterCombat()
    {
        if (!base.CanEnterCombat())
            return false;

        if (melee == null || cachedAttackModule == null)
            return false;

        return cachedAttackModule.CanAttack();
    }

    void AttemptAttack()
    {
        if (cachedAttackModule == null || !cachedAttackModule.CanAttack())
            return;

        cachedAttackModule.StartLightAttack();
        RecordAttack();

        if (debugMode)
        {
            Debug.Log($"[MeleeCombatBehavior] {gameObject.name} executed attack via AttemptAttack()");
        }
    }
}