using UnityEngine;

/// <summary>
/// Resource System Debug Display - OnGUI based (no canvas required)
/// 
/// Shows resource state:
/// - System status (ResourceSystem found, provider registered, manager loaded)
/// - Current/max values for all resources
/// - Stat integration mapping
/// </summary>
public class ResourceDebugDisplay : MonoBehaviour
{
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

    private InputSystem inputSystem;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle backgroundStyle;
    private bool stylesInitialized = false;

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

        // Re-find player if lost
        if (brain == null || inputSystem == null)
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
        }
    }

    void OnGUI()
    {
        if (!showDebug || brain == null)
            return;

        InitializeStyles();

        float x = xOffset;
        float y = yOffset;
        float width = 380f;

        // Calculate height dynamically
        int lineCount = CalculateLineCount();
        float height = lineHeight * lineCount + 20f;

        // Draw background
        GUI.Box(new Rect(x - 10f, y - 10f, width, height), "", backgroundStyle);

        // Draw header
        DrawHeader(ref x, ref y, width);

        // Draw system status
        DrawSystemStatus(ref x, ref y, width);

        // Draw resource values
        DrawResourceValues(ref x, ref y, width);

        // Draw stat integration
        DrawStatIntegration(ref x, ref y, width);

        // Draw toggle hint
        DrawToggleHint(x, y, width);
    }

    void DrawHeader(ref float x, ref float y, float width)
    {
        headerStyle.normal.textColor = headerColor;
        GUI.Label(new Rect(x, y, width - 20f, lineHeight),
            $"=== RESOURCE SYSTEM DEBUG ===", headerStyle);
        y += lineHeight + 5f;
    }

    void DrawSystemStatus(ref float x, ref float y, float width)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, width - 20f, lineHeight), "--- System Status ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var resourceSystem = brain.ResourceSys;
        var resourceProvider = brain.GetProvider<IResourceProvider>();
        var resourceManager = ResourceManager.Instance;

        // ResourceSystem
        if (resourceSystem != null)
        {
            labelStyle.normal.textColor = okColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                $"[OK] ResourceSystem: Found", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "[X] ResourceSystem: NOT FOUND", labelStyle);
        }
        y += lineHeight;

        // IResourceProvider
        if (resourceProvider != null)
        {
            labelStyle.normal.textColor = okColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "[OK] IResourceProvider: Registered", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "[X] IResourceProvider: NOT REGISTERED", labelStyle);
        }
        y += lineHeight;

        // ResourceManager
        if (resourceManager != null)
        {
            int count = 0;
            foreach (var def in resourceManager.GetAll())
                count++;

            labelStyle.normal.textColor = okColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                $"[OK] ResourceManager: {count} definitions", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "[X] ResourceManager: NOT FOUND", labelStyle);
        }
        y += lineHeight + 5f;
    }

    void DrawResourceValues(ref float x, ref float y, float width)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, width - 20f, lineHeight), "--- Resource Values ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var resources = brain.GetProvider<IResourceProvider>();
        if (resources == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "No IResourceProvider available", labelStyle);
            y += lineHeight;
            return;
        }

        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "ResourceManager not found", labelStyle);
            y += lineHeight;
            return;
        }

        // Show primary resources first
        DrawResource(resources, resourceManager, "health", ref x, ref y, width, healthColor);
        DrawResource(resources, resourceManager, "mana", ref x, ref y, width, manaColor);
        DrawResource(resources, resourceManager, "stamina", ref x, ref y, width, staminaColor);

        // Show remaining resources
        foreach (var def in resourceManager.GetAll())
        {
            if (def == null) continue;
            string id = def.resourceId.ToLower();
            if (id == "health" || id == "mana" || id == "stamina") continue;

            DrawResource(resources, resourceManager, def.resourceId, ref x, ref y, width, Color.white);
        }

        y += 5f;
    }

    void DrawResource(IResourceProvider resources, ResourceManager manager, string resourceId,
                      ref float x, ref float y, float width, Color color)
    {
        var def = manager.Get(resourceId);
        if (def == null) return;

        float current = resources.GetResource(def);
        float max = resources.GetMaxResource(def);
        float percent = resources.GetResourcePercentage(def);

        labelStyle.normal.textColor = color;
        string percentStr = max > 0 ? $"{percent * 100f:F1}%" : "N/A";
        GUI.Label(new Rect(x, y, width - 20f, lineHeight),
            $"{def.displayName}: {current:F1} / {max:F1} ({percentStr})", labelStyle);
        y += lineHeight;
    }

    void DrawStatIntegration(ref float x, ref float y, float width)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, width - 20f, lineHeight), "--- Stat Integration ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        var stats = brain.Stats;
        if (stats == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "StatSystem not found", labelStyle);
            y += lineHeight;
            return;
        }

        var resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            labelStyle.normal.textColor = errorColor;
            GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                "ResourceManager not found", labelStyle);
            y += lineHeight;
            return;
        }

        // Show stat mappings
        foreach (var def in resourceManager.GetAll())
        {
            if (def == null || string.IsNullOrEmpty(def.maxStatId)) continue;

            float statValue = stats.GetValue(def.maxStatId, -1f);

            if (statValue < 0)
            {
                labelStyle.normal.textColor = errorColor;
                GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                    $"[X] {def.displayName}: '{def.maxStatId}' NOT FOUND", labelStyle);
            }
            else
            {
                labelStyle.normal.textColor = okColor;
                GUI.Label(new Rect(x, y, width - 20f, lineHeight),
                    $"[OK] {def.displayName}: {statValue:F0}", labelStyle);
            }
            y += lineHeight;
        }

        y += 5f;
    }

    void DrawToggleHint(float x, float y, float width)
    {
        labelStyle.fontSize = fontSize - 4;
        labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        labelStyle.fontStyle = FontStyle.Italic;

        string hint = useHotbar3Toggle ? "[3] to toggle display" : "Toggle in Inspector";
        GUI.Label(new Rect(x, y, width - 20f, lineHeight), hint, labelStyle);

        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Normal;
    }

    int CalculateLineCount()
    {
        int count = 2; // Header + blank

        // System status section
        count += 4; // Header + 3 status lines

        // Resource values section
        var resourceManager = ResourceManager.Instance;
        if (resourceManager != null)
        {
            int resourceCount = 0;
            foreach (var def in resourceManager.GetAll())
                resourceCount++;
            count += 1 + resourceCount + 1; // Header + resources + blank
        }
        else
        {
            count += 2; // Header + error
        }

        // Stat integration section
        if (resourceManager != null)
        {
            int statCount = 0;
            foreach (var def in resourceManager.GetAll())
            {
                if (!string.IsNullOrEmpty(def.maxStatId))
                    statCount++;
            }
            count += 1 + statCount + 1; // Header + mappings + blank
        }
        else
        {
            count += 2; // Header + error
        }

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

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = MakeTex(2, 2, backgroundColor);

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
}