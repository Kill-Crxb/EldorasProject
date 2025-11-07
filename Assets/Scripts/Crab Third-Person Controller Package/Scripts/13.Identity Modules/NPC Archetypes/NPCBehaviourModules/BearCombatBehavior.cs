using UnityEngine;
using System.Collections;

public class BearCombatBehavior : AICombatBehaviorModule
{
    [Header("Bear Attack Settings")]
    [SerializeField] private float leftClawWeight = 0.4f;
    [SerializeField] private float rightClawWeight = 0.4f;
    [SerializeField] private float biteWeight = 0.2f;

    [Header("Attack Distance Management")]
    [SerializeField] private float minAttackDistance = 3f;
    [SerializeField] private float maxAttackDistance = 6f;
    [SerializeField] private float distanceBuffer = 0.5f;
    [SerializeField] private float weaponSwitchDelay = 0.1f;

    [Header("Movement Behavior")]
    [SerializeField] private NPCMovementModule.MovementSpeed backawaySpeed = NPCMovementModule.MovementSpeed.Walk;
    [SerializeField] private NPCMovementModule.MovementSpeed approachSpeed = NPCMovementModule.MovementSpeed.Walk;
    [SerializeField] private float facingAngleThreshold = 45f;

    private MeleeModule meleeModule;
    private AttackModule attackModule;
    private WeaponModule weaponModule;
    private Animator animator;

    private bool isExecutingAttack = false;
    private int lastAttackType = -1;

    protected override void OnInitialize()
    {
        meleeModule = brain.GetModule<MeleeModule>();
        if (meleeModule == null)
        {
            isEnabled = false;
            return;
        }

        attackModule = meleeModule.Attack;
        if (attackModule == null)
        {
            isEnabled = false;
            return;
        }

        weaponModule = brain.GetModule<WeaponModule>();
        if (weaponModule == null)
        {
            isEnabled = false;
            return;
        }

        if (weaponModule.GetWeaponCount() < 3)
        {
            isEnabled = false;
            return;
        }

        animator = brain.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            isEnabled = false;
            return;
        }

        attackModule.OnAttackComplete += OnAttackComplete;
    }

    public override void UpdateCombat(Transform target)
    {
        if (!isEnabled || target == null || npcMovement == null) return;

        float distanceToTarget = npcMovement.GetDistanceTo(target.position);

        if (distanceToTarget < minAttackDistance)
        {
            BackAwayFromTarget(target.position, distanceToTarget);
        }
        else if (distanceToTarget > maxAttackDistance + distanceBuffer)
        {
            npcMovement.MoveTowards(target.position, approachSpeed);
        }
        else if (distanceToTarget > maxAttackDistance)
        {
            npcMovement.RotateTowards(target.position);
            npcMovement.Stop();
        }
        else
        {
            npcMovement.Stop();
            npcMovement.RotateTowards(target.position);

            if (!isExecutingAttack && IsAttackReady() && npcMovement.IsFacing(target.position, facingAngleThreshold))
            {
                int chosenWeapon = ChooseAttackType();
                ExecuteAttack(chosenWeapon);
            }
        }
    }

    void BackAwayFromTarget(Vector3 targetPosition, float currentDistance)
    {
        if (npcMovement == null) return;

        npcMovement.RotateTowards(targetPosition);
        npcMovement.MoveInDirection(-transform.forward, backawaySpeed);
    }

    int ChooseAttackType()
    {
        float totalWeight = leftClawWeight + rightClawWeight + biteWeight;
        float normalizedLeft = leftClawWeight / totalWeight;
        float normalizedRight = rightClawWeight / totalWeight;

        float roll = Random.value;

        if (roll < normalizedLeft)
            return 0;
        else if (roll < normalizedLeft + normalizedRight)
            return 1;
        else
            return 2;
    }

    void ExecuteAttack(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weaponModule.GetWeaponCount())
        {
            return;
        }

        isExecutingAttack = true;
        lastAttackType = weaponIndex;

        if (animator != null)
        {
            animator.SetInteger("AttackType", weaponIndex);
            animator.SetTrigger("Attack");
        }

        StartCoroutine(SwitchWeaponAfterDelay(weaponIndex));
    }

    IEnumerator SwitchWeaponAfterDelay(int weaponIndex)
    {
        yield return new WaitForSeconds(weaponSwitchDelay);

        if (weaponModule != null && weaponModule.GetWeaponCount() > weaponIndex)
        {
            weaponModule.SwitchToWeapon(weaponIndex);
        }

        if (attackModule != null && attackModule.CanAttack())
        {
            attackModule.StartLightAttack();
        }
        else
        {
            isExecutingAttack = false;
        }
    }

    void OnAttackComplete()
    {
        isExecutingAttack = false;
    }

    string GetAttackName(int weaponIndex)
    {
        switch (weaponIndex)
        {
            case 0: return "Left Claw";
            case 1: return "Right Claw";
            case 2: return "Bite";
            default: return "Unknown";
        }
    }

    bool HasAnimatorParameter(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    public override bool CanAttack()
    {
        bool isFacingTarget = true;
        if (aiModule != null && aiModule.CurrentTarget != null && npcMovement != null)
        {
            isFacingTarget = npcMovement.IsFacing(aiModule.CurrentTarget.position, facingAngleThreshold);
        }

        return base.CanAttack() &&
               !isExecutingAttack &&
               attackModule != null &&
               attackModule.CanAttack() &&
               isFacingTarget;
    }

    public override void OnCombatEnter(Transform target)
    {
        base.OnCombatEnter(target);
        isExecutingAttack = false;
        lastAttackType = -1;
    }

    public override void OnCombatExit()
    {
        base.OnCombatExit();
        isExecutingAttack = false;

        if (npcMovement != null)
        {
            npcMovement.Stop();
        }
    }

    void OnDestroy()
    {
        if (attackModule != null)
        {
            attackModule.OnAttackComplete -= OnAttackComplete;
        }
    }
}