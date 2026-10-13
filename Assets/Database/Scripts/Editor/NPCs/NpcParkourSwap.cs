using UnityEditor;
using UnityEngine;
using CrabSystem;

// Tools → AI → Move Dummies And Spirit To Parkour (roadmap V4). TargetDummy and Nature_Spirit are hand-built NPCs
// that ran ARPGLocomotionHandler and StubAIControlSource, both now deleted. This gives them Base_PC's movement
// stack (CharacterMotor on the root, ParkourLocomotionHandler beside MovementSystem, copied with Base_PC's
// tuning) and an AIControlSource, strips the missing scripts left behind, and removes the switched-off input and
// camera children (player-only modules an NPC declines).
//
// Edited in place rather than rebuilt as Base_NPC variants: the Light / Mid / Heavy dummies and the Zoo instances
// carry overrides keyed to these prefabs' objects, and a rebuild would drop them (Known Issues A3 keeps the
// variant move open). Rerunnable.
public static class NpcParkourSwap
{
    static readonly string[] Targets =
    {
        "Assets/Database/Characters/TargetDummy/TargetDummy.prefab",
        "Assets/Database/Characters/Nature Spirit/Nature_Spirit.prefab",
    };

    static readonly string[] DeadChildren = { "Input_System", "Component_Camera" };

    [MenuItem("Tools/AI/Move Dummies And Spirit To Parkour")]
    static void Run()
    {
        GameObject basePc = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabKit.BasePcPath);
        var motorSource = basePc.GetComponent<CharacterMotor>();
        var handlerSource = basePc.GetComponentInChildren<ParkourLocomotionHandler>(true);
        if (motorSource == null || handlerSource == null)
        {
            Debug.LogError("[NpcParkourSwap] Base_PC has no CharacterMotor or ParkourLocomotionHandler to copy.");
            return;
        }

        foreach (string path in Targets)
            Swap(path, motorSource, handlerSource);

        AssetDatabase.SaveAssets();
    }

    static void Swap(string path, CharacterMotor motorSource, ParkourLocomotionHandler handlerSource)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int stripped = StripMissingScripts(root);
        RemoveDeadChildren(root);

        CharacterMotor motor = AddMotor(root, motorSource);
        AddHandler(root, handlerSource, motor);
        NpcPrefabKit.EnsureAiSystem(root.GetComponentInChildren<ControllerBrain>(true));

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[NpcParkourSwap] {System.IO.Path.GetFileNameWithoutExtension(path)}: parkour movement and AIControlSource; {stripped} missing script(s) removed.");
    }

    static int StripMissingScripts(GameObject root)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        return count;
    }

    static void RemoveDeadChildren(GameObject root)
    {
        var brain = root.GetComponentInChildren<ControllerBrain>(true);
        foreach (string name in DeadChildren)
        {
            Transform child = brain.transform.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    // The motor sweeps its own capsule, sized to the body the CharacterController already describes.
    static CharacterMotor AddMotor(GameObject root, CharacterMotor source)
    {
        var capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = root.AddComponent<CapsuleCollider>();
            var controller = root.GetComponent<CharacterController>();
            if (controller != null)
            {
                capsule.radius = controller.radius;
                capsule.height = controller.height;
                capsule.center = controller.center;
            }
        }

        // The motor owns the transform; the controller stays for its body shape, switched off as on Base_PC.
        var oldController = root.GetComponent<CharacterController>();
        if (oldController != null) oldController.enabled = false;

        var motor = root.GetComponent<CharacterMotor>();
        if (motor != null) return motor;

        motor = root.AddComponent<CharacterMotor>();
        EditorUtility.CopySerialized(source, motor);
        NpcPrefabKit.Write(motor, "capsule", p => p.objectReferenceValue = capsule);
        return motor;
    }

    static void AddHandler(GameObject root, ParkourLocomotionHandler source, CharacterMotor motor)
    {
        var movement = root.GetComponentInChildren<MovementSystem>(true);
        if (movement == null) return;

        var handler = movement.GetComponent<ParkourLocomotionHandler>();
        if (handler == null)
        {
            handler = movement.gameObject.AddComponent<ParkourLocomotionHandler>();
            EditorUtility.CopySerialized(source, handler);
            NpcPrefabKit.Write(handler, "motor", p => p.objectReferenceValue = motor);
        }
        NpcPrefabKit.Write(movement, "locomotionHandler", p => p.objectReferenceValue = handler);
    }
}
