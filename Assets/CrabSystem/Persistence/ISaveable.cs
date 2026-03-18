/// <summary>
/// Contract for any system that wants to participate in the save/load lifecycle.
/// SaveManager discovers all ISaveable modules via the ControllerBrain and coordinates
/// serialisation in dependency order (stats → inventory → equipment).
///
/// Architecture: Each implementing system owns exactly one save file.
/// GetSaveId() returns the filename key (e.g. "inventory" → inventory.json).
/// </summary>
public interface ISaveable
{
    /// <summary>Maps to the filename: "inventory", "equipment", "stats".</summary>
    string GetSaveId();

    /// <summary>Returns a JSON string of this system's current state.</summary>
    string GetSaveData();

    /// <summary>Restores this system's state from a previously serialised JSON string.</summary>
    void LoadSaveData(string json);

    /// <summary>Schema version — increment when serialised fields change.</summary>
    int GetSaveVersion();
}