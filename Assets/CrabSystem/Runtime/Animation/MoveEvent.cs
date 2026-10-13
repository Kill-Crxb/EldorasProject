// The clip events a move is built from (Combat_Framework.md §2.1, Strike_Build.md). A clip calls them
// through AnimationEventForwarder as OnTell, OnStrike(int), OnCue(int), OnChainOpen, OnUnlocked,
// OnInvuln(int) and OnTravel(int); the event's int travels with it as its value.
//
// Never serialized: abilities name their cue by number (effectCue), so members may be reordered.
public enum MoveEvent
{
    // The read: the frame a defender can first see the attack coming. Sits in the wind-up.
    Tell,
    // One hit check, on this frame. Value: which entry of the ability's strikes to use.
    Strike,
    // Releases the ability's effects, spell or projectile when the value equals its effectCue.
    Cue,
    // From here a held press may start the string's next step.
    ChainOpen,
    // The move is over and the fighter may act again.
    Unlocked,
    // Value 1 turns invulnerability on, 0 turns it off.
    Invuln,
    // Value 1: the clip's travel (a dash, a lunge) starts here; 0: it ends. Nothing acts on it yet — it marks
    // the window a teleport or charge will replace (Combat_Roadmap, the teleport/charge idea of 7 Oct).
    Travel,
}
