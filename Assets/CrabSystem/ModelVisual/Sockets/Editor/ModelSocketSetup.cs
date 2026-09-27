using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds socket transforms on a model rig and wires them into its ModelSocketProvider.
//
// Sockets are empty children of bones, not the bones themselves. A weapon almost never sits at a
// hand bone's exact pivot and rotation, and a child can be nudged without touching the skin.
//
// Idempotent: re-running reuses existing socket objects (keeping any offsets you tuned) and
// rewrites the provider's two lists from the tables below.
public static class ModelSocketSetup
{
    const string PorphiPrefabPath = "Assets/Database/Characters/MC/Porphi.prefab";

    private struct SlotSocket
    {
        public string SlotId;
        public string BoneName;
    }

    private struct NamedSocket
    {
        public string SocketId;
        public string BoneName;
    }

    // Props only. Armour is normally a skinned mesh bound to the whole rig, not a rigid child of
    // one bone — mapping bodyarmor to UpperChest would make the chestpiece swing with the chest
    // bone alone. Add those here only if a given piece really is a rigid prop.
    static readonly SlotSocket[] Slots =
    {
        new SlotSocket { SlotId = "mainwep", BoneName = "Hand_R" },
        new SlotSocket { SlotId = "offwep",  BoneName = "Hand_L" },
    };

    // Ids the code actually asks for. hand_r is the default for both ProjectileSpawn.socketId and
    // SpellcraftSystem.drawSocketId; mainwep_sheathed is CombatStanceModule.sheathSocketId. The
    // rest are anchors for VFX and the footstep hooks.
    static readonly NamedSocket[] Named =
    {
        new NamedSocket { SocketId = "hand_r",           BoneName = "Hand_R" },
        new NamedSocket { SocketId = "hand_l",           BoneName = "Hand_L" },
        new NamedSocket { SocketId = "mainwep_sheathed", BoneName = "UpperChest" },
        new NamedSocket { SocketId = "head",             BoneName = "Head" },
        new NamedSocket { SocketId = "chest",            BoneName = "UpperChest" },
        new NamedSocket { SocketId = "hips",             BoneName = "Hips" },
        new NamedSocket { SocketId = "foot_l",           BoneName = "Foot_L" },
        new NamedSocket { SocketId = "foot_r",           BoneName = "Foot_R" },
    };

    [MenuItem("Tools/Crab/Sockets/Set Up Porphi Sockets")]
    static void SetUpPorphi() => SetUp(PorphiPrefabPath);

    [MenuItem("Tools/Crab/Sockets/Log Porphi Sockets")]
    static void LogPorphi() => Log(PorphiPrefabPath);

    // Point a new menu item at another model prefab to reuse the same tables.
    static void SetUp(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError($"[ModelSocketSetup] Could not load prefab at {prefabPath}");
            return;
        }

        try
        {
            ModelSocketProvider provider = root.GetComponent<ModelSocketProvider>();
            if (provider == null) provider = root.AddComponent<ModelSocketProvider>();

            Dictionary<string, EquipmentSlotDefinition> slotAssets = LoadSlotDefinitions();

            List<ModelSocketProvider.SlotSocketMapping> slotList = new List<ModelSocketProvider.SlotSocketMapping>();
            List<ModelSocketProvider.NamedSocketMapping> namedList = new List<ModelSocketProvider.NamedSocketMapping>();

            string report = $"[ModelSocketSetup] {prefabPath}\n";

            foreach (SlotSocket entry in Slots)
            {
                if (!slotAssets.TryGetValue(entry.SlotId, out EquipmentSlotDefinition slot))
                {
                    report += $"  SKIP slot '{entry.SlotId}' — no EquipmentSlotDefinition asset has that slotId\n";
                    continue;
                }

                Transform socket = FindOrCreateSocket(root.transform, entry.BoneName, "Socket_" + entry.SlotId, ref report);
                if (socket == null) continue;

                slotList.Add(new ModelSocketProvider.SlotSocketMapping { slot = slot, socket = socket });
                report += $"  slot  {entry.SlotId,-18} -> {entry.BoneName}/{socket.name}\n";
            }

            foreach (NamedSocket entry in Named)
            {
                Transform socket = FindOrCreateSocket(root.transform, entry.BoneName, "Socket_" + entry.SocketId, ref report);
                if (socket == null) continue;

                namedList.Add(new ModelSocketProvider.NamedSocketMapping { socketId = entry.SocketId, socket = socket });
                report += $"  named {entry.SocketId,-18} -> {entry.BoneName}/{socket.name}\n";
            }

            provider.slotSockets = slotList;
            provider.namedSockets = namedList;

            EditorUtility.SetDirty(provider);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            report += "\n  Every socket sits at its bone's origin with no rotation. Nudge them in the\n" +
                      "  scene view until the props sit right — re-running keeps those offsets.";

            Debug.Log(report);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Log(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError($"[ModelSocketSetup] Could not load prefab at {prefabPath}");
            return;
        }

        try
        {
            ModelSocketProvider provider = root.GetComponent<ModelSocketProvider>();

            if (provider == null)
            {
                Debug.LogError($"[ModelSocketSetup] {prefabPath} has no ModelSocketProvider.");
                return;
            }

            string report = $"[ModelSocketSetup] {prefabPath}\n  slot sockets: {provider.slotSockets.Count}\n";

            foreach (ModelSocketProvider.SlotSocketMapping mapping in provider.slotSockets)
            {
                string slotId = mapping.slot != null ? mapping.slot.slotId : "(NULL SLOT ASSET)";
                string target = mapping.socket != null ? Path(root.transform, mapping.socket) : "(NULL TRANSFORM)";
                report += $"    {slotId,-18} -> {target}\n";
            }

            report += $"  named sockets: {provider.namedSockets.Count}\n";

            foreach (ModelSocketProvider.NamedSocketMapping mapping in provider.namedSockets)
            {
                string target = mapping.socket != null ? Path(root.transform, mapping.socket) : "(NULL TRANSFORM)";
                report += $"    {mapping.socketId,-18} -> {target}\n";
            }

            Debug.Log(report);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindOrCreateSocket(Transform root, string boneName, string socketName, ref string report)
    {
        Transform bone = FindDeep(root, boneName);

        if (bone == null)
        {
            report += $"  SKIP '{socketName}' — bone '{boneName}' not found on this rig\n";
            return null;
        }

        Transform existing = bone.Find(socketName);
        if (existing != null) return existing;

        GameObject socket = new GameObject(socketName);
        socket.transform.SetParent(bone, false);
        socket.transform.localPosition = Vector3.zero;
        socket.transform.localRotation = Quaternion.identity;
        socket.transform.localScale = Vector3.one;

        return socket.transform;
    }

    static Dictionary<string, EquipmentSlotDefinition> LoadSlotDefinitions()
    {
        Dictionary<string, EquipmentSlotDefinition> byId = new Dictionary<string, EquipmentSlotDefinition>();

        foreach (string guid in AssetDatabase.FindAssets("t:EquipmentSlotDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EquipmentSlotDefinition slot = AssetDatabase.LoadAssetAtPath<EquipmentSlotDefinition>(path);

            if (slot == null || string.IsNullOrEmpty(slot.slotId)) continue;

            byId[slot.slotId] = slot;
        }

        return byId;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform hit = FindDeep(parent.GetChild(i), name);
            if (hit != null) return hit;
        }

        return null;
    }

    static string Path(Transform root, Transform target)
    {
        string path = target.name;

        for (Transform t = target.parent; t != null && t != root; t = t.parent)
            path = t.name + "/" + path;

        return path;
    }
}
