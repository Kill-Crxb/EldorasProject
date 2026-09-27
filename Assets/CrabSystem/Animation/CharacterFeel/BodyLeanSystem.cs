using UnityEngine;

// Banks into turns and pitches into acceleration by rotating a pivot ABOVE the Animator.
//
// This is deliberately not an IK receiver. animator.bodyRotation is applied after the IK pass
// resolves, so foot IK would never see the lean and the feet would swing out from under her.
// Rotating an ancestor of the Animator in Update happens before the animation update, so every
// world position the IK pass reads — GetIKPosition included — already carries the lean.
public class BodyLeanSystem : MonoBehaviour, IBrainModule
{
    [Header("Pivot")]
    [Tooltip("A transform ABOVE the Animator, whose local rotation nothing else drives. Make an " +
             "empty child of the character root, move the model root under it, and assign it " +
             "here. Assigning something that movement or facing already rotates will fight.")]
    [SerializeField] private Transform leanPivot;

    [Header("Bank")]
    [Tooltip("Degrees of roll per degree-per-second of turn. Negate it if she banks the wrong way.")]
    [SerializeField] private float bankPerTurnRate = 0.03f;
    [SerializeField] private float maxBankAngle = 8f;

    [Header("Pitch")]
    [Tooltip("Degrees of forward pitch per m/s² of acceleration. Negative values lean back.")]
    [SerializeField] private float pitchPerAcceleration = 0.6f;
    [SerializeField] private float maxPitchAngle = 6f;

    [Header("Scaling")]
    [Tooltip("Speed at which the lean reaches full strength. Below it the lean scales down, so " +
             "a slow turn on the spot does not throw her sideways.")]
    [SerializeField] private float referenceSpeed = 6f;
    [SerializeField] private float leanSmoothing = 8f;

    [Tooltip("Airborne turning has nothing to lean against.")]
    [SerializeField] private bool groundedOnly = true;

    private ControllerBrain brain;
    private MovementSystem movement;
    private CharacterMotor motor;

    private Quaternion basePivotRotation = Quaternion.identity;
    private bool pivotValid;

    private float previousYaw;
    private float previousSpeed;
    private float currentBank;
    private float currentPitch;

    public bool IsEnabled { get; set; } = true;

    public float CurrentBank => currentBank;
    public float CurrentPitch => currentPitch;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        movement = brain?.Movement;
        motor = FeelReferences.FindMotor(brain);

        if (leanPivot == null)
        {
            Debug.LogError($"[BodyLeanSystem] No leanPivot assigned on '{name}'. Lean is disabled. It must be a transform above the Animator that nothing else rotates.", this);
            IsEnabled = false;
            return;
        }

        basePivotRotation = leanPivot.localRotation;
        pivotValid = true;
        previousYaw = FacingYaw();
        previousSpeed = movement != null ? movement.Speed : 0f;
    }

    public void UpdateModule()
    {
        if (!pivotValid) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float yaw = FacingYaw();
        float turnRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
        previousYaw = yaw;

        float speed = movement != null ? movement.Speed : 0f;
        float acceleration = (speed - previousSpeed) / dt;
        previousSpeed = speed;

        float targetBank = 0f;
        float targetPitch = 0f;

        if (IsEnabled && (!groundedOnly || IsGrounded()))
        {
            float scale = referenceSpeed > 0f ? Mathf.Clamp01(speed / referenceSpeed) : 1f;

            targetBank = Mathf.Clamp(-turnRate * bankPerTurnRate, -maxBankAngle, maxBankAngle) * scale;
            targetPitch = Mathf.Clamp(acceleration * pitchPerAcceleration, -maxPitchAngle, maxPitchAngle) * scale;
        }

        float blend = 1f - Mathf.Exp(-leanSmoothing * dt);
        currentBank = Mathf.Lerp(currentBank, targetBank, blend);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, blend);

        leanPivot.localRotation = basePivotRotation * Quaternion.Euler(currentPitch, 0f, currentBank);
    }

    private float FacingYaw()
    {
        if (brain != null && brain.EntityRoot != null) return brain.EntityRoot.eulerAngles.y;
        return transform.eulerAngles.y;
    }

    private bool IsGrounded()
    {
        if (motor != null) return motor.IsGrounded;
        if (movement != null) return movement.IsGrounded;
        return brain != null && brain.IsGrounded;
    }

    void OnDisable()
    {
        if (pivotValid) leanPivot.localRotation = basePivotRotation;
    }
}
