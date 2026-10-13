using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Runs the CrabSystem Standard's review checklist (section 13) over CrabSystem's own scripts and lists every hit
// with file and line. Read-only: it reports, it never edits. A hit is a prompt to look, not proof of a
// defect - a rule broken on purpose should carry a comment saying why, and stays listed so the next
// audit can judge it.

namespace CrabSystem
{
    public static class CrabSystemValidator
    {
        const string ReportPath = "Logs/CrabSystem_Validate.md";

        // Folders where a rule doesn't apply. Editor code may log and search; diagnostics may log;
        // presentation may read wall-clock time and unseeded randomness.
        static readonly string[] EditorFolders = { "/Editor/", "/Tests/" };
        static readonly string[] DiagnosticFolders = { "/Editor/", "/Tests/", "/DebugTools/", "/Debugging/" };
        static readonly string[] PresentationFolders = { "/Editor/", "/Tests/", "/DebugTools/", "/Debugging/", "/UIWindows/", "/Juice/", "/VFX/", "/Rendering/", "/Animation/CharacterFeel/", "/DamageNumbers/" };

        // Physics-step movement and the camera stay on Time by design (EntityClock); views animate on real time.
        static readonly string[] ClockExemptFolders = PresentationFolders.Concat(new[] { "/MovementLocomotion/", "/Camera/", "/Hotbar/" }).ToArray();

        struct Rule
        {
            public string Name;
            public Regex Pattern;
            public string[] SkipFolders;
            public string[] SkipFiles;
        }

        public struct Hit
        {
            public string Rule;
            public string Path;
            public int Line;
            public string Text;
        }

        static readonly Rule[] Rules =
        {
            new Rule { Name = "#region", Pattern = new Regex(@"^\s*#region\b"), SkipFolders = new string[0] },
            new Rule { Name = "XML doc comment", Pattern = new Regex(@"^\s*///"), SkipFolders = new string[0] },
            new Rule { Name = "Debug.Log in production", Pattern = new Regex(@"\bDebug\.Log\s*\("), SkipFolders = DiagnosticFolders },
            new Rule { Name = "Scene search in gameplay", Pattern = new Regex(@"\b(FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsOfType|FindObjectsByType|GetComponentInChildren|GetComponentsInChildren)\b"), SkipFolders = EditorFolders },
            new Rule { Name = "Third-party asset in framework", Pattern = new Regex(@"\b(MoreMountains|MagicaCloth\w*)\b"), SkipFolders = new string[0] },
            new Rule { Name = "Literal stat id", Pattern = new Regex(@"""(core|atr|def|cmb|character|mov|mag|res|act)\.[a-z_]+"""), SkipFolders = EditorFolders, SkipFiles = new[] { "StatIds.cs", "DialIds.cs" } },
            new Rule { Name = "Literal fact key", Pattern = new Regex(@"(BlackboardKey\.Hash\(\s*""\w+""|new\s+BlackboardKey\s*\(\s*""\w+""\s*\))"), SkipFolders = EditorFolders, SkipFiles = new[] { "BlackboardKey.cs" } },
            new Rule { Name = "Unstable key hash", Pattern = new Regex(@"(""\w+""|\w*([Kk]ey|[Ff]act|[Kk]eyName))\s*\.GetHashCode\(\)"), SkipFolders = new string[0] },
            new Rule { Name = "Unseeded random / wall clock in gameplay", Pattern = new Regex(@"\b(UnityEngine\.)?Random\.(Range|value|insideUnit\w+)\b|\bTime\.time\b"), SkipFolders = ClockExemptFolders, SkipFiles = new[] { "EntityClock.cs", "ControllerBrain.cs" } },
        };

        static readonly Regex Subscribe = new Regex(@"\.(On\w+)\s*\+=");
        static readonly Regex Unsubscribe = new Regex(@"\.(On\w+)\s*-=");

        [MenuItem("Tools/CrabSystem/Validate")]
        static void Validate()
        {
            List<Hit> hits = Scan();
            WriteReport(hits);
            Debug.Log($"[CrabSystemValidator] {hits.Count} findings in {CrabPackage.AssetRoot}. Report: {ReportPath}");
        }

