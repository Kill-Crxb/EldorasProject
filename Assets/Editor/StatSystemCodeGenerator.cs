using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NinjaGame.Stats.Editor
{
    /// <summary>
    /// Code generator for StatSystem property accessors.
    /// Scans all StatSchema assets and generates type-safe properties.
    /// 
    /// Usage: Tools → NinjaGame → Regenerate Stat Properties
    /// 
    /// Example Output:
    /// statsSystem.GetValue("character.body") → statsSystem.Body
    /// statsSystem.GetValue("combat.attack_power") → statsSystem.AttackPower
    /// </summary>
    public static class StatSystemCodeGenerator
    {
        private const string MENU_PATH = "Tools/NinjaGame/Regenerate Stat Properties";
        private const string GENERATED_FILE_NAME = "StatSystem_Generated.cs";

        [MenuItem(MENU_PATH, false, 100)]
        public static void GenerateStatProperties()
        {
            Debug.Log("[StatSystemCodeGenerator] Starting code generation...");

            // Step 1: Find all StatSchema assets
            var schemas = FindAllStatSchemas();
            if (schemas.Count == 0)
            {
                Debug.LogWarning("[StatSystemCodeGenerator] No StatSchema assets found! Create some schemas first.");
                return;
            }

            Debug.Log($"[StatSystemCodeGenerator] Found {schemas.Count} StatSchema assets");

            // Step 2: Parse all stat definitions
            var statDefinitions = ParseStatDefinitions(schemas);
            if (statDefinitions.Count == 0)
            {
                Debug.LogWarning("[StatSystemCodeGenerator] No stat definitions found in schemas!");
                return;
            }

            Debug.Log($"[StatSystemCodeGenerator] Parsed {statDefinitions.Count} stat definitions");

            // Step 3: Generate property names and detect conflicts
            var properties = GeneratePropertyData(statDefinitions);
            if (properties.Count == 0)
            {
                Debug.LogError("[StatSystemCodeGenerator] Failed to generate properties!");
                return;
            }

            Debug.Log($"[StatSystemCodeGenerator] Generated {properties.Count} properties");

            // Step 4: Generate C# code
            string generatedCode = GenerateCode(properties);

            // Step 5: Find StatSystem.cs location
            string targetPath = FindStatSystemLocation();
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogError("[StatSystemCodeGenerator] Could not find StatSystem.cs! Make sure it exists in your project.");
                return;
            }

            // Step 6: Write generated file
            File.WriteAllText(targetPath, generatedCode);
            AssetDatabase.Refresh();

            Debug.Log($"[StatSystemCodeGenerator] ✅ Successfully generated properties at: {targetPath}");
            Debug.Log($"[StatSystemCodeGenerator] You can now use properties like: statsSystem.Body, statsSystem.AttackPower, etc.");

            // Optional: Ping the file in the project window
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(targetPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }

        #region Schema Discovery

        private static List<StatSchema> FindAllStatSchemas()
        {
            var schemas = new List<StatSchema>();



            // Find all StatSchema assets in the project
            string[] guids = AssetDatabase.FindAssets("t:StatSchema");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StatSchema schema = AssetDatabase.LoadAssetAtPath<StatSchema>(path);

                if (schema != null)
                {
                    schemas.Add(schema);
                    Debug.Log($"[StatSystemCodeGenerator]   Found schema: {schema.name}");
                }
            }

            return schemas;
        }

        #endregion

        #region Stat Parsing

        private class StatDefinitionData
        {
            public string statId;           // "character.body"
            public string displayName;      // "Body"
            public string description;      // "Physical power..."
            public string schemaName;       // "RPGCoreStats"
        }

        private static List<StatDefinitionData> ParseStatDefinitions(List<StatSchema> schemas)
        {
            var definitions = new List<StatDefinitionData>();

            foreach (var schema in schemas)
            {
                if (schema.stats == null) continue;

                foreach (var stat in schema.stats)
                {
                    if (string.IsNullOrEmpty(stat.statId)) continue;

                    definitions.Add(new StatDefinitionData
                    {
                        statId = stat.statId,
                        displayName = stat.displayName,
                        description = stat.description,
                        schemaName = schema.name
                    });
                }
            }

            return definitions;
        }

        #endregion

        #region Property Generation

        private class PropertyData
        {
            public string propertyName;     // "Body"
            public string statId;           // "character.body"
            public string description;      // XML doc comment
            public string schemaName;       // Source schema
        }

        private static List<PropertyData> GeneratePropertyData(List<StatDefinitionData> definitions)
        {
            var properties = new List<PropertyData>();
            var usedNames = new HashSet<string>();
            var conflicts = new Dictionary<string, List<string>>(); // propertyName → list of statIds

            foreach (var def in definitions)
            {
                // Generate property name
                string propertyName = GeneratePropertyName(def.statId);

                // Validate C# identifier
                if (!IsValidCSharpIdentifier(propertyName))
                {
                    Debug.LogWarning($"[StatSystemCodeGenerator] Skipping invalid property name: '{propertyName}' from stat '{def.statId}'");
                    continue;
                }

                // Check for conflicts
                if (usedNames.Contains(propertyName))
                {
                    if (!conflicts.ContainsKey(propertyName))
                    {
                        conflicts[propertyName] = new List<string>();
                    }
                    conflicts[propertyName].Add(def.statId);
                    continue; // Skip duplicate
                }

                usedNames.Add(propertyName);

                properties.Add(new PropertyData
                {
                    propertyName = propertyName,
                    statId = def.statId,
                    description = def.description,
                    schemaName = def.schemaName
                });
            }

            // Report conflicts
            if (conflicts.Count > 0)
            {
                Debug.LogWarning($"[StatSystemCodeGenerator] Found {conflicts.Count} naming conflicts:");
                foreach (var conflict in conflicts)
                {
                    Debug.LogWarning($"  Property '{conflict.Key}' conflicts with: {string.Join(", ", conflict.Value)}");
                }
            }

            return properties;
        }

        /// <summary>
        /// Convert stat ID to valid C# property name
        /// Examples:
        /// - "character.body" → "Body"
        /// - "combat.attack_power" → "AttackPower"
        /// - "resources.max_health" → "MaxHealth"
        /// </summary>
        private static string GeneratePropertyName(string statId)
        {
            // Step 1: Remove namespace prefix (everything before last dot)
            string name = statId;
            int lastDot = statId.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < statId.Length - 1)
            {
                name = statId.Substring(lastDot + 1);
            }

            // Step 2: Convert snake_case to PascalCase
            name = SnakeCaseToPascalCase(name);

            // Step 3: Ensure first character is uppercase
            if (!string.IsNullOrEmpty(name) && char.IsLower(name[0]))
            {
                name = char.ToUpper(name[0]) + name.Substring(1);
            }

            return name;
        }

        /// <summary>
        /// Convert snake_case to PascalCase
        /// Example: "attack_power" → "AttackPower"
        /// </summary>
        private static string SnakeCaseToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var parts = input.Split('_');
            var result = new StringBuilder();

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                // Capitalize first letter, lowercase rest
                result.Append(char.ToUpper(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Check if string is a valid C# identifier
        /// </summary>
        private static bool IsValidCSharpIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;

            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                    return false;
            }

            // Check against C# keywords
            string[] keywords = { "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
                "char", "checked", "class", "const", "continue", "decimal", "default", "delegate",
                "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
                "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
                "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator",
                "out", "override", "params", "private", "protected", "public", "readonly", "ref",
                "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
                "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
                "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while" };

            return !keywords.Contains(name.ToLower());
        }

        #endregion

        #region Code Generation

        private static string GenerateCode(List<PropertyData> properties)
        {
            var code = new StringBuilder();

            // File header
            code.AppendLine("// AUTO-GENERATED CODE - DO NOT MODIFY BY HAND");
            code.AppendLine("// Generated by StatSystemCodeGenerator");
            code.AppendLine($"// Generated on: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            code.AppendLine("// To regenerate: Tools → NinjaGame → Regenerate Stat Properties");
            code.AppendLine();
            code.AppendLine("using UnityEngine;");
            code.AppendLine();

            // Partial class declaration
            code.AppendLine("/// <summary>");
            code.AppendLine("/// Auto-generated property accessors for StatSystem.");
            code.AppendLine("/// Provides type-safe, IntelliSense-friendly access to stats.");
            code.AppendLine("/// </summary>");
            code.AppendLine("public partial class StatSystem");
            code.AppendLine("{");

            // Sort properties alphabetically for easier navigation
            var sortedProperties = properties.OrderBy(p => p.propertyName).ToList();

            // Generate properties
            foreach (var prop in sortedProperties)
            {
                // XML documentation
                code.AppendLine("    /// <summary>");
                if (!string.IsNullOrEmpty(prop.description))
                {
                    code.AppendLine($"    /// {prop.description}");
                }
                else
                {
                    code.AppendLine($"    /// Stat: {prop.statId}");
                }
                code.AppendLine($"    /// Source: {prop.schemaName}");
                code.AppendLine("    /// </summary>");

                // Property declaration
                code.AppendLine($"    public float {prop.propertyName}");
                code.AppendLine("    {");
                code.AppendLine($"        get => GetValue(\"{prop.statId}\");");
                code.AppendLine($"        set => SetBaseValue(\"{prop.statId}\", value);");
                code.AppendLine("    }");
                code.AppendLine();
            }

            // Close class
            code.AppendLine("}");

            return code.ToString();
        }

        #endregion

        #region File Location

        private static string FindStatSystemLocation()
        {
            // Find all scripts containing "StatSystem" in name
            string[] guids = AssetDatabase.FindAssets("StatSystem t:Script");

            if (guids.Length == 0)
            {
                return null;
            }

            // Find EXACTLY "StatSystem.cs" (not StatSystemCodeGenerator.cs or others)
            string statSystemPath = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileName(path);

                // We want exactly "StatSystem.cs"
                if (filename == "StatSystem.cs")
                {
                    statSystemPath = path;
                    break;
                }
            }

            if (statSystemPath == null)
            {
                Debug.LogError("[StatSystemCodeGenerator] Could not find StatSystem.cs! Found these files instead:");
                foreach (string guid in guids)
                {
                    Debug.LogError($"  - {AssetDatabase.GUIDToAssetPath(guid)}");
                }
                return null;
            }

            // Extract directory
            string directory = Path.GetDirectoryName(statSystemPath);

            // Build path for generated file
            string generatedPath = Path.Combine(directory, GENERATED_FILE_NAME);

            Debug.Log($"[StatSystemCodeGenerator] StatSystem.cs found at: {statSystemPath}");
            Debug.Log($"[StatSystemCodeGenerator] Will generate at: {generatedPath}");

            return generatedPath;
        }

        #endregion

        #region Validation Menu

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateGenerateStatProperties()
        {
            // Menu item is always available
            return true;
        }

        #endregion
    }
}