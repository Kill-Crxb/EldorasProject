using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerOriginDatabase
///
/// ScriptableObject that defines all available player origins.
/// An origin is a flavour starting point — it pre-fills the stat allocation
/// screen but places no restrictions on how the player develops from there.
/// The game is classless; origins are just named stat templates.
///
/// Setup:
///   Assets → Create → NinjaGame → Player Origin Database
///   Add one PlayerOrigin entry per starting archetype.
///
/// Inspector fields:
///   bonusPointsAtCreation  — extra points the player can freely distribute
///                            on top of the origin's base spread
///   origins                — list of origin definitions
///
/// Usage:
///   CharacterCreationPanel reads this SO to populate the stat pre-fill.
///   SaveManager writes the chosen originId into CharacterMetadata.
///   The originId is display-only after creation — it carries no gameplay lock.
/// </summary>
[CreateAssetMenu(fileName = "PlayerOriginDatabase", menuName = "NinjaGame/Player Origin Database")]
public class PlayerOriginDatabase : ScriptableObject
{
    // ── Configuration ─────────────────────────────────────────────────────

    [Tooltip("Bonus points the player can freely allocate on top of their chosen origin's base spread.")]
    [Min(0)]
    public int bonusPointsAtCreation = 5;

    // ── Origins ───────────────────────────────────────────────────────────

    [Tooltip("All available player origins. Order determines display order in the creation panel.")]
    public List<PlayerOrigin> origins = new List<PlayerOrigin>();

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Find an origin by its id. Returns null if not found.
    /// </summary>
    public PlayerOrigin GetOrigin(string originId)
    {
        if (string.IsNullOrEmpty(originId)) return null;

        foreach (var origin in origins)
            if (origin.originId == originId)
                return origin;

        return null;
    }

    /// <summary>
    /// Returns the first origin in the list, or null if the list is empty.
    /// Used as the default selection in the creation panel.
    /// </summary>
    public PlayerOrigin GetDefault()
    {
        if (origins == null || origins.Count == 0) return null;
        return origins[0];
    }

    // ── Validation ────────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (origins == null) return;

        // Warn on duplicate IDs
        var seen = new HashSet<string>();
        foreach (var origin in origins)
        {
            if (string.IsNullOrEmpty(origin.originId)) continue;
            if (!seen.Add(origin.originId))
                Debug.LogWarning($"[PlayerOriginDatabase] Duplicate originId: '{origin.originId}'");
        }
    }
}

// ── PlayerOrigin ──────────────────────────────────────────────────────────────

/// <summary>
/// A single player origin entry.
///
/// Expansion points:
///   - Add starterLoadoutId (string) to reference a starter item set SO
///   - Add abilityUnlocks (List<AbilityDefinition>) for origin-flavoured starting moves
///   - Add unlockCondition to hide origins until the player earns them
/// </summary>
[System.Serializable]
public class PlayerOrigin
{
    [Header("Identity")]
    [Tooltip("Unique string key stored in CharacterMetadata. Never change after release.")]
    public string originId;

    [Tooltip("Player-facing name shown in the creation panel.")]
    public string displayName;

    [Tooltip("Short flavour description shown below the origin name.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icon shown in the creation panel alongside the origin name.")]
    public Sprite icon;

    [Header("Starting Stats")]
    [Tooltip("Base stat spread this origin pre-fills into the allocation screen. " +
             "Player can redistribute bonus points freely from here.")]
    public StatAllocation baseStats = new StatAllocation();
}
