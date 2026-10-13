using UnityEngine;

// Shared plumbing for IK modules: finding the animator, attaching the relay, re-binding after a
// model swap, and catching the one setup mistake that silently does nothing (IK Pass unticked).
// Subclasses supply IKOrder and Solve.
public abstract class AnimatorIKModule : MonoBehaviour, IBrainModule, IAnimatorIKReceiver
{
    protected ControllerBrain brain;

    private AnimatorIKRelay relay;
    private Animator boundAnimator;
    private bool ikCallbackSeen;
    private bool warnedNoIKPass;
    private float ikWaitTime;

    public bool IsEnabled { get; set; } = true;
    public Animator BoundAnimator => boundAnimator;
    public bool IKCallbackSeen => ikCallbackSeen;

    public abstract int IKOrder { get; }

    protected abstract void Solve(Animator animator);
    protected virtual void OnRebind(Animator animator) { }
    protected virtual void ResolveReferences() { }

    public virtual void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public virtual void LateInitialize()
    {
        ResolveReferences();
        BindRelay();
    }

    public virtual void UpdateModule()
    {
        if (brain == null) return;

        if (brain.EntityAnimator != boundAnimator)
            BindRelay();

        WarnIfIKPassMissing();
    }

    public void ApplyIK(Animator animator)
    {
        ikCallbackSeen = true;
        Solve(animator);
    }

    void OnEnable()
    {
        if (relay != null) relay.Bind(this);
    }

    void OnDisable()
    {
        if (relay != null) relay.Unbind(this);
    }

    private void BindRelay()
    {
        Animator animator = brain?.EntityAnimator;

        boundAnimator = animator;
        ikCallbackSeen = false;
        warnedNoIKPass = false;
        ikWaitTime = 0f;

        if (animator == null) return;

        if (!animator.isHuman)
        {
            Debug.LogError($"[{GetType().Name}] Animator '{animator.name}' is not humanoid. IK requires a humanoid avatar.", animator);
            IsEnabled = false;
            return;
        }

        relay = animator.GetComponent<AnimatorIKRelay>();
        if (relay == null) relay = animator.gameObject.AddComponent<AnimatorIKRelay>();
        relay.Bind(this);

        OnRebind(animator);
    }

    private void WarnIfIKPassMissing()
    {
        if (ikCallbackSeen || warnedNoIKPass || boundAnimator == null || !IsEnabled) return;

        ikWaitTime += Time.deltaTime;
        if (ikWaitTime < 1f) return;

        warnedNoIKPass = true;
        Debug.LogError($"[{GetType().Name}] OnAnimatorIK never fired for '{boundAnimator.name}'. Enable IK Pass on a layer of its Animator Controller.", boundAnimator);
    }
}
