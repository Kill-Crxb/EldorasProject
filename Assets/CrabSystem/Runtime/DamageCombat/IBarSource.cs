// A bar the HUD can draw: the Armour shield, the Guard bar and posture (Combat_Framework §3.5).

namespace CrabSystem
{
    public interface IBarSource
    {
        float Current { get; }
        float Max { get; }
    }
}
