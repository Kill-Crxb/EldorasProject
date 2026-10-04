using System;
using System.Collections.Generic;
using UnityEngine;

// Mirrors blackboard facts into animator parameters, so systems set facts and nothing else writes
// animator bools. Block sets IsBlocking on the blackboard; the Guard states follow it. Spellcraft
// sets IsDrawingSigns; the sign-hold state follows it.
//
// Only bools and floats — triggers are events, not state, and stay with whoever fires them.
// A parameter the animator lacks is skipped silently, so one bridge serves every controller.
public class AnimatorFactBridge : MonoBehaviour, IBrainModule
{
    public enum ParameterKind { Bool, Float }

    [Serializable]
    public struct Binding
    {
        [IdRef(IdKind.Fact)] public string fact;
        public string parameter;
        public ParameterKind kind;
    }

    [SerializeField] private List<Binding> bindings = new List<Binding>
    {
        new Binding { fact = "IsBlocking", parameter = "IsBlocking", kind = ParameterKind.Bool },
        new Binding { fact = "IsDrawingSigns", parameter = "IsDrawing", kind = ParameterKind.Bool },
    };

    private ControllerBrain brain;
    private Blackboard blackboard;
    private AnimationSystem animation;
    private int[] factHashes;
    private bool[] present;
    private Animator boundAnimator;
    private int boundParameterCount = -1;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        blackboard = brain?.Blackboard;
        animation = brain?.Animation;

        factHashes = new int[bindings.Count];
        present = new bool[bindings.Count];
        for (int i = 0; i < bindings.Count; i++)
            factHashes[i] = new BlackboardKey(bindings[i].fact).hash;

        if (blackboard == null) Debug.LogError($"[AnimatorFactBridge] No blackboard on '{name}'.", this);
        if (animation == null) Debug.LogError($"[AnimatorFactBridge] No AnimationSystem on '{name}'.", this);
    }

    // Every frame rather than on change events: ModelModule can swap the animator, and a freshly
    // bound one starts from defaults. Unchanged values cost a hash lookup.
    public void UpdateModule()
    {
        if (blackboard == null || animation == null) return;

        RebindIfAnimatorChanged();

        for (int i = 0; i < bindings.Count; i++)
        {
            if (!present[i]) continue;
            Push(bindings[i], factHashes[i]);
        }
    }

    // HasParameter walks animator.parameters, which allocates — so it runs once per animator, not
    // once per frame. The count check catches an Animator that exists but hasn't bound its
    // controller yet (0 parameters on the first frames after ModelModule spawns it).
    private void RebindIfAnimatorChanged()
    {
        Animator animator = brain.EntityAnimator;
        int count = animator != null ? animator.parameterCount : -1;
        if (animator == boundAnimator && count == boundParameterCount) return;

        boundAnimator = animator;
        boundParameterCount = count;
        for (int i = 0; i < bindings.Count; i++)
            present[i] = animator != null && animation.HasParameter(bindings[i].parameter);
    }

    private void Push(Binding binding, int factHash)
    {
        if (binding.kind == ParameterKind.Bool)
            animation.SetBool(binding.parameter, blackboard.GetBool(factHash));
        else
            animation.SetFloat(binding.parameter, blackboard.GetFloat(factHash));
    }
}
