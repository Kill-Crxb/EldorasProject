using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Where this project keeps the things CrabSystem's editor tools read and write. Lives in ProjectSettings (not
// Assets), so the package carries no game paths and each project sets its own: Project Settings → CrabSystem.
// The defaults suit a fresh project.

namespace CrabSystem
{
    [FilePath("ProjectSettings/CrabSystemSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class CrabSystemSettings : ScriptableSingleton<CrabSystemSettings>
    {
        [Tooltip("Where the item tools create item definitions. Keep it under a Resources folder named " +
                 "CrabResources.Items so ItemManager finds them, or assign them on Manager_Brain.")]
        public string itemFolder = "Assets/Resources/" + CrabResources.Items;

        [Tooltip("Where the Talent wizard creates trees.")]
        public string talentFolder = "Assets/Talents";

        [Tooltip("Where unproven features land before they graduate; Health lists anything left in it.")]
        public string stagingFolder = "Assets/_Staging";

        [Tooltip("Downloaded packs. Validate Ids and Health report them apart, and asset scans skip them.")]
        public List<string> thirdPartyFolders = new() { "Assets/ThirdParty/" };

        // ScriptableSingleton hides its instance as not editable; the settings page needs to edit it.
        void OnEnable() => hideFlags &= ~HideFlags.NotEditable;

        public void Save() => Save(true);

        public static bool IsThirdParty(string assetPath) =>
            instance.thirdPartyFolders.Any(folder => !string.IsNullOrEmpty(folder) && assetPath.StartsWith(folder));
    }
}
