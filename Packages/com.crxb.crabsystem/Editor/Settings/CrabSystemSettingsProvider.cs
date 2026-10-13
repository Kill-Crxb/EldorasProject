using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CrabSystem
{
    static class CrabSystemSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider("Project/CrabSystem", SettingsScope.Project)
        {
            label = "CrabSystem",
            activateHandler = Build,
            keywords = new[] { "CrabSystem", "items", "talents", "staging", "third party" }
        };

        static void Build(string search, VisualElement root)
        {
            var settings = CrabSystemSettings.instance;
            var serialized = new SerializedObject(settings);

            root.Add(new Label("CrabSystem") { style = { fontSize = 19, unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, marginLeft = 10, marginTop = 2, marginBottom = 8 } });
            AddField(root, serialized, nameof(CrabSystemSettings.dataFolder));
            AddField(root, serialized, nameof(CrabSystemSettings.itemFolder));
            AddField(root, serialized, nameof(CrabSystemSettings.talentFolder));
            AddField(root, serialized, nameof(CrabSystemSettings.stagingFolder));
            AddField(root, serialized, nameof(CrabSystemSettings.thirdPartyFolders));

            root.Bind(serialized);
            root.TrackSerializedObjectValue(serialized, _ => settings.Save());
        }

        static void AddField(VisualElement root, SerializedObject serialized, string name)
        {
            var field = new PropertyField(serialized.FindProperty(name)) { style = { marginLeft = 10, marginRight = 10 } };
            root.Add(field);
        }
    }
}
