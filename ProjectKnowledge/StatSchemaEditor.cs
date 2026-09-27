#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NinjaGame.Stats;

/// <summary>
/// Custom editor for StatSchema with enhanced validation
/// Shows formula errors, circular dependencies, and missing stat references
/// </summary>
[CustomEditor(typeof(StatSchema))]
public class StatSchemaEditor : Editor
{
    private StatSchema schema;
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
    private Dictionary<string, string> validationErrors = new Dictionary<string, string>();
    private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();

    private GUIStyle errorStyle;
    private GUIStyle warningStyle;
    private GUIStyle successStyle;
    private GUIStyle headerStyle;

    private void OnEnable()
    {
        schema = (StatSchema)target;
        ValidateAllStats();
        BuildDependencyGraph();
    }

    public override void OnInspectorGUI()
    {
        InitializeStyles();

        serializedObject.Update();

        // Header
        DrawCustomHeader();

        EditorGUILayout.Space(10);

        // Schema info
        DrawSchemaInfo();

        EditorGUILayout.Space(10);

        // Validation summary
        DrawValidationSummary();

        EditorGUILayout.Space(10);

        // Stats list
        DrawStatsList();

        EditorGUILayout.Space(10);

        // Quick actions
        DrawQuickActions();

        serializedObject.ApplyModifiedProperties();

        // Auto-validate on changes
        if (GUI.changed)
        {
            ValidateAllStats();
            BuildDependencyGraph();
        }
    }

    private void InitializeStyles()
    {
        if (errorStyle == null)
        {
            errorStyle = new GUIStyle(EditorStyles.helpBox);
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
            errorStyle.fontSize = 11;
        }

        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(EditorStyles.helpBox);
            warningStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
            warningStyle.fontSize = 11;
        }

