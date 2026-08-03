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
    public static event Action<string> OnTargetedSaveRequested;
    public static event Action OnApplicationExiting;

    #endregion

    #region Character Configuration

    public static event Action<CharacterConfigData> OnCharacterConfigDataReady;

    #endregion

    #region Hotbar

    public static event Action<string, int> OnHotbarSlotChanged;

    #endregion

    #region Equipment

    public static event Action<EquipmentSlotDefinition, ItemInstance> OnItemEquipped;

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

    public static void TargetedSaveRequested(string filename) =>
        OnTargetedSaveRequested?.Invoke(filename);

    public static void ApplicationExiting() =>
        OnApplicationExiting?.Invoke();

    public static void CharacterConfigDataReady(CharacterConfigData data) =>
        OnCharacterConfigDataReady?.Invoke(data);

    public static void HotbarSlotChanged(string barId, int index) =>
        OnHotbarSlotChanged?.Invoke(barId, index);

    public static void ItemEquipped(EquipmentSlotDefinition slot, ItemInstance item) =>
        OnItemEquipped?.Invoke(slot, item);

    #endregion

    #region Scene Cleanup

    public static void ClearAll()
    {
        OnCharacterSelected = null;
        OnLoadCompleted = null;
        OnGameSceneReady = null;
        OnSaveRequested = null;
        OnTargetedSaveRequested = null;
        OnApplicationExiting = null;
        OnCharacterConfigDataReady = null;
        OnHotbarSlotChanged = null;
        OnItemEquipped = null;
    }

    #endregion
}