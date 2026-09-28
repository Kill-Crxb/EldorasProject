// Editor-only. Three of these scan the scene with FindObjectsOfType every frame, and
// none of them belong in a player build.
#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// On-screen movement readout for the parkour controller. OnGUI, so it needs no canvas.
///
/// ⚠ This was previously bound to ARPGLocomotionHandler with a hard cast, so the moment slice 1
/// swapped the handler the cast returned null and OnGUI returned before drawing anything. It had
/// been silently blank ever since. It now reads the systems rather than one concrete handler, so
/// swapping handlers again degrades it to fewer rows instead of switching it off.
///
/// The point of this is not to admire the numbers. Every row exists to answer a question that has
/// actually cost time: does the gait reach its wish speed, which slope band am I in, and — the one
/// that is otherwise invisible — WHY is a posture change being refused.
/// </summary>
public class MovementDebugDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ControllerBrain to monitor. Left empty, the first brain flagged IsPlayer is found.")]
    [SerializeField] private ControllerBrain brain;

    [Header("Display")]
    [SerializeField] private bool showDebug = true;

    [Tooltip("Toggle the overlay with the Hotbar1 key.")]
    [SerializeField] private bool useHotbar1Toggle = true;

    [SerializeField] private float xOffset = 10f;
    [SerializeField] private float yOffset = 10f;
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private float panelWidth = 430f;
    [SerializeField] private int fontSize = 14;

    [Header("Colours")]
    [SerializeField] private Color okColor = new Color(0.45f, 0.95f, 0.5f);
    [SerializeField] private Color warnColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color badColor = new Color(1f, 0.45f, 0.4f);
    [SerializeField] private Color idleColor = new Color(0.65f, 0.65f, 0.65f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);

    private InputSystem inputSystem;
    private MovementSystem movementSystem;
    private StateMachineModule stateMachine;
    private ParkourLocomotionHandler parkour;
    private CharacterMotor motor;
    private Blackboard blackboard;

    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle backgroundStyle;
    private bool stylesReady;

    // Running cursor for the row helper, so adding a row is one call and never a layout edit.
    private float rowX;
    private float rowY;

    void Start() => Resolve();

    void Update()
    {
        if (useHotbar1Toggle && inputSystem != null && inputSystem.Hotbar1Pressed)
            showDebug = !showDebug;

        if (brain == null || movementSystem == null) Resolve();
    }

    void Resolve()
    {
        if (brain == null)
        {
            ControllerBrain[] brains = FindObjectsOfType<ControllerBrain>();

            for (int i = 0; i < brains.Length; i++)
            {
                if (!brains[i].IsPlayer) continue;
                brain = brains[i];
                break;
            }
        }

        if (brain == null) return;

        inputSystem = brain.GetModule<InputSystem>();
        movementSystem = brain.GetModule<MovementSystem>();
        stateMachine = brain.GetModule<StateMachineModule>();
        blackboard = brain.Blackboard;

        // Read through the interface, not a concrete handler — that hard cast is what broke this
        // display last time. A handler that is not the parkour one simply leaves those rows blank.
        parkour = movementSystem != null ? movementSystem.Locomotion as ParkourLocomotionHandler : null;

        Transform root = brain.EntityRoot != null ? brain.EntityRoot : brain.transform;
        motor = root.GetComponent<CharacterMotor>();
    }

    void OnGUI()
    {
        if (!showDebug || movementSystem == null) return;

        InitStyles();

        MovementProfile profile = parkour != null ? parkour.Profile : null;
        int rows = profile != null ? 14 : 8;

        GUI.Box(new Rect(xOffset, yOffset, panelWidth, lineHeight * rows + 18f), "", backgroundStyle);

        rowX = xOffset + 10f;
        rowY = yOffset + 8f;

        DrawSpeed(profile);
        DrawGait(profile);
        DrawFacts();
        DrawGround(profile);
        DrawVelocity();

        if (profile == null)
        {
            Row("Handler", "not ParkourLocomotionHandler — rows below unavailable", warnColor);
            Row("", "[1] toggles this overlay", idleColor);
            return;
        }

        DrawCapsule();
        DrawFriction();
        DrawJumps();
        Row("", "", idleColor);
        DrawPosture();
        DrawCrouchInput();
        DrawCrouchDiagnosis();
        Row("", "[1] toggles this overlay", idleColor);
    }

    // ── Rows ──────────────────────────────────────────────────────────────────────────────

    /// <summary>One label/value line. Replaces a method per row — they were identical but six.</summary>
    void Row(string label, string value, Color color)
    {
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(rowX, rowY, 120f, lineHeight), label, labelStyle);

        valueStyle.normal.textColor = color;
        GUI.Label(new Rect(rowX + 120f, rowY, panelWidth - 130f, lineHeight), value, valueStyle);

        rowY += lineHeight;
    }

    void DrawSpeed(MovementProfile profile)
    {
        float speed = movementSystem.Speed;

        if (profile == null)
        {
            Row("Speed", $"{speed:F2} m/s", idleColor);
            return;
        }

        // Target is what the CURRENT gait asks for. A gait that settles below its target means the
        // accel/friction ratio has fallen under 1 and wishSpeed has stopped meaning anything.
        float target = ResolveGaitSpeed(profile);
        bool moving = speed > 0.2f;
        bool carrying = speed > target + 0.15f;
        bool arrived = speed >= target - 0.15f;

        // Three states, not two. "Carrying" is the momentum band — speed above the gait, bleeding
        // at momentumFriction instead of groundFriction. Without this row you cannot tell earned
        // speed from a gait that simply reads high.
        if (!moving) { Row("Speed", $"{speed:F2} / {target:F2} m/s", idleColor); return; }

        if (carrying)
        {
            Row("Speed", $"{speed:F2} / {target:F2} m/s   CARRYING +{speed - target:F2}", okColor);
            return;
        }

        string note = arrived ? "  at target" : $"  SHORT of {target:F2}";
        Row("Speed", $"{speed:F2} / {target:F2} m/s{note}", arrived ? okColor : warnColor);
    }

    void DrawGait(MovementProfile profile)
    {
        bool granted = blackboard != null && blackboard.GetBool(BlackboardKey.SprintGranted);
        bool denied = blackboard != null && blackboard.GetBool(BlackboardKey.CannotSprint);
        string sprint = (granted ? "SprintGranted" : "no sprint grant") + (denied ? ", CannotSprint" : "");

        Gait gait = movementSystem.Locomotion.CurrentGait;
        Row("Gait", $"{gait.ToString().ToUpper()}   ({sprint})", gait == Gait.Sprint ? okColor : idleColor);
    }

    void DrawFacts()
    {
        string facts = $"Running {Yes(movementSystem.IsRunning)}   " +
                       $"Sprinting {Yes(movementSystem.IsSprinting)}   " +
                       $"Blend {movementSystem.SpeedBlend:F2}";

        Row("Facts", facts, movementSystem.IsSprinting ? okColor : idleColor);
    }

    void DrawGround(MovementProfile profile)
    {
        bool grounded = movementSystem.IsGrounded;

        if (motor == null)
        {
            Row("Ground", grounded ? "GROUNDED" : "airborne", grounded ? okColor : warnColor);
            return;
        }

        float angle = Vector3.Angle(motor.GroundNormal, Vector3.up);
        string band = $"   {SlopeBand(profile, angle)}";
        string distance = float.IsInfinity(motor.GroundDistance) ? "—" : $"{motor.GroundDistance:F3} m";

        Row("Ground", $"{(grounded ? "GROUNDED" : "AIRBORNE")}   {angle:F1}°   gap {distance}{band}",
            grounded ? okColor : warnColor);
    }

    void DrawVelocity()
    {
        Vector3 v = movementSystem.Velocity;
        Row("Velocity", $"x {v.x:F2}   y {v.y:F2}   z {v.z:F2}", idleColor);
    }

    void DrawCapsule()
    {
        CapsuleCollider capsule = motor != null ? motor.Capsule : null;
        if (capsule == null) return;

        float bottom = capsule.center.y - capsule.height * 0.5f;
        Row("Capsule", $"height {capsule.height:F2}   centre {capsule.center.y:F2}   underside {bottom:F3}", idleColor);
    }

    /// <summary>
    /// Whether ground friction is currently held off. The one row that tells you a dash actually
    /// fired: an impulse alone is nearly invisible at these speeds, but the holiday is unambiguous,
    /// and if Speed does not hold flat while this counts down, the impulse never landed.
    /// </summary>
    void DrawFriction()
    {
        if (parkour == null) return;

        bool held = parkour.FrictionSuppressed;
        string value = held ? $"HELD OFF   {parkour.FrictionHolidayRemaining:F2}s left" : "on";

        Row("Friction", value, held ? okColor : idleColor);
    }

    /// <summary>
    /// Jump budgets and whether a wall is kickable right now.
    ///
    /// WALL lights up from the sensor, not from having jumped — so if it never lights while you
    /// are against a wall, the problem is the ParkourAssistant's reach or probe layers, not the
    /// jump code. That distinction is otherwise invisible and costs an afternoon.
    /// </summary>
    void DrawJumps()
    {
        if (parkour == null) return;

        bool wall = parkour.WallInReach;

        // Which VARIANT a wall jump would be, not just whether one is available — the climb cone
        // is a feel number and you tune it by standing at its edge and watching this flip.
        string reach = !wall ? "no wall" : parkour.WallClimbInReach ? "WALL — climb" : "WALL — kick";

        string value = $"air {parkour.AirJumpsLeft}   wall {parkour.WallJumpsLeft}   " +
                       $"mantle {parkour.MantlesLeft}   {reach}";

        Row("Budgets", value, wall ? okColor : idleColor);
    }

    void DrawPosture()
    {
        if (stateMachine == null)
        {
            Row("Posture", "NO StateMachineModule — crouch and slide cannot work", badColor);
            return;
        }

        PostureState posture = stateMachine.GetPostureState();
        LowerBodyState lower = stateMachine.GetLowerBodyState();

        Color color = posture == PostureState.Sliding ? okColor
                    : posture == PostureState.Crouching ? warnColor : idleColor;

        Row("Posture", $"{posture}   ({stateMachine.GetTimeInPostureState():F1}s)", color);
        Row("LowerBody", $"{lower}   ({stateMachine.GetTimeInLowerBodyState():F1}s)", idleColor);
    }

    void DrawCrouchInput()
    {
        if (inputSystem == null)
        {
            Row("Crouch key", "no InputSystem", badColor);
            return;
        }

        // If this never lights up, the problem is the binding or the generated wrapper, not the
        // state machine — and that is a completely different search.
        bool held = inputSystem.CrouchHeld;
        Row("Crouch key", held ? "HELD" : "up", held ? okColor : idleColor);
    }


    void DrawCrouchDiagnosis()
    {
        if (stateMachine == null) return;
        if (stateMachine.GetPostureState() == PostureState.Crouching) { Row("Crouch", "CROUCHED", warnColor); return; }

        bool allowed = stateMachine.CanChangePosture(PostureState.Crouching);
        Row("Crouch", allowed ? "available" : "BLOCKED — StatePermissionMatrix refuses Crouching",
            allowed ? idleColor : badColor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────

    float ResolveGaitSpeed(MovementProfile profile)
    {
        switch (movementSystem.Locomotion.CurrentGait)
        {
            case Gait.Crouch: return profile.crouchSpeed;
            case Gait.Walk: return profile.walkSpeed;
            case Gait.Sprint: return profile.sprintSpeed;
            default: return profile.runSpeed;
        }
    }

    /// <summary>
    /// Two bands only. Grip means the surface behaves exactly like flat floor; no grip means the
    /// body is falling down it. The threshold is CharacterMotor.maxSlopeAngle, with hysteresis.
    /// </summary>
    string SlopeBand(MovementProfile profile, float angle)
    {
        return movementSystem.IsGrounded ? "grip" : "NO GRIP — falling";
    }

    static string Yes(bool value) => value ? "yes" : "no";

    void InitStyles()
    {
        if (stylesReady) return;

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold };
        valueStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = SolidTexture(backgroundColor);

        stylesReady = true;
    }

    static Texture2D SolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(2, 2);
        Color[] pixels = new Color[4];

        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    [ContextMenu("Toggle Display")]
    void ToggleDisplay() => showDebug = !showDebug;

    void OnDrawGizmos()
    {
        if (!showDebug || movementSystem == null || brain == null) return;

        Vector3 velocity = movementSystem.Velocity;
        if (velocity.magnitude < 0.1f) return;

        Vector3 start = brain.transform.position + Vector3.up;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + velocity.normalized * 2f);

        if (motor == null || !motor.IsGrounded) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, start + motor.GroundNormal);
    }
}

#endif
