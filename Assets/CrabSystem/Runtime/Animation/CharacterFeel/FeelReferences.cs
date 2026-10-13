using UnityEngine;

// Shared lookup for components the feel systems need but the Brain's provider cache does not hold.
//
// ControllerBrain.GetModule<T> falls back to GetComponentInChildren from the BRAIN's GameObject.
// CharacterMotor lives on the entity root, which is the Brain's parent, so that search never
// reaches it. Searching from EntityRoot covers the whole character regardless of which child the
// feel systems are parked on.
public static class FeelReferences
{
    public static CharacterMotor FindMotor(ControllerBrain brain)
    {
        if (brain == null) return null;

        CharacterMotor cached = brain.GetModule<CharacterMotor>();
        if (cached != null) return cached;

        if (brain.EntityRoot != null) return brain.EntityRoot.GetComponentInChildren<CharacterMotor>(true);

        return null;
    }
}
