#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Editor-only. Spawns itself when play starts (or drop one in the scene). Logs every ability an
// entity starts, its guard going up or down, parry windows, parries, stamina spent on guarded and
// parried hits, and guard breaks, with the time, so input feel can be read from the console.
//
//   [ActionTrace] 12.43  Base_PC(Clone)  used BasicAttack1
//   [ActionTrace] 13.02  Base_PC(Clone)  guard UP
//   [ActionTrace] 13.38  Base_PC(Clone)  parry window 8f
//   [ActionTrace] 13.40  Base_PC(Clone)  PARRY
//   [ActionTrace] 13.40  Mirror_NPC  parried  -4 posture  (26/30)
public class ActionTrace : MonoBehaviour
{
    [SerializeField] private float rescanInterval = 1f;

    private readonly HashSet<AbilitySystem> tracked = new();
    private readonly HashSet<GuardModule> trackedGuards = new();
    private float nextScan;

    // A copy placed in a scene loaded after the self-spawned one stands down, so lines aren't doubled.
    private void Awake()
    {
        if (FindObjectsByType<ActionTrace>().Length > 1) Destroy(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindAnyObjectByType<ActionTrace>() != null) return;

        GameObject host = new GameObject("[ActionTrace]");
        DontDestroyOnLoad(host);
        host.AddComponent<ActionTrace>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanInterval;

        foreach (AbilitySystem abilities in FindObjectsByType<AbilitySystem>())
        {
            if (!tracked.Add(abilities)) continue;

            AbilitySystem a = abilities;
            a.OnAbilityUsed += id => Log(a, $"used {id}");
        }

        foreach (GuardModule guard in FindObjectsByType<GuardModule>())
        {
            if (!trackedGuards.Add(guard)) continue;

            GuardModule g = guard;
            g.OnBlockStart += () => Log(g, "guard UP");
            g.OnBlockEnd += () => Log(g, "guard DOWN");
            g.OnPerfectBlock += () => Log(g, "PARRY");
            g.OnParryWindowOpened += frames => Log(g, $"parry window {frames}f");
            g.OnGuardedHit += (cost, posture, stun) => Log(g, $"blocked  -{cost} stamina {Stamina(g)}  +{posture:0.#} posture {Posture(g)}  blockstun {stun}f");
            g.OnPostureDamaged += posture => Log(g, $"guarded by target  +{posture:0.#} posture {Posture(g)}");
            g.OnGuardBreak += () => Log(g, "GUARD BROKEN");
            g.OnGuardFlanked += angle => Log(g, $"hit OUTSIDE guard arc ({angle:0}° off facing)");
        }
    }

    private static string Posture(Component who)
    {
        ControllerBrain brain = who.GetComponentInParent<ControllerBrain>();
        PostureModule posture = brain != null ? brain.GetModule<PostureModule>() : null;
        if (posture == null) return "(no PostureModule)";

        return $"({posture.Current:0}/{posture.Max:0})";
    }

    private static string Stamina(Component who)
    {
        ControllerBrain brain = who.GetComponentInParent<ControllerBrain>();
        ResourceSystem resources = brain != null ? brain.Resources : null;
        ResourceDefinition stamina = resources != null ? resources.FindDefinition("stamina") : null;
        if (stamina == null) return "(no stamina)";

        return $"({resources.GetResource(stamina):0}/{resources.GetMaxResource(stamina):0})";
    }

    private void Log(Component who, string what)
    {
        if (this == null || who == null) return;
        Debug.Log($"[ActionTrace] {Time.time:0.00}  {who.transform.root.name}  {what}");
    }
}
#endif
