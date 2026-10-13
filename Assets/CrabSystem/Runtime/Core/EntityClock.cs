// One clock per character (CrabSystem_Standard §9). Gameplay timers — cooldowns, statuses, regen, guard and AI memory —
// read Now and Delta here instead of Time.time and Time.deltaTime, so the clock that drives them can change in one
// place: scaled by action speed (CF11), frozen by hit-stop, or stepped by a server tick.
//
// Today it advances by Time.deltaTime at scale 1 from Time.time, so Now tracks Time.time and code still reading Time
// stays comparable with code reading the clock.

namespace CrabSystem
{
    public class EntityClock
    {
        public float Now { get; private set; }
        public float Delta { get; private set; }
        public float Scale { get; set; } = 1f;

        public EntityClock(float start)
        {
            Now = start;
        }

        public void Advance(float realDelta)
        {
            Delta = realDelta * Scale;
            Now += Delta;
        }
    }
}
