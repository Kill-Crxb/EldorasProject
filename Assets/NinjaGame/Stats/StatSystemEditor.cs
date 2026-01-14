#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for StatSystem - Shows runtime stats during Play Mode
/// Phase 1.7b: System Consolidation
/// Created: January 09, 2026
/// </summary>
[CustomEditor(typeof(StatSystem))]
public class StatSystemEditor : Editor
{
    private bool showCoreStats = true;
    private bool showCombatStats = true;
    private bool showResourceStats = true;
    private bool showOtherStats = true;

    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        StatSystem statSystem = (StatSystem)target;

        // Only show stats in Play Mode
        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("📊 Runtime Stats will be visible here during Play Mode", MessageType.Info);
            return;
        }

        // Check if engine is initialized
        if (statSystem.Engine == null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("⚠️ StatEngine not initialized yet", MessageType.Warning);
            return;
        }

        // Draw runtime stats section
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Statistics", EditorStyles.boldLabel);

        DrawStatsSection(statSystem, "character", "Core Stats", ref showCoreStats, new Color(0.3f, 0.6f, 1f));
        DrawStatsSection(statSystem, "combat", "Combat Stats", ref showCombatStats, new Color(1f, 0.3f, 0.3f));
        DrawStatsSection(statSystem, "resource", "Resource Stats", ref showResourceStats, new Color(0.3f, 1f, 0.3f));
        DrawOtherStats(statSystem);

        // Repaint continuously during play mode for live updates
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void DrawStatsSection(StatSystem statSystem, string category, string label, ref bool foldout, Color headerColor)
    {
        var allStatIds = statSystem.Engine.GetAllStatIds();
        var categoryStats = System.Linq.Enumerable.Where(allStatIds, id => id.StartsWith(category + ".")).ToList();

        if (categoryStats.Count == 0) return;

        EditorGUILayout.Space(5);

        // Colored foldout header
        var oldBgColor = GUI.backgroundColor;
        GUI.backgroundColor = headerColor;

        var style = new GUIStyle(EditorStyles.foldoutHeader);
        style.fontStyle = FontStyle.Bold;

        foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, $"{label} ({categoryStats.Count})", style);
        GUI.backgroundColor = oldBgColor;

        if (foldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");

            foreach (var statId in categoryStats)
            {
                var stat = statSystem.Engine.GetStat(statId);
                if (stat != null)
                {
                    DrawStatRow(stat);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawOtherStats(StatSystem statSystem)
    {
        var allStatIds = statSystem.Engine.GetAllStatIds();
        var otherStats = System.Linq.Enumerable.Where(allStatIds, id =>
            !id.StartsWith("character.") &&
            !id.StartsWith("combat.") &&
            !id.StartsWith("resource.")).ToList();

        if (otherStats.Count == 0) return;

        EditorGUILayout.Space(5);

        showOtherStats = EditorGUILayout.BeginFoldoutHeaderGroup(showOtherStats, $"Other Stats ({otherStats.Count})");

        if (showOtherStats)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");

            foreach (var statId in otherStats)
            {
                var stat = statSystem.Engine.GetStat(statId);
                if (stat != null)
                {
                    DrawStatRow(stat);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawStatRow(NinjaGame.Stats.StatNode stat)
    {
        EditorGUILayout.BeginHorizontal();

        // Stat name
        EditorGUILayout.LabelField(stat.displayName, GUILayout.Width(140));

        // Base value
        var baseLabel = string.IsNullOrEmpty(stat.formula) ? "Value" : "Base";
        EditorGUILayout.LabelField($"{baseLabel}: {stat.BaseValue:F1}", GUILayout.Width(90));

        // Final value (only show if different from base)
        if (!string.IsNullOrEmpty(stat.formula) || Mathf.Abs(stat.FinalValue - stat.BaseValue) > 0.01f)
        {
            // Calculate modifier
            float modifier = stat.FinalValue - stat.BaseValue;
            string modifierText = modifier >= 0 ? $"+{modifier:F1}" : $"{modifier:F1}";

            // Color based on modifier
            Color valueColor = modifier > 0 ? Color.green : (modifier < 0 ? Color.red : Color.white);

            var oldColor = GUI.contentColor;
            GUI.contentColor = valueColor;
            EditorGUILayout.LabelField($"Final: {stat.FinalValue:F1} ({modifierText})", GUILayout.Width(130));
            GUI.contentColor = oldColor;
        }

        // Formula indicator
        if (!string.IsNullOrEmpty(stat.formula))
        {
            EditorGUILayout.LabelField("📐", GUILayout.Width(20));
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif