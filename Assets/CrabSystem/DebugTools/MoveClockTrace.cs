#if UNITY_EDITOR
using System.Collections.Generic;
using NinjaGame.Animation;
using UnityEngine;

// Editor-only. Spawns itself when play starts. Moves run on their clip's events; for moves that also
// carry authored frame data, this logs where HitboxStart / HitboxEnd / AnimUnlocked landed against
// those frames, so the frame data can be kept honest (blockstun and armour windows read it).
//
//   [MoveClockTrace] Base_PC(Clone)  BasicAttack1  HitboxStart @22f, frames say 10 (+12)
//   [MoveClockTrace] Base_PC(Clone)  BasicAttack1  AnimUnlocked after the move ended (frames say 30)
public class MoveClockTrace : MonoBehaviour
{
    [SerializeField] private float rescanInterval = 1f;

    [Tooltip("Drift of this many frames or fewer isn't logged (30 fps clips land on even frames).")]
    [SerializeField] private int tolerance = 1;

    private readonly HashSet<AbilitySystem> tracked = new();
    private readonly Dictionary<AbilitySystem, AbilityDefinition> lastMove = new();
    private float nextScan;

    private void Awake()
    {
        if (FindObjectsByType<MoveClockTrace>().Length > 1) Destroy(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindAnyObjectByType<MoveClockTrace>() != null) return;

        GameObject host = new GameObject("[MoveClockTrace]");
        DontDestroyOnLoad(host);
        host.AddComponent<MoveClockTrace>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanInterval;

        foreach (AbilitySystem abilities in FindObjectsByType<AbilitySystem>())
        {
            if (!tracked.Add(abilities)) continue;

            AbilitySystem a = abilities;
            a.OnAbilityUsed += id => lastMove[a] = a.CurrentAbility;
            a.OnAbilityAnimationEvent += evt => Check(a, evt);
        }
    }

    private void Check(AbilitySystem abilities, AnimationEventType evt)
    {
        if (this == null || abilities == null) return;

        lastMove.TryGetValue(abilities, out AbilityDefinition move);
        if (move == null || !move.HasMoveData) return;

        int expected = ExpectedFrame(move, evt);
        if (expected < 0) return;

        string who = $"{abilities.transform.root.name}  {move.abilityId}  {evt}";

        if (abilities.CurrentAbility != move)
        {
            Debug.Log($"[MoveClockTrace] {who} after the move ended (frames say {expected})");
            return;
        }

        int drift = abilities.CurrentMoveFrame - expected;
        if (Mathf.Abs(drift) <= tolerance) return;

        Debug.Log($"[MoveClockTrace] {who} @{abilities.CurrentMoveFrame}f, frames say {expected} ({drift:+0;-0})");
    }

    private static int ExpectedFrame(AbilityDefinition move, AnimationEventType evt) => evt switch
    {
        AnimationEventType.HitboxStart => move.ActiveStart,
        AnimationEventType.HitboxEnd => move.RecoveryStart,
        AnimationEventType.AnimUnlocked => move.TotalFrames,
        _ => -1
    };
}
#endif
