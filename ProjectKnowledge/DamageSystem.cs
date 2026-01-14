using UnityEngine;
using System;
using NinjaGame.Stats;

/// <summary>
/// Universal Damage System - Works for all entities (Player, NPC, Objects)
/// 
/// Responsibilities:
/// - Calculate outgoing damage (attacker role)
/// - Process incoming damage (defender role)
/// - Manage health state through StatSystem
/// - Provide combat stat queries (delegates to StatSystem)
/// - Handle death logic
/// - Fire damage events for feedback systems
/// 
/// Integration:
/// - Uses StatSystem for health and combat stats
/// - Queries IDefenseProvider for active defense (block/parry)
/// - Fires events for UI, VFX, and audio systems
/// 
/// Architecture:
/// - Replaces old NPCDamageable (functionality absorbed here)
/// - Implements IHealthProvider (universal health interface)
/// - Implements ICombatStatsProvider (universal combat stats interface)
/// 
/// Phase 1.7b: Universal Systems Consolidation
/// Created: January 2026
/// </summary>
public class DamageSystem : MonoBehaviour,
    IBrainModule,
    IHealthProvider,
    ICombatStatsProvider
{
    #region Inspector Fields

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Configuration (Optional Override)")]
    [Tooltip("Override config for this entity (leave null to use DamageManager's active config)")]
    [SerializeField] private DamageCalculationConfig configOverride;

    [Header("Death Settings")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 2f;
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;

    [Header("Debug")]
    [SerializeField] private bool debugDamage = false;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private StatSystem stats;
    private IDefenseProvider defense;
    private bool isDead = false;

    #endregion

    #region Events

    /// <summary>Fired when this entity deals damage to another</summary>
    public event Action<CombatDamagePacket> OnDamageDealt;

    /// <summary>Fired when this entity takes damage</summary>
    public event Action<CombatDamagePacket> OnDamageTaken;

    /// <summary>Fired when health changes (current health only - IHealthProvider)</summary>
    public event Action<float> OnHealthChanged;

    /// <summary>Fired when health changes with max health (for UI - extra event)</summary>
    public event Action<float, float> OnHealthChangedDetailed; // (current, max)

    /// <summary>Fired when entity dies</summary>
    public event Action OnDeath;

    #endregion

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;

    /// <summary>
    /// Get effective damage config (override or DamageManager's active config)
    /// </summary>
    public DamageCalculationConfig Config
    {
        get
        {
            // Use override if assigned
            if (configOverride != null)
                return configOverride;

            // Otherwise use DamageManager's active config
            if (DamageManager.Instance != null)
                return DamageManager.Instance.ActiveConfig;

            // Fallback
            return null;
        }
    }

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        if (stats == null)
        {
            Debug.LogError($"[DamageSystem] No StatSystem found on {brain.name}! DamageSystem requires StatSystem.");
            isEnabled = false;
            return;
        }

        // Try to get defense provider (optional)
        defense = brain.GetModule<IDefenseProvider>();

        // Validate health stats exist
        ValidateHealthStats();

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] Initialized on {brain.name}. " +
                     $"Health: {GetCurrentHealth()}/{GetMaxHealth()}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        // Check for death each frame
        if (!isDead && GetCurrentHealth() <= 0)
        {
            Die();
        }
    }

    #endregion

    #region IHealthProvider Implementation

    /// <summary>Get current health from StatSystem</summary>
    public float GetCurrentHealth()
    {
        if (stats == null) return 0f;

        // Try common health stat IDs
        if (stats.HasStat("character.health"))
            return stats.GetValue("character.health");
        if (stats.HasStat("character.current_health"))
            return stats.GetValue("character.current_health");
        if (stats.HasStat("resources.health"))
            return stats.GetValue("resources.health");

        Debug.LogWarning($"[DamageSystem] No health stat found on {brain.name}");
        return 0f;
    }

    /// <summary>Get max health from StatSystem</summary>
    public float GetMaxHealth()
    {
        if (stats == null) return 100f;

        // Try common max health stat IDs
        if (stats.HasStat("character.max_health"))
            return stats.GetValue("character.max_health");
        if (stats.HasStat("resources.max_health"))
            return stats.GetValue("resources.max_health");

        // Fallback: use current health as max if no max stat exists
        return GetCurrentHealth();
    }

    /// <summary>Get health as percentage (0-1)</summary>
    public float GetHealthPercentage()
    {
        float max = GetMaxHealth();
        if (max <= 0) return 0f;
        return Mathf.Clamp01(GetCurrentHealth() / max);
    }

    /// <summary>Check if entity is alive</summary>
    public bool IsAlive()
    {
        return !isDead && GetCurrentHealth() > 0;
    }

    /// <summary>Apply raw damage to health (used internally after mitigation)</summary>
    public void ApplyDamage(float amount)
    {
        if (!isEnabled || isDead) return;

        float current = GetCurrentHealth();
        float newHealth = Mathf.Max(0, current - amount);

        SetHealth(newHealth);

        OnHealthChanged?.Invoke(newHealth);
        OnHealthChangedDetailed?.Invoke(newHealth, GetMaxHealth());

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} health: {current:F1} → {newHealth:F1} (-{amount:F1})");
        }
    }

    /// <summary>Apply healing (IHealthProvider interface method)</summary>
    public void ApplyHealing(float amount)
    {
        Heal(amount);
    }

    /// <summary>Heal entity</summary>
    public void Heal(float amount)
    {
        if (!isEnabled || isDead) return;

        float current = GetCurrentHealth();
        float max = GetMaxHealth();
        float newHealth = Mathf.Min(max, current + amount);

        SetHealth(newHealth);

        OnHealthChanged?.Invoke(newHealth);
        OnHealthChangedDetailed?.Invoke(newHealth, max);

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} healed: {current:F1} → {newHealth:F1} (+{amount:F1})");
        }
    }

    /// <summary>Set health directly (bypasses mitigation)</summary>
    public void SetHealth(float value)
    {
        if (stats == null) return;

        // Set health in StatSystem
        if (stats.HasStat("character.health"))
            stats.SetBaseValue("character.health", value);
        else if (stats.HasStat("character.current_health"))
            stats.SetBaseValue("character.current_health", value);
        else if (stats.HasStat("resources.health"))
            stats.SetBaseValue("resources.health", value);
    }

    /// <summary>Set health to maximum (IHealthProvider interface method)</summary>
    public void SetHealthToMax()
    {
        RestoreFullHealth();
    }

    /// <summary>Restore to full health</summary>
    public void RestoreFullHealth()
    {
        float maxHealth = GetMaxHealth();
        SetHealth(maxHealth);
        isDead = false;

        OnHealthChanged?.Invoke(maxHealth);
        OnHealthChangedDetailed?.Invoke(maxHealth, maxHealth);

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} restored to full health");
        }
    }

    #endregion

    #region ICombatStatsProvider Implementation

    /// <summary>Get attack power (adds to base damage)</summary>
    public float GetAttackPower()
    {
        if (stats == null) return 0f;

        if (stats.HasStat("combat.attack_power"))
            return stats.GetValue("combat.attack_power");
        if (stats.HasStat("combat.attack"))
            return stats.GetValue("combat.attack");

        return 0f;
    }

    /// <summary>Get armor (reduces physical damage)</summary>
    public float GetArmor()
    {
        if (stats == null) return 0f;

        if (stats.HasStat("combat.armor"))
            return stats.GetValue("combat.armor");
        if (stats.HasStat("combat.defense"))
            return stats.GetValue("combat.defense");

        return 0f;
    }

    /// <summary>Get magic resistance (reduces magical damage)</summary>
    public float GetMagicResistance()
    {
        if (stats == null) return 0f;

        if (stats.HasStat("combat.magic_resistance"))
            return stats.GetValue("combat.magic_resistance");
        if (stats.HasStat("combat.resistance"))
            return stats.GetValue("combat.resistance");

        return 0f;
    }

    /// <summary>Get critical hit chance (0-100)</summary>
    public float GetCriticalChance()
    {
        if (stats == null) return 0f;

        if (stats.HasStat("combat.critical_chance"))
            return stats.GetValue("combat.critical_chance");
        if (stats.HasStat("combat.crit_chance"))
            return stats.GetValue("combat.crit_chance");

        return 0f;
    }

    /// <summary>Get critical damage multiplier (1.5 = 150% damage)</summary>
    public float GetCriticalMultiplier()
    {
        if (stats == null) return 1.5f;

        if (stats.HasStat("combat.critical_damage"))
            return stats.GetValue("combat.critical_damage");
        if (stats.HasStat("combat.crit_multiplier"))
            return stats.GetValue("combat.crit_multiplier");

        return 1.5f;
    }

    /// <summary>Get armor penetration (0-1, reduces enemy armor effectiveness)</summary>
    public float GetArmorPenetration()
    {
        if (stats == null) return 0f;

        if (stats.HasStat("combat.penetration"))
            return stats.GetValue("combat.penetration");
        if (stats.HasStat("combat.armor_penetration"))
            return stats.GetValue("combat.armor_penetration");

        return 0f;
    }

    #endregion

    #region Damage Calculation (Outgoing)

    /// <summary>
    /// Calculate damage from attack data (used by melee/ranged hitboxes)
    /// </summary>
    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        // PHASE 1: Get weapon damage from equipped weapon (if available)
        float weaponDamage = 0f;
        var weaponBridge = brain?.GetComponentInChildren<EquippedWeaponBridge>();
        if (weaponBridge != null && weaponBridge.HasWeaponEquipped())
        {
            weaponDamage = weaponBridge.GetEquippedWeaponDamage();
        }

        // Priority: Weapon damage > attackData.baseDamage > weaponDamageMultiplier fallback
        float baseDamage = weaponDamage > 0f ? weaponDamage :
                           (attackData.baseDamage > 0f ? attackData.baseDamage :
                            attackData.weaponDamageMultiplier * 10f);

        if (debugDamage && weaponDamage > 0f)
        {
            Debug.Log($"[DamageSystem] Using equipped weapon damage: {weaponDamage:F1}");
        }

        // Add attack power from stats
        baseDamage += GetAttackPower();

        // Apply combo multiplier
        baseDamage *= attackData.comboMultiplier;

        // Roll for critical hit
        bool isCritical = false;
        float critMultiplier = 1f;

        float critChance = GetCriticalChance();
        if (UnityEngine.Random.Range(0f, 100f) < critChance)
        {
            isCritical = true;
            critMultiplier = GetCriticalMultiplier();
            baseDamage *= critMultiplier;
        }

        // Use damage type from attack data
        DamageType damageType = attackData.damageType;

        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: weaponDamage > 0f ? weaponDamage : attackData.baseDamage,
            finalDamage: baseDamage,
            isCriticalHit: isCritical,
            criticalMultiplier: critMultiplier,
            damageType: damageType,
            attacker: attackData.attackerTransform,
            attackerId: attackData.attackerTransform?.name ?? "Unknown",
            hitPoint: attackData.hitPoint,
            hitNormal: attackData.hitNormal,
            attackDirection: (attackData.hitPoint - attackData.attackerTransform.position).normalized,
            comboCount: attackData.comboCount,
            isHeavyAttack: attackData.isHeavyAttack,
            weaponId: attackData.weaponId
        );

        OnDamageDealt?.Invoke(packet);

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} calculated damage: {baseDamage:F1} " +
                     $"(weapon: {weaponDamage:F1}, base: {packet.baseDamage:F1}, crit: {isCritical})");
        }

        return packet;
    }

    /// <summary>
    /// Calculate simple outgoing damage (used by abilities/effects)
    /// </summary>
    public CombatDamagePacket CalculateOutgoingDamage(
        float baseDamage,
        DamageType damageType = DamageType.Physical,
        bool canCrit = true)
    {
        float totalDamage = baseDamage + GetAttackPower();

        // Roll for critical hit (if allowed)
        bool isCritical = false;
        float critMultiplier = 1f;

        if (canCrit)
        {
            float critChance = GetCriticalChance();
            if (UnityEngine.Random.Range(0f, 100f) < critChance)
            {
                isCritical = true;
                critMultiplier = GetCriticalMultiplier();
                totalDamage *= critMultiplier;
            }
        }

        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: baseDamage,
            finalDamage: totalDamage,
            isCriticalHit: isCritical,
            criticalMultiplier: critMultiplier,
            damageType: damageType,
            attacker: transform,
            attackerId: brain.name,
            hitPoint: transform.position,
            hitNormal: Vector3.up,
            attackDirection: Vector3.forward
        );

        OnDamageDealt?.Invoke(packet);

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} dealt {totalDamage:F1} damage " +
                     $"(base: {baseDamage:F1}, power: {GetAttackPower():F1}, crit: {isCritical})");
        }

        return packet;
    }

    #endregion

    #region Damage Application (Incoming)

    /// <summary>
    /// Take damage from a combat packet (full damage calculation)
    /// </summary>
    public void TakeDamage(CombatDamagePacket incomingPacket)
    {
        if (!isEnabled || isDead)
        {
            if (debugDamage)
                Debug.Log($"[DamageSystem] {brain.name} ignoring damage (disabled or dead)");
            return;
        }

        float incomingDamage = incomingPacket.finalDamage;

        // STAGE 1: Active defense (block/parry)
        if (defense != null)
        {
            incomingDamage = defense.ProcessIncomingDamage(incomingDamage, incomingPacket.attackDirection);
        }

        // STAGE 2: Armor/Resistance mitigation
        incomingDamage = ApplyDamageMitigation(incomingDamage, incomingPacket);

        // STAGE 3: Apply final damage
        ApplyDamage(incomingDamage);

        OnDamageTaken?.Invoke(incomingPacket);

        // Audio feedback
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} took {incomingDamage:F1} damage " +
                     $"(incoming: {incomingPacket.finalDamage:F1}, type: {incomingPacket.damageType})");
        }
    }

    /// <summary>
    /// Take simple damage (no packet - creates one internally)
    /// </summary>
    public void TakeDamage(float damage, DamageType damageType = DamageType.Physical)
    {
        CombatDamagePacket packet = CombatDamagePacket.CreateSimple(damage, null, transform.position);
        TakeDamage(packet);
    }

    /// <summary>
    /// Apply damage mitigation based on armor/resistance
    /// </summary>
    private float ApplyDamageMitigation(float incomingDamage, CombatDamagePacket packet)
    {
        float mitigatedDamage = incomingDamage;

        // Use DamageManager config if available
        if (Config != null)
        {
            mitigatedDamage = ApplyConfigBasedMitigation(incomingDamage, packet);
        }
        else
        {
            // Fallback: simple armor/resistance system
            mitigatedDamage = ApplySimpleMitigation(incomingDamage, packet);
        }

        return mitigatedDamage;
    }

    /// <summary>
    /// Data-driven mitigation using DamageManager config
    /// </summary>
    private float ApplyConfigBasedMitigation(float incomingDamage, CombatDamagePacket packet)
    {
        var config = Config.GetConfig(packet.damageType);

        // Check if damage ignores defenses
        if (config.calculationMode == DamageCalculationMode.IgnoreAllDefenses)
        {
            return incomingDamage; // True damage
        }

        float totalMitigation = 0f;

        // Sum all defender stat contributions
        for (int i = 0; i < config.defenderStatIds.Count; i++)
        {
            string statId = config.defenderStatIds[i];
            float multiplier = i < config.defenderStatMultipliers.Count ?
                               config.defenderStatMultipliers[i] : 1f;

            float statValue = stats.GetValue(statId);
            totalMitigation += statValue * multiplier;
        }

        // Apply damage reduction formula
        float damageReduction = totalMitigation / (totalMitigation + 100f);
        return incomingDamage * (1f - damageReduction);
    }

    /// <summary>
    /// Simple mitigation (fallback when no config)
    /// </summary>
    private float ApplySimpleMitigation(float incomingDamage, CombatDamagePacket packet)
    {
        float mitigatedDamage = incomingDamage;

        // Physical damage: reduced by armor
        if (packet.damageType == DamageType.Physical)
        {
            float armor = GetArmor();
            float armorReduction = armor / (armor + 100f);
            mitigatedDamage *= (1f - armorReduction);
        }

        // Magical damage: reduced by magic resistance
        if (packet.damageType == DamageType.Magical ||
            packet.damageType == DamageType.Fire ||
            packet.damageType == DamageType.Lightning ||
            packet.damageType == DamageType.Ice)
        {
            float magicResist = GetMagicResistance();
            float resistReduction = magicResist / (magicResist + 100f);
            mitigatedDamage *= (1f - resistReduction);
        }

        // True damage: no mitigation
        if (packet.damageType == DamageType.True)
        {
            mitigatedDamage = incomingDamage;
        }

        return mitigatedDamage;
    }

    #endregion

    #region Death Handling

    /// <summary>
    /// Handle entity death
    /// </summary>
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        OnDeath?.Invoke();

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} died!");
        }

        // Audio feedback
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        // Visual feedback
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, transform.rotation);
        }

        // Destroy entity (if configured)
        if (destroyOnDeath)
        {
            Destroy(brain.gameObject, destroyDelay);
        }
    }

    /// <summary>
    /// Resurrect entity (for respawning)
    /// </summary>
    public void Resurrect()
    {
        isDead = false;
        RestoreFullHealth();

        if (debugDamage)
        {
            Debug.Log($"[DamageSystem] {brain.name} resurrected!");
        }
    }

    #endregion

    #region Validation & Helpers

    /// <summary>
    /// Validate that health stats exist in StatSystem
    /// </summary>
    private void ValidateHealthStats()
    {
        bool hasHealth = stats.HasStat("character.health") ||
                        stats.HasStat("character.current_health") ||
                        stats.HasStat("resources.health");

        bool hasMaxHealth = stats.HasStat("character.max_health") ||
                           stats.HasStat("resources.max_health");

        if (!hasHealth)
        {
            Debug.LogWarning($"[DamageSystem] No health stat found on {brain.name}! " +
                           "Add 'character.health' or 'resources.health' to StatSchema.");
        }

        if (!hasMaxHealth)
        {
            Debug.LogWarning($"[DamageSystem] No max_health stat found on {brain.name}! " +
                           "Add 'character.max_health' or 'resources.max_health' to StatSchema.");
        }
    }

    /// <summary>
    /// Check if entity is currently defending (blocking/parrying)
    /// </summary>
    public bool IsDefending()
    {
        if (defense == null) return false;

        bool isBlocking = defense.IsBlocking();
        bool isParrying = defense.IsParrying();
        return isBlocking || isParrying;
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Take 10 Damage")]
    private void DebugTakeDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageSystem] Must be in Play Mode!");
            return;
        }

        TakeDamage(10f, DamageType.Physical);
    }

    [ContextMenu("Heal 50 HP")]
    private void DebugHeal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageSystem] Must be in Play Mode!");
            return;
        }

        Heal(50f);
    }

    [ContextMenu("Kill Entity")]
    private void DebugKill()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageSystem] Must be in Play Mode!");
            return;
        }

        SetHealth(0f);
    }

    [ContextMenu("Resurrect Entity")]
    private void DebugResurrect()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageSystem] Must be in Play Mode!");
            return;
        }

        Resurrect();
    }

    [ContextMenu("Print Health")]
    private void DebugPrintHealth()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageSystem] Must be in Play Mode!");
            return;
        }

        Debug.Log($"[DamageSystem] {brain.name} Health: {GetCurrentHealth():F1}/{GetMaxHealth():F1} " +
                 $"({GetHealthPercentage() * 100:F0}%)");
    }

    #endregion
}