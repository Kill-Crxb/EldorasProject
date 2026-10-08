#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Editor-only. Spawns itself when play starts. Moves run on their clip's events; for moves that also
// carry baked frame data, this logs where Tell / the first Strike / Unlocked landed against those
// frames, so the frame data can be kept honest (blockstun and armour windows read it).
//
//   [MoveClockTrace] Base_PC(Clone)  BasicAttack1  Strike @22f, frames say 10 (+12)
//   [MoveClockTrace] Base_PC(Clone)  BasicAttack1  Unlocked after the move ended (frames say 30)
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
            a.OnMoveEvent += (evt, value) => Check(a, evt, value);
        }
    }

    private void Check(AbilitySystem abilities, MoveEvent evt, int value)
    {
        if (this == null || abilities == null) return;

        lastMove.TryGetValue(abilities, out AbilityDefinition move);
        if (move == null || !move.HasMoveData) return;

        int expected = ExpectedFrame(move, evt, value);
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

    private static int ExpectedFrame(AbilityDefinition move, MoveEvent evt, int value) => evt switch
    {
        MoveEvent.Tell => move.frames.tell,
        MoveEvent.Strike when value == 0 => move.ActiveStart,
        MoveEvent.Unlocked => move.TotalFrames,
        _ => -1
    };
}
#endif
