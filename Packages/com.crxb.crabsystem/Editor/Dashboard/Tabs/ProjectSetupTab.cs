using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrabSystem.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// What a project needs before CrabSystem runs in it (README, "What a game provides"), checked live, with a fix
// where one can be made safely. Fixes only add: a missing layer, folder or starter asset. They never change
// existing data. Starter data goes in CrabSystemSettings.dataFolder.

namespace CrabSystem
{
    public class ProjectSetupTab : IDashboardTab
    {
        public string Title => "Project Setup";
        public DashboardGroup Group => DashboardGroup.Build;
        public int Order => 1;

        class Check
        {
            public string name;
            public Func<string> problem;    // null when fine
            public string fixLabel;
            public Action fix;
        }

        DashboardContext context;
        VisualElement rows;

        static readonly string[] ResourceFolders = { CrabResources.Abilities, CrabResources.Items, CrabResources.Stats, CrabResources.Keywords };

        public VisualElement Build(DashboardContext dashboard)
        {
            context = dashboard;
            rows = new VisualElement();

            var editor = new VisualElement();
            editor.Add(DashboardLayout.Heading("Requirements"));
            editor.Add(rows);

            var side = new VisualElement();
            side.Add(DashboardLayout.Heading("About"));
            side.Add(DashboardLayout.Note("Everything CrabSystem expects a project to provide. Fixes only add what is missing."));
            side.Add(DashboardLayout.Note("Folders come from Project Settings > CrabSystem."));
            side.Add(new Button(Refresh) { text = "Check again" });
            side.Add(new Button(() => SettingsService.OpenProjectSettings("Project/CrabSystem")) { text = "Open CrabSystem settings" });

            Refresh();
            return DashboardLayout.Columns(null, editor, side);
        }

        void Refresh()
        {
            rows.Clear();
            int open = 0;
            foreach (Check check in Checks())
            {
                string problem = check.problem();
                if (problem != null) open++;
                rows.Add(Row(check, problem));
            }
            context.Log(open == 0 ? "Project Setup: everything CrabSystem needs is here." : $"Project Setup: {open} to fix.");
        }

        VisualElement Row(Check check, string problem)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            row.Add(new Label(problem == null ? "✓" : "✗") { style = { width = 18, color = problem == null ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.45f, 0.35f) } });

