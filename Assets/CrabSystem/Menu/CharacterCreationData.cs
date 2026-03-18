/// <summary>
/// CharacterCreationData
///
/// Plain data transfer object — carries all choices made in CharacterCreationPanel
/// to SaveManager.CreateCharacter() in a single call.
///
/// All fields are value types or strings — no Unity object references.
/// Safe to pass across any system boundary.
///
/// Expansion:
///   - Add starterLoadoutId (string) when starter items are implemented
///   - Add shapeKeyData (ShapeKeySnapshot) when body customisation is added
///   - Add colorData when tint/dye system is added
/// </summary>
[System.Serializable]
public struct CharacterCreationData
{
    // ── Identity ──────────────────────────────────────────────────────────

    /// <summary>Player-entered character name. Already validated and trimmed.</summary>
    public string characterName;

    /// <summary>Account this character belongs to (from AccountManager.ActiveAccountName).</summary>
    public string accountName;

    // ── Appearance ────────────────────────────────────────────────────────

    /// <summary>
    /// ID of the chosen model from ModelDatabase.
    /// Single mesh for now — shape key snapshot data added here later.
    /// </summary>
    public string modelId;

    // ── Origin ────────────────────────────────────────────────────────────

    /// <summary>
    /// ID of the chosen origin from PlayerOriginDatabase.
    /// Stored for display purposes only — no gameplay lock after creation.
    /// </summary>
    public string originId;

    // ── Stats ─────────────────────────────────────────────────────────────

    /// <summary>Final stat values after origin pre-fill + bonus point allocation.</summary>
    public int mind;
    public int body;
    public int spirit;
    public int resilience;
    public int endurance;
    public int insight;

    /// <summary>Any unspent bonus points carried forward as unallocated RPG points.</summary>
    public int unspentPoints;
}
