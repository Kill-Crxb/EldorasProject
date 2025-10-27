using UnityEngine;
using System.Linq;
using CrabThirdPerson.Character;

public class RPGSystemsCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private StatsCoordinator stats;
    private PlayerItemsModule items;
    private SaveSystemModule saveSystem;
    private ModelModule model;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        stats = brain.GetModule<StatsCoordinator>();
        items = brain.GetModule<PlayerItemsModule>();
        saveSystem = brain.GetModule<SaveSystemModule>();
        model = brain.GetModule<ModelModule>();
    }

    public void UpdateModule()
    {
    }

    public StatsCoordinator GetStatsCoordinator() => stats;
    public PlayerItemsModule GetItemsModule() => items;
    public SaveSystemModule GetSaveSystem() => saveSystem;
    public ModelModule GetModelModule() => model;

    public int GetLevel() => stats?.GetLevel() ?? 1;
    public float GetCurrentHealth() => stats?.GetCurrentHealth() ?? 0f;
    public float GetHealthPercentage() => stats?.GetHealthPercentage() ?? 0f;

    public bool HasItem(string itemId) => items?.GetItemInstance(itemId) != null;
    public int GetInventoryItemCount() => items?.GetAllInventoryItems()?.Count() ?? 0;

    public void SaveGame(int slot) => saveSystem?.SavePlayerData(slot);
    public void LoadGame(int slot) => saveSystem?.LoadPlayerData(slot);
}