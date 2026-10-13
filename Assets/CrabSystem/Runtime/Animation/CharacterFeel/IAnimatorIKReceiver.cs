using UnityEngine;

// Implemented by any module that wants a slice of the animator's IK pass.
// AnimatorIKRelay collects them and drives them in IKOrder, lowest first, so receivers that
// move the body run before those that only set goals.
public interface IAnimatorIKReceiver
{
    int IKOrder { get; }
    void ApplyIK(Animator animator);
}