            var text = new VisualElement { style = { flexGrow = 1 } };
            text.Add(new Label(check.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (problem != null) text.Add(new Label(problem) { style = { whiteSpace = WhiteSpace.Normal, opacity = 0.8f } });
            row.Add(text);

            if (problem != null && check.fix != null)
                row.Add(new Button(() => { check.fix(); AssetDatabase.SaveAssets(); Refresh(); }) { text = check.fixLabel });
            return row;
        }

        IEnumerable<Check> Checks()
        {
            yield return new Check { name = "Universal Render Pipeline", problem = UrpProblem };
            yield return new Check { name = "Input System", problem = InputProblem };
            yield return new Check { name = "TextMesh Pro essentials", problem = TmpProblem, fixLabel = "Import", fix = ImportTmp };
            yield return new Check { name = "Layers (CrabLayers)", problem = LayerProblem, fixLabel = "Add layers", fix = AddLayers };
            yield return new Check { name = "Resources folders (CrabResources)", problem = FolderProblem, fixLabel = "Create folders", fix = CreateFolders };
            yield return new Check { name = "Stats (StatIds, DialIds)", problem = StatProblem, fixLabel = "Create starter schema", fix = CreateStatSchema };
            yield return new Check { name = "Damage types", problem = DamageProblem, fixLabel = "Create starter config", fix = CreateDamageConfig };
        }

        static string UrpProblem() =>
            GraphicsSettings.defaultRenderPipeline != null && GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("Universal")
                ? null
                : "No URP asset in Project Settings > Graphics. The toon shaders and post effects need URP.";

        static string InputProblem()
        {
#if ENABLE_INPUT_SYSTEM
            return null;
#else
            return "Project Settings > Player > Active Input Handling must include the Input System package.";
#endif
        }

        static string TmpProblem() =>
            AssetDatabase.FindAssets("t:TMP_Settings").Length > 0 ? null : "The UI prefabs use TextMesh Pro's essential resources.";

        static void ImportTmp() => EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");

        static IEnumerable<string> MissingLayers() =>
            IdValidator.Constants(typeof(CrabLayers)).Select(field => (string)field.GetValue(null)).Where(layer => LayerMask.NameToLayer(layer) < 0);

        static string LayerProblem()
        {
            var missing = MissingLayers().ToList();
            return missing.Count == 0 ? null : "Missing: " + string.Join(", ", missing);
        }

        // User layers are 8–31; each missing layer takes the first empty slot.
        static void AddLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach (string layer in MissingLayers())
            {
                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                    slot.stringValue = layer;
                    break;
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        static IEnumerable<string> MissingFolders() =>
            ResourceFolders.Where(folder => !AssetDatabase.GetAllAssetPaths().Any(path => path.EndsWith("/Resources/" + folder) && AssetDatabase.IsValidFolder(path)));

        static string FolderProblem()
        {
            var missing = MissingFolders().ToList();
            return missing.Count == 0 ? null : $"No Resources/{string.Join(", Resources/", missing)}. Needed unless those definitions are assigned on Manager_Brain.";
        }

        static void CreateFolders()
        {
            foreach (string folder in MissingFolders()) EnsureFolder("Assets/Resources/" + folder);
        }

        static IEnumerable<string> MissingStats() =>
            IdValidator.Constants(typeof(StatIds)).Concat(IdValidator.Constants(typeof(DialIds)))
                .Select(field => (string)field.GetValue(null)).Distinct().Where(id => !IdSources.Exists(IdKind.Stat, id));

        static string StatProblem()
        {
            var missing = MissingStats().ToList();
            return missing.Count == 0 ? null : $"{missing.Count} stat ids the framework names have no schema entry: {string.Join(", ", missing.Take(8))}{(missing.Count > 8 ? " …" : "")}";
        }

        // One schema holding every missing id at 0, under Resources/StatDatabase so StatsManager loads it. Edit the
        // ranges and defaults afterwards, or move the entries into the game's own schemas.
        static void CreateStatSchema()
        {
            var missing = MissingStats().ToList();
            string folder = EnsureFolder($"Assets/Resources/{CrabResources.Stats}");
            var schema = ScriptableObject.CreateInstance<StatSchema>();
            AssetDatabase.CreateAsset(schema, AssetDatabase.GenerateUniqueAssetPath($"{folder}/StarterStats.asset"));

            var serialized = new SerializedObject(schema);
            SerializedProperty entries = serialized.FindProperty("entries");
            foreach (string id in missing)
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("id").stringValue = id;
                entry.FindPropertyRelative("displayName").stringValue = id;
                entry.FindPropertyRelative("maxValue").floatValue = 99f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            IdSources.Invalidate();
        }

        static string DamageProblem()
        {
            var configs = Find<DamageCalculationConfig>().ToList();
            if (configs.Count == 0) return "No Damage Calculation Config. Manager_Brain's DamageManager needs one.";
            if (configs.All(config => config.Find(DamageTypeIds.Physical)?.id != DamageTypeIds.Physical))
                return $"No config lists a '{DamageTypeIds.Physical}' damage type, the type code defaults to.";
            return null;
        }

        // A physical type (soaked by the armour shield) as the default, in a config to assign on the game's Manager_Brain.
        static void CreateDamageConfig()
        {
            string folder = EnsureFolder($"{CrabSystemSettings.instance.dataFolder}/DamageTypes");
            var physical = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            physical.id = DamageTypeIds.Physical;
            physical.displayName = "Physical";
            physical.absorbedByShield = true;
            AssetDatabase.CreateAsset(physical, AssetDatabase.GenerateUniqueAssetPath($"{folder}/DT_Physical.asset"));

            var config = ScriptableObject.CreateInstance<DamageCalculationConfig>();
            config.damageTypes.Add(physical);
            config.defaultType = physical;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{CrabSystemSettings.instance.dataFolder}/Damage Calculation Config.asset");
            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[Project Setup] Created {path}. Assign it on your Manager_Brain variant's DamageManager.", config);
        }

        static IEnumerable<T> Find<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

        static string EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return path;
        }
    }
}
