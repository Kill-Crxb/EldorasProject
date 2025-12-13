using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject asset that stores a serialized behavior tree
/// Can be created in Unity editor and assigned to NPCArchetypes
/// BehaviorTreeFactory instantiates runtime trees from these assets
/// 
/// Design: Version control friendly, reusable across multiple NPCs
/// </summary>
[CreateAssetMenu(fileName = "BT_NewTree", menuName = "AI/Behavior Tree Asset", order = 1)]
public class BehaviorTreeAsset : ScriptableObject
{
    [Header("Tree Identity")]
    [Tooltip("Unique identifier for this tree")]
    public string treeId;

    [Tooltip("Display name for this tree")]
    public string treeName = "New Behavior Tree";

    [Tooltip("Description of what this tree does")]
    [TextArea(3, 6)]
    public string description;

    [Header("Tree Structure")]
    [Tooltip("Serialized node data (JSON format)")]
    [SerializeField] private List<NodeData> nodes = new List<NodeData>();

    [Tooltip("GUID of the root node")]
    [SerializeField] private string rootNodeGuid;

    [Header("Metadata")]
    [Tooltip("Version number for migration support")]
    public int version = 1;

    [Tooltip("Tags for categorization")]
    public List<string> tags = new List<string>();

    // Properties
    public List<NodeData> Nodes => nodes;
    public string RootNodeGuid => rootNodeGuid;
    public bool HasNodes => nodes != null && nodes.Count > 0;
    public bool HasRootNode => !string.IsNullOrEmpty(rootNodeGuid);

    /// <summary>
    /// Initialize the asset with basic data
    /// </summary>
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(treeId))
        {
            treeId = System.Guid.NewGuid().ToString();
        }

        if (string.IsNullOrEmpty(treeName))
        {
            treeName = name; // Use asset name
        }
    }

    /// <summary>
    /// Add a node to the tree
    /// </summary>
    public void AddNode(NodeData node)
    {
        if (nodes == null)
            nodes = new List<NodeData>();

        nodes.Add(node);
    }

    /// <summary>
    /// Remove a node from the tree
    /// </summary>
    public void RemoveNode(string guid)
    {
        if (nodes == null) return;

        nodes.RemoveAll(n => n.guid == guid);

        // If removed node was root, clear root reference
        if (rootNodeGuid == guid)
        {
            rootNodeGuid = null;
        }
    }

    /// <summary>
    /// Set the root node
    /// </summary>
    public void SetRootNode(string guid)
    {
        rootNodeGuid = guid;
    }

    /// <summary>
    /// Get a node by GUID
    /// </summary>
    public NodeData GetNode(string guid)
    {
        if (nodes == null) return null;
        return nodes.Find(n => n.guid == guid);
    }

    /// <summary>
    /// Clear all nodes
    /// </summary>
    public void Clear()
    {
        nodes?.Clear();
        rootNodeGuid = null;
    }

    /// <summary>
    /// Validate the tree structure
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        errorMessage = null;

        // Check has nodes
        if (!HasNodes)
        {
            errorMessage = "Tree has no nodes";
            return false;
        }

        // Check has root
        if (!HasRootNode)
        {
            errorMessage = "Tree has no root node";
            return false;
        }

        // Check root exists in nodes
        if (GetNode(rootNodeGuid) == null)
        {
            errorMessage = $"Root node {rootNodeGuid} not found in node list";
            return false;
        }

        // Check all child references exist
        foreach (var node in nodes)
        {
            if (node.childGuids != null)
            {
                foreach (var childGuid in node.childGuids)
                {
                    if (GetNode(childGuid) == null)
                    {
                        errorMessage = $"Node {node.guid} references non-existent child {childGuid}";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Create a copy of this asset
    /// </summary>
    public BehaviorTreeAsset Clone()
    {
        var clone = CreateInstance<BehaviorTreeAsset>();
        clone.treeId = System.Guid.NewGuid().ToString(); // New ID for clone
        clone.treeName = treeName + " (Clone)";
        clone.description = description;
        clone.version = version;
        clone.tags = new List<string>(tags);
        clone.nodes = new List<NodeData>(nodes);
        clone.rootNodeGuid = rootNodeGuid;
        return clone;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug info for editor
    /// </summary>
    [ContextMenu("Print Tree Info")]
    private void PrintInfo()
    {
        Debug.Log($"=== {treeName} ===\n" +
                 $"ID: {treeId}\n" +
                 $"Nodes: {nodes?.Count ?? 0}\n" +
                 $"Root: {rootNodeGuid}\n" +
                 $"Version: {version}\n" +
                 $"Valid: {Validate(out string error)}\n" +
                 $"Error: {error ?? "None"}");
    }

    [ContextMenu("Validate Tree")]
    private void ValidateTree()
    {
        if (Validate(out string error))
        {
            Debug.Log($"✓ Tree '{treeName}' is valid");
        }
        else
        {
            Debug.LogError($"✗ Tree '{treeName}' is invalid: {error}");
        }
    }
#endif
}

/// <summary>
/// Serializable node data structure
/// Stores all information needed to reconstruct a node at runtime
/// </summary>
[System.Serializable]
public class NodeData
{
    [Tooltip("Unique identifier for this node")]
    public string guid;

    [Tooltip("Full type name (e.g., 'BTSequence', 'BTLogAction')")]
    public string typeName;

    [Tooltip("Display name for this node")]
    public string nodeName;

    [Tooltip("Serialized field data (JSON format)")]
    [TextArea(2, 4)]
    public string fieldsJson;

    [Tooltip("GUIDs of child nodes")]
    public List<string> childGuids;

    [Header("Editor Data (Not used at runtime)")]
    [Tooltip("Position in editor graph")]
    public Vector2 editorPosition;

    [Tooltip("Comment/note for this node")]
    public string comment;

    /// <summary>
    /// Constructor
    /// </summary>
    public NodeData()
    {
        guid = System.Guid.NewGuid().ToString();
        childGuids = new List<string>();
    }

    /// <summary>
    /// Constructor with type
    /// </summary>
    public NodeData(string typeName, string nodeName = null) : this()
    {
        this.typeName = typeName;
        this.nodeName = nodeName ?? typeName;
    }

    /// <summary>
    /// Add a child reference
    /// </summary>
    public void AddChild(string childGuid)
    {
        if (childGuids == null)
            childGuids = new List<string>();

        if (!childGuids.Contains(childGuid))
        {
            childGuids.Add(childGuid);
        }
    }

    /// <summary>
    /// Remove a child reference
    /// </summary>
    public void RemoveChild(string childGuid)
    {
        childGuids?.Remove(childGuid);
    }
}