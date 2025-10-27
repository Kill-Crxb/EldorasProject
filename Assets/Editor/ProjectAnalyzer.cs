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
                showDependencies
            );
        }
    }

    public class IntegrationAnalyzer
    {
        private readonly Dictionary<string, ModuleInfo> modules = new Dictionary<string, ModuleInfo>();
        private readonly List<string> interfaces = new List<string>();
        private readonly Dictionary<string, List<string>> animationParameters = new Dictionary<string, List<string>>();

        public string GenerateIntegrationReport(string folder, bool showIntegration, bool showMethods,
            bool showEvents, bool showAnimation, bool showDependencies)
        {
            ScanFolder(folder, showIntegration, showMethods, showEvents, showAnimation, showDependencies);
            return BuildIntegrationReport();
        }

        private void ScanFolder(string folder, bool showIntegration, bool showMethods, bool showEvents,
            bool showAnimation, bool showDependencies)
        {
            modules.Clear();
            interfaces.Clear();
            animationParameters.Clear();

            string fullPath = Path.Combine(Application.dataPath, folder.Replace("Assets/", ""));

            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"Folder does not exist: {fullPath}");
                return;
            }

            string[] scriptFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

            foreach (string filePath in scriptFiles)
            {
                AnalyzeScriptForIntegration(filePath, showIntegration, showMethods, showEvents, showAnimation, showDependencies);
            }
        }

        private void AnalyzeScriptForIntegration(string filePath, bool showIntegration, bool showMethods,
            bool showEvents, bool showAnimation, bool showDependencies)
        {
            string content = File.ReadAllText(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // Extract interfaces
            ExtractInterfaces(content, fileName);

            // Analyze classes for integration points
            var classMatches = Regex.Matches(content, @"public\s+class\s+(\w+)(?:\s*:\s*([^{]+))?");
            foreach (Match match in classMatches)
            {
                string className = match.Groups[1].Value;
                string inheritance = match.Groups[2].Value.Trim();

                var moduleInfo = new ModuleInfo
                {
                    Name = className,
                    FileName = fileName,
                    Inheritance = ParseInheritance(inheritance),
                    Category = CategorizeClass(className, inheritance, content)
                };

                if (showIntegration) ExtractIntegrationPoints(content, moduleInfo);
                if (showMethods) ExtractPublicAPI(content, moduleInfo);
                if (showEvents) ExtractEventDetails(content, moduleInfo);
                if (showAnimation) ExtractAnimationIntegration(content, moduleInfo, className);
                if (showDependencies) ExtractDependencies(content, moduleInfo);

                modules[className] = moduleInfo;
            }
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
                    interfaceInfo += $" : {inheritance}";

                if (methods.Any())
                    interfaceInfo += $"\n  - Methods: {string.Join(", ", methods)}";

                interfaces.Add(interfaceInfo);
            }
        }

        private List<string> ParseInheritance(string inheritance)
        {
            if (string.IsNullOrEmpty(inheritance)) return new List<string>();

            return inheritance.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private string CategorizeClass(string className, string inheritance, string content)
        {
            if (className.Contains("Module") || inheritance.Contains("IPlayerModule"))
                return "Core Module";

            if (inheritance.Contains("ICombatSubModule"))
                return "Combat Sub-Module";

            if (className.Contains("Controller") && !className.Contains("CharacterController"))
                return "Controller";

            if ((className.Contains("Effects") || className.Contains("Sounds")) && inheritance.Contains("MonoBehaviour"))
                return "Companion Script";

            if (className.Contains("Stats") || className.Contains("RPG"))
                return "Stats System";

            if (className.Contains("Camera"))
                return "Camera System";

            if (className.Contains("UI"))
                return "UI System";

            if (className.Contains("Weapon") || className.Contains("Hit"))
                return "Weapon System";

            return "Component";
        }

        private void ExtractIntegrationPoints(string content, ModuleInfo moduleInfo)
        {
            // Brain integration
            if (content.Contains("brain.GetModule"))
            {
                var brainCalls = Regex.Matches(content, @"brain\.GetModule<(\w+)>\(\)");
                foreach (Match match in brainCalls)
                {
                    moduleInfo.IntegrationPoints.Add($"Gets module: {match.Groups[1].Value}");
                }
            }

            if (content.Contains("brain.Get"))
            {
                var brainMethods = Regex.Matches(content, @"brain\.(\w+)\(");
                foreach (Match match in brainMethods)
                {
                    if (!match.Groups[1].Value.StartsWith("GetModule"))
                        moduleInfo.IntegrationPoints.Add($"Uses brain method: {match.Groups[1].Value}()");
                }
            }

            // Controller integration
            if (content.Contains("controller."))
            {
                var controllerCalls = Regex.Matches(content, @"controller\.(\w+)\(");
                foreach (Match match in controllerCalls)
                {
                    moduleInfo.IntegrationPoints.Add($"Uses controller: {match.Groups[1].Value}()");
                }
            }

            // Event subscriptions
            var eventSubscriptions = Regex.Matches(content, @"(\w+)\.(\w+)\s*\+=\s*(\w+)");
            foreach (Match match in eventSubscriptions)
            {
                moduleInfo.IntegrationPoints.Add($"Subscribes to: {match.Groups[1].Value}.{match.Groups[2].Value}");
            }

            // Interface implementations
            foreach (var impl in moduleInfo.Inheritance)
            {
                if (impl.StartsWith("I") && impl != "IEnumerator")
                    moduleInfo.IntegrationPoints.Add($"Implements: {impl}");
            }
        }

        private void ExtractPublicAPI(string content, ModuleInfo moduleInfo)
        {
            // Public methods with full signatures
            var methodPattern = @"public\s+([\w<>\[\]]+)\s+(\w+)\s*\(([^)]*)\)";
            var matches = Regex.Matches(content, methodPattern);

            foreach (Match match in matches)
            {
                string returnType = match.Groups[1].Value;
                string methodName = match.Groups[2].Value;
                string parameters = match.Groups[3].Value.Trim();

                if (methodName == moduleInfo.Name) continue; // Skip constructors

                string signature = $"{returnType} {methodName}({parameters})";
                moduleInfo.PublicMethods.Add(signature);
            }

            // Public properties
            var propertyPattern = @"public\s+([\w<>\[\]]+)\s+(\w+)\s*\{\s*([^}]+)\}";
            var propMatches = Regex.Matches(content, propertyPattern);

            foreach (Match match in propMatches)
            {
                string propType = match.Groups[1].Value;
                string propName = match.Groups[2].Value;
                string accessors = match.Groups[3].Value;

                moduleInfo.PublicProperties.Add($"{propType} {propName} {{ {accessors.Trim()} }}");
            }

            // Public fields
            var fieldPattern = @"(?:\[[^\]]*\]\s*)*public\s+([\w<>\[\]]+)\s+(\w+)\s*[=;]";
            var fieldMatches = Regex.Matches(content, fieldPattern);

            foreach (Match match in fieldMatches)
            {
                string fieldType = match.Groups[1].Value;
                string fieldName = match.Groups[2].Value;
                moduleInfo.PublicFields.Add($"{fieldType} {fieldName}");
            }
        }

        private void ExtractEventDetails(string content, ModuleInfo moduleInfo)
        {
            // Event declarations
            var eventPattern = @"public\s+(?:static\s+)?event\s+([\w<>\[\]]+)\s+(\w+)";
            var matches = Regex.Matches(content, eventPattern);

            foreach (Match match in matches)
            {
                string eventType = match.Groups[1].Value;
                string eventName = match.Groups[2].Value;
                moduleInfo.Events.Add($"{eventType} {eventName}");
            }

            // Event invocations
            var invokePattern = @"(\w+)?.Invoke\(";
            var invokeMatches = Regex.Matches(content, invokePattern);

            foreach (Match match in invokeMatches)
            {
                if (!string.IsNullOrEmpty(match.Groups[1].Value))
                    moduleInfo.EventInvocations.Add(match.Groups[1].Value);
            }
        }

        private void ExtractAnimationIntegration(string content, ModuleInfo moduleInfo, string className)
        {
            var animParams = new List<string>();

            // SetAnimation calls
            var setAnimPattern = @"SetAnimation(?:Bool|Int|Float|Trigger)\s*\(\s*[""']([^""']+)[""']";
            var matches = Regex.Matches(content, setAnimPattern);

            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;
                animParams.Add(paramName);
                moduleInfo.AnimationParameters.Add(paramName);
            }

            // animator.Set calls
            var animatorPattern = @"animator\.Set(?:Bool|Int|Float|Trigger)\s*\(\s*[""']([^""']+)[""']";
            var animatorMatches = Regex.Matches(content, animatorPattern);

            foreach (Match match in animatorMatches)
            {
                string paramName = match.Groups[1].Value;
                animParams.Add(paramName);
                moduleInfo.AnimationParameters.Add(paramName);
            }

            if (animParams.Any())
            {
                animationParameters[className] = animParams.Distinct().ToList();
            }
        }

        private void ExtractDependencies(string content, ModuleInfo moduleInfo)
        {
            // Required components
            var requirePattern = @"\[RequireComponent\(typeof\((\w+)\)\)\]";
            var matches = Regex.Matches(content, requirePattern);

            foreach (Match match in matches)
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

            // Summary
            report.AppendLine("## Project Summary");
            report.AppendLine($"- **Total Components Analyzed**: {modules.Count}");
            report.AppendLine($"- **Core Modules**: {modules.Count(m => m.Value.Category == "Core Module")}");
            report.AppendLine($"- **Combat Sub-Modules**: {modules.Count(m => m.Value.Category == "Combat Sub-Module")}");
            report.AppendLine($"- **Companion Scripts**: {modules.Count(m => m.Value.Category == "Companion Script")}");
            report.AppendLine($"- **Controllers**: {modules.Count(m => m.Value.Category == "Controller")}");
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
                    report.AppendLine($"### {module.Name}");
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