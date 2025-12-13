using UnityEngine;

public class InputModule : MonoBehaviour, IBrainModule, IInputProvider
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Input State - Movement")]
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool DashPressed { get; private set; }

    [Header("Input State - Combat")]
    public bool LightAttackPressed { get; private set; }
    public bool HeavyAttackPressed { get; private set; }
    public bool BlockHeld { get; private set; }
    public bool ParryPressed { get; private set; }

    [Header("Input State - Ability Quickslots")]
    public bool AbilityQPressed { get; private set; }
    public bool AbilityZPressed { get; private set; }
    public bool AbilityXPressed { get; private set; }
    public bool AbilityCPressed { get; private set; }
    public bool AbilityVPressed { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugInput = false;

    private ControllerBrain brain;
    private PlayerInputControls inputActions;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Use the Brain's PlayerInputControls instead of creating our own
        inputActions = brain.GetInputControls();

        if (debugInput)
            Debug.Log($"[InputModule] Initialized for {gameObject.name}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        ReadMovementInput();
        ReadCombatInput();
        ReadAbilityInput();
    }

    private void ReadMovementInput()
    {
        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        LookInput = inputActions.Player.Look.ReadValue<Vector2>();

        JumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
        JumpHeld = inputActions.Player.Jump.IsPressed();

        SprintHeld = inputActions.Player.Sprint.IsPressed();
        DashPressed = false; // TODO: Add Dash to PlayerInputControls
    }

    private void ReadCombatInput()
    {
        // Use existing Attack action for light attack
        LightAttackPressed = inputActions.Player.Attack.WasPressedThisFrame();
        HeavyAttackPressed = false; // TODO: Add HeavyAttack to PlayerInputControls

        BlockHeld = inputActions.Player.Block.IsPressed();
        ParryPressed = false; // TODO: Add Parry to PlayerInputControls
    }

    private void ReadAbilityInput()
    {
        AbilityQPressed = inputActions.Player.QuickslotQ.WasPressedThisFrame();
        AbilityZPressed = inputActions.Player.QuickslotZ.WasPressedThisFrame();
        AbilityXPressed = inputActions.Player.QuickslotX.WasPressedThisFrame();
        AbilityCPressed = inputActions.Player.QuickslotC.WasPressedThisFrame();
        AbilityVPressed = inputActions.Player.QuickslotV.WasPressedThisFrame();
    }

    // No OnDestroy needed - Brain owns and cleans up PlayerInputControls
}