        // Also read by the dashboard's Health tab.
        public static List<Hit> Scan()
        {
            var hits = new List<Hit>();
            foreach (string path in Directory.GetFiles(CrabPackage.DiskRoot, "*.cs", SearchOption.AllDirectories))
                ScanFile(path, CrabPackage.ToAssetPath(path), hits);

            return hits;
        }

        // Read from disk, reported by asset path so a hit can be pinged and opened.
        static void ScanFile(string diskPath, string path, List<Hit> hits)
        {
            string[] lines = File.ReadAllLines(diskPath);
            if (IsGenerated(lines)) return;

            for (int i = 0; i < lines.Length; i++)
                ScanLine(path, i + 1, lines[i], hits);

            ScanSubscriptions(path, lines, hits);
        }

        static void ScanLine(string path, int lineNumber, string line, List<Hit> hits)
        {
            foreach (Rule rule in Rules)
            {
                if (Skips(path, rule)) continue;
                if (!rule.Pattern.IsMatch(line)) continue;

                hits.Add(new Hit { Rule = rule.Name, Path = path, Line = lineNumber, Text = line.Trim() });
            }
        }

        // Code written by a generator (the Input System wrapper) isn't ours to style.
        static bool IsGenerated(string[] lines)
        {
            int head = Mathf.Min(lines.Length, 10);
            for (int i = 0; i < head; i++)
                if (lines[i].Contains("<auto-generated")) return true;

            return false;
        }

        static bool Skips(string path, Rule rule)
        {
            if (rule.SkipFiles != null && rule.SkipFiles.Any(file => path.EndsWith("/" + file))) return true;

            foreach (string folder in rule.SkipFolders)
                if (path.Contains(folder)) return true;

            return false;
        }

        // An event subscribed with += and never released with -= in the same file. Counted by event name,
        // so a named handler subscribed twice and released once still shows up.
        static void ScanSubscriptions(string path, string[] lines, List<Hit> hits)
        {
            var added = new Dictionary<string, int>();
            var removed = new Dictionary<string, int>();
            var firstLine = new Dictionary<string, int>();

            for (int i = 0; i < lines.Length; i++)
            {
                Count(Subscribe, lines[i], added, firstLine, i + 1);
                Count(Unsubscribe, lines[i], removed, null, i + 1);
            }

            foreach (var pair in added)
            {
                removed.TryGetValue(pair.Key, out int released);
                if (released >= pair.Value) continue;

                hits.Add(new Hit { Rule = "Unmatched event subscription", Path = path, Line = firstLine[pair.Key], Text = $"{pair.Key}: {pair.Value} += / {released} -=" });
            }
        }

        static void Count(Regex pattern, string line, Dictionary<string, int> counts, Dictionary<string, int> firstLine, int lineNumber)
        {
            foreach (Match match in pattern.Matches(line))
            {
                string name = match.Groups[1].Value;
                counts.TryGetValue(name, out int count);
                counts[name] = count + 1;

                if (firstLine != null && !firstLine.ContainsKey(name)) firstLine[name] = lineNumber;
            }
        }

        static void WriteReport(List<Hit> hits)
        {
            var byRule = new SortedDictionary<string, List<Hit>>();
            foreach (Hit hit in hits)
            {
                if (!byRule.ContainsKey(hit.Rule)) byRule[hit.Rule] = new List<Hit>();
                byRule[hit.Rule].Add(hit);
            }

            var report = new StringBuilder();
            report.AppendLine("# CrabSystem Validate");
            report.AppendLine();
            report.AppendLine($"{hits.Count} findings in `{CrabPackage.AssetRoot}`. Rules: `claude/CrabSystem_Standard.md` section 13.");
            report.AppendLine();

            foreach (var group in byRule)
            {
                report.AppendLine($"## {group.Key} ({group.Value.Count})");
                report.AppendLine();
                foreach (Hit hit in group.Value)
                    report.AppendLine($"- `{hit.Path}:{hit.Line}` {hit.Text}");
                report.AppendLine();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
        }
    }
}
