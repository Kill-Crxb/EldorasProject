using UnityEditor;
using UnityEngine;

// Play mode only: shows what the entity actually has, which the serialized fields can't.
// Base is what the character owns; +/- is everything contributed on top (gear, statuses, archetype).
[CustomEditor(typeof(StatSystem))]
public class StatSystemInspector : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;

        var stats = (StatSystem)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Live values", EditorStyles.boldLabel);
        DrawColumnHeader();

        foreach (string id in stats.GetStatIds())
            DrawRow(stats, id);
    }

    private static void DrawColumnHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Stat", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Base", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("Final", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        EditorGUILayout.LabelField("+/-", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawRow(StatSystem stats, string id)
    {
        float baseValue = stats.GetBaseValue(id);
        float value = stats.GetValue(id);
        string label = stats.IsDerived(id) ? id + " (derived)" : id;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label);
        EditorGUILayout.LabelField(baseValue.ToString("0.##"), GUILayout.Width(50));
        EditorGUILayout.LabelField(value.ToString("0.##"), GUILayout.Width(50));
        EditorGUILayout.LabelField((value - baseValue).ToString("+0.##;-0.##;0"), GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }
}
