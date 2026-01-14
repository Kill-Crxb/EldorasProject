using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Observer-based Debug UI for GOAP system
/// Watches the player's TargetLockModule and displays AI debug info for locked target
/// 
/// Setup:
/// 1. Create Canvas GameObject in scene (NOT on player or NPC prefabs)
/// 2. Add this component to Canvas
/// 3. Assign player's TargetLockModule reference
/// 4. Create UI panel with Text fields and assign them
/// 5. Press Tab to lock onto enemy and see debug info
/// 
/// Architecture:
/// - Decoupled from NPCs (NPCs don't know about debug UI)
/// - Decoupled from spawners (spawners don't care about UI)
/// - Player targeting drives what we observe
/// - Can handle both GOAP and FSM on same panel
/// </summary>
public class GOAPDebugUIController : MonoBehaviour
{
    [Header("Target Link")]
    [SerializeField] private TargetLockModule playerTargeting;

    [Header("UI Panel")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private bool showOnStart = true;

    [Header("GOAP Info")]
    [SerializeField] private TextMeshProUGUI targetNameText;
    [SerializeField] private TextMeshProUGUI currentGoalText;
    [SerializeField] private TextMeshProUGUI goalWeightsText;
    [SerializeField] private TextMeshProUGUI contextInfoText;

    [Header("Perception Info")]
    [SerializeField] private TextMeshProUGUI perceptionText;

    [Header("Pathfinding Info")]
    [SerializeField] private TextMeshProUGUI pathfindingText;

    [Header("State Machine Info (Optional)")]
    [SerializeField] private TextMeshProUGUI stateMachineText;

    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.1f;

    private Transform currentTarget;
    private float lastUpdateTime;

    // Cached components from target
    private GOAPModule goapModule;
    private PerceptionModule perceptionModule;
    private PathfindingModule pathfindingModule;
    private StateMachineModule stateMachineModule;
    private ControllerBrain targetBrain;

    void Start()
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(showOnStart);
        }

