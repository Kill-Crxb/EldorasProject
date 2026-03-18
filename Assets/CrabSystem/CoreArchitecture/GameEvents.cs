using System;

/// <summary>
/// Static event bus that decouples every save trigger from every listener.
/// Any system — a chest closing, a pause menu, a scene transition — fires an
/// event without knowing what handles it. SaveManager and UI subscribe independently.
///
/// Architecture: No MonoBehaviour, no instance, no coupling.
/// Fire: GameEvents.OnSaveRequested?.Invoke();
/// Subscribe: GameEvents.OnLoadCompleted += HandleLoadCompleted;
///
/// All events are cleared between scenes via ClearAll() called by ManagerBrain
/// on scene load to prevent stale subscriptions from destroyed objects.
/// </summary>
public static class GameEvents
{
    // ── Character Flow ────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player selects a character on the character select screen.
    /// Payload: characterId (e.g. "Crxb/Hanzo_1740220800").
    /// Subscriber: SaveManager — stores characterId, triggers LoadCharacter().
    /// </summary>
    public static event Action<string> OnCharacterSelected;

    /// <summary>
    /// Fired after all ISaveable modules have finished loading their data.
    /// Subscriber: UI systems — safe to open windows and display live data.
    /// </summary>
    public static event Action OnLoadCompleted;

    /// <summary>
    /// Fired once the game scene is fully loaded and ready for system use.
    /// Subscriber: Any system that defers initialisation until scene is ready.
    /// </summary>
    public static event Action OnGameSceneReady;

    // ── Save Triggers ─────────────────────────────────────────────────────

    /// <summary>
    /// Request a full save of all ISaveable modules for the active character.
    /// Subscriber: SaveManager — triggers SaveAll().
    /// </summary>
    public static event Action OnSaveRequested;

    /// <summary>
    /// Request a targeted save of a specific file only (e.g. "stats").
    /// Payload: filename key matching ISaveable.GetSaveId().
    /// Subscriber: SaveManager — calls SaveFile(filename, json).
    /// </summary>
    public static event Action<string> OnTargetedSaveRequested;

    /// <summary>
    /// Fired by Application.quitting — SaveManager performs final SaveAll()
    /// before the process exits.
    /// </summary>
    public static event Action OnApplicationExiting;

    // ── Invoke Helpers ────────────────────────────────────────────────────

    public static void CharacterSelected(string characterId) =>
        OnCharacterSelected?.Invoke(characterId);

    public static void LoadCompleted() =>
        OnLoadCompleted?.Invoke();

    public static void GameSceneReady() =>
        OnGameSceneReady?.Invoke();

    public static void SaveRequested() =>
        OnSaveRequested?.Invoke();

    public static void TargetedSaveRequested(string filename) =>
        OnTargetedSaveRequested?.Invoke(filename);

    public static void ApplicationExiting() =>
        OnApplicationExiting?.Invoke();

    // ── Scene Cleanup ─────────────────────────────────────────────────────

    /// <summary>
    /// Clear all subscriptions. Call from ManagerBrain on scene load to
    /// prevent stale references from destroyed MonoBehaviours.
    /// </summary>
    public static void ClearAll()
    {
        OnCharacterSelected = null;
        OnLoadCompleted = null;
        OnGameSceneReady = null;
        OnSaveRequested = null;
        OnTargetedSaveRequested = null;
        OnApplicationExiting = null;
    }
}