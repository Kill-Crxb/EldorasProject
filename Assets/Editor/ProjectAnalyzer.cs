using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace ProjectAnalysis
{
    public class ProjectAnalyzerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string analysisResult = "";
        private string targetFolder = "Assets/Scripts";
        private bool showIntegrationPoints = true;
        private bool showMethodSignatures = true;
        private bool showEventDetails = true;
        private bool showAnimationParameters = true;
        private bool showDependencies = true;
        private bool includeNestedClasses = false; // NEW: Toggle for nested classes

        [MenuItem("Tools/Project Integration Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<ProjectAnalyzerWindow>("Integration Analyzer");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Project Integration Points Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Folder selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Folder:", GUILayout.Width(100));
            targetFolder = EditorGUILayout.TextField(targetFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Scripts Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    targetFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Analysis options
            EditorGUILayout.LabelField("Analysis Options:", EditorStyles.boldLabel);
            showIntegrationPoints = EditorGUILayout.Toggle("Show Integration Points", showIntegrationPoints);
            showMethodSignatures = EditorGUILayout.Toggle("Show Method Signatures", showMethodSignatures);
            showEventDetails = EditorGUILayout.Toggle("Show Event Details", showEventDetails);
            showAnimationParameters = EditorGUILayout.Toggle("Show Animation Parameters", showAnimationParameters);
            showDependencies = EditorGUILayout.Toggle("Show Module Dependencies", showDependencies);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Component Counting:", EditorStyles.boldLabel);
            includeNestedClasses = EditorGUILayout.Toggle("Include Nested Classes", includeNestedClasses);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Analyze Integration Points", GUILayout.Height(30)))
            {
                AnalyzeIntegration();
            }

            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(analysisResult))
            {
                EditorGUILayout.LabelField("Integration Analysis Results:", EditorStyles.boldLabel);

                if (GUILayout.Button("Copy to Clipboard"))
                {
                    GUIUtility.systemCopyBuffer = analysisResult;
                    Debug.Log("Integration analysis copied to clipboard!");
                }

                EditorGUILayout.Space(5);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                EditorGUILayout.TextArea(analysisResult, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void AnalyzeIntegration()
        {
            var analyzer = new IntegrationAnalyzer();
            analysisResult = analyzer.GenerateIntegrationReport(
                targetFolder,
                showIntegrationPoints,
                showMethodSignatures,
                showEventDetails,
                showAnimationParameters,
                showDependencies,
                includeNestedClasses
            );
        }
    }

    public class IntegrationAnalyzer
    {
        private readonly Dictionary<string, ModuleInfo> modules = new Dictionary<string, ModuleInfo>();
        private readonly List<string> interfaces = new List<string>();
        private readonly Dictionary<string, List<string>> animationParameters = new Dictionary<string, List<string>>();
        private int totalScriptFiles = 0; // NEW: Track actual script files
        private HashSet<string> processedFiles = new HashSet<string>(); // NEW: Track processed files

        public string GenerateIntegrationReport(string folder, bool showIntegration, bool showMethods,
            bool showEvents, bool showAnimation, bool showDependencies, bool includeNested)
        {
            ScanFolder(folder, showIntegration, showMethods, showEvents, showAnimation, showDependencies, includeNested);
            return BuildIntegrationReport();
        }

        private void ScanFolder(string folder, bool showIntegration, bool showMethods, bool showEvents,
            bool showAnimation, bool showDependencies, bool includeNested)
        {
            modules.Clear();
            interfaces.Clear();
            animationParameters.Clear();
            totalScriptFiles = 0;
            processedFiles.Clear();

            string fullPath = Path.Combine(Application.dataPath, folder.Replace("Assets/", ""));

            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"Folder does not exist: {fullPath}");
                return;
            }

            string[] scriptFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
            totalScriptFiles = scriptFiles.Length; // Count actual .cs files

            foreach (string filePath in scriptFiles)
            {
                processedFiles.Add(Path.GetFileName(filePath));
                AnalyzeScriptForIntegration(filePath, showIntegration, showMethods, showEvents, showAnimation, showDependencies, includeNested);
            }
        }

        private void AnalyzeScriptForIntegration(string filePath, bool showIntegration, bool showMethods,
            bool showEvents, bool showAnimation, bool showDependencies, bool includeNested)
        {
            string content = File.ReadAllText(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Extract interfaces
            ExtractInterfaces(content, fileName);

            // Analyze classes for integration points
            // IMPROVED: Better pattern to detect top-level vs nested classes
            var lines = content.Split('\n');
            int braceDepth = 0;
            bool inNamespace = false;
            int namespaceDepth = 0;

            var classMatches = Regex.Matches(content, @"public\s+(?:abstract\s+)?class\s+(\w+)(?:\s*:\s*([^{]+))?");

            foreach (Match match in classMatches)
            {
                string className = match.Groups[1].Value;
                string inheritance = match.Groups[2].Value.Trim();

                // NEW: Check if this is a nested class
                int matchPosition = match.Index;
                bool isNested = IsNestedClass(content, matchPosition);

                // Skip nested classes unless includeNested is true
                if (isNested && !includeNested)
                {
                    continue;
                }

                var moduleInfo = new ModuleInfo
                {
                    Name = className,
                    FileName = fileName,
                    Inheritance = ParseInheritance(inheritance),
                    Category = CategorizeClass(className, inheritance, content),
                    IsNestedClass = isNested // NEW: Mark if nested
                };

                if (showIntegration) ExtractIntegrationPoints(content, moduleInfo);
                if (showMethods) ExtractPublicAPI(content, moduleInfo);
                if (showEvents) ExtractEventDetails(content, moduleInfo);
                if (showAnimation) ExtractAnimationIntegration(content, moduleInfo, className);
                if (showDependencies) ExtractDependencies(content, moduleInfo);

                // Use fileName.className as key for nested classes to avoid conflicts
                string key = isNested ? $"{fileName}.{className}" : className;
                modules[key] = moduleInfo;
            }
        }

        // NEW: Helper method to detect if a class is nested
        private bool IsNestedClass(string content, int classPosition)
        {
            // Count opening braces before this class declaration
            string beforeClass = content.Substring(0, classPosition);

            // Remove string literals and comments to avoid counting braces in them
            beforeClass = Regex.Replace(beforeClass, @"""[^""\\]*(?:\\.[^""\\]*)*""", "");
            beforeClass = Regex.Replace(beforeClass, @"//[^\n]*", "");
            beforeClass = Regex.Replace(beforeClass, @"/\*.*?\*/", "", RegexOptions.Singleline);

            int openBraces = beforeClass.Count(c => c == '{');
            int closeBraces = beforeClass.Count(c => c == '}');
            int braceDepth = openBraces - closeBraces;

            // If brace depth > 1, it's nested (namespace = 1, nested class > 1)
            // Account for namespace brace
            var namespaceMatches = Regex.Matches(beforeClass, @"namespace\s+[\w\.]+\s*\{");
            int namespaceCount = namespaceMatches.Count;

            return braceDepth > namespaceCount;
        }

        private void ExtractInterfaces(string content, string fileName)
        {
            var interfacePattern = @"public\s+interface\s+(\w+)(?:\s*:\s*([^{]+))?\s*\{([^}]*)\}";
            var matches = Regex.Matches(content, interfacePattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string interfaceName = match.Groups[1].Value;
                string inheritance = match.Groups[2].Value.Trim();
                string body = match.Groups[3].Value;

                var methodMatches = Regex.Matches(body, @"(\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)");
                var methods = methodMatches.Cast<Match>()
                    .Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value}()")
                    .ToList();

                string interfaceInfo = $"**{interfaceName}** (in {fileName})";
                if (!string.IsNullOrEmpty(inheritance))
                {
                    interfaceInfo += $" : {inheritance}";
                }

                if (methods.Any())
                {
                    interfaceInfo += $"\n  - Methods: {string.Join(", ", methods)}";
                }

                interfaces.Add(interfaceInfo);
            }
        }

        private List<string> ParseInheritance(string inheritance)
        {
            if (string.IsNullOrWhiteSpace(inheritance)) return new List<string>();

            return inheritance.Split(',')
                .Select(i => i.Trim())
                .Where(i => !string.IsNullOrEmpty(i))
                .ToList();
        }

        private string CategorizeClass(string className, string inheritance, string content)
        {
            // More accurate categorization
            if (className.Contains("Controller") && !className.Contains("Debug"))
                return "Controller";

            if (inheritance.Contains("IPlayerModule") || inheritance.Contains("IBrainModule"))
                return "Core Module";

            if (inheritance.Contains("IMeleeSubModule"))
                return "Combat Sub-Module";

            if (className.Contains("Effects") || className.Contains("Sounds"))
                return "Companion Script";

            if (inheritance.Contains("ISystemCoordinator") || className.Contains("Coordinator"))
                return "System Coordinator";

            if (className.Contains("Camera"))
                return "Camera System";

            if (className.Contains("Adapter"))
                return "Adapter";

            if (className.Contains("UI") && !className.Contains("Debug"))
                return "UI System";

            if (className.Contains("Stats") || className.Contains("RPG"))
                return "Stats System";

            if (className.Contains("Weapon") && !className.Contains("Module"))
                return "Weapon System";

            if (className.Contains("AI") || className.Contains("NPC"))
                return "AI/NPC System";

            if (className.Contains("Faction"))
                return "Faction System";

            if (className.Contains("Inventory") || className.Contains("Item") || className.Contains("Equipment"))
                return "Inventory System";

            if (className.Contains("Grid"))
                return "Grid System";

            return "Component";
        }

        private void ExtractIntegrationPoints(string content, ModuleInfo moduleInfo)
        {
            // brain.GetModule calls
            var getModulePattern = @"brain\.GetModule<(\w+)>\(\)";
            var moduleMatches = Regex.Matches(content, getModulePattern);

            foreach (Match match in moduleMatches)
            {
                moduleInfo.IntegrationPoints.Add($"Gets module: {match.Groups[1].Value}");
            }

            // brain method calls
            var brainMethodPattern = @"brain\.(\w+)\(";
            var brainMatches = Regex.Matches(content, brainMethodPattern);

            foreach (Match match in brainMatches)
            {
                string method = match.Groups[1].Value;
                if (method != "GetModule" && method != "GetComponent")
                {
                    moduleInfo.IntegrationPoints.Add($"Uses brain method: {method}()");
                }
            }

            // Controller method calls
            var controllerPattern = @"(?:controller|thirdPersonController)\.(\w+)\(";
            var controllerMatches = Regex.Matches(content, controllerPattern);

            foreach (Match match in controllerMatches)
            {
                moduleInfo.IntegrationPoints.Add($"Uses controller: {match.Groups[1].Value}()");
            }

            // Event subscriptions
            var eventPattern = @"(\w+)\.(\w+)\s*\+=";
            var eventMatches = Regex.Matches(content, eventPattern);

            foreach (Match match in eventMatches)
            {
                moduleInfo.IntegrationPoints.Add($"Subscribes to: {match.Groups[1].Value}.{match.Groups[2].Value}");
            }

            // Interface implementation
            foreach (var iface in moduleInfo.Inheritance)
            {
                if (iface.StartsWith("I") && iface.Length > 1 && char.IsUpper(iface[1]))
                {
                    moduleInfo.IntegrationPoints.Add($"Implements: {iface}");
                }
            }
        }

        private void ExtractPublicAPI(string content, ModuleInfo moduleInfo)
        {
            // Public methods
            var methodPattern = @"public\s+(?:virtual\s+|override\s+|static\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)";
            var methodMatches = Regex.Matches(content, methodPattern);

            foreach (Match match in methodMatches)
            {
                string returnType = match.Groups[1].Value;
                string methodName = match.Groups[2].Value;

                // Skip properties and special methods
                if (methodName == "get" || methodName == "set") continue;

                moduleInfo.PublicMethods.Add($"{returnType} {methodName}()");
            }

            // Public properties
            var propPattern = @"public\s+(\w+(?:<[^>]+>)?)\s+(\w+)\s*\{\s*get";
            var propMatches = Regex.Matches(content, propPattern);

            foreach (Match match in propMatches)
            {
                string type = match.Groups[1].Value;
                string propName = match.Groups[2].Value;
                moduleInfo.PublicProperties.Add($"{type} {propName}");
            }

            // Public fields
            var fieldPattern = @"public\s+(\w+(?:<[^>]+>)?)\s+(\w+)\s*(?:=|;)";
            var fieldMatches = Regex.Matches(content, fieldPattern);

            foreach (Match match in fieldMatches)
            {
                string type = match.Groups[1].Value;
                string fieldName = match.Groups[2].Value;

                // Skip if it looks like it might be a property or method
                if (!type.Contains("(") && !fieldName.Contains("("))
                {
                    moduleInfo.PublicFields.Add($"{type} {fieldName}");
                }
            }
        }

        private void ExtractEventDetails(string content, ModuleInfo moduleInfo)
        {
            // Event declarations
            var eventPattern = @"public\s+event\s+(\w+(?:<[^>]+>)?)\s+(\w+)";
            var eventMatches = Regex.Matches(content, eventPattern);

            foreach (Match match in eventMatches)
            {
                string eventType = match.Groups[1].Value;
                string eventName = match.Groups[2].Value;
                moduleInfo.Events.Add($"{eventType} {eventName}");
            }

            // Event invocations
            var invokePattern = @"(\w+)\?\s*\.Invoke\(";
            var invokeMatches = Regex.Matches(content, invokePattern);

            foreach (Match match in invokeMatches)
            {
                moduleInfo.EventInvocations.Add(match.Groups[1].Value);
            }
        }

        private void ExtractAnimationIntegration(string content, ModuleInfo moduleInfo, string className)
        {
            // Animation parameter sets
            var animPatterns = new[]
            {
                @"SetAnimation(?:Bool|Int|Float|Trigger)\([""'](\w+)[""']",
                @"animator\.Set(?:Bool|Integer|Float|Trigger)\([""'](\w+)[""']"
            };

            var foundParams = new HashSet<string>();

            foreach (var pattern in animPatterns)
            {
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    foundParams.Add(match.Groups[1].Value);
                }
            }

            moduleInfo.AnimationParameters.AddRange(foundParams);

            if (foundParams.Any() && !animationParameters.ContainsKey(className))
            {
                animationParameters[className] = foundParams.ToList();
            }
        }

        private void ExtractDependencies(string content, ModuleInfo moduleInfo)
        {
            // Requires attribute
            var requiresPattern = @"\[RequireComponent\(typeof\((\w+)\)\)\]";
            var requiresMatches = Regex.Matches(content, requiresPattern);

            foreach (Match match in requiresMatches)
            {
                moduleInfo.Dependencies.Add($"Requires: {match.Groups[1].Value}");
            }

            // GetComponent calls
            var getCompPattern = @"GetComponent(?:InChildren|InParent)?<(\w+)>\(\)";
            var compMatches = Regex.Matches(content, getCompPattern);

            foreach (Match match in compMatches)
            {
                moduleInfo.Dependencies.Add($"Uses component: {match.Groups[1].Value}");
            }

            // Find calls to other modules
            var moduleCallPattern = @"(\w+Module)\s+\w+\s*=\s*brain\.GetModule";
            var moduleMatches = Regex.Matches(content, moduleCallPattern);

            foreach (Match match in moduleMatches)
            {
                moduleInfo.Dependencies.Add($"Depends on: {match.Groups[1].Value}");
            }
        }

        private string BuildIntegrationReport()
        {
            var report = new StringBuilder();

            report.AppendLine("# Unity Project Integration Analysis");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            // IMPROVED: More accurate summary
            var topLevelModules = modules.Values.Where(m => !m.IsNestedClass).ToList();
            var nestedClasses = modules.Values.Where(m => m.IsNestedClass).ToList();

            report.AppendLine("## Project Summary");
            report.AppendLine($"- **Total Script Files (.cs)**: {totalScriptFiles}");
            report.AppendLine($"- **Total Components Analyzed**: {modules.Count}");
            report.AppendLine($"  - Top-Level Classes: {topLevelModules.Count}");
            report.AppendLine($"  - Nested Classes: {nestedClasses.Count}");
            report.AppendLine($"- **Core Modules**: {topLevelModules.Count(m => m.Category == "Core Module")}");
            report.AppendLine($"- **Combat Sub-Modules**: {topLevelModules.Count(m => m.Category == "Combat Sub-Module")}");
            report.AppendLine($"- **Companion Scripts**: {topLevelModules.Count(m => m.Category == "Companion Script")}");
            report.AppendLine($"- **System Coordinators**: {topLevelModules.Count(m => m.Category == "System Coordinator")}");
            report.AppendLine($"- **Controllers**: {topLevelModules.Count(m => m.Category == "Controller")}");
            report.AppendLine($"- **Adapters**: {topLevelModules.Count(m => m.Category == "Adapter")}");
            report.AppendLine($"- **AI/NPC Systems**: {topLevelModules.Count(m => m.Category == "AI/NPC System")}");
            report.AppendLine($"- **Interfaces Found**: {interfaces.Count}");
            report.AppendLine();

            // Interface definitions
            if (interfaces.Any())
            {
                report.AppendLine("## Interface Definitions");
                foreach (var iface in interfaces)
                {
                    report.AppendLine($"- {iface}");
                }
                report.AppendLine();
            }

            // Modules by category
            var categories = modules.Values.GroupBy(m => m.Category).OrderBy(g => g.Key);

            foreach (var category in categories)
            {
                report.AppendLine($"## {category.Key}s");
                report.AppendLine();

                foreach (var module in category.OrderBy(m => m.Name))
                {
                    // Mark nested classes
                    string nameDisplay = module.IsNestedClass ? $"{module.Name} (nested in {module.FileName})" : module.Name;
                    report.AppendLine($"### {nameDisplay}");
                    report.AppendLine($"**File**: {module.FileName}.cs");

                    if (module.Inheritance.Any())
                    {
                        report.AppendLine($"**Inherits/Implements**: {string.Join(", ", module.Inheritance)}");
                    }

                    // Integration Points
                    if (module.IntegrationPoints.Any())
                    {
                        report.AppendLine("**Integration Points**:");
                        foreach (var point in module.IntegrationPoints)
                        {
                            report.AppendLine($"- {point}");
                        }
                    }

                    // Public API
                    if (module.PublicMethods.Any())
                    {
                        report.AppendLine("**Public Methods**:");
                        foreach (var method in module.PublicMethods.Take(10)) // Limit to prevent overwhelming
                        {
                            report.AppendLine($"- `{method}`");
                        }
                        if (module.PublicMethods.Count > 10)
                            report.AppendLine($"- ... and {module.PublicMethods.Count - 10} more methods");
                    }

                    if (module.PublicProperties.Any())
                    {
                        report.AppendLine("**Public Properties**:");
                        foreach (var prop in module.PublicProperties)
                        {
                            report.AppendLine($"- `{prop}`");
                        }
                    }

                    if (module.PublicFields.Any())
                    {
                        report.AppendLine("**Public Fields**:");
                        foreach (var field in module.PublicFields.Take(5))
                        {
                            report.AppendLine($"- `{field}`");
                        }
                        if (module.PublicFields.Count > 5)
                            report.AppendLine($"- ... and {module.PublicFields.Count - 5} more fields");
                    }

                    // Events
                    if (module.Events.Any())
                    {
                        report.AppendLine("**Events**:");
                        foreach (var evt in module.Events)
                        {
                            report.AppendLine($"- `{evt}`");
                        }
                    }

                    if (module.EventInvocations.Any())
                    {
                        report.AppendLine($"**Fires Events**: {string.Join(", ", module.EventInvocations.Distinct())}");
                    }

                    // Animation Parameters
                    if (module.AnimationParameters.Any())
                    {
                        report.AppendLine($"**Animation Parameters**: {string.Join(", ", module.AnimationParameters)}");
                    }

                    // Dependencies
                    if (module.Dependencies.Any())
                    {
                        report.AppendLine("**Dependencies**:");
                        foreach (var dep in module.Dependencies.Distinct())
                        {
                            report.AppendLine($"- {dep}");
                        }
                    }

                    report.AppendLine();
                }
            }

            // Integration Matrix
            report.AppendLine("## Module Integration Matrix");
            report.AppendLine("Shows how modules connect to each other:");
            report.AppendLine();

            foreach (var module in modules.Values.Where(m => m.IntegrationPoints.Any()))
            {
                var connections = module.IntegrationPoints
                    .Where(p => p.Contains("Gets module:") || p.Contains("Depends on:"))
                    .ToList();

                if (connections.Any())
                {
                    report.AppendLine($"**{module.Name}** connects to:");
                    foreach (var connection in connections)
                    {
                        report.AppendLine($"  - {connection}");
                    }
                    report.AppendLine();
                }
            }

            return report.ToString();
        }
    }

    public class ModuleInfo
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public List<string> Inheritance { get; set; } = new List<string>();
        public string Category { get; set; }
        public bool IsNestedClass { get; set; } // NEW: Track if nested
        public List<string> IntegrationPoints { get; set; } = new List<string>();
        public List<string> PublicMethods { get; set; } = new List<string>();
        public List<string> PublicProperties { get; set; } = new List<string>();
        public List<string> PublicFields { get; set; } = new List<string>();
        public List<string> Events { get; set; } = new List<string>();
        public List<string> EventInvocations { get; set; } = new List<string>();
        public List<string> AnimationParameters { get; set; } = new List<string>();
        public List<string> Dependencies { get; set; } = new List<string>();
    }
}