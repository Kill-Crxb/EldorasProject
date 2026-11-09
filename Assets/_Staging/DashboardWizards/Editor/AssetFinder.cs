using System.Collections.Generic;
using UnityEditor;

// Every asset of a type in the project, for the dashboard's staging tabs. Graduates with WizardTab.

public static class AssetFinder
{
    public static List<T> LoadAll<T>(string filter = null) where T : UnityEngine.Object
    {
        var found = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets(filter ?? $"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }

        return found;
    }
}
