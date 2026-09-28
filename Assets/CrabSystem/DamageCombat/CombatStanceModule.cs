using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Which set of hands the entity is fighting with.
/// Armed   — the weapon in weaponSlotId is drawn and drives damage, moveset and hitboxes.
/// Unarmed — the weapon is sheathed (still equipped, still in the slot) and the unarmed
///           profile drives damage, moveset and hitboxes.
/// </summary>
public enum CombatStance
{
    Armed,
    Unarmed,
}

/// <summary>
/// CombatStanceModule — owns the armed/unarmed toggle (default key: R).
///
/// Nothing is unequipped when you go unarmed. The item stays in "mainwep", so stats,
/// tooltips, the paperdoll and the save file never see a stance change. What changes is:
///
///   1. Blackboard  — the "IsUnarmed" fact. This is the one that matters: SlotTransformation-
///                    System.EvaluateBuffTransforms already swaps a hotbar slot's ability while
///                    a named fact is true, so the unarmed moveset is authored on each attack
///                    ability's buffTransforms list rather than swapped in code.
///   2. Damage      — DamageEffect reads RollUnarmedDamage() instead of the weapon dice.
///   3. Visual      — the weapon model reparents from the hand socket to the sheath socket.
///   4. Hitboxes    — WeaponHitbox components filtered to the wrong stance refuse to enable.
///   5. Animator    — the "IsUnarmed" bool, for fist idle/locomotion.
///
/// Equipping into the weapon slot draws the weapon; emptying the slot forces unarmed.
///
/// Setup:
///   1. Add this component next to the other brain modules.
///   2. Author a DiceProfile asset for fists (1d4, flat bonus +2) and assign it.
///   3. Add a "mainwep_sheathed" entry to ModelSocketProvider.namedSockets on each model,
///      pointing at a back or hip bone. Without it the weapon just hides instead.
///   4. On each armed attack ability, add a buffTransforms entry: blackboardKey "IsUnarmed",
///      transformAbility = the unarmed variant. SlotTransformationSystem does the rest.
/// </summary>
public class CombatStanceModule : MonoBehaviour, IBrainModule
{
    [Header("Unarmed Profile")]
    [Tooltip("DiceProfile rolled on every unarmed hit. Author it as 1d4 with a flat bonus of 2.")]
    [SerializeField] private DiceProfile unarmedWeapon;

    [Header("Slots & Sockets")]
    [Tooltip("Equipment slot this stance sheathes and draws from.")]
    [SerializeField] private string weaponSlotId = "mainwep";

    [Tooltip("Named socket in ModelSocketProvider the weapon reparents to while sheathed. " +
             "If the model has no such socket the weapon is hidden instead.")]
    [SerializeField] private string sheathSocketId = "mainwep_sheathed";

    [Header("Transition")]
    [Tooltip("Optional ability played when sheathing. Put it on the Full Body Actions layer " +
             "so it blocks attacks until AnimUnlocked fires. Leave empty for an instant swap.")]
    [SerializeField] private AbilityDefinition sheatheAbility;

    [Tooltip("Optional ability played when drawing. Same rules as the sheathe ability.")]
    [SerializeField] private AbilityDefinition drawAbility;

    [Tooltip("Seconds to wait before the stance actually commits, so the weapon moves partway " +
             "through the sheathe/draw clip rather than on frame one. 0 = commit immediately.")]
    [SerializeField] private float transitionDelay = 0f;

    [Header("Wiring")]
    [Tooltip("Animator bool set to true while unarmed. Leave empty to skip.")]
    [SerializeField] private string animatorBool = "IsUnarmed";

    [Tooltip("Blackboard fact set to true while unarmed. Leave empty to skip.")]
    [SerializeField] private string blackboardFact = "IsUnarmed";

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private ControllerBrain brain;
    private EquipmentSystem equipment;
    private AbilitySystem abilities;
    private Blackboard blackboard;

    private CombatStance stance = CombatStance.Unarmed;
    private bool transitionPending;

    public bool IsEnabled { get; set; } = true;

    public CombatStance Stance => stance;
    public bool IsUnarmed => stance == CombatStance.Unarmed;
    public DiceProfile UnarmedWeapon => unarmedWeapon;

    public event Action<CombatStance> OnStanceChanged;

    /// <summary>Rolls the unarmed damage expression. Returns 0 if no profile is assigned.</summary>
    public float RollUnarmedDamage() => unarmedWeapon != null ? unarmedWeapon.RollDamage() : 0f;

    /// <summary>Tooltip label for the unarmed profile — e.g. "1d4 + 2".</summary>
    public string UnarmedDamageLabel() => unarmedWeapon != null ? unarmedWeapon.DamageLabel() : "—";

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        equipment = brain.GetModule<EquipmentSystem>();
        abilities = brain.GetModule<AbilitySystem>();
        blackboard = brain.Blackboard;

