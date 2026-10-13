using UnityEngine;

/// <summary>
/// NPCSpawnConfig - defines which prefab + which archetype = a spawnable NPC type.
/// This is the "template" for an NPC. Separates structure from behavior.
/// 
/// Example:
///   - Prefab: NatureSpirit_Base (sphere + particles)
///   - Archetype: Archetype_NatureSpirit (behavior config)
///   - Result: A nature spirit NPC that can be spawned anywhere
/// 
/// Alternative: Archer_Female (model)
///   - Prefab: Archer_Female (humanoid with sockets)
///   - Archetype: Archetype_ArcherSoldier (stats, abilities, combat)
///   - Result: An archer soldier NPC
/// 
/// Create in editor: Right-click > Create > RPG/NPC/Spawn Config
/// </summary>
[CreateAssetMenu(fileName = "NPCSpawnConfig", menuName = "RPG/NPC/Spawn Config")]
public class NPCSpawnConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique ID for this NPC template (e.g., 'npc_nature_spirit_01')")]
    public string spawnConfigId;

    [Tooltip("Display name for debugging (e.g., 'Nature Spirit - Serene')")]
    [TextArea(1, 2)]
    public string displayName;

    [Header("Structure")]
    [Tooltip("The prefab that defines this NPC's visual structure and components")]
    public GameObject prefab;

    [Header("Behavior")]
    [Tooltip("The archetype that defines this NPC's stats, abilities, and AI")]
    public NPCArchetype archetype;

    [Header("Spawn Variants (Optional)")]
    [Tooltip("If true, spawn a random variant from this pool (for respawns, etc.)")]
    public bool useRandomVariant = false;

    [Tooltip("Other spawn configs that are variants of the same archetype (e.g., different colors of the same enemy)")]
    public NPCSpawnConfig[] variants;

    [Header("Level Override")]
    [Tooltip("Override archetype's base level (-1 = use archetype level)")]
    public int spawnLevel = -1;

    // ── Validation ────────────────────────────────────────────────────────

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(spawnConfigId) &&
               prefab != null &&
               archetype != null;
    }

    // ── Variant Selection ─────────────────────────────────────────────────

    public NPCSpawnConfig GetRandomVariant()
    {
        if (!useRandomVariant || variants == null || variants.Length == 0)
            return this;

        return variants[Random.Range(0, variants.Length)];
    }

    // ── Debug ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(spawnConfigId) && !string.IsNullOrEmpty(displayName))
        {
            spawnConfigId = "npc_" + displayName.ToLower().Replace(" ", "_");
        }
    }
#endif
}