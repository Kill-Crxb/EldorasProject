using UnityEngine;

// Posture (Parry_Build.md, Combat_Framework §3.5): Sekiro's guard meter, separate from stamina. A block
// fills the defender's bar (more) and the attacker's (a little), an empty Guard bar adds the rest of the
// hit, and a parry fills the attacker's. A full bar is a Guard Break: the bar stays full while the
// fighter is open, then empties. Otherwise it drains back down after a short quiet spell.
public class PostureModule : MonoBehaviour, IBrainModule, IBarSource
{
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Posture a fighter can take before the guard breaks.")]
    [SerializeField] private float maxPosture = 30f;

    [Tooltip("Posture recovered per second once recovery starts.")]
    [SerializeField] private float recoveryPerSecond = 6f;

    [Tooltip("Seconds after the last posture damage before recovery starts.")]
    [SerializeField] private float recoveryDelay = 1.5f;

    [Tooltip("Seconds a Guard Break leaves the fighter open (the GuardBroken hit state).")]
    [SerializeField] private float breakSeconds = 1f;

    private float current;
    private float recoverAt;
    private float brokenUntil = -999f;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public float Current => current;
    public float Max => maxPosture;
    public float Fraction => maxPosture > 0f ? current / maxPosture : 0f;
    public float BreakSeconds => breakSeconds;
    public bool IsBroken => Time.time < brokenUntil;

    public void Initialize(ControllerBrain brain) { }

    public void LateInitialize() { }

    public void UpdateModule()
    {
        if (!isEnabled || IsBroken) return;

        if (brokenUntil > 0f)
        {
            brokenUntil = -999f;
            current = 0f;
            return;
        }

        if (current <= 0f || Time.time < recoverAt) return;

        current = Mathf.Max(0f, current - recoveryPerSecond * Time.deltaTime);
    }

    // True when this fills the bar: the caller breaks the guard. A broken fighter takes no more.
    public bool Damage(float amount)
    {
        if (!isEnabled || amount <= 0f || IsBroken) return false;

        current = Mathf.Min(maxPosture, current + amount);
        recoverAt = Time.time + recoveryDelay;
        if (current < maxPosture) return false;

        brokenUntil = Time.time + breakSeconds;
        return true;
    }
}
