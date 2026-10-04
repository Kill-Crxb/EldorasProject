using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > CrabSystem > Validate Ids. Read-only. Walks every prefab component and every
// ScriptableObject under Assets/, finds [IdRef] fields (inside nested classes and lists too)
// and reports each value that no asset defines. Also lists assets with missing scripts, since
// loading them is the only way to find those. Writes Logs/CrabSystem_IdCheck.md; each console
// error pings its asset when clicked.
public static class IdValidator
{
    private const string ReportPath = "Logs/CrabSystem_IdCheck.md";
    private const int MaxDepth = 6;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<Type, bool> typeHasIds = new();

    private struct Finding
    {
        public UnityEngine.Object Asset;
        public string Where;
        public IdKind Kind;
        public string Value;
    }

    [MenuItem("Tools/CrabSystem/Validate Ids")]
    public static void Run()
    {
        var findings = new List<Finding>();
        var missingScripts = new List<string>();
        int checkedFields = 0;

        foreach (string path in AssetPaths("t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (MissingScriptCount(prefab) > 0) missingScripts.Add(path);

            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                string where = $"{path} > {component.gameObject.name} > {component.GetType().Name}";
                checkedFields += Walk(component, component.GetType(), where, prefab, findings, 0);
            }
        }

        foreach (string path in AssetPaths("t:ScriptableObject"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                missingScripts.Add(path);
                continue;
            }

            checkedFields += Walk(asset, asset.GetType(), path, asset, findings, 0);
        }

        WriteReport(findings, missingScripts, checkedFields);

        foreach (var f in findings)
            Debug.LogError($"[Validate Ids] {f.Kind} '{f.Value}' doesn't exist: {f.Where}", f.Asset);

        Debug.Log($"[Validate Ids] {checkedFields} id fields checked, {findings.Count} missing; {missingScripts.Count} assets with missing scripts. Report: {ReportPath}");
    }

    private static int MissingScriptCount(GameObject prefab)
    {
        int count = 0;
        foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        return count;
    }

    private static IEnumerable<string> AssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(filter, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath);
    }

    private static int Walk(object target, Type type, string where, UnityEngine.Object asset, List<Finding> findings, int depth)
    {
        if (target == null || depth > MaxDepth || !HasIds(type)) return 0;

        int count = 0;
        foreach (var field in SerializedFields(type))
        {
            object value = field.GetValue(target);
            string fieldWhere = $"{where}.{field.Name}";
            var idRef = field.GetCustomAttribute<IdRefAttribute>();

            if (idRef != null)
            {
                count += CheckIds(value, idRef.Kind, fieldWhere, asset, findings);
                continue;
            }

            count += WalkValue(value, ElementType(field.FieldType), fieldWhere, asset, findings, depth + 1);
        }

        return count;
    }

    private static int WalkValue(object value, Type type, string where, UnityEngine.Object asset, List<Finding> findings, int depth)
    {
        if (value == null || type == null || typeof(UnityEngine.Object).IsAssignableFrom(type)) return 0;

        if (value is IList list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
                count += Walk(list[i], type, $"{where}[{i}]", asset, findings, depth);
            return count;
        }

        return Walk(value, type, where, asset, findings, depth);
    }

    private static int CheckIds(object value, IdKind kind, string where, UnityEngine.Object asset, List<Finding> findings)
    {
        var ids = value is IList list ? list.Cast<object>().Select(v => v as string) : new[] { value as string };

        int count = 0;
        foreach (string id in ids)
        {
            count++;
            if (string.IsNullOrEmpty(id) || IdSources.Exists(kind, id)) continue;
            findings.Add(new Finding { Asset = asset, Where = where, Kind = kind, Value = id });
        }

        return count;
    }

    // A type is walked only if it, or something it holds, has an [IdRef] field. Cached per type,
    // so third-party components cost one check each.
    private static bool HasIds(Type type)
    {
        if (type == null) return false;
        if (typeHasIds.TryGetValue(type, out bool has)) return has;

        typeHasIds[type] = false; // guards recursive types while this one is being checked
        has = SerializedFields(type).Any(f =>
            f.IsDefined(typeof(IdRefAttribute)) ||
            (IsWalkable(ElementType(f.FieldType)) && HasIds(ElementType(f.FieldType))));

        typeHasIds[type] = has;
        return has;
    }

    private static bool IsWalkable(Type type)
    {
        return type != null && !type.IsPrimitive && type != typeof(string) && !type.IsEnum &&
               !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.IsDefined(typeof(SerializableAttribute));
    }

    private static Type ElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
        return type;
    }

    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        for (var t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
        {
            foreach (var field in t.GetFields(Fields | BindingFlags.DeclaredOnly))
            {
                if (field.IsNotSerialized) continue;
                if (!field.IsPublic && !field.IsDefined(typeof(SerializeField))) continue;
                yield return field;
            }
        }
    }

    private static void WriteReport(List<Finding> findings, List<string> missingScripts, int checkedFields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# CrabSystem — id check");
        sb.AppendLine();
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm}. {checkedFields} id fields checked, **{findings.Count} missing**. " +
                      $"**{missingScripts.Count}** assets with missing scripts.");
        sb.AppendLine();

        foreach (var group in findings.GroupBy(f => f.Kind).OrderBy(g => g.Key.ToString()))
        {
            sb.AppendLine($"## {group.Key} ({group.Count()})");
            sb.AppendLine();
            foreach (var f in group.OrderBy(f => f.Where))
                sb.AppendLine($"- `{f.Value}` — {f.Where}");
            sb.AppendLine();
        }

        if (missingScripts.Count > 0)
        {
            sb.AppendLine($"## Missing scripts ({missingScripts.Count})");
            sb.AppendLine();
            foreach (string path in missingScripts.OrderBy(p => p))
                sb.AppendLine($"- {path}");
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
