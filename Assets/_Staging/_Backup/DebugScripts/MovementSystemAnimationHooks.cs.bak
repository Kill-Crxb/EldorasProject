using UnityEngine;

public class MovementSystemAnimationHooks : MonoBehaviour
{
    private MovementSystem movementSystem;
    private AnimationDebugger debugger;

    void Start()
    {
        var brain = GetComponent<ControllerBrain>();
        if (brain == null) brain = GetComponentInParent<ControllerBrain>();

        movementSystem = brain?.Movement;
        debugger = FindObjectOfType<AnimationDebugger>();

        if (movementSystem == null)
            Debug.LogWarning("[MovementHooks] MovementSystem not found");

        if (debugger == null)
            Debug.LogWarning("[MovementHooks] AnimationDebugger not found in scene");
    }

    void Update()
    {
        if (movementSystem == null || debugger == null) return;

        TraceMovementAnimationCalls();
    }

    private void TraceMovementAnimationCalls()
    {
        var animationProvider = movementSystem.Brain?.Animation;
        if (animationProvider == null) return;

        var animator = animationProvider.GetAnimator();
        if (animator == null) return;

        TraceParameter("MovementSpeed");
        TraceParameter("StrafeX");
        TraceParameter("StrafeY");
        TraceParameter("IsGrounded");
        TraceParameter("VerticalVelocity");
        TraceParameter("IsLockedOn");
    }

    private void TraceParameter(string paramName)
    {
        var animator = movementSystem.Brain?.Animation?.GetAnimator();
        if (animator == null) return;

        var paramType = GetParameterType(animator, paramName);
        if (paramType == null) return;

        switch (paramType.Value)
        {
            case AnimatorControllerParameterType.Float:
                float floatVal = animator.GetFloat(paramName);
                if (floatVal != 0f)
                    debugger.TraceMovementParameter(paramName, floatVal.ToString("F2"));
                break;

            case AnimatorControllerParameterType.Bool:
                bool boolVal = animator.GetBool(paramName);
                if (boolVal)
                    debugger.TraceMovementParameter(paramName, boolVal);
                break;
        }
    }

    private bool HasParameter(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    private AnimatorControllerParameterType? GetParameterType(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return param.type;
        }
        return null;
    }
}