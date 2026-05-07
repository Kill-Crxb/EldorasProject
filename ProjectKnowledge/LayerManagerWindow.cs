using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// LayerManagerWindow — Editor window for validating and applying layer and physics matrix settings.
///
/// Open via: Tools → CrabSystem → Layer Manager
///
/// Features:
///   - Validates current project layers against a LayerConfig SO
///   - Validates current physics collision matrix against LayerConfig rules
///   - One-click apply for layers, matrix, or both
///   - Clear pass/fail status per item
///   - Data-driven via LayerConfig SO — swap SO for different projects
///
/// Place this file in any Editor/ folder in your project.
/// </summary>
public class LayerManagerWindow : EditorWindow
{
    // ── Config ────────────────────────────────────────────────────────────
    private LayerConfig config;

    // ── Scroll positions ──────────────────────────────────────────────────
    private Vector2 layerScroll;
    private Vector2 matrixScroll;

    // ── Validation results ────────────────────────────────────────────────
    private List<LayerValidationResult> layerResults = new List<LayerValidationResult>();
    private List<MatrixValidationResult> matrixResults = new List<MatrixValidationResult>();
    private bool hasValidated = false;

    // ── Styles (initialised lazily) ───────────────────────────────────────
    private GUIStyle passStyle;
    private GUIStyle failStyle;
    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;
    private bool stylesInitialised;

    // ── Tab ───────────────────────────────────────────────────────────────
    private int selectedTab = 0;
    private readonly string[] tabs = { "Layers", "Collision Matrix", "Apply" };

    // =====================================================================

    [MenuItem("Tools/CrabSystem/Layer Manager")]
    public static void Open()
    {
        var window = GetWindow<LayerManagerWindow>("Layer Manager");
        window.minSize = new Vector2(520, 400);
        window.Show();
    }

    // =====================================================================
    // GUI
    // =====================================================================

