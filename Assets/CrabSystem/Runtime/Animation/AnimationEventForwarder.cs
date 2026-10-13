using System;
using UnityEngine;

// Hands a clip's move events to the brain (Combat_Framework.md §2.1). Unity calls clip events only on the
// components next to the Animator, so this sits there; AbilitySystem binds to it, and presentation (the
// socket relay) may listen too.
public class AnimationEventForwarder : MonoBehaviour
{
    public event Action<MoveEvent, int> OnMoveEvent;

    // Unity fires a clip's events once per layer that plays it with weight above 0. The v2
    // animator's Actions Upper layer is SYNCED to Actions — same states, same clips — so while
    // both have weight (standing still) every event arrives twice in the same frame; moving drops
    // Actions to 0 and they arrive once (Known Issues B15). Events carry no layer index, so the
    // duplicate is recognised instead: the same event with the same value twice in one frame is
    // always the second layer, because one clip cannot fire one event twice in a frame.
    private static readonly int EventCount = Enum.GetValues(typeof(MoveEvent)).Length;
    private readonly int[] lastFrame = new int[EventCount];
    private readonly int[] lastValue = new int[EventCount];

    public void OnTell() => Broadcast(MoveEvent.Tell, 0);
    public void OnStrike(int index) => Broadcast(MoveEvent.Strike, index);
    public void OnCue(int cue) => Broadcast(MoveEvent.Cue, cue);
    public void OnChainOpen() => Broadcast(MoveEvent.ChainOpen, 0);
    public void OnUnlocked() => Broadcast(MoveEvent.Unlocked, 0);
    public void OnInvuln(int on) => Broadcast(MoveEvent.Invuln, on);
    public void OnTravel(int on) => Broadcast(MoveEvent.Travel, on);

    private void Broadcast(MoveEvent evt, int value)
    {
        if (IsDuplicate((int)evt, value)) return;
        OnMoveEvent?.Invoke(evt, value);
    }

    // Frames are stored +1 so the zeroed array never matches frame 0.
    private bool IsDuplicate(int index, int value)
    {
        int frame = Time.frameCount + 1;
        if (lastFrame[index] == frame && lastValue[index] == value) return true;

        lastFrame[index] = frame;
        lastValue[index] = value;
        return false;
    }
}
