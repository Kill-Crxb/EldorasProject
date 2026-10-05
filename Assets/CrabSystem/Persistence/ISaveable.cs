using System;

// A system that saves itself (CrabSystem_Standard §3.3). Each one owns exactly one file: GetSaveId()
// is the filename key ("inventory" → inventory.json). SaveManager finds every ISaveable under the
// player's brain and never names them.
//
// A saveable whose state changes between autosaves (an item picked up, a slot equipped) raises
// Dirty, and SaveManager writes it on its next flush. The default does nothing, so a saveable that
// never raises it is written on the autosave timer and on quit, as before.
public interface ISaveable
{
    string GetSaveId();
    string GetSaveData();
    void LoadSaveData(string json);

    // Increment when the serialised fields change.
    int GetSaveVersion();

    // Lower loads first: stats before anything derived from them, inventory before equipment.
    int LoadOrder => 1000;

    event Action<ISaveable> Dirty { add { } remove { } }
}
