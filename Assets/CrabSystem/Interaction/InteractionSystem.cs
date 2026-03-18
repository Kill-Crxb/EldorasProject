using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class InteractionSystem : MonoBehaviour, IBrainModule
{
    [Header("Module")]
    [SerializeField] private bool isEnabled = true;

    [Header("Interaction Config")]
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private InteractionAction interactionAction = InteractionAction.None;
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private bool canInteractMultipleTimes = true;

    [Header("Detection (Player Only)")]
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private float detectionInterval = 0.2f;
    [SerializeField] private LayerMask detectionLayer = -1;

    [Header("Visual Feedback")]
    [SerializeField] private InteractionIconDatabase iconDatabase;
    [SerializeField] private GameObject iconPromptPrefab;

    [Header("Lock System")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKeyId = "";

    [Header("Fallback (Simple Objects)")]
    public UnityEvent<ControllerBrain> OnDirectInteraction;

    private ControllerBrain brain;
    private InputSystem inputSystem;
    private InteractionIconPrompt activePrompt;
    private bool hasBeenInteracted = false;
    private float detectionTimer;
    private InteractionSystem currentTarget;
    private List<InteractionSystem> detectedSystems = new List<InteractionSystem>();
    private Collider[] detectionBuffer = new Collider[20];
    private Dictionary<System.Type, object> cachedProviders = new Dictionary<System.Type, object>();

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool IsInteractable => isEnabled && isInteractable && (!hasBeenInteracted || canInteractMultipleTimes);
    public float InteractionRange => interactionRange;
    public InteractionAction InteractionAction => GetInteractionAction();
    public ControllerBrain Brain => brain;
    public InteractionSystem CurrentTarget => currentTarget;

    public void Initialize(ControllerBrain controllerBrain)
    {
        if (controllerBrain == null) return;
        brain = controllerBrain;
        inputSystem = brain.GetModule<InputSystem>();
        CacheCapabilities();
    }

    public void LateInitialize() { }

    public void UpdateModule()
    {
        if (!isEnabled || brain == null) return;
        if (!brain.IsPlayer) return;

        UpdateDetection();
        UpdatePrompt();
        HandleInput();
    }

    private void CacheCapabilities()
    {
        cachedProviders.Clear();
        var inventoryProvider = brain.GetProvider<IInventoryProvider>();
        if (inventoryProvider != null)
            cachedProviders[typeof(IInventoryProvider)] = inventoryProvider;
    }

    public bool HasCapability<T>() where T : class => cachedProviders.ContainsKey(typeof(T));

    public T GetCapability<T>() where T : class
    {
        cachedProviders.TryGetValue(typeof(T), out object provider);
        return provider as T;
    }

    private int GetCapabilityCount() => cachedProviders.Count;

    private InteractionAction GetInteractionAction()
    {
        if (interactionAction != InteractionAction.None) return interactionAction;

        int count = GetCapabilityCount();
        if (count == 0) return GetContextualInteractionAction();
        if (count == 1 && HasCapability<IInventoryProvider>()) return InteractionAction.Loot;
        return InteractionAction.Use;
    }

    private InteractionAction GetContextualInteractionAction()
    {
        switch (brain.Identity.GetEntityType())
        {
            case EntityType.Prop: return InteractionAction.Open;
            case EntityType.NPC:
            case EntityType.Enemy:
            case EntityType.Neutral: return InteractionAction.Talk;
            default: return InteractionAction.Use;
        }
    }

    private void UpdateDetection()
    {
        detectionTimer -= Time.deltaTime;
        if (detectionTimer > 0f) return;

        DetectNearbyInteractionSystems();
        detectionTimer = detectionInterval;
    }

    private void DetectNearbyInteractionSystems()
    {
        detectedSystems.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            brain.transform.position, detectionRadius, detectionBuffer, detectionLayer);

        for (int i = 0; i < hitCount; i++)
        {
            var otherBrain = detectionBuffer[i].GetComponentInParent<ControllerBrain>();
            if (otherBrain == null || otherBrain == brain) continue;

            var otherInteraction = otherBrain.GetModule<InteractionSystem>()
                ?? otherBrain.GetComponentInChildren<InteractionSystem>();

            if (otherInteraction != null && otherInteraction.IsInteractable)
                detectedSystems.Add(otherInteraction);
        }

        var newTarget = GetBestTarget();
        if (newTarget != currentTarget)
            OnTargetChanged(newTarget);
    }

    private InteractionSystem GetBestTarget()
    {
        if (detectedSystems.Count == 0) return null;

        InteractionSystem closest = null;
        float closestDistance = float.MaxValue;

        foreach (var system in detectedSystems)
        {
            float distance = Vector3.Distance(brain.transform.position, system.brain.transform.position);
            if (distance <= system.InteractionRange && distance < closestDistance)
            {
                closest = system;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void OnTargetChanged(InteractionSystem newTarget)
    {
        currentTarget = newTarget;
        if (currentTarget != null) ShowPrompt(currentTarget);
        else HidePrompt();
    }

    private void ShowPrompt(InteractionSystem target)
    {
        if (!brain.Identity.IsPlayer || iconDatabase == null || iconPromptPrefab == null) return;

        if (activePrompt == null)
        {
            var promptObj = Instantiate(iconPromptPrefab);
            activePrompt = promptObj.GetComponent<InteractionIconPrompt>();
            if (activePrompt == null) { Destroy(promptObj); return; }
        }

        Sprite icon = iconDatabase.GetIcon(target.InteractionAction);
        if (icon != null) activePrompt.Initialize(target.transform, icon);
    }

    private void HidePrompt() => activePrompt?.HideImmediate();

    private void UpdatePrompt()
    {
        if (activePrompt != null && currentTarget == null) HidePrompt();
    }

    private void HandleInput()
    {
        if (inputSystem == null) return;
        if (inputSystem.InteractPressed) TryInteractWithCurrentTarget();
    }

    public bool TryInteractWithCurrentTarget()
    {
        if (currentTarget == null) return false;
        return currentTarget.OnInteractedWith(brain);
    }

    public bool OnInteractedWith(ControllerBrain actor)
    {
        if (!IsInteractable) return false;

        float distance = Vector3.Distance(actor.transform.position, brain.transform.position);
        if (distance > interactionRange) return false;

        if (isLocked)
        {
            if (HasRequiredKey(actor)) Unlock();
            else return false;
        }

        RouteInteraction(actor);

        if (!canInteractMultipleTimes)
            hasBeenInteracted = true;

        return true;
    }

    private void RouteInteraction(ControllerBrain actor)
    {
        int count = GetCapabilityCount();

        if (count == 0)
            OnDirectInteraction?.Invoke(actor);
        else if (count == 1 && HasCapability<IInventoryProvider>())
            OpenLootWindow(actor);
        else
            ShowInteractionMenu(actor);
    }

    private void OpenLootWindow(ControllerBrain actor)
    {
        if (UniversalWindowManager.Instance == null)
        {
            Debug.LogError("[InteractionSystem] UniversalWindowManager not found!");
            return;
        }
        UniversalWindowManager.Instance.OpenContainerWindow(actor, brain);
    }

    private void ShowInteractionMenu(ControllerBrain actor) { }

    private bool HasRequiredKey(ControllerBrain actor)
    {
        if (string.IsNullOrEmpty(requiredKeyId)) return true;
        var inventory = actor.GetProvider<IInventoryProvider>();
        return inventory != null && inventory.HasItem(requiredKeyId);
    }

    public void Unlock() { if (!isLocked) return; isLocked = false; }
    public void Lock() => isLocked = true;
    public void SetInteractable(bool interactable) => isInteractable = interactable;
    public void SetInteractionAction(InteractionAction action) => interactionAction = action;
    public void SetDetectionRadius(float radius) => detectionRadius = Mathf.Max(0.5f, radius);
    public void RefreshCapabilities() => CacheCapabilities();

    void OnDestroy()
    {
        if (activePrompt != null) Destroy(activePrompt.gameObject);
    }
}