        if (unarmedWeapon == null)
            Debug.LogWarning($"[CombatStance] No unarmed DiceProfile on {name} — unarmed hits will roll 0.");
    }

    public void LateInitialize()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged += HandleEquipmentChanged;

        GameEvents.OnLoadCompleted += HandleLoadCompleted;

        SyncToEquippedWeapon();
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        if (brain == null || brain.Input == null) return;
        if (!brain.Input.ToggleStancePressed) return;

        ToggleStance();
    }

    private void OnDestroy()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;

        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
    }

    #endregion

    #region Toggle

    /// <summary>
    /// Flips stance, playing the sheathe/draw ability if one is assigned and usable.
    /// Refuses to draw when the weapon slot is empty.
    /// </summary>
    public void ToggleStance()
    {
        SetStance(IsUnarmed ? CombatStance.Armed : CombatStance.Unarmed);
    }

    public void SetStance(CombatStance target)
    {
        if (target == stance) return;
        if (transitionPending) return;

        if (target == CombatStance.Armed && GetEquippedWeaponItem() == null)
        {
            if (debugLogging)
                Debug.Log($"[CombatStance] Cannot draw — '{weaponSlotId}' is empty.");
            return;
        }

        var transition = target == CombatStance.Unarmed ? sheatheAbility : drawAbility;
        PlayTransition(transition);

        if (transitionDelay <= 0f)
        {
            ApplyStance(target);
            return;
        }

        transitionPending = true;
        StartCoroutine(CommitAfterDelay(target));
    }

    private IEnumerator CommitAfterDelay(CombatStance target)
    {
        yield return new WaitForSeconds(transitionDelay);
        transitionPending = false;
        ApplyStance(target);
    }

    private void PlayTransition(AbilityDefinition ability)
    {
        if (ability == null || abilities == null) return;
        if (!abilities.CanUseAbility(ability.abilityId)) return;

        abilities.UseAbility(ability.abilityId);
    }

    /// <summary>
    /// Commits the stance — moveset, visual, animator and blackboard. Public so a sheathe
    /// or draw animation event can call it at the exact frame the hand meets the scabbard.
    /// </summary>
    public void ApplyStance(CombatStance target)
    {
        transitionPending = false;

        if (target == stance) return;

        stance = target;
        bool unarmed = stance == CombatStance.Unarmed;

        ApplyBlackboard(unarmed);
        ApplyVisual(unarmed);
        ApplyAnimator(unarmed);

        OnStanceChanged?.Invoke(stance);

        if (debugLogging)
            Debug.Log($"[CombatStance] {brain.EntityName} → {stance}");
    }

    #endregion

    #region Application

    private void ApplyVisual(bool sheathed)
    {
        var model = brain != null ? brain.Model : null;
        if (model == null) return;

        Transform hand = model.GetSocket(weaponSlotId);
        Transform sheath = model.GetNamedSocket(sheathSocketId);

        if (hand == null) return;

        if (sheath == null)
        {
            SetSocketVisible(hand, !sheathed);
            return;
        }

        if (sheathed)
            ModelModule.MoveSocketContents(hand, sheath);
        else
            ModelModule.MoveSocketContents(sheath, hand);
    }

    private void ApplyAnimator(bool unarmed)
    {
        var animation = brain != null ? brain.Animation : null;
        if (animation == null) return;
        if (string.IsNullOrEmpty(animatorBool)) return;
        if (!animation.HasParameter(animatorBool)) return;

        animation.SetBool(animatorBool, unarmed);
    }

    private void ApplyBlackboard(bool unarmed)
    {
        if (blackboard == null) blackboard = brain?.Blackboard;
        if (blackboard == null) return;
        if (string.IsNullOrEmpty(blackboardFact)) return;

        blackboard.SetBool(new BlackboardKey(blackboardFact).hash, unarmed);
    }

    private static void SetSocketVisible(Transform socket, bool visible)
    {
        foreach (Transform child in socket)
            child.gameObject.SetActive(visible);
    }

    #endregion

    #region Equipment Sync

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
        SyncToEquippedWeapon();
    }

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        if (slot == null || slot.slotId != weaponSlotId) return;

        SetStanceImmediate(item != null ? CombatStance.Armed : CombatStance.Unarmed);
    }

    /// <summary>
    /// Puts the stance where the equipment says it should be, skipping the transition
    /// animation and the empty-slot guard. Used at load and on equip/unequip.
    /// </summary>
    private void SyncToEquippedWeapon()
    {
        SetStanceImmediate(GetEquippedWeaponItem() != null ? CombatStance.Armed : CombatStance.Unarmed);
    }

    private void SetStanceImmediate(CombatStance target)
    {
        if (target == stance) return;

        transitionPending = false;
        StopAllCoroutines();
        ApplyStance(target);
    }

    private ItemInstance GetEquippedWeaponItem()
    {
        if (equipment == null) equipment = brain?.GetModule<EquipmentSystem>();
        return equipment?.GetEquippedItem(weaponSlotId);
    }

    #endregion
}
