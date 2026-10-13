// A bar the HUD can draw: the Armour shield, the Guard bar and posture (Combat_Framework §3.5).
public interface IBarSource
{
    float Current { get; }
    float Max { get; }
}
