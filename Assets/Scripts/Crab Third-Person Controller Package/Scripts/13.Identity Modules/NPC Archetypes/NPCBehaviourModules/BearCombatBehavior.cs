using UnityEngine;
using System.Collections;

/// <summary>
/// Bear combat behavior - OPTIMIZED VERSION
/// Properly utilizes NPCMovementModule's built-in features
/// 
/// Three attack types:
/// - Left Claw (Weapon 0)
/// - Right Claw (Weapon 1)  
/// - Bite (Weapon 2)
/// 
/// Distance Management:
/// - Backs away if too close (< 3 units)
/// - Maintains optimal range (3-6 units)
/// - Approaches if too far (> 6 units)
/// 
/// Animation Setup Required:
/// - Parameter: "Attack" (Trigger)
/// - Parameter: "AttackType" (Int) - 0=LeftClaw, 1=RightClaw, 2=Bite
/// </summary>
public class BearCombatBehavior : AICombatBehaviorModule
{
    [Header("Bear Attack Settings")]
    [SerializeField] private float leftClawWeight = 0.4f;  // 40% chance
    [SerializeField] private float rightClawWeight = 0.4f; // 40% chance
    [SerializeField] private float biteWeight = 0.2f;      // 20% chance

    [Header("Attack Distance Management")]
    [Tooltip("Minimum comfortable distance - bear backs away if closer")]
    [SerializeField] private float minAttackDistance = 3f;
    [Tooltip("Maximum attack distance - bear moves closer if farther")]
    [SerializeField] private float maxAttackDistance = 6f;
    [Tooltip("Distance buffer to prevent oscillation")]
    [SerializeField] private float distanceBuffer = 0.5f;
    [SerializeField] private float weaponSwitchDelay = 0.1f;

    [Header("Movement Behavior")]
    [Tooltip("Speed when backing away from target")]
    [SerializeField] private NPCMovementModule.MovementSpeed backawaySpeed = NPCMovementModule.MovementSpeed.Walk;
    [Tooltip("Speed when approaching target")]
    [SerializeField] private NPCMovementModule.MovementSpeed approachSpeed = NPCMovementModule.MovementSpeed.Walk;
    [Tooltip("Angle threshold to consider 'facing' target")]
    [SerializeField] private float facingAngleThreshold = 45f;

    [Header("Debug")]
    [SerializeField] private bool showAttackDebug = true;
    [SerializeField] private bool showMovementDebug = false;

    // Module references
    private MeleeModule meleeModule;
    private AttackModule attackModule;
    private WeaponModule weaponModule;
    private Animator animator;

    // State tracking
    private bool isExecutingAttack = false;
    private int lastAttackType = -1;

