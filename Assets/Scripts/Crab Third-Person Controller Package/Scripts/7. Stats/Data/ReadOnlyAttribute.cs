using UnityEngine;

/// <summary>
/// Custom attribute to mark fields as read-only in the Unity Inspector.
/// Used for displaying calculated values that should not be manually edited.
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }