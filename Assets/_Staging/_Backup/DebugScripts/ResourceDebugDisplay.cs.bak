using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Resource System Debug Display - Performance-optimized OnGUI display.
/// Shows resource state, system status, and stat integration.
/// </summary>
public class ResourceDebugDisplay : MonoBehaviour
{
    #region Constants

    private const float FIND_PLAYER_INTERVAL = 1f;
    private const float DEBUG_WINDOW_WIDTH = 380f;
    private const float DEBUG_WINDOW_PADDING = 10f;
    private const int HINT_FONT_SIZE_REDUCTION = 4;

    #endregion

    #region Inspector

    [Header("References")]
    [Tooltip("ControllerBrain to monitor (leave null to auto-find player)")]
    [SerializeField] private ControllerBrain brain;

    [Header("Display Settings")]
    [SerializeField] private bool showDebug = true;
    [Tooltip("Toggle display using Hotbar3 key (default: Keyboard 3)")]
    [SerializeField] private bool useHotbar3Toggle = true;

    [Header("Position")]
    [SerializeField] private float xOffset = 1050f;
    [SerializeField] private float yOffset = 10f;
    [SerializeField] private float lineHeight = 22f;

    [Header("Styling")]
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color headerColor = Color.yellow;
    [SerializeField] private Color healthColor = Color.red;
    [SerializeField] private Color manaColor = Color.cyan;
    [SerializeField] private Color staminaColor = Color.green;
    [SerializeField] private Color okColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);

    #endregion

    #region State

    private InputSystem inputSystem;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle backgroundStyle;
    private bool stylesInitialized = false;

    private Texture2D backgroundTexture;
    private float findPlayerCooldown = 0f;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        // Toggle display with Hotbar3 (keyboard 3)
        if (useHotbar3Toggle && inputSystem != null && inputSystem.Hotbar3Pressed)
        {
            showDebug = !showDebug;
        }

        // Re-find player with cooldown (performance fix)
        if (brain == null || inputSystem == null)
        {
            findPlayerCooldown -= Time.deltaTime;
            if (findPlayerCooldown <= 0f)
            {
                FindPlayer();
                findPlayerCooldown = FIND_PLAYER_INTERVAL;
            }
        }
    }

    void OnGUI()
    {
        if (!showDebug || brain == null)
            return;

        InitializeStyles();

        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return;

        // Cache GetAll() result to avoid multiple iterations (performance fix)
        var allResources = resourceManager.GetAll().ToList();

        float x = xOffset;
        float y = yOffset;

        // Calculate height dynamically
        int lineCount = CalculateLineCount(allResources);
        float height = lineHeight * lineCount + 20f;

        // Draw background
        GUI.Box(new Rect(x - DEBUG_WINDOW_PADDING, y - DEBUG_WINDOW_PADDING,
                         DEBUG_WINDOW_WIDTH, height), "", backgroundStyle);

        // Draw sections (pass cached list)
        DrawHeader(ref x, ref y);
        DrawSystemStatus(ref x, ref y, allResources);
        DrawResourceValues(ref x, ref y, allResources);
        DrawStatIntegration(ref x, ref y, allResources);
        DrawToggleHint(x, y);
    }

    void OnDestroy()
    {
        // Clean up texture to prevent memory leak (performance fix)
        if (backgroundTexture != null)
        {
            Destroy(backgroundTexture);
            backgroundTexture = null;
        }
    }

    #endregion

    #region Player Finding

    void FindPlayer()
    {
        if (brain == null)
        {
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
        }
    }

    #endregion

    #region Drawing Methods

    void DrawHeader(ref float x, ref float y)
    {
        headerStyle.normal.textColor = headerColor;
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
            "=== RESOURCE SYSTEM DEBUG ===", headerStyle);
        y += lineHeight + 5f;
    }

    void DrawSystemStatus(ref float x, ref float y, List<ResourceDefinition> allResources)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight), "--- System Status ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var resourceSystem = brain.ResourceSys;
        var resourceProvider = brain.GetProvider<IResourceProvider>();

        // ResourceSystem
        if (resourceSystem != null)
        {
            labelStyle.normal.textColor = okColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "[OK] ResourceSystem: Found", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "[X] ResourceSystem: NOT FOUND", labelStyle);
        }
        y += lineHeight;

        // IResourceProvider
        if (resourceProvider != null)
        {
            labelStyle.normal.textColor = okColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "[OK] IResourceProvider: Registered", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "[X] IResourceProvider: NOT REGISTERED", labelStyle);
        }
        y += lineHeight;

        // ResourceManager (use cached list count)
        labelStyle.normal.textColor = okColor;
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
            $"[OK] ResourceManager: {allResources.Count} definitions", labelStyle);
        y += lineHeight + 5f;
    }

    void DrawResourceValues(ref float x, ref float y, List<ResourceDefinition> allResources)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight), "--- Resource Values ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var resources = brain.GetProvider<IResourceProvider>();
        if (resources == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "No IResourceProvider available", labelStyle);
            y += lineHeight;
            return;
        }

        // Show primary resources first
        DrawResource(resources, "health", ref x, ref y, healthColor, allResources);
        DrawResource(resources, "mana", ref x, ref y, manaColor, allResources);
        DrawResource(resources, "stamina", ref x, ref y, staminaColor, allResources);

        // Show remaining resources (use cached list)
        foreach (var def in allResources)
        {
            if (def == null) continue;
            string id = def.resourceId.ToLower();
            if (id == "health" || id == "mana" || id == "stamina") continue;

            DrawResource(resources, def.resourceId, ref x, ref y, Color.white, allResources);
        }

        y += 5f;
    }

    void DrawResource(IResourceProvider resources, string resourceId,
                      ref float x, ref float y, Color color, List<ResourceDefinition> allResources)
    {
        var def = allResources.FirstOrDefault(d => d != null && d.resourceId == resourceId);
        if (def == null) return;

        float current = resources.GetResource(def);
        float max = resources.GetMaxResource(def);
        float percent = resources.GetResourcePercentage(def);

        labelStyle.normal.textColor = color;
        string percentStr = max > 0 ? $"{percent * 100f:F1}%" : "N/A";
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
            $"{def.displayName}: {current:F1} / {max:F1} ({percentStr})", labelStyle);
        y += lineHeight;
    }

    void DrawStatIntegration(ref float x, ref float y, List<ResourceDefinition> allResources)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight), "--- Stat Integration ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var stats = brain.Stats;
        if (stats == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                "StatSystem not found", labelStyle);
            y += lineHeight;
            return;
        }

        // Show stat mappings (use cached list)
        foreach (var def in allResources)
        {
            if (def == null || string.IsNullOrEmpty(def.maxStatId)) continue;

            float statValue = stats.GetValue(def.maxStatId, -1f);

            if (statValue < 0)
            {
                labelStyle.normal.textColor = errorColor;
                GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                    $"[X] {def.displayName}: '{def.maxStatId}' NOT FOUND", labelStyle);
            }
            else
            {
                labelStyle.normal.textColor = okColor;
                GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight),
                    $"[OK] {def.displayName}: {statValue:F0}", labelStyle);
            }
            y += lineHeight;
        }

        y += 5f;
    }

    void DrawToggleHint(float x, float y)
    {
        labelStyle.fontSize = fontSize - HINT_FONT_SIZE_REDUCTION;
        labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        labelStyle.fontStyle = FontStyle.Italic;

        string hint = useHotbar3Toggle ? "[3] to toggle display" : "Toggle in Inspector";
        GUI.Label(new Rect(x, y, DEBUG_WINDOW_WIDTH - 20f, lineHeight), hint, labelStyle);

        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Normal;
    }

    #endregion

    #region Helper Methods

    int CalculateLineCount(List<ResourceDefinition> allResources)
    {
        int count = 2; // Header + blank

        // System status section
        count += 4; // Header + 3 status lines

        // Resource values section (use cached list count)
        count += 1 + allResources.Count + 1; // Header + resources + blank

        // Stat integration section (use cached list)
        int statCount = allResources.Count(def => def != null && !string.IsNullOrEmpty(def.maxStatId));
        count += 1 + statCount + 1; // Header + mappings + blank

        count += 1; // Toggle hint

        return count;
    }

    void InitializeStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = Color.white;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = fontSize + 2;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = headerColor;

        // Create and cache texture once (performance fix)
        backgroundTexture = MakeTex(2, 2, backgroundColor);
        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;

        stylesInitialized = true;
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

    #endregion

    #region Context Menu Actions

    [ContextMenu("Toggle Display")]
    void ToggleDisplay()
    {
        showDebug = !showDebug;
    }

    [ContextMenu("Test Damage (50%)")]
    void TestDamage50()
    {
        var health = brain.GetProvider<IHealthProvider>();
        if (health != null)
        {
            float damage = health.GetMaxHealth() * 0.5f;
            health.ApplyDamage(damage);
            Debug.Log($"[ResourceDebugDisplay] Applied {damage:F1} damage (50%)");
        }
    }

    [ContextMenu("Test Damage (90%)")]
    void TestDamage90()
    {
        var health = brain.GetProvider<IHealthProvider>();
        if (health != null)
        {
            float damage = health.GetMaxHealth() * 0.9f;
            health.ApplyDamage(damage);
            Debug.Log($"[ResourceDebugDisplay] Applied {damage:F1} damage (90%)");
        }
    }

    [ContextMenu("Heal to Full")]
    void HealToFull()
    {
        var resources = brain.GetProvider<IResourceProvider>();
        var resourceManager = ResourceManager.Instance;

        if (resources != null && resourceManager != null)
        {
            foreach (var def in resourceManager.GetAll())
            {
                if (def != null)
                    resources.SetResourceToMax(def);
            }
            Debug.Log("[ResourceDebugDisplay] Healed all resources to max");
        }
    }

    #endregion
}