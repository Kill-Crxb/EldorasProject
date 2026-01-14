using UnityEngine;

/// <summary>
/// Bridges PlayerItemsModule (inventory) with DamageModule (combat)
/// 
/// Simple solution for Phase 1.1:
/// - Checks what weapon is equipped
/// - Provides weapon damage to DamageModule
/// - Uses existing ItemInstance.baseStats.damage
/// 
/// Future Phase 3:
/// - Full equipment visual system
/// - WeaponData integration
/// - Socket-based attachment
/// </summary>
public class EquippedWeaponBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerItemsModule playerItems;
    [SerializeField] private DamageSystem damageSystem;

    [Header("Debug")]
    [SerializeField] private bool debugWeapon = true;

    // Cached
    private ControllerBrain brain;
    private ItemInstance cachedMainHandWeapon;
    private float cachedWeaponDamage;

    void Start()
    {
        brain = GetComponent<ControllerBrain>();

        // Auto-find modules if not assigned
        if (playerItems == null)
            playerItems = GetComponentInChildren<PlayerItemsModule>();

        if (damageSystem == null)
            damageSystem = brain?.GetModule<DamageSystem>();

        if (playerItems == null)
        {
            Debug.LogError("[EquippedWeaponBridge] PlayerItemsModule not found!");
            enabled = false;
            return;
        }

        // Subscribe to equipment changes
        if (playerItems != null)
        {
            playerItems.OnEquipmentChanged += OnEquipmentChanged;
        }

        // Cache initial weapon
        UpdateCachedWeapon();
    }

    void OnDestroy()
    {
        if (playerItems != null)
        {
            playerItems.OnEquipmentChanged -= OnEquipmentChanged;
        }
    }

    /// <summary>
    /// Called when any equipment slot changes
    /// </summary>
    private void OnEquipmentChanged(EquipmentSlot slot)
    {
        // Only care about weapon slots
        if (slot == EquipmentSlot.Weapon1 || slot == EquipmentSlot.Weapon2)
        {
            UpdateCachedWeapon();
        }
    }

    /// <summary>
    /// Update cached weapon reference and damage
    /// </summary>
    private void UpdateCachedWeapon()
    {
        // Get main hand weapon (Weapon1 slot)
        cachedMainHandWeapon = playerItems.GetEquippedItem(EquipmentSlot.Weapon1);

        if (cachedMainHandWeapon != null)
        {
            // Get damage from ItemInstance
            var definition = ItemDatabase.GetDefinition(cachedMainHandWeapon.definitionId);

            if (definition != null && definition.category == ItemCategory.Weapon)
            {
                // Base damage scaled by tier
                cachedWeaponDamage = definition.baseStats.damage * GetTierMultiplier(cachedMainHandWeapon);

                if (debugWeapon)
                {
                    Debug.Log($"[WeaponBridge] Equipped: {definition.displayName} " +
                             $"(Tier: {cachedMainHandWeapon.currentTier}, Damage: {cachedWeaponDamage:F1})");
                }
            }
            else
            {
                cachedWeaponDamage = 0f;
                Debug.LogWarning($"[WeaponBridge] Equipped item is not a weapon!");
            }
        }
        else
        {
            cachedWeaponDamage = 0f;

            if (debugWeapon)
            {
                Debug.Log("[WeaponBridge] No weapon equipped (unarmed)");
            }
        }
    }

    /// <summary>
    /// Get tier multiplier for scaling weapon damage
    /// </summary>
    private float GetTierMultiplier(ItemInstance item)
    {
        var definition = ItemDatabase.GetDefinition(item.definitionId);

        if (definition == null || definition.tierScaling == null)
            return 1.0f;

        int tierIndex = (int)item.currentTier;

        if (tierIndex >= 0 && tierIndex < definition.tierScaling.tierMultipliers.Length)
        {
            return definition.tierScaling.tierMultipliers[tierIndex];
        }

        return 1.0f;
    }

    #region Public API

    /// <summary>
    /// Get currently equipped weapon damage
    /// Called by DamageModule or AbilityDefinition
    /// </summary>
    public float GetEquippedWeaponDamage()
    {
        return cachedWeaponDamage;
    }

    /// <summary>
    /// Get equipped weapon instance (for additional properties)
    /// </summary>
    public ItemInstance GetEquippedWeapon()
    {
        return cachedMainHandWeapon;
    }

    /// <summary>
    /// Check if player has a weapon equipped
    /// </summary>
    public bool HasWeaponEquipped()
    {
        return cachedMainHandWeapon != null;
    }

    /// <summary>
    /// Get weapon attack speed modifier (if needed)
    /// </summary>
    public float GetWeaponAttackSpeed()
    {
        if (cachedMainHandWeapon == null)
            return 1.0f;

        var definition = ItemDatabase.GetDefinition(cachedMainHandWeapon.definitionId);

        if (definition != null)
        {
            return definition.baseStats.attackSpeed;
        }

        return 1.0f;
    }

    #endregion

    #region Debug

    [ContextMenu("Debug: Show Equipped Weapon")]
    private void DebugShowWeapon()
    {
        UpdateCachedWeapon();

        if (cachedMainHandWeapon != null)
        {
            var def = ItemDatabase.GetDefinition(cachedMainHandWeapon.definitionId);
            Debug.Log("=== EQUIPPED WEAPON ===");
            Debug.Log($"Name: {def?.displayName ?? "Unknown"}");
            Debug.Log($"Tier: {cachedMainHandWeapon.currentTier}");
            Debug.Log($"Base Damage: {def?.baseStats.damage ?? 0f}");
            Debug.Log($"Scaled Damage: {cachedWeaponDamage:F1}");
            Debug.Log($"Attack Speed: {def?.baseStats.attackSpeed ?? 1f}");
            Debug.Log("=======================");
        }
        else
        {
            Debug.Log("[WeaponBridge] No weapon equipped");
        }
    }

    #endregion
}