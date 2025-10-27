// AnimationEventForwarder.cs - Now put this on the Model GameObject (where Animator is)
using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    private ControllerBrain brain;

    void Start()
    {
        // Find the ControllerBrain by going up the hierarchy
        brain = GetComponentInParent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("AnimationEventForwarder: No ControllerBrain found in parent hierarchy!");
        }
    }

    // Animation Event Handlers (unchanged)
    public void Attack_On()
    {
        brain?.GetModule<MeleeModule>()?.Attack_On();
    }

    public void Attack_Off()
    {
        brain?.GetModule<MeleeModule>()?.Attack_Off();
    }

    public void Combo_Up()
    {
        brain?.GetModule<MeleeModule>()?.Combo_Up();
    }
}