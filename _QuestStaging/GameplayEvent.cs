/// <summary>
/// Something happened that other systems may care about: a pickup, an arrival, a talk.
/// Raised by whatever already knows it happened; the raiser never learns quests exist.
///
/// Multiplayer: raise from server-side code only. In a FishNet host build a static bus
/// hears both the server and client copy of an entity (Multiplayer_Readiness §3.6).
/// </summary>
public readonly struct GameplayEvent
{
    public readonly ObjectiveType Type;
    public readonly ControllerBrain Actor;
    public readonly string TargetId;
    public readonly int Amount;

    public GameplayEvent(ObjectiveType type, ControllerBrain actor, string targetId, int amount = 1)
    {
        Type = type;
        Actor = actor;
        TargetId = targetId;
        Amount = amount;
    }
}
