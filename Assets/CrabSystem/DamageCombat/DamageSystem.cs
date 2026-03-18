using NinjaGame.Stats;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles damage calculation, damage reception, and death for any entity with a Brain.
/// Includes automatic hurtbox setup for clean damage detection.
/// </summary>
public class DamageSystem : MonoBehaviour, IBrainModule
{
    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Header("Hurtbox Configuration")]
    [Tooltip("The collider that receives damage. Auto-created if null.")]
    [SerializeField] private Collider hurtbox;
    [SerializeField] private bool autoSetupHurtbox = true;
    [SerializeField] private float hurtboxRadius = 0.5f;
    [SerializeField] private float hurtboxHeight = 2f;
    [SerializeField] private Vector3 hurtboxCenter = new Vector3(0, 1, 0);

    // IBrainModule implementation
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private StatSystem stats;
    private IHealthProvider health;
    private IDefenseProvider defense;

    private bool isDead;

    // Public accessors
    public ControllerBrain Brain => brain;
    public Collider Hurtbox => hurtbox;

    // Events
    public event Action<float> OnHealthChanged;
    public event Action<float, float> OnHealthChangedDetailed;
    public event Action OnDeath;
    public event Action<CombatDamagePacket> OnDamageDealt;
    public event Action<CombatDamagePacket> OnDamageTaken;

    private void Awake()
    {
        if (autoSetupHurtbox)
        {
            SetupHurtbox();
        }
    }

    private void SetupHurtbox()
    {
        if (hurtbox != null)
            return;

        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        hurtboxGO.transform.localRotation = Quaternion.identity;

        var capsule = hurtboxGO.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = hurtboxRadius;
        capsule.height = hurtboxHeight;
        capsule.center = hurtboxCenter;

        hurtbox = capsule;
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        // Get ResourceSystem which implements IHealthProvider
        health = brain.ResourceSys;

        // Defense provider may not exist (optional)
        defense = brain.GetModule<IDefenseProvider>();

        if (stats == null || health == null)
        {
            isEnabled = false;
            Debug.LogError($"[DamageSystem] Missing dependencies on {brain.name} - Stats: {stats != null}, Health: {health != null}");
            return;
        }

        health.OnDeath += Die;
        health.OnHealthChanged += HandleHealthChanged;

        Debug.Log($"[DamageSystem] Initialized on {brain.name} - Health provider ready!");
    }

    public void UpdateModule()
    {
        // DamageSystem is event-driven, no per-frame logic needed
    }

    private void HandleHealthChanged(float current)
    {
        OnHealthChanged?.Invoke(current);
        OnHealthChangedDetailed?.Invoke(current, health.GetMaxHealth());
    }

    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        var config = DamageManager.Instance?.ActiveConfig?.GetConfig(attackData.damageType);
        float damage = attackData.baseDamage + GetDynamicStat(config?.attackerStatIds);

        bool crit = false;
        float critMult = 1f;

        // Crit calculation - guard clauses first
        if (config == null || !config.canCrit || stats == null)
        {
            // No crit possible - skip
        }
        else
        {
            // Query crit stats directly
            float critChance = stats.GetValue("combat.crit_chance", 5f);

            if (UnityEngine.Random.value * 100f < critChance)
            {
                crit = true;
                float critDamage = stats.GetValue("combat.crit_damage", 1.5f);
                damage *= critDamage;
                critMult = critDamage;
            }
        }

        float finalDamage = ApplyMitigation(damage, config);

        var packet = new CombatDamagePacket(
            attackData.baseDamage,
            finalDamage,
            crit,
            critMult,
            attackData.damageType,
            attackData.attackerTransform,
            attackData.attackerTransform?.name ?? "Unknown",
            attackData.hitPoint,
            attackData.hitNormal,
            attackData.hitPoint - (attackData.attackerTransform?.position ?? Vector3.zero)
        );

        OnDamageDealt?.Invoke(packet);
        return packet;
    }

    public void TakeDamage(CombatDamagePacket packet)
    {
        if (!isEnabled || isDead) return;

        float dmg = packet.finalDamage;

        if (defense != null)
            dmg = defense.ProcessIncomingDamage(dmg, packet.attackDirection);

        health.ApplyDamage(dmg);
        OnDamageTaken?.Invoke(packet);
    }

    private float ApplyMitigation(float dmg, DamageTypeConfig config)
    {
        if (config == null) return dmg;

        float mitigation = GetDynamicStat(config.defenderStatIds);
        float reduction = mitigation / (mitigation + 100f);
        return dmg * (1f - reduction);
    }

    private float GetDynamicStat(List<string> statIds)
    {
        if (stats == null || statIds == null) return 0f;
        float total = 0f;
        foreach (var id in statIds)
            total += stats.GetValue(id);
        return total;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure hurtbox stays a trigger in editor
        if (hurtbox != null)
        {
            hurtbox.isTrigger = true;
        }
    }
#endif
}