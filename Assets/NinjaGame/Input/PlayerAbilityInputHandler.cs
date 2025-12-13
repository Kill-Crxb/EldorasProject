using UnityEngine;

public class PlayerAbilityInputHandler : MonoBehaviour, IBrainModule
{
    [Header("Combo Settings")]
    [SerializeField] private float comboWindow = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugInput = false;

    private IInputProvider inputProvider;
    private IAbilityProvider abilityProvider;
    private AbilityLoadoutModule loadoutModule;
    private ControllerBrain brain;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        inputProvider = brain.GetModuleImplementing<IInputProvider>();
        abilityProvider = brain.GetModuleImplementing<IAbilityProvider>();
        loadoutModule = brain.GetModule<AbilityLoadoutModule>();

        if (inputProvider == null)
        {
            Debug.LogWarning("[PlayerAbilityInputHandler] No IInputProvider found!");
            IsEnabled = false;
            return;
        }

        if (abilityProvider == null)
        {
            Debug.LogWarning("[PlayerAbilityInputHandler] No IAbilityProvider found!");
            IsEnabled = false;
            return;
        }

        if (loadoutModule == null)
        {
            Debug.LogError("[PlayerAbilityInputHandler] No AbilityLoadoutModule found! " +
                          "Add AbilityLoadoutModule to your entity.");
            IsEnabled = false;
            return;
        }

        if (debugInput)
        {
            Debug.Log("[PlayerAbilityInputHandler] Initialized");
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled || inputProvider == null || abilityProvider == null || loadoutModule == null)
            return;

        HandleBasicAttackInput();
        HandleMovementInput();
        HandleQuickslotInput();
    }

    private void HandleBasicAttackInput()
    {
        if (inputProvider.LightAttackPressed)
        {
            UseAbilityFromSlot("BasicAttack");
        }
    }

    private void HandleMovementInput()
    {
        if (inputProvider.DashPressed)
        {
            // TODO: Hook up dash when movement abilities are ready
            // UseAbilityFromSlot("Dash");
        }
    }

    private void HandleQuickslotInput()
    {
        if (inputProvider.AbilityQPressed)
        {
            UseAbilityFromSlot("Q");
        }

        if (inputProvider.AbilityZPressed)
        {
            UseAbilityFromSlot("Z");
        }

        if (inputProvider.AbilityXPressed)
        {
            UseAbilityFromSlot("X");
        }

        if (inputProvider.AbilityCPressed)
        {
            UseAbilityFromSlot("C");
        }

        if (inputProvider.AbilityVPressed)
        {
            UseAbilityFromSlot("V");
        }
    }

    private void UseAbilityFromSlot(string slotKey)
    {
        var ability = loadoutModule.GetCurrentAbilityForSlot(slotKey);

        if (ability == null)
        {
            if (debugInput)
                Debug.Log($"[PlayerAbilityInputHandler] No ability assigned to slot {slotKey}");
            return;
        }

        if (debugInput)
        {
            var slotData = loadoutModule.GetSlotData(slotKey);
            int comboIndex = loadoutModule.GetComboIndex(slotKey);
            Debug.Log($"[PlayerAbilityInputHandler] {slotKey}: {ability.abilityName} " +
                      $"(combo {comboIndex + 1}/{slotData.ChainLength})");
        }

        // Try to use the ability
        // CanUseAbility checks isAnimationLocked, so this will fail if we're still locked
        bool abilityStarted = abilityProvider.CanUseAbility(ability.abilityId);

        if (abilityStarted)
        {
            // Mark slot as used for combo timing
            loadoutModule.MarkSlotUsed(slotKey);

            // Trigger the ability
            abilityProvider.UseAbility(ability.abilityId);

            // Advance combo ONLY if we successfully started the ability
            // This means we were in the cancel window (animation unlocked)
            var slotData = loadoutModule.GetSlotData(slotKey);
            if (slotData != null && slotData.IsCombo)
            {
                loadoutModule.AdvanceCombo(slotKey);

                if (debugInput)
                {
                    int newIndex = loadoutModule.GetComboIndex(slotKey);
                    Debug.Log($"[PlayerAbilityInputHandler] Advanced {slotKey} combo to {newIndex + 1}/{slotData.ChainLength}");
                }
            }
        }
        else
        {
            if (debugInput)
            {
                Debug.Log($"[PlayerAbilityInputHandler] Cannot use {ability.abilityName} - locked or on cooldown");
            }
        }
    }

    private void OnDestroy()
    {
        // No longer need to subscribe to OnAbilityCastComplete
    }
}