        // Try to auto-find player targeting if not assigned
        if (playerTargeting == null)
        {
            playerTargeting = Object.FindFirstObjectByType<TargetLockModule>();
            if (playerTargeting == null)
            {
                Debug.LogWarning("[GOAPDebugUI] No TargetLockModule found. Assign manually.");
            }
        }
    }

    void Update()
    {
        if (playerTargeting == null) return;

        // Check if target changed
        Transform newTarget = playerTargeting.LockedTarget;
        if (newTarget != currentTarget)
        {
            OnTargetChanged(newTarget);
        }

        // Update UI at intervals (not every frame for performance)
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDebugUI();
            lastUpdateTime = Time.time;
        }
    }

    void OnTargetChanged(Transform newTarget)
    {
        currentTarget = newTarget;

        // Clear cached components
        goapModule = null;
        perceptionModule = null;
        pathfindingModule = null;
        stateMachineModule = null;
        targetBrain = null;

        if (currentTarget == null)
        {
            // No target - hide or clear UI
            if (debugPanel != null)
            {
                ClearAllText();
            }
            return;
        }

        // Try to get components from new target
        // First check if target has a brain
        targetBrain = currentTarget.GetComponentInChildren<ControllerBrain>();
        if (targetBrain == null)
        {
            targetBrain = currentTarget.GetComponentInParent<ControllerBrain>();
        }

        if (targetBrain != null)
        {
            // Get AI modules
            goapModule = targetBrain.GetComponentInChildren<GOAPModule>();
            perceptionModule = targetBrain.GetComponentInChildren<PerceptionModule>();
            pathfindingModule = targetBrain.GetComponentInChildren<PathfindingModule>();
            stateMachineModule = targetBrain.GetComponentInChildren<StateMachineModule>();
        }

        // Show panel if we have valid target
        if (debugPanel != null && (goapModule != null || stateMachineModule != null))
        {
            debugPanel.SetActive(true);
        }
    }

    void UpdateDebugUI()
    {
        if (currentTarget == null)
        {
            ClearAllText();
            return;
        }

        // Update target name
        if (targetNameText != null)
        {
            string targetName = currentTarget.name;
            string entityType = targetBrain != null ?
           targetBrain.IsNPC ? "NPC" : (targetBrain.IsPlayer ? "Player" : "Unknown")
           : "No Brain";
            targetNameText.text = $"Target: {targetName}\nType: {entityType}";
        }

        // Update GOAP info
        UpdateGOAPInfo();

        // Update Perception info
        UpdatePerceptionInfo();

        // Update Pathfinding info
        UpdatePathfindingInfo();

        // Update State Machine info (if present)
        UpdateStateMachineInfo();
    }

    void UpdateGOAPInfo()
    {
        if (goapModule == null)
        {
            if (currentGoalText != null) currentGoalText.text = "No GOAP Module";
            if (goalWeightsText != null) goalWeightsText.text = "";
            if (contextInfoText != null) contextInfoText.text = "";
            return;
        }

        // Get GOAP context
        GOAPContext ctx = goapModule.GetContext();
        if (ctx == null)
        {
            if (currentGoalText != null) currentGoalText.text = "GOAP: No Context";
            return;
        }

        // Current active goal
        if (currentGoalText != null)
        {
            GOAPGoal activeGoal = goapModule.CurrentGoal; // Use property instead of method
            if (activeGoal != null)
            {
                currentGoalText.text = $"<color=green><b>Active Goal:</b></color>\n{activeGoal.goalName}\nWeight: {activeGoal.CalculateWeight(ctx):F2}";
            }
            else
            {
                currentGoalText.text = "<color=yellow>No Active Goal</color>";
            }
        }

        // All goal weights
        if (goalWeightsText != null)
        {
            var goals = goapModule.goalPool; // Direct access to public field
            if (goals != null && goals.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("<b>Goal Weights:</b>");

                foreach (var goal in goals)
                {
                    float weight = goal.CalculateWeight(ctx);
                    bool canExecute = goal.CanExecute(ctx);
                    string status = canExecute ? "<color=green>✓</color>" : "<color=red>✗</color>";
                    sb.AppendLine($"{status} {goal.goalName}: {weight:F2}");
                }

                goalWeightsText.text = sb.ToString();
            }
            else
            {
                goalWeightsText.text = "No goals configured";
            }
        }

        // Context info
        if (contextInfoText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Context:</b>");
            sb.AppendLine($"Has Target: {(ctx.target != null ? "Yes" : "No")}");
            if (ctx.target != null)
            {
                sb.AppendLine($"Distance: {ctx.distanceToTarget:F2}m");
            }
            sb.AppendLine($"Health: {ctx.healthPercent:P0}");
            sb.AppendLine($"Is Busy: {ctx.isBusy}");

            contextInfoText.text = sb.ToString();
        }
    }

    void UpdatePerceptionInfo()
    {
        if (perceptionModule == null)
        {
            if (perceptionText != null) perceptionText.text = "No Perception Module";
            return;
        }

        if (perceptionText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Perception:</b>");

            var currentTarget = perceptionModule.CurrentTarget;
            if (currentTarget != null)
            {
                sb.AppendLine($"<color=green>Target: {currentTarget.name}</color>");
            }
            else
            {
                sb.AppendLine("<color=yellow>No Target</color>");
            }

            var lastKnown = perceptionModule.LastKnownPosition;
            if (lastKnown != Vector3.zero)
            {
                sb.AppendLine($"Last Known: ({lastKnown.x:F1}, {lastKnown.y:F1}, {lastKnown.z:F1})");
            }

            perceptionText.text = sb.ToString();
        }
    }

    void UpdatePathfindingInfo()
    {
        if (pathfindingModule == null)
        {
            if (pathfindingText != null) pathfindingText.text = "No Pathfinding Module";
            return;
        }

        if (pathfindingText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Pathfinding:</b>");

            if (pathfindingModule.HasPath)
            {
                sb.AppendLine($"<color=green>Has Path</color>");
                sb.AppendLine($"Distance: {pathfindingModule.RemainingDistance:F2}m");
            }
            else
            {
                sb.AppendLine("<color=yellow>No Path</color>");
            }

            pathfindingText.text = sb.ToString();
        }
    }

    void UpdateStateMachineInfo()
    {
        if (stateMachineModule == null)
        {
            if (stateMachineText != null) stateMachineText.text = "";
            return;
        }

        if (stateMachineText != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>State Machine:</b>");
            sb.AppendLine($"State: {stateMachineModule.GetBrainState().ToString()}");

            stateMachineText.text = sb.ToString();
        }
    }

    void ClearAllText()
    {
        if (targetNameText != null) targetNameText.text = "No Target";
        if (currentGoalText != null) currentGoalText.text = "";
        if (goalWeightsText != null) goalWeightsText.text = "";
        if (contextInfoText != null) contextInfoText.text = "";
        if (perceptionText != null) perceptionText.text = "";
        if (pathfindingText != null) pathfindingText.text = "";
        if (stateMachineText != null) stateMachineText.text = "";
    }

    // Public methods for manual control
    public void TogglePanel()
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(!debugPanel.activeSelf);
        }
    }

    public void ShowPanel(bool show)
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(show);
        }
    }
}