        if (successStyle == null)
        {
            successStyle = new GUIStyle(EditorStyles.helpBox);
            successStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
            errorStyle.fontSize = 11;
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
        }
    }

    private void DrawCustomHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Stat Schema Editor", headerStyle);
        EditorGUILayout.LabelField("Enhanced Validation & Formula Checking", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawSchemaInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Schema Configuration", EditorStyles.boldLabel);

        SerializedProperty namespaceProp = serializedObject.FindProperty("statNamespace");
        SerializedProperty descProp = serializedObject.FindProperty("description");

        EditorGUILayout.PropertyField(namespaceProp, new GUIContent("Namespace"));
        EditorGUILayout.PropertyField(descProp, new GUIContent("Description"));

        EditorGUILayout.EndVertical();
    }

    private void DrawValidationSummary()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Validation Summary", EditorStyles.boldLabel);

        int totalStats = schema.stats.Count;
        int statsWithFormulas = 0;
        int errors = 0;
        int warnings = 0;

        foreach (var stat in schema.stats)
        {
            if (!string.IsNullOrEmpty(stat.formula))
                statsWithFormulas++;

            if (validationErrors.TryGetValue(stat.statId, out string error))
            {
                if (error.StartsWith("ERROR:"))
                    errors++;
                else if (error.StartsWith("WARNING:"))
                    warnings++;
            }
        }

        EditorGUILayout.LabelField($"Total Stats: {totalStats}");
        EditorGUILayout.LabelField($"Stats with Formulas: {statsWithFormulas}");

        if (errors > 0)
            EditorGUILayout.LabelField($"Errors: {errors}", errorStyle);

        if (warnings > 0)
            EditorGUILayout.LabelField($"Warnings: {warnings}", warningStyle);

        if (errors == 0 && warnings == 0)
            EditorGUILayout.LabelField("All stats validated successfully", successStyle);

        EditorGUILayout.EndVertical();
    }

    private void DrawStatsList()
    {
        SerializedProperty statsList = serializedObject.FindProperty("stats");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Stat Definitions", EditorStyles.boldLabel);

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            statsList.arraySize++;
            var newStat = statsList.GetArrayElementAtIndex(statsList.arraySize - 1);
            newStat.FindPropertyRelative("statId").stringValue = $"{schema.statNamespace}.new_stat";
            newStat.FindPropertyRelative("displayName").stringValue = "New Stat";
            newStat.FindPropertyRelative("baseValue").floatValue = 0f;
            newStat.FindPropertyRelative("formula").stringValue = "";
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        for (int i = 0; i < statsList.arraySize; i++)
        {
            DrawStatDefinition(statsList.GetArrayElementAtIndex(i), i);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawStatDefinition(SerializedProperty statProp, int index)
    {
        var statIdProp = statProp.FindPropertyRelative("statId");
        var displayNameProp = statProp.FindPropertyRelative("displayName");
        var formulaProp = statProp.FindPropertyRelative("formula");

        string statId = statIdProp.stringValue;

        if (!foldoutStates.ContainsKey(statId))
            foldoutStates[statId] = false;

        Color headerColor = Color.white;
        string statusIcon = "o";

        if (validationErrors.TryGetValue(statId, out string error))
        {
            if (error.StartsWith("ERROR:"))
            {
                headerColor = new Color(1f, 0.4f, 0.4f);
                statusIcon = "X";
            }
            else if (error.StartsWith("WARNING:"))
            {
                headerColor = new Color(1f, 0.9f, 0.3f);
                statusIcon = "!";
            }
        }
        else if (!string.IsNullOrEmpty(formulaProp.stringValue))
        {
            headerColor = new Color(0.4f, 1f, 0.4f);
            statusIcon = "✓";
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUI.color = headerColor;
        foldoutStates[statId] = EditorGUILayout.Foldout(foldoutStates[statId],
            $"{statusIcon} {displayNameProp.stringValue} ({statId})", true);
        GUI.color = Color.white;

        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            serializedObject.FindProperty("stats").DeleteArrayElementAtIndex(index);
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (foldoutStates[statId])
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(statProp);

            if (validationErrors.TryGetValue(statId, out string validationMessage))
            {
                if (validationMessage.StartsWith("ERROR:"))
                    EditorGUILayout.HelpBox(validationMessage.Substring(6), MessageType.Error);
                else if (validationMessage.StartsWith("WARNING:"))
                    EditorGUILayout.HelpBox(validationMessage.Substring(8), MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate All"))
        {
            ValidateAllStats();
            BuildDependencyGraph();
            Debug.Log($"Validation complete. Errors: {CountErrors()}, Warnings: {CountWarnings()}");
        }

        EditorGUILayout.EndVertical();
    }

    private void ValidateAllStats()
    {
        validationErrors.Clear();
        HashSet<string> allStatIds = new HashSet<string>();
        foreach (var stat in schema.stats)
            allStatIds.Add(stat.statId);

        foreach (var stat in schema.stats)
            ValidateStat(stat, allStatIds);
    }

    private void ValidateStat(StatDefinition stat, HashSet<string> allStatIds)
    {
        if (string.IsNullOrEmpty(stat.formula))
            return;

        int openBraces = 0;
        int closeBraces = 0;
        foreach (char c in stat.formula)
        {
            if (c == '{') openBraces++;
            if (c == '}') closeBraces++;
        }

        if (openBraces != closeBraces)
        {
            validationErrors[stat.statId] = "ERROR: Mismatched braces in formula";
            return;
        }

        var references = ExtractStatReferences(stat.formula);
        List<string> missingStats = new List<string>();
        foreach (var reference in references)
        {
            if (!allStatIds.Contains(reference))
                missingStats.Add(reference);
        }

        if (missingStats.Count > 0)
        {
            validationErrors[stat.statId] = $"WARNING: References undefined stats: {string.Join(", ", missingStats)}";
            return;
        }

        if (references.Contains(stat.statId))
        {
            validationErrors[stat.statId] = "ERROR: Stat references itself (circular dependency)";
            return;
        }
    }

    private List<string> ExtractStatReferences(string formula)
    {
        List<string> references = new List<string>();
        Regex regex = new Regex(@"\{([^}]+)\}");
        MatchCollection matches = regex.Matches(formula);

        foreach (Match match in matches)
        {
            string statId = match.Groups[1].Value;
            if (!references.Contains(statId))
                references.Add(statId);
        }

        return references;
    }

    private void BuildDependencyGraph()
    {
        dependencies.Clear();
        foreach (var stat in schema.stats)
        {
            if (!string.IsNullOrEmpty(stat.formula))
                dependencies[stat.statId] = ExtractStatReferences(stat.formula);
        }
    }

    private int CountErrors()
    {
        int count = 0;
        foreach (var error in validationErrors.Values)
            if (error.StartsWith("ERROR:"))
                count++;
        return count;
    }

    private int CountWarnings()
    {
        int count = 0;
        foreach (var error in validationErrors.Values)
            if (error.StartsWith("WARNING:"))
                count++;
        return count;
    }
}
#endif