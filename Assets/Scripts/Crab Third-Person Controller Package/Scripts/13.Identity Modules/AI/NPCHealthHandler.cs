using UnityEngine;


public class NPCHealthHandler : MonoBehaviour, INPCHandler
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugLogs = true;

    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 3f;  // Time before destroying NPC after death
    [SerializeField] private bool destroyOnDeath = false;  // Optional: Destroy GameObject on death
    [SerializeField] private bool disableAIOnDeath = true;  // Disable AI module on death

    // Component references
    private NPCModule parentNPC;
    private ControllerBrain brain;
    private DamageIn damageIn;
    private IHealthProvider healthAdapter;
    private AIModule aiModule;

    // State
    private bool isDead = false;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public bool IsDead => isDead;

    // ========================================
    // INPCHandler Implementation
    // ========================================

    public void Initialize(NPCModule parent)
    {
        parentNPC = parent;
        brain = parent.Brain;

        if (brain == null)
        {
            Debug.LogError($"[NPCHealthHandler] {parent.NPCName}: No ControllerBrain found!", this);
            return;
        }

        // Find DamageIn component (should be under Component_Brain or Component_Damage)
        damageIn = brain.GetComponentInChildren<DamageIn>();
        if (damageIn == null)
        {
            Debug.LogError($"[NPCHealthHandler] {parent.NPCName}: No DamageIn found! " +
                          "Add DamageIn component under Component_Brain/Component_Damage.", this);
            return;
        }

        // Find health adapter
        var healthAdapterMono = brain.GetComponentInChildren<IHealthProvider>() as MonoBehaviour;
        if (healthAdapterMono != null)
        {
            healthAdapter = healthAdapterMono as IHealthProvider;
        }

        if (healthAdapter == null)
        {
            Debug.LogError($"[NPCHealthHandler] {parent.NPCName}: No IHealthProvider found! " +
                          "Add RPGHealthAdapter or similar health provider.", this);
            return;
        }

        // Find AI module (for disabling on death)
        if (disableAIOnDeath)
        {
            aiModule = brain.GetComponentInChildren<AIModule>();
        }

        // Subscribe to DamageIn events
        SubscribeToEvents();

        if (debugLogs)
        {
            Debug.Log($"[NPCHealthHandler] ✓ Initialized for {parent.NPCName}\n" +
                     $"  DamageIn: {(damageIn != null ? "✓" : "✗")}\n" +
                     $"  HealthAdapter: {(healthAdapter != null ? "✓" : "✗")}\n" +
                     $"  AIModule: {(aiModule != null ? "✓" : "✗")}");
        }
    }

    public void UpdateHandler()
    {
        if (!isEnabled) return;

        // Health handler is event-driven, no per-frame updates needed
    }

    public string GetHandlerSaveData()
    {
        // For future save system integration
        var saveData = new NPCHealthSaveData
        {
            isDead = isDead
        };

        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<NPCHealthSaveData>(json);
        isDead = saveData.isDead;

        // If loaded as dead, trigger death sequence
        if (isDead)
        {
            HandleDeath(null);
        }
    }

    public void ResetHandler()
    {
        isDead = false;
        isEnabled = true;

        // Re-enable AI if it was disabled
        if (aiModule != null)
        {
            aiModule.IsEnabled = true;
        }

        // Reset health to max
        if (healthAdapter != null)
        {
            float currentHealth = healthAdapter.GetCurrentHealth();
            float maxHealth = healthAdapter.GetMaxHealth();
            float healAmount = maxHealth - currentHealth;

            if (healAmount > 0)
            {
                healthAdapter.ApplyHealing(healAmount); // Heal to full
            }
        }

        if (debugLogs)
        {
            Debug.Log($"[NPCHealthHandler] {parentNPC.NPCName} reset to alive");
        }
    }

    // ========================================
    // Event Subscription
    // ========================================

    private void SubscribeToEvents()
    {
        if (damageIn != null)
        {
            damageIn.OnDamageReceived += HandleDamageReceived;
            damageIn.OnDeath += HandleDeath;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (damageIn != null)
        {
            damageIn.OnDamageReceived -= HandleDamageReceived;
            damageIn.OnDeath -= HandleDeath;
        }
    }

    // ========================================
    // Event Handlers
    // ========================================

    /// <summary>
    /// Called when DamageIn receives damage.
    /// Updates nameplate health bar.
    /// </summary>
    private void HandleDamageReceived(CombatDamagePacket damagePacket, float finalDamageReceived)
    {
        if (!isEnabled || isDead) return;

        // Update nameplate health bar
        UpdateNameplateHealth();

        if (debugLogs)
        {
            string attackerName = damagePacket.attackerId ?? "Unknown";
            Debug.Log($"[NPCHealthHandler] {parentNPC.NPCName} took {finalDamageReceived:F1} damage from {attackerName}\n" +
                     $"  Health: {healthAdapter.GetCurrentHealth():F0} / {healthAdapter.GetMaxHealth():F0} " +
                     $"({healthAdapter.GetHealthPercentage() * 100:F0}%)");
        }
    }

    /// <summary>
    /// Called when DamageIn detects death (health <= 0).
    /// Disables AI, updates nameplate, triggers death sequence.
    /// </summary>
    private void HandleDeath(CombatDamagePacket killingBlow)
    {
        if (isDead) return; // Already dead

        isDead = true;

        // Disable AI
        if (disableAIOnDeath && aiModule != null)
        {
            aiModule.IsEnabled = false;

            if (debugLogs)
            {
                Debug.Log($"[NPCHealthHandler] {parentNPC.NPCName}: AI disabled");
            }
        }

        // Update nameplate (hide or show death state)
        if (parentNPC != null)
        {
            parentNPC.SetNameplateVisible(false); // Hide nameplate on death
        }

        // Log death
        if (debugLogs)
        {
            string killerName = killingBlow?.attackerId ?? "Unknown";
            Debug.Log($"<color=red>[NPCHealthHandler] ☠ {parentNPC.NPCName} has DIED! Killed by: {killerName}</color>");
        }

        // Optional: Destroy after delay
        if (destroyOnDeath)
        {
            Destroy(brain.transform.parent.gameObject, deathDelay);
        }

        // TODO Phase 4: Play death animation
        // TODO Phase 4: Spawn loot
        // TODO Phase 4: Grant XP to player
    }

    // ========================================
    // Public API
    // ========================================

    /// <summary>
    /// Update nameplate health bar with current health percentage.
    /// </summary>
    public void UpdateNameplateHealth()
    {
        if (parentNPC == null || healthAdapter == null) return;

        float healthPercent = healthAdapter.GetHealthPercentage();
        parentNPC.UpdateNameplateHealth(healthPercent);
    }

    /// <summary>
    /// Get current health value.
    /// </summary>
    public float GetCurrentHealth()
    {
        return healthAdapter?.GetCurrentHealth() ?? 0f;
    }

    /// <summary>
    /// Get max health value.
    /// </summary>
    public float GetMaxHealth()
    {
        return healthAdapter?.GetMaxHealth() ?? 100f;
    }

    /// <summary>
    /// Get health as percentage (0-1).
    /// </summary>
    public float GetHealthPercentage()
    {
        return healthAdapter?.GetHealthPercentage() ?? 0f;
    }

    /// <summary>
    /// Check if NPC is currently alive.
    /// </summary>
    public bool IsAlive()
    {
        return !isDead && (healthAdapter?.IsAlive() ?? false);
    }

    /// <summary>
    /// Manually trigger death (for testing or special cases).
    /// </summary>
    public void ForceDeath()
    {
        if (isDead) return;

        // Set health to 0
        if (healthAdapter != null)
        {
            float currentHealth = healthAdapter.GetCurrentHealth();
            healthAdapter.ApplyDamage(currentHealth); // Damage equal to current health = 0
        }

        // Trigger death handler
        HandleDeath(null);
    }

    // ========================================
    // Debug Utilities
    // ========================================

    [ContextMenu("Debug: Take 50 Damage")]
    private void DebugTakeDamage()
    {
        if (damageIn == null)
        {
            Debug.LogError("[NPCHealthHandler] No DamageIn found!");
            return;
        }

        // Create a test damage packet
        var testPacket = new CombatDamagePacket(
            baseDamage: 50f,
            finalDamage: 50f,
            isCriticalHit: false,
            criticalMultiplier: 1f,
            damageType: DamageType.Physical,
            attacker: null,
            attackerId: "Debug Test",
            hitPoint: transform.position,
            hitNormal: Vector3.up,
            attackDirection: Vector3.forward,
            comboCount: 0,
            isHeavyAttack: false,
            weaponId: "TestWeapon"
        );

        damageIn.TakeDamage(testPacket);
    }

    [ContextMenu("Debug: Force Death")]
    private void DebugForceDeath()
    {
        ForceDeath();
    }

    [ContextMenu("Debug: Reset to Alive")]
    private void DebugReset()
    {
        ResetHandler();
    }

    [ContextMenu("Debug: Print Health Status")]
    private void DebugPrintHealthStatus()
    {
        Debug.Log($"=== {parentNPC.NPCName} Health Status ===\n" +
                 $"Alive: {IsAlive()}\n" +
                 $"Dead: {isDead}\n" +
                 $"Health: {GetCurrentHealth():F0} / {GetMaxHealth():F0}\n" +
                 $"Percentage: {GetHealthPercentage() * 100:F0}%\n" +
                 $"AI Enabled: {(aiModule != null ? aiModule.IsEnabled.ToString() : "No AI")}");
    }

    // ========================================
    // Cleanup
    // ========================================

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // ========================================
    // Save Data Structure
    // ========================================

    [System.Serializable]
    private class NPCHealthSaveData
    {
        public bool isDead;
    }
}