using UnityEngine;

public abstract class ProviderCoordinator : MonoBehaviour
{
    protected ControllerBrain brain;

    public bool IsEnabled { get; private set; } = true;

    public virtual void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (!ValidateSlots())
        {
            Debug.LogError($"[{GetType().Name}] Slot validation failed on {gameObject.name}", this);
            IsEnabled = false;
            enabled = false;
            return;
        }

        CacheProviders();

    }

    protected abstract bool ValidateSlots();

    protected abstract void CacheProviders();

    protected virtual void OnInitialized() { }

    public virtual void UpdateCoordinator() { }

    public virtual void PhysicsUpdateCoordinator() { }

    protected bool ValidateProvider<T>(MonoBehaviour provider, string slotName) where T : class
    {
        if (provider == null)
        {
            Debug.LogWarning($"[{GetType().Name}] {slotName} slot is empty on {gameObject.name}. Assign a provider or use a Null provider.", this);
            return false;
        }

        if (provider is T)
        {
            return true;
        }

        Debug.LogError($"[{GetType().Name}] {slotName} ({provider.GetType().Name}) does not implement {typeof(T).Name} on {gameObject.name}", this);
        return false;
    }

    protected bool ValidateOptionalProvider<T>(MonoBehaviour provider, string slotName) where T : class
    {
        if (provider == null)
        {
            return true;
        }

        if (provider is T)
        {
            return true;
        }

        Debug.LogError($"[{GetType().Name}] {slotName} ({provider.GetType().Name}) does not implement {typeof(T).Name} on {gameObject.name}", this);
        return false;
    }

    public virtual void InitializeChildModules()
    {
        OnInitialized();
    }
}