    void OnGUI()
    {
        InitStyles();

        DrawHeader();
        DrawConfigField();

        if (config == null)
        {
            EditorGUILayout.HelpBox("Assign a LayerConfig asset to get started.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        EditorGUILayout.Space(4);

        switch (selectedTab)
        {
            case 0: DrawLayersTab(); break;
            case 1: DrawMatrixTab(); break;
            case 2: DrawApplyTab(); break;
        }
    }

    // ── Header ────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("CrabSystem — Layer Manager", headerStyle);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Validate and apply project layers and physics collision matrix.", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);
    }

    private void DrawConfigField()
    {
        EditorGUI.BeginChangeCheck();
        config = (LayerConfig)EditorGUILayout.ObjectField("Layer Config", config, typeof(LayerConfig), false);
        if (EditorGUI.EndChangeCheck())
        {
            hasValidated = false;
            layerResults.Clear();
            matrixResults.Clear();
        }
    }

    // ── Layers tab ────────────────────────────────────────────────────────

    private void DrawLayersTab()
    {
        EditorGUILayout.LabelField("Layer Validation", sectionStyle);
        EditorGUILayout.Space(2);

        if (GUILayout.Button("Validate Layers", GUILayout.Height(28)))
            ValidateLayers();

        if (!hasValidated || layerResults.Count == 0)
        {
            EditorGUILayout.HelpBox("Press Validate to check current project layers against the config.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4);

        int passCount = layerResults.FindAll(r => r.pass).Count;
        int failCount = layerResults.Count - passCount;

        EditorGUILayout.LabelField($"Results: {passCount} passing  |  {failCount} failing",
            failCount > 0 ? failStyle : passStyle);

        EditorGUILayout.Space(4);

        layerScroll = EditorGUILayout.BeginScrollView(layerScroll);

        foreach (var result in layerResults)
        {
            DrawLayerResult(result);
        }

        EditorGUILayout.EndScrollView();

        if (failCount > 0)
        {
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Apply Missing / Incorrect Layers", GUILayout.Height(28)))
            {
                ApplyLayers();
                ValidateLayers();
            }
        }
    }

    private void DrawLayerResult(LayerValidationResult result)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Label(result.pass ? "✓" : "✗",
            result.pass ? passStyle : failStyle, GUILayout.Width(20));

        GUILayout.Label($"[{result.index:D2}]", EditorStyles.miniLabel, GUILayout.Width(30));
        GUILayout.Label(result.expectedName, GUILayout.Width(140));

        if (!result.pass)
        {
            GUILayout.Label($"→ currently: \"{result.currentName}\"", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("OK", EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(result.description))
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(result.description, EditorStyles.miniLabel, GUILayout.MaxWidth(180));
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── Matrix tab ────────────────────────────────────────────────────────

    private void DrawMatrixTab()
    {
        EditorGUILayout.LabelField("Collision Matrix Validation", sectionStyle);
        EditorGUILayout.Space(2);

        if (GUILayout.Button("Validate Matrix", GUILayout.Height(28)))
            ValidateMatrix();

        if (matrixResults.Count == 0)
        {
            EditorGUILayout.HelpBox("Press Validate to check the physics collision matrix against the config.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4);

        int passCount = matrixResults.FindAll(r => r.pass).Count;
        int failCount = matrixResults.Count - passCount;

        EditorGUILayout.LabelField($"Results: {passCount} passing  |  {failCount} failing",
            failCount > 0 ? failStyle : passStyle);

        EditorGUILayout.Space(4);

        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll);

        foreach (var result in matrixResults)
        {
            DrawMatrixResult(result);
        }

        EditorGUILayout.EndScrollView();

        if (failCount > 0)
        {
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Apply Matrix Rules", GUILayout.Height(28)))
            {
                ApplyMatrix();
                ValidateMatrix();
            }
        }
    }

    private void DrawMatrixResult(MatrixValidationResult result)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Label(result.pass ? "✓" : "✗",
            result.pass ? passStyle : failStyle, GUILayout.Width(20));

        GUILayout.Label($"{result.layerAName} ↔ {result.layerBName}", GUILayout.Width(200));

        string expected = result.shouldCollide ? "COLLIDE" : "IGNORE";
        string current = result.currentlyCollides ? "COLLIDE" : "IGNORE";

        if (result.pass)
        {
            GUILayout.Label(expected, EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label($"expected: {expected}  current: {current}", EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(result.note))
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label(result.note, EditorStyles.miniLabel, GUILayout.MaxWidth(180));
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── Apply tab ─────────────────────────────────────────────────────────

    private void DrawApplyTab()
    {
        EditorGUILayout.LabelField("Apply Configuration", sectionStyle);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Apply buttons write directly to ProjectSettings. This modifies TagManager.asset and " +
            "DynamicsManager.asset. Recommend committing current changes to source control first.",
            MessageType.Warning);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Individual Actions", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        if (GUILayout.Button("Apply Layers Only", GUILayout.Height(30)))
        {
            ApplyLayers();
            ValidateLayers();
            selectedTab = 0;
        }

        EditorGUILayout.Space(2);

        if (GUILayout.Button("Apply Collision Matrix Only", GUILayout.Height(30)))
        {
            ApplyMatrix();
            ValidateMatrix();
            selectedTab = 1;
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Apply Everything", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("✓  Apply All — Layers + Matrix", GUILayout.Height(36)))
        {
            ApplyLayers();
            ApplyMatrix();
            ValidateLayers();
            ValidateMatrix();
            selectedTab = 0;
            Debug.Log("[LayerManager] Applied all layers and matrix rules from config.");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Config Summary", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"Layers defined: {config.layers.Count}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Collision rules: {config.collisionRules.Count}", EditorStyles.miniLabel);
    }

    // =====================================================================
    // Validation
    // =====================================================================

    private void ValidateLayers()
    {
        layerResults.Clear();
        hasValidated = true;

        foreach (var def in config.layers)
        {
            if (def.isBuiltIn) continue;

            string currentName = LayerMask.LayerToName(def.index);
            bool pass = currentName == def.name;

            layerResults.Add(new LayerValidationResult
            {
                index = def.index,
                expectedName = def.name,
                currentName = currentName,
                description = def.description,
                pass = pass,
            });
        }

        Repaint();
    }

    private void ValidateMatrix()
    {
        matrixResults.Clear();

        foreach (var rule in config.collisionRules)
        {
            string nameA = LayerMask.LayerToName(rule.layerIndexA);
            string nameB = LayerMask.LayerToName(rule.layerIndexB);

            if (string.IsNullOrEmpty(nameA)) nameA = $"Layer {rule.layerIndexA}";
            if (string.IsNullOrEmpty(nameB)) nameB = $"Layer {rule.layerIndexB}";

            // Physics.GetIgnoreLayerCollision returns true when ignored (not colliding)
            bool currentlyIgnored = Physics.GetIgnoreLayerCollision(rule.layerIndexA, rule.layerIndexB);
            bool currentlyCollides = !currentlyIgnored;
            bool pass = currentlyCollides == rule.collides;

            matrixResults.Add(new MatrixValidationResult
            {
                layerIndexA = rule.layerIndexA,
                layerIndexB = rule.layerIndexB,
                layerAName = nameA,
                layerBName = nameB,
                shouldCollide = rule.collides,
                currentlyCollides = currentlyCollides,
                note = rule.note,
                pass = pass,
            });
        }

        Repaint();
    }

    // =====================================================================
    // Apply
    // =====================================================================

    private void ApplyLayers()
    {
        // Layers are stored in TagManager.asset — modify via SerializedObject
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layersProp = tagManager.FindProperty("layers");

        foreach (var def in config.layers)
        {
            if (def.isBuiltIn) continue;
            if (def.index < 0 || def.index > 31) continue;

            SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(def.index);
            string currentValue = layerProp.stringValue;

            if (currentValue == def.name) continue;

            // Warn if overwriting a non-empty layer we didn't define
            if (!string.IsNullOrEmpty(currentValue) && currentValue != def.name)
            {
                Debug.LogWarning($"[LayerManager] Layer {def.index} is '{currentValue}', overwriting with '{def.name}'");
            }

            layerProp.stringValue = def.name;
            Debug.Log($"[LayerManager] Set layer {def.index} = '{def.name}'");
        }

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private void ApplyMatrix()
    {
        foreach (var rule in config.collisionRules)
        {
            // IgnoreLayerCollision(a, b, ignore) — true = ignore (no collision), false = collide
            Physics.IgnoreLayerCollision(rule.layerIndexA, rule.layerIndexB, !rule.collides);
        }

        // Persist to ProjectSettings
        SerializedObject dynamicsManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
        dynamicsManager.Update();
        dynamicsManager.ApplyModifiedProperties();

        Debug.Log($"[LayerManager] Applied {config.collisionRules.Count} collision matrix rules.");
    }

    // =====================================================================
    // Styles
    // =====================================================================

    private void InitStyles()
    {
        if (stylesInitialised) return;

        passStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.3f, 0.9f, 0.3f) },
            fontStyle = FontStyle.Bold,
        };

        failStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
            fontStyle = FontStyle.Bold,
        };

        headerStyle = new GUIStyle(EditorStyles.largeLabel)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14,
        };

        sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
        };

        stylesInitialised = true;
    }

    // =====================================================================
    // Result types
    // =====================================================================

    private class LayerValidationResult
    {
        public int index;
        public string expectedName;
        public string currentName;
        public string description;
        public bool pass;
    }

    private class MatrixValidationResult
    {
        public int layerIndexA;
        public int layerIndexB;
        public string layerAName;
        public string layerBName;
        public bool shouldCollide;
        public bool currentlyCollides;
        public string note;
        public bool pass;
    }
}