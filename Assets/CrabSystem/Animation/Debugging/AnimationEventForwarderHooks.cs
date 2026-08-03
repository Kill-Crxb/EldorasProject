using UnityEngine;

public class AnimationEventForwarderHooks : MonoBehaviour
{
    private AnimationEventForwarder eventForwarder;
    private AnimationDebugger debugger;

    void Start()
    {
        var brain = GetComponent<ControllerBrain>();
        if (brain == null) brain = GetComponentInParent<ControllerBrain>();

        eventForwarder = brain?.GetComponentInChildren<AnimationEventForwarder>();
        debugger = FindObjectOfType<AnimationDebugger>();

        if (eventForwarder == null)
        {
            debugger?.Log("[EventForwarderHooks] AnimationEventForwarder not found", true);
            return;
        }

        debugger?.Log("[EventForwarderHooks] Hooked into AnimationEventForwarder", true);
    }

    void Update()
    {
        if (eventForwarder == null || debugger == null) return;

        TraceEventForwarderState();
    }

    private void TraceEventForwarderState()
    {
        if (eventForwarder == null) return;

        var eventField = eventForwarder.GetType().GetField("onAnimationEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (eventField != null)
        {
            var onEvent = eventField.GetValue(eventForwarder) as System.Delegate;
            if (onEvent != null && onEvent.GetInvocationList().Length > 0)
            {
                debugger?.Log($"[EventForwarder] {onEvent.GetInvocationList().Length} listeners registered");
            }
        }
    }

    public void NotifyEventFired(string eventName)
    {
        debugger?.TraceAnimationEvent(eventName);
    }

    public void NotifyListenerRegistered(string eventName, System.Delegate listener)
    {
        debugger?.TraceAnimationEventListener(eventName, listener?.Target?.GetType().Name ?? "Unknown");
    }

    public void NotifyForwarderInitialized()
    {
        debugger?.Log("[EventForwarder] Initialized and ready to forward events", true);
    }

    public void NotifyForwarderMissing()
    {
        debugger?.Log("⚠️  [EventForwarder] NOT FOUND - Animation events won't propagate!", true);
    }
}