    protected override void OnInitialize()
    {


        // Get required modules from brain
        meleeModule = brain.GetModule<MeleeModule>();
        if (meleeModule == null)
        {
            Debug.LogError($"[BearCombatBehavior] MeleeModule not found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        attackModule = meleeModule.Attack;
        if (attackModule == null)
        {
            Debug.LogError($"[BearCombatBehavior] AttackModule not found in MeleeModule");
            isEnabled = false;
            return;
        }

        weaponModule = brain.GetModule<WeaponModule>();
        if (weaponModule == null)
        {
            Debug.LogError($"[BearCombatBehavior] WeaponModule not found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        // Verify we have 3 weapons
        if (weaponModule.GetWeaponCount() < 3)
        {
            Debug.LogError($"[BearCombatBehavior] Need 3 weapons configured. Found: {weaponModule.GetWeaponCount()}");
            isEnabled = false;
            return;
        }

        // Get animator
        animator = brain.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError($"[BearCombatBehavior] No Animator found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        // Verify animation parameters
        if (!HasAnimatorParameter(animator, "Attack"))
            Debug.LogWarning($"[BearCombatBehavior] Missing 'Attack' trigger parameter");

        if (!HasAnimatorParameter(animator, "AttackType"))
            Debug.LogWarning($"[BearCombatBehavior] Missing 'AttackType' int parameter");

        // Subscribe to attack events
        attackModule.OnAttackComplete += OnAttackComplete;

        if (showAttackDebug)
        {
            Debug.Log($"[BearCombatBehavior] Initialized on {gameObject.name}");
            Debug.Log($"  Combat Ranges: Attack={attackRange}, Exit={exitCombatRange}");
            Debug.Log($"  Attack Distance: Min={minAttackDistance}, Max={maxAttackDistance}");
            Debug.Log($"  Weapons: {weaponModule.GetWeaponCount()}");
        }
    }

    public override void UpdateCombat(Transform target)
    {
        if (!isEnabled || target == null || npcMovement == null) return;

        // Use NPCMovementModule's distance calculation
        float distanceToTarget = npcMovement.GetDistanceTo(target.position);

        // Distance-based behavior using NPCMovementModule features
        if (distanceToTarget < minAttackDistance)
        {
            // TOO CLOSE - Back away
            BackAwayFromTarget(target.position, distanceToTarget);
        }
        else if (distanceToTarget > maxAttackDistance + distanceBuffer)
        {
            // TOO FAR - Move closer using NPCMovementModule's MoveTowards
            // This handles both movement AND rotation automatically
            npcMovement.MoveTowards(target.position, approachSpeed);

            if (showMovementDebug)
            {
                Debug.Log($"[BearCombatBehavior] Moving closer - distance: {distanceToTarget:F2}");
            }
        }
        else if (distanceToTarget > maxAttackDistance)
        {
            // At edge of range - just rotate, don't move (prevents oscillation)
            npcMovement.RotateTowards(target.position);
            npcMovement.Stop();
        }
        else
        {
            // IN OPTIMAL RANGE - Stop and prepare to attack
            npcMovement.Stop();

            // Still rotate to face target
            npcMovement.RotateTowards(target.position);

            // Execute attack when ready and facing target
            if (!isExecutingAttack && IsAttackReady() && npcMovement.IsFacing(target.position, facingAngleThreshold))
            {
                int chosenWeapon = ChooseAttackType();
                ExecuteAttack(chosenWeapon);
            }
        }
    }

    #region Movement Helpers

    /// <summary>
    /// Back away from target when too close
    /// Uses NPCMovementModule's rotation and custom backward direction
    /// </summary>
    void BackAwayFromTarget(Vector3 targetPosition, float currentDistance)
    {
        if (npcMovement == null) return;

        // Keep facing the target while backing away
        npcMovement.RotateTowards(targetPosition);

        // Move backward (negative forward direction)
        npcMovement.MoveInDirection(-transform.forward, backawaySpeed);

        if (showMovementDebug)
        {
            Debug.Log($"[BearCombatBehavior] Backing away - distance: {currentDistance:F2} (min: {minAttackDistance})");
        }
    }

    #endregion

    #region Attack System

    /// <summary>
    /// Choose which attack to use based on weighted probabilities
    /// </summary>
    int ChooseAttackType()
    {
        float totalWeight = leftClawWeight + rightClawWeight + biteWeight;
        float normalizedLeft = leftClawWeight / totalWeight;
        float normalizedRight = rightClawWeight / totalWeight;

        float roll = Random.value;

        if (roll < normalizedLeft)
            return 0; // Left Claw
        else if (roll < normalizedLeft + normalizedRight)
            return 1; // Right Claw
        else
            return 2; // Bite
    }

    /// <summary>
    /// Execute attack with specific weapon index
    /// </summary>
    void ExecuteAttack(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weaponModule.GetWeaponCount())
        {
            Debug.LogError($"[BearCombatBehavior] Invalid weapon index: {weaponIndex}");
            return;
        }

        isExecutingAttack = true;
        lastAttackType = weaponIndex;

        // Set animation parameters BEFORE switching weapon
        if (animator != null)
        {
            animator.SetInteger("AttackType", weaponIndex);
            animator.SetTrigger("Attack");

            if (showAttackDebug)
                Debug.Log($"[BearCombatBehavior] Animation: AttackType={weaponIndex}, Attack triggered");
        }

        // Switch weapon
        weaponModule.SwitchToWeapon(weaponIndex);

        // Delay attack execution for weapon switch
        StartCoroutine(DelayedAttack(weaponSwitchDelay, weaponIndex));

        // Record attack time for cooldown
        RecordAttack();

        if (showAttackDebug)
        {
            Debug.Log($"[BearCombatBehavior] Executing {GetAttackName(weaponIndex)} attack");
        }
    }

    /// <summary>
    /// Interface requirement - calls ExecuteAttack with current target
    /// </summary>
    public override void ExecuteAttack()
    {
        if (aiModule != null && aiModule.CurrentTarget != null)
        {
            int chosenWeapon = ChooseAttackType();
            ExecuteAttack(chosenWeapon);
        }
    }

    /// <summary>
    /// Interface requirement with target parameter
    /// </summary>
    public override void ExecuteAttack(Transform target)
    {
        int chosenWeapon = ChooseAttackType();
        ExecuteAttack(chosenWeapon);
    }

    IEnumerator DelayedAttack(float delay, int weaponIndex)
    {
        yield return new WaitForSeconds(delay);

        if (attackModule != null && attackModule.CanAttack())
        {
            attackModule.StartLightAttack();

            if (showAttackDebug)
                Debug.Log($"[BearCombatBehavior] Attack triggered for weapon {weaponIndex}");
        }
        else if (showAttackDebug)
        {
            Debug.LogWarning($"[BearCombatBehavior] Cannot execute attack - AttackModule not ready");
        }
    }

    void OnAttackComplete()
    {
        isExecutingAttack = false;

        if (showAttackDebug)
            Debug.Log($"[BearCombatBehavior] Attack complete");
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

    #endregion

    #region Combat Behavior Overrides

    public override bool CanAttack()
    {
        // Check facing direction using NPCMovementModule
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

        if (showAttackDebug)
        {
            float distance = npcMovement != null ? npcMovement.GetDistanceTo(target.position) :
                            Vector3.Distance(transform.position, target.position);
            Debug.Log($"[BearCombatBehavior] Entered combat at distance: {distance:F2}");
        }
    }

    public override void OnCombatExit()
    {
        base.OnCombatExit();
        isExecutingAttack = false;

        // Stop movement when exiting combat
        if (npcMovement != null)
        {
            npcMovement.Stop();
        }
    }

    #endregion

    #region Debug Context Menus

    [ContextMenu("Test: Animate Left Claw")]
    void TestAnimateLeftClaw()
    {
        if (animator != null)
        {
            animator.SetInteger("AttackType", 0);
            animator.SetTrigger("Attack");
            Debug.Log("[BearCombatBehavior] TEST: Left Claw (AttackType=0)");
        }
    }

    [ContextMenu("Test: Animate Right Claw")]
    void TestAnimateRightClaw()
    {
        if (animator != null)
        {
            animator.SetInteger("AttackType", 1);
            animator.SetTrigger("Attack");
            Debug.Log("[BearCombatBehavior] TEST: Right Claw (AttackType=1)");
        }
    }

    [ContextMenu("Test: Animate Bite")]
    void TestAnimateBite()
    {
        if (animator != null)
        {
            animator.SetInteger("AttackType", 2);
            animator.SetTrigger("Attack");
            Debug.Log("[BearCombatBehavior] TEST: Bite (AttackType=2)");
        }
    }

    [ContextMenu("Test: Execute Random Attack")]
    void TestExecuteRandomAttack()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BearCombatBehavior] Play Mode required");
            return;
        }

        int weaponIndex = ChooseAttackType();
        Debug.Log($"[BearCombatBehavior] TEST: {GetAttackName(weaponIndex)} attack");
        ExecuteAttack(weaponIndex);
    }

    [ContextMenu("Debug: Show Status")]
    void DebugShowStatus()
    {
        Debug.Log("=== BEAR COMBAT STATUS ===");
        Debug.Log($"Enabled: {isEnabled}");
        Debug.Log($"Executing Attack: {isExecutingAttack}");
        Debug.Log($"Last Attack: {(lastAttackType >= 0 ? GetAttackName(lastAttackType) : "None")}");
        Debug.Log($"Combat Ranges: Attack={attackRange}, Exit={exitCombatRange}");
        Debug.Log($"Distance: Min={minAttackDistance}, Max={maxAttackDistance}, Buffer={distanceBuffer}");

        Debug.Log("\n=== MODULE STATUS ===");
        Debug.Log($"MeleeModule: {(meleeModule != null ? "✓" : "✗")}");
        Debug.Log($"AttackModule: {(attackModule != null ? "✓" : "✗")}");
        Debug.Log($"WeaponModule: {(weaponModule != null ? "✓" : "✗")}");
        Debug.Log($"NPCMovement: {(npcMovement != null ? "✓" : "✗")}");
        Debug.Log($"Animator: {(animator != null ? animator.gameObject.name : "✗")}");

        if (weaponModule != null)
        {
            Debug.Log($"\n=== WEAPON INFO ===");
            Debug.Log($"Count: {weaponModule.GetWeaponCount()}");
            Debug.Log($"Current: {weaponModule.CurrentWeapon?.weaponName ?? "None"}");
        }

        if (attackModule != null)
        {
            Debug.Log($"\n=== ATTACK INFO ===");
            Debug.Log($"Can Attack: {attackModule.CanAttack()}");
            Debug.Log($"Is Attacking: {attackModule.IsAttacking}");
        }

        if (npcMovement != null)
        {
            Debug.Log($"\n=== MOVEMENT INFO ===");
            Debug.Log($"Is Moving: {npcMovement.IsMoving}");
            Debug.Log($"Speed: {npcMovement.CurrentSpeed}");
            Debug.Log($"Grounded: {npcMovement.IsGrounded}");

            if (aiModule != null && aiModule.CurrentTarget != null)
            {
                float dist = npcMovement.GetDistanceTo(aiModule.CurrentTarget.position);
                bool facing = npcMovement.IsFacing(aiModule.CurrentTarget.position, facingAngleThreshold);
                Debug.Log($"Distance to Target: {dist:F2}");
                Debug.Log($"Facing Target: {facing}");
            }
        }
    }

    [ContextMenu("Debug: Attack Probabilities")]
    void DebugAttackProbabilities()
    {
        float total = leftClawWeight + rightClawWeight + biteWeight;
        Debug.Log("=== ATTACK PROBABILITIES ===");
        Debug.Log($"Left Claw: {(leftClawWeight / total * 100):F1}%");
        Debug.Log($"Right Claw: {(rightClawWeight / total * 100):F1}%");
        Debug.Log($"Bite: {(biteWeight / total * 100):F1}%");
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (!showAttackDebug) return;

        Vector3 pos = transform.position;

        // Draw combat state ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, exitCombatRange);

        // Draw attack distance ranges
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, minAttackDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, maxAttackDistance);

        // Draw buffer zone
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(pos, maxAttackDistance + distanceBuffer);

        // Show current attack
        if (isExecutingAttack && lastAttackType >= 0)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(pos + Vector3.up * 3f, Vector3.one * 0.5f);
        }

        // Show facing direction
        if (npcMovement != null && Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(pos + Vector3.up, transform.forward * 2f);
        }
    }

    #endregion

    void OnDestroy()
    {
        if (attackModule != null)
        {
            attackModule.OnAttackComplete -= OnAttackComplete;
        }
    }
}