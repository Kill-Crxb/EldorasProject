using System;

public static class GameEvents
{
    #region Character Flow

    public static event Action<string> OnCharacterSelected;
    public static event Action OnLoadCompleted;
    public static event Action OnGameSceneReady;

    #endregion

    #region Save Triggers

    public static event Action OnSaveRequested;

    #endregion

    #region Character Configuration

    public static event Action<CharacterConfigData> OnCharacterConfigDataReady;

    #endregion

    #region Camera

    public static event Action<string> OnCameraModeChangeRequested;

    #endregion

    #region Dialogue

    public static event Action<ControllerBrain, ControllerBrain> OnDialogueStarted;
    public static event Action<ControllerBrain> OnDialogueEnded;

    #endregion

    #region Invoke Helpers

    public static void CharacterSelected(string characterId) =>
        OnCharacterSelected?.Invoke(characterId);

    public static void LoadCompleted() =>
        OnLoadCompleted?.Invoke();

    public static void GameSceneReady() =>
        OnGameSceneReady?.Invoke();

    public static void SaveRequested() =>
        OnSaveRequested?.Invoke();

    public static void CharacterConfigDataReady(CharacterConfigData data) =>
        OnCharacterConfigDataReady?.Invoke(data);

    public static void CameraModeChangeRequested(string modeName) =>
        OnCameraModeChangeRequested?.Invoke(modeName);

    public static void DialogueStarted(ControllerBrain npc, ControllerBrain actor) =>
        OnDialogueStarted?.Invoke(npc, actor);

    public static void DialogueEnded(ControllerBrain npc) =>
        OnDialogueEnded?.Invoke(npc);

    #endregion

    #region Scene Cleanup

    public static void ClearAll()
    {
        OnCharacterSelected = null;
        OnLoadCompleted = null;
        OnGameSceneReady = null;
        OnSaveRequested = null;
        OnCharacterConfigDataReady = null;
        OnCameraModeChangeRequested = null;
        OnDialogueStarted = null;
        OnDialogueEnded = null;
    }

    #endregion
}