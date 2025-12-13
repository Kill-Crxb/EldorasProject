using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Helper class for building BehaviorTreeAsset ScriptableObjects programmatically
/// Useful for creating trees in code before visual editor is ready
/// 
/// Usage:
/// var builder = new BehaviorTreeAssetBuilder("My Tree");
/// var root = builder.AddSelector("Root");
/// var combat = builder.AddSequence("Combat", root);
/// builder.AddCondition<BTHasTargetCondition>("HasTarget", combat);
/// var asset = builder.Build();
/// </summary>
public class BehaviorTreeAssetBuilder
{
    private BehaviorTreeAsset asset;
    private Dictionary<string, NodeData> nodeMap = new Dictionary<string, NodeData>();

    /// <summary>
    /// Constructor
    /// </summary>
    public BehaviorTreeAssetBuilder(string treeName, string description = "")
    {
        asset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
        asset.treeName = treeName;
        asset.description = description;
    }

    /// <summary>
    /// Add a Sequence node
    /// </summary>
    public string AddSequence(string name, string parentGuid = null)
    {
        return AddNode("BTSequence", name, parentGuid);
    }

    /// <summary>
    /// Add a Selector node
    /// </summary>
    public string AddSelector(string name, string parentGuid = null)
    {
        return AddNode("BTSelector", name, parentGuid);
    }

    /// <summary>
    /// Add a Parallel node
    /// </summary>
    public string AddParallel(string name, string parentGuid = null)
    {
        return AddNode("BTParallel", name, parentGuid);
    }

    /// <summary>
    /// Add a custom node by type
    /// </summary>
    public string AddNode<T>(string name, string parentGuid = null) where T : BTNode
    {
        return AddNode(typeof(T).Name, name, parentGuid);
    }

    /// <summary>
    /// Add a custom node by type name
    /// </summary>
    public string AddNode(string typeName, string name, string parentGuid = null, string fieldsJson = null)
    {
        var nodeData = new NodeData(typeName, name);
        nodeData.fieldsJson = fieldsJson;

        // Add to asset
        asset.AddNode(nodeData);
        nodeMap[nodeData.guid] = nodeData;

        // Connect to parent if specified
        if (!string.IsNullOrEmpty(parentGuid) && nodeMap.TryGetValue(parentGuid, out NodeData parent))
        {
            parent.AddChild(nodeData.guid);
        }

        return nodeData.guid;
    }

    /// <summary>
    /// Add a condition node
    /// </summary>
    public string AddCondition<T>(string name, string parentGuid = null) where T : BTConditionNode
    {
        return AddNode<T>(name, parentGuid);
    }

    /// <summary>
    /// Add an action node
    /// </summary>
    public string AddAction<T>(string name, string parentGuid = null) where T : BTActionNode
    {
        return AddNode<T>(name, parentGuid);
    }

    /// <summary>
    /// Add a decorator node
    /// </summary>
    public string AddDecorator<T>(string name, string childGuid) where T : BTDecoratorNode
    {
        string guid = AddNode<T>(name, null);

        // Add child to decorator
        if (nodeMap.TryGetValue(guid, out NodeData decorator))
        {
            decorator.AddChild(childGuid);
        }

        return guid;
    }

    /// <summary>
    /// Set the root node
    /// </summary>
    public BehaviorTreeAssetBuilder SetRoot(string nodeGuid)
    {
        asset.SetRootNode(nodeGuid);
        return this;
    }

    /// <summary>
    /// Set node position in editor (for future visual editor)
    /// </summary>
    public BehaviorTreeAssetBuilder SetNodePosition(string nodeGuid, Vector2 position)
    {
        if (nodeMap.TryGetValue(nodeGuid, out NodeData node))
        {
            node.editorPosition = position;
        }
        return this;
    }

    /// <summary>
    /// Set node comment
    /// </summary>
    public BehaviorTreeAssetBuilder SetNodeComment(string nodeGuid, string comment)
    {
        if (nodeMap.TryGetValue(nodeGuid, out NodeData node))
        {
            node.comment = comment;
        }
        return this;
    }

    /// <summary>
    /// Add a tag to the asset
    /// </summary>
    public BehaviorTreeAssetBuilder AddTag(string tag)
    {
        if (!asset.tags.Contains(tag))
        {
            asset.tags.Add(tag);
        }
        return this;
    }

