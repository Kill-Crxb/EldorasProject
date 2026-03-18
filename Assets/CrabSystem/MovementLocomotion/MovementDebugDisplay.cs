using UnityEngine;

/// <summary>
/// Movement Debug Display - OnGUI based (no canvas required)
/// 
/// Shows movement state directly on screen:
/// - Walk/Run mode (from ARPGLocomotionHandler)
/// - Current velocity
/// - Sprint state
/// - Animation speed (velocity-based)
/// - Grounded state
/// </summary>
public class MovementDebugDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ControllerBrain to monitor (leave null to auto-find player)")]
    [SerializeField] private ControllerBrain brain;

    [Header("Display Settings")]
    [SerializeField] private bool showDebug = true;
    [Tooltip("Toggle display using Hotbar1 key (default: Keyboard 1)")]
    [SerializeField] private bool useHotbar1Toggle = true;

    [Header("Position")]
    [SerializeField] private float xOffset = 10f;
    [SerializeField] private float yOffset = 10f;
    [SerializeField] private float lineHeight = 25f;

    [Header("Styling")]
    [SerializeField] private int fontSize = 16;
    [SerializeField] private Color walkColor = Color.cyan;
    [SerializeField] private Color runColor = Color.green;
    [SerializeField] private Color sprintColor = Color.yellow;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);

    private InputSystem inputSystem;
    private MovementSystem movementSystem;
    private ARPGLocomotionHandler locomotionHandler;
    private GUIStyle labelStyle;
    private GUIStyle backgroundStyle;
    private bool stylesInitialized = false;

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        // Toggle display with Hotbar1 (keyboard 1)
        if (useHotbar1Toggle && inputSystem != null && inputSystem.Hotbar1Pressed)
        {
            showDebug = !showDebug;
        }

        // Re-find player if lost
        if (brain == null || inputSystem == null || movementSystem == null || locomotionHandler == null)
        {
            FindPlayer();
        }
    }

    void FindPlayer()
    {
        if (brain == null)
        {
            // Find player brain
            var allBrains = FindObjectsOfType<ControllerBrain>();
            foreach (var b in allBrains)
            {
                if (b.IsPlayer)
                {
                    brain = b;
                    break;
                }
            }
        }

        if (brain != null)
        {
            inputSystem = brain.GetModule<InputSystem>();
            movementSystem = brain.GetModule<MovementSystem>();

            // Get locomotion handler
            if (movementSystem != null)
            {
                locomotionHandler = movementSystem.Locomotion as ARPGLocomotionHandler;
            }
        }
    }

    void OnGUI()
    {
        if (!showDebug || inputSystem == null || movementSystem == null || locomotionHandler == null)
            return;

        InitializeStyles();

        float x = xOffset;
        float y = yOffset;
        float width = 280f;
        float height = lineHeight * 7 + 20f; // 7 lines + padding

        // Draw background
        GUI.Box(new Rect(x, y, width, height), "", backgroundStyle);

        y += 10f; // Padding

        // Movement Mode (walk/run toggle)
        DrawMovementMode(x + 10f, y);
        y += lineHeight;

        // Sprint State
        DrawSprintState(x + 10f, y);
        y += lineHeight;

        // Velocity
        DrawVelocity(x + 10f, y);
        y += lineHeight;

        // Animation State (velocity-based)
        DrawAnimationState(x + 10f, y);
        y += lineHeight;

        // Grounded State
        DrawGroundedState(x + 10f, y);
        y += lineHeight;

        // Strafe Mode
        DrawStrafeMode(x + 10f, y);
        y += lineHeight;

        // Toggle hint
        DrawToggleHint(x + 10f, y);
    }

    void InitializeStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = MakeTex(2, 2, backgroundColor);

        stylesInitialized = true;
    }

    void DrawMovementMode(float x, float y)
    {
        bool isWalkMode = locomotionHandler.IsInWalkMode;
        string mode = isWalkMode ? "WALK MODE" : "RUN MODE";
        Color color = isWalkMode ? walkColor : runColor;

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Mode:", labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), mode, labelStyle);
    }

    void DrawSprintState(float x, float y)
    {
        bool isSprinting = locomotionHandler.IsSprinting;
        string state = isSprinting ? "SPRINTING" : "Normal";
        Color color = isSprinting ? sprintColor : Color.gray;

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Sprint:", labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), state, labelStyle);
    }

    void DrawVelocity(float x, float y)
    {
        Vector3 velocity = movementSystem.Velocity;
        float speed = velocity.magnitude;

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Velocity:", labelStyle);

        Color velColor = speed < 2f ? Color.gray : (speed > 6f ? sprintColor : (speed > 3.5f ? runColor : walkColor));
        labelStyle.normal.textColor = velColor;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), $"{speed:F2} m/s", labelStyle);
    }

    void DrawAnimationState(float x, float y)
    {
        // Show actual velocity being sent to animator
        float speed = movementSystem.Velocity.magnitude;
        string stateName;
        Color animColor;

        // Approximate which animation is playing based on velocity
        // (These are just visual hints - actual blending happens in Unity)
        if (speed < 0.5f)
        {
            stateName = "Idle";
            animColor = Color.gray;
        }
        else if (speed < 3.0f) // Adjust based on your walk speed
        {
            stateName = "Walk";
            animColor = walkColor;
        }
        else if (speed < 5.0f) // Adjust based on your run speed
        {
            stateName = "Run";
            animColor = runColor;
        }
        else
        {
            stateName = "Sprint";
            animColor = sprintColor;
        }

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Animation:", labelStyle);

        labelStyle.normal.textColor = animColor;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), $"{stateName} ({speed:F2})", labelStyle);
    }

    void DrawGroundedState(float x, float y)
    {
        bool isGrounded = movementSystem.IsGrounded;
        string state = isGrounded ? "Grounded" : "Airborne";
        Color color = isGrounded ? Color.green : Color.red;

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Ground:", labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), state, labelStyle);
    }

    void DrawStrafeMode(float x, float y)
    {
        bool isStrafing = locomotionHandler.IsStrafing;
        string mode = isStrafing ? "Strafe (Lock-On)" : "Free Movement";
        Color color = isStrafing ? Color.yellow : Color.white;

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 100f, lineHeight), "Control:", labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(new Rect(x + 100f, y, 180f, lineHeight), mode, labelStyle);
    }

    void DrawToggleHint(float x, float y)
    {
        labelStyle.fontSize = fontSize - 4;
        labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        labelStyle.fontStyle = FontStyle.Italic;

        string hint = useHotbar1Toggle ? "[1] to toggle display" : "Toggle in Inspector";
        GUI.Label(new Rect(x, y, 280f, lineHeight), hint, labelStyle);

        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Bold;
    }

    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    [ContextMenu("Toggle Display")]
    void ToggleDisplay()
    {
        showDebug = !showDebug;
    }

    // ========================================
    // Debug Helpers
    // ========================================

    void OnDrawGizmos()
    {
        if (!showDebug || movementSystem == null) return;

        // Draw velocity vector
        Vector3 velocity = movementSystem.Velocity;
        if (velocity.magnitude > 0.1f)
        {
            Vector3 start = brain.transform.position + Vector3.up * 1f;
            Vector3 end = start + velocity.normalized * 2f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.1f);
        }
    }
}