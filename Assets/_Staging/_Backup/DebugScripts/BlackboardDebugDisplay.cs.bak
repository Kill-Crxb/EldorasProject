using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Blackboard Debug Display - OnGUI based (no canvas required)
/// 
/// Shows semantic bridge and blackboard state:
/// - All blackboard facts (IsWounded, CanDodge, etc.)
/// - Raw values from value sources (health%, stamina%, etc.)
/// - Condition count and update rate
/// </summary>
public class BlackboardDebugDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ControllerBrain to monitor (leave null to auto-find player)")]
    [SerializeField] private ControllerBrain brain;

    [Header("Display Settings")]
    [SerializeField] private bool showDebug = true;
    [Tooltip("Toggle display using Hotbar2 key (default: Keyboard 2)")]
    [SerializeField] private bool useHotbar2Toggle = true;
    [SerializeField] private bool showRawValues = true;

    [Header("Position")]
    [SerializeField] private float xOffset = 300f;
    [SerializeField] private float yOffset = 10f;
    [SerializeField] private float lineHeight = 22f;

    [Header("Styling")]
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color trueColor = Color.green;
    [SerializeField] private Color falseColor = Color.red;
    [SerializeField] private Color valueColor = Color.cyan;
    [SerializeField] private Color headerColor = Color.yellow;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);

    private InputSystem inputSystem;
    private BlackboardSystem blackboardSystem;
    private SemanticBridgeSystem semanticBridge;
    private Blackboard blackboard;
    private BlackboardSchema schema;

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
        // Toggle display with Hotbar2 (keyboard 2)
        if (useHotbar2Toggle && inputSystem != null && inputSystem.Hotbar2Pressed)
        {
            showDebug = !showDebug;
        }

        // Re-find player if lost
        if (brain == null || blackboardSystem == null)
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
            blackboardSystem = brain.GetModule<BlackboardSystem>();
            semanticBridge = brain.GetModule<SemanticBridgeSystem>();

            if (blackboardSystem != null)
            {
                blackboard = blackboardSystem.Blackboard;
                schema = blackboardSystem.Schema;
            }
        }
    }

    void OnGUI()
    {
        if (!showDebug || blackboard == null || schema == null)
            return;

        InitializeStyles();

        float x = xOffset;
        float y = yOffset;
        float width = 350f;

        // Calculate height based on what we're showing
        int lineCount = 3; // Header + schema info + semantic bridge info

        if (schema != null && schema.keys.Count > 0)
        {
            lineCount += schema.keys.Count; // Facts
        }

        if (showRawValues)
        {
            lineCount += 4; // Raw values section header + 3 resources
        }

        lineCount += 1; // Toggle hint

        float height = lineHeight * lineCount + 20f;

        // Draw background
        GUI.Box(new Rect(x, y, width, height), "", backgroundStyle);

        y += 10f; // Padding

        // Header
        DrawHeader(x + 10f, y);
        y += lineHeight;

        // Schema Info
        DrawSchemaInfo(x + 10f, y);
        y += lineHeight;

        // Semantic Bridge Info
        DrawSemanticBridgeInfo(x + 10f, y);
        y += lineHeight;

        // Blackboard Facts
        if (schema != null && schema.keys.Count > 0)
        {
            y = DrawBlackboardFacts(x + 10f, y);
        }
        else
        {
            DrawNoSchema(x + 10f, y);
            y += lineHeight;
        }

        // Raw Values
        if (showRawValues)
        {
            y = DrawRawValues(x + 10f, y);
        }

        // Toggle hint
        DrawToggleHint(x + 10f, y);
    }

    void InitializeStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.normal.textColor = Color.white;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = fontSize + 2;
        headerStyle.normal.textColor = Color.white;
        headerStyle.fontStyle = FontStyle.Bold;

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = MakeTex(2, 2, backgroundColor);

        stylesInitialized = true;
    }

    void DrawHeader(float x, float y)
    {
        headerStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 330f, lineHeight), "=== BLACKBOARD DEBUG ===", headerStyle);
    }

    void DrawSchemaInfo(float x, float y)
    {
        string schemaName = schema != null ? schema.name : "None";
        int keyCount = schema != null ? schema.keys.Count : 0;

        labelStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(x, y, 330f, lineHeight), $"Schema: {schemaName} ({keyCount} keys)", labelStyle);
    }

    void DrawSemanticBridgeInfo(float x, float y)
    {
        if (semanticBridge == null)
        {
            labelStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(x, y, 330f, lineHeight), "No SemanticBridgeSystem found!", labelStyle);
            return;
        }

        int conditionCount = semanticBridge.ConditionCount;
        bool usesLibrary = semanticBridge.UsesLibrary;
        string source = usesLibrary ? "Library" : "Entity-Specific";

        labelStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(x, y, 330f, lineHeight), $"Conditions: {conditionCount} ({source})", labelStyle);
    }

    float DrawBlackboardFacts(float x, float y)
    {
        labelStyle.normal.textColor = Color.white;

        foreach (var key in schema.keys)
        {
            int hash = key.keyName.GetHashCode();

            // Draw based on type
            switch (key.type)
            {
                case BlackboardValueType.Bool:
                    bool boolValue = blackboard.GetBool(hash);
                    DrawBoolFact(x, y, key.keyName, boolValue);
                    break;

                case BlackboardValueType.Float:
                    float floatValue = blackboard.GetFloat(hash);
                    DrawFloatFact(x, y, key.keyName, floatValue);
                    break;

                case BlackboardValueType.Int:
                    int intValue = blackboard.GetInt(hash);
                    DrawIntFact(x, y, key.keyName, intValue);
                    break;
            }

            y += lineHeight;
        }

        return y;
    }

    void DrawBoolFact(float x, float y, string keyName, bool value)
    {
        // Draw key name
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 200f, lineHeight), $"{keyName}:", labelStyle);

        // Draw value with color
        labelStyle.normal.textColor = value ? trueColor : falseColor;
        string valueText = value ? "TRUE" : "FALSE";
        GUI.Label(new Rect(x + 210f, y, 120f, lineHeight), valueText, labelStyle);
    }

    void DrawFloatFact(float x, float y, string keyName, float value)
    {
        // Draw key name
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 200f, lineHeight), $"{keyName}:", labelStyle);

        // Draw value
        labelStyle.normal.textColor = valueColor;
        GUI.Label(new Rect(x + 210f, y, 120f, lineHeight), $"{value:F2}", labelStyle);
    }

    void DrawIntFact(float x, float y, string keyName, int value)
    {
        // Draw key name
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 200f, lineHeight), $"{keyName}:", labelStyle);

        // Draw value
        labelStyle.normal.textColor = valueColor;
        GUI.Label(new Rect(x + 210f, y, 120f, lineHeight), value.ToString(), labelStyle);
    }

    void DrawNoSchema(float x, float y)
    {
        labelStyle.normal.textColor = Color.red;
        GUI.Label(new Rect(x, y, 330f, lineHeight), "No Blackboard Schema Assigned!", labelStyle);
    }

    float DrawRawValues(float x, float y)
    {
        labelStyle.normal.textColor = headerColor;
        labelStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x, y, 330f, lineHeight), "--- Raw Values ---", labelStyle);
        labelStyle.fontStyle = FontStyle.Normal;
        y += lineHeight;

        // Try to get resource provider for health/mana/stamina
        var resources = brain.GetProvider<IResourceProvider>();
        if (resources != null)
        {
            // Get resource manager to query definitions
            var resourceManager = FindObjectOfType<ResourceManager>();
            if (resourceManager != null)
            {
                DrawResourcePercentage(x, ref y, resources, resourceManager, "Health");
                DrawResourcePercentage(x, ref y, resources, resourceManager, "Mana");
                DrawResourcePercentage(x, ref y, resources, resourceManager, "Stamina");
            }
            else
            {
                labelStyle.normal.textColor = Color.gray;
                GUI.Label(new Rect(x, y, 330f, lineHeight), "ResourceManager not found", labelStyle);
                y += lineHeight;
            }
        }
        else
        {
            labelStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, 330f, lineHeight), "No IResourceProvider found", labelStyle);
            y += lineHeight;
        }

        return y;
    }

    void DrawResourcePercentage(float x, ref float y, IResourceProvider resources, ResourceManager manager, string resourceName)
    {
        // Try to find resource by ID (assuming ID matches name in lowercase)
        string resourceId = resourceName.ToLower();
        var resourceDef = manager.Get(resourceId);

        if (resourceDef == null)
        {
            // Try to find by searching all resources for matching displayName
            foreach (var def in manager.GetAll())
            {
                if (def.displayName.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase))
                {
                    resourceDef = def;
                    break;
                }
            }
        }

        if (resourceDef == null)
        {
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, 120f, lineHeight), $"{resourceName}:", labelStyle);

            labelStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x + 130f, y, 200f, lineHeight), "Not Found", labelStyle);
            y += lineHeight;
            return;
        }

        // Get current and max values
        float current = resources.GetResource(resourceDef);
        float max = resources.GetMaxResource(resourceDef);
        float percentage = resources.GetResourcePercentage(resourceDef) * 100f;

        // Draw label
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 120f, lineHeight), $"{resourceName}:", labelStyle);

        // Draw percentage with color based on value
        Color percentColor = percentage > 75f ? Color.green :
                            percentage > 50f ? Color.yellow :
                            percentage > 25f ? new Color(1f, 0.5f, 0f) : // Orange
                            Color.red;

        labelStyle.normal.textColor = percentColor;
        GUI.Label(new Rect(x + 130f, y, 200f, lineHeight), $"{percentage:F1}% ({current:F0}/{max:F0})", labelStyle);

        y += lineHeight;
    }

    void DrawToggleHint(float x, float y)
    {
        labelStyle.fontSize = fontSize - 4;
        labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        labelStyle.fontStyle = FontStyle.Italic;

        string hint = useHotbar2Toggle ? "[2] to toggle display" : "Toggle in Inspector";
        GUI.Label(new Rect(x, y, 330f, lineHeight), hint, labelStyle);

        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Normal;
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

    [ContextMenu("Toggle Raw Values")]
    void ToggleRawValues()
    {
        showRawValues = !showRawValues;
    }

    [ContextMenu("Force Evaluate Semantic Bridge")]
    void ForceEvaluate()
    {
        if (semanticBridge != null)
        {
            semanticBridge.ForceEvaluateAll();
            Debug.Log("Forced semantic bridge evaluation");
        }
    }

    [ContextMenu("Print All Facts to Console")]
    void PrintAllFacts()
    {
        if (blackboardSystem != null)
        {
            blackboardSystem.PrintAllFacts();
        }
    }
}