    /// <summary>
    /// Build and return the final asset
    /// </summary>
    public BehaviorTreeAsset Build()
    {
        // Validate
        if (!asset.Validate(out string error))
        {
            Debug.LogError($"[BehaviorTreeAssetBuilder] Built tree is invalid: {error}");
        }

        return asset;
    }

    /// <summary>
    /// Build and save asset to Resources folder
    /// </summary>
    public BehaviorTreeAsset BuildAndSave(string path)
    {
#if UNITY_EDITOR
        var built = Build();
        
        // Ensure path starts with Assets/
        if (!path.StartsWith("Assets/"))
        {
            path = "Assets/" + path;
        }

        // Ensure path ends with .asset
        if (!path.EndsWith(".asset"))
        {
            path += ".asset";
        }

        // Create asset
        UnityEditor.AssetDatabase.CreateAsset(built, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        Debug.Log($"[BehaviorTreeAssetBuilder] Saved tree to {path}");
        return built;
#else
        Debug.LogError("[BehaviorTreeAssetBuilder] BuildAndSave only works in editor");
        return Build();
#endif
    }
}

#if UNITY_EDITOR
/// <summary>
/// Editor utility for creating example behavior tree assets
/// </summary>
public static class BehaviorTreeAssetExamples
{
    [UnityEditor.MenuItem("Assets/Create/AI/Example Behavior Trees/Simple Patrol Combat")]
    public static void CreateSimplePatrolCombatTree()
    {
        var builder = new BehaviorTreeAssetBuilder(
            "Simple Patrol Combat",
            "Basic patrol with combat response when target detected"
        );

        // Root selector
        var root = builder.AddSelector("Root");
        builder.SetRoot(root);

        // Combat branch
        var combatSeq = builder.AddSequence("Combat Branch", root);
        builder.AddCondition<BTHasTargetCondition>("Has Target?", combatSeq);
        builder.AddCondition<BTIsInCombatCondition>("In Combat?", combatSeq);
        
        var attackSeq = builder.AddSequence("Attack Sequence", combatSeq);
        builder.AddAction<BTMoveTowardTargetAction>("Move To Target", attackSeq);
        builder.AddAction<BTFaceTargetAction>("Face Target", attackSeq);
        builder.AddNode("BTExecuteAbilityAction", "Execute Attack", attackSeq, "{\"abilityId\":\"BasicAttack\"}");

        // Patrol branch (default)
        var patrolSeq = builder.AddSequence("Patrol Branch", root);
        builder.AddNode("BTWaitAction", "Wait at Point", patrolSeq, "{\"duration\":2.0}");

        // Add tags
        builder.AddTag("Example");
        builder.AddTag("Combat");
        builder.AddTag("Patrol");

        // Save
        builder.BuildAndSave("Assets/Resources/BehaviorTrees/BT_SimplePatrolCombat.asset");
    }

    [UnityEditor.MenuItem("Assets/Create/AI/Example Behavior Trees/Aggressive Melee")]
    public static void CreateAggressiveMeleeTree()
    {
        var builder = new BehaviorTreeAssetBuilder(
            "Aggressive Melee",
            "Always chases and attacks target when in range"
        );

        // Root selector
        var root = builder.AddSelector("Root");
        builder.SetRoot(root);

        // Combat branch
        var combatSeq = builder.AddSequence("Combat", root);
        builder.AddCondition<BTHasTargetCondition>("Has Target?", combatSeq);

        var attackSelector = builder.AddSelector("Attack or Chase", combatSeq);
        
        // Option 1: Attack if in range
        var attackSeq = builder.AddSequence("Attack", attackSelector);
        builder.AddNode("BTDistanceCheckCondition", "In Range?", attackSeq, "{\"comparison\":1,\"distance1\":3.0}");
        builder.AddAction<BTFaceTargetAction>("Face Target", attackSeq);
        builder.AddNode("BTExecuteAbilityAction", "Attack", attackSeq, "{\"abilityId\":\"BasicAttack\"}");

        // Option 2: Chase if not in range
        builder.AddAction<BTMoveTowardTargetAction>("Chase", attackSelector);

        // Idle branch
        builder.AddNode("BTWaitAction", "Idle", root, "{\"duration\":1.0}");

        builder.AddTag("Example");
        builder.AddTag("Aggressive");
        builder.AddTag("Melee");

        builder.BuildAndSave("Assets/Resources/BehaviorTrees/BT_AggressiveMelee.asset");
    }
}
#endif