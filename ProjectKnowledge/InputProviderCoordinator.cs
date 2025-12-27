using UnityEngine;

public class InputProviderCoordinator : ProviderCoordinator
{
    [Header("Required Provider Slot (Player Only)")]
    [Tooltip("Must implement IInputProvider (InputModule for player, leave empty for NPCs)")]
    [SerializeField] private MonoBehaviour inputProvider;

    private IInputProvider input;

    protected override bool ValidateSlots()
    {
        // Input is optional - NPCs don't need it
        if (inputProvider != null)
        {
            return ValidateProvider<IInputProvider>(inputProvider, "Input Provider");
        }
        return true;
    }

    protected override void CacheProviders()
    {
        input = inputProvider as IInputProvider;
    }

    protected override void OnInitialized()
    {
        if (inputProvider is IBrainModule module)
            module.Initialize(brain);
    }

    public IInputProvider Input => input;
}