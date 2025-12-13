using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Factory that instantiates runtime BehaviorTree instances from BehaviorTreeAsset ScriptableObjects
/// Uses reflection to create nodes from type names and deserialize field data
/// 
/// Design: Converts serialized assets into live, executable behavior trees
/// </summary>
public static class BehaviorTreeFactory
{
    private static bool debugMode = false;

    /// <summary>
    /// Create a runtime BehaviorTree from a BehaviorTreeAsset
    /// </summary>
    public static BehaviorTree CreateFromAsset(BehaviorTreeAsset asset)
    {
        if (asset == null)
        {
            Debug.LogError("[BehaviorTreeFactory] Cannot create tree from null asset");
            return null;
        }

        // Validate asset
        if (!asset.Validate(out string error))
        {
            Debug.LogError($"[BehaviorTreeFactory] Asset '{asset.treeName}' is invalid: {error}");
            return null;
        }

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeFactory] Creating tree '{asset.treeName}' with {asset.Nodes.Count} nodes");
        }

        // Create tree container
        var tree = new BehaviorTree(asset.treeName);

        // Build node lookup for connecting children
        var nodeLookup = new Dictionary<string, BTNode>();

        // Phase 1: Instantiate all nodes
        foreach (var nodeData in asset.Nodes)
        {
            BTNode node = InstantiateNode(nodeData);
            if (node != null)
            {
                nodeLookup[nodeData.guid] = node;
            }
            else
            {
                Debug.LogError($"[BehaviorTreeFactory] Failed to instantiate node: {nodeData.typeName}");
            }
        }

        // Phase 2: Connect children
        foreach (var nodeData in asset.Nodes)
        {
            if (!nodeLookup.TryGetValue(nodeData.guid, out BTNode parent))
                continue;

            ConnectChildren(parent, nodeData, nodeLookup);
        }

        // Phase 3: Set root node
        if (nodeLookup.TryGetValue(asset.RootNodeGuid, out BTNode rootNode))
        {
            tree.SetRootNode(rootNode);
        }
        else
        {
            Debug.LogError($"[BehaviorTreeFactory] Root node not found: {asset.RootNodeGuid}");
            return null;
        }

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeFactory] Successfully created tree '{tree.TreeName}'");
        }

        return tree;
    }

    /// <summary>
    /// Instantiate a single node from NodeData
    /// </summary>
    private static BTNode InstantiateNode(NodeData nodeData)
    {
        // Get the type
        Type nodeType = GetNodeType(nodeData.typeName);
        if (nodeType == null)
        {
            Debug.LogError($"[BehaviorTreeFactory] Unknown node type: {nodeData.typeName}");
            return null;
        }

        // Create instance with parameterless constructor
        BTNode node;
        try
        {
            node = Activator.CreateInstance(nodeType) as BTNode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BehaviorTreeFactory] Failed to create {nodeData.typeName}: {e.Message}");
            return null;
        }

        if (node == null)
        {
            Debug.LogError($"[BehaviorTreeFactory] Created node is null: {nodeData.typeName}");
            return null;
        }

        // Set node name
        node.NodeName = nodeData.nodeName;
        node.NodeId = nodeData.guid;

        // Deserialize field data if present
        if (!string.IsNullOrEmpty(nodeData.fieldsJson))
        {
            DeserializeFields(node, nodeData.fieldsJson);
        }

        return node;
    }

    /// <summary>
    /// Connect children to a parent node
    /// </summary>
    private static void ConnectChildren(BTNode parent, NodeData nodeData, Dictionary<string, BTNode> nodeLookup)
    {
        if (nodeData.childGuids == null || nodeData.childGuids.Count == 0)
            return;

        // Check if parent can have children
        if (parent is BTCompositeNode composite)
        {
            // Composite node - add all children
            foreach (var childGuid in nodeData.childGuids)
            {
                if (nodeLookup.TryGetValue(childGuid, out BTNode child))
                {
                    composite.AddChild(child);
                }
                else
                {
                    Debug.LogWarning($"[BehaviorTreeFactory] Child node not found: {childGuid}");
                }
            }
        }
        else if (parent is BTDecoratorNode decorator)
        {
            // Decorator node - single child only
            if (nodeData.childGuids.Count > 0)
            {
                if (nodeLookup.TryGetValue(nodeData.childGuids[0], out BTNode child))
                {
                    decorator.SetChild(child);
                }

                if (nodeData.childGuids.Count > 1)
                {
                    Debug.LogWarning($"[BehaviorTreeFactory] Decorator {parent.NodeName} has multiple children, only using first");
                }
            }
        }
        else
        {
            // Leaf node with children? That's an error
            if (nodeData.childGuids.Count > 0)
            {
                Debug.LogWarning($"[BehaviorTreeFactory] Leaf node {parent.NodeName} has children (will be ignored)");
            }
        }
    }

    /// <summary>
    /// Get node type from type name string
    /// </summary>
    private static Type GetNodeType(string typeName)
    {
        // Try direct type lookup
        Type type = Type.GetType(typeName);
        if (type != null) return type;

        // Try with Assembly-CSharp prefix
        type = Type.GetType($"{typeName}, Assembly-CSharp");
        if (type != null) return type;

        // Try searching all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null) return type;
        }

        return null;
    }

    /// <summary>
    /// Deserialize JSON field data into node fields
    /// Simple implementation - can be enhanced with JsonUtility or custom serialization
    /// </summary>
    private static void DeserializeFields(BTNode node, string fieldsJson)
    {
        try
        {
            // For now, use JsonUtility for simple data types
            // More complex deserialization can be added as needed
            JsonUtility.FromJsonOverwrite(fieldsJson, node);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BehaviorTreeFactory] Failed to deserialize fields for {node.NodeName}: {e.Message}");
        }
    }

    /// <summary>
    /// Enable/disable debug logging
    /// </summary>
    public static void SetDebugMode(bool enabled)
    {
        debugMode = enabled;
    }
}