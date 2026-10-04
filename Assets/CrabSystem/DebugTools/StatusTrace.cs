#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Editor-only. Drop one on any object in the scene. Logs every status applied, restacked or
// removed on every entity, with the stats it feeds and the blackboard facts it holds, read back
// from the live systems. Also logs each entity's watched stats when it is first found, so an
// armoured dummy shows its armour before anything hits it.
public class StatusTrace : MonoBehaviour
{
    [SerializeField] private float rescanInterval = 1f;
    [SerializeField] private string[] watchedStats = { "atr.arm", "atr.arm_dice", "atr.arm_def", "def.avoidance" };

    private readonly HashSet<StatusSystem> tracked = new();
    private float nextScan;

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanInterval;

        foreach (StatusSystem system in FindObjectsByType<StatusSystem>())
        {
            if (!tracked.Add(system)) continue;

            StatusSystem s = system;
            s.OnStatusApplied += i => Log(s, "+", i);
            s.OnStacksChanged += i => Log(s, "~", i);
            s.OnStatusRemoved += i => Log(s, "-", i);
            Debug.Log($"[StatusTrace] tracking {Owner(s)}  {Watched(s)}");
        }
    }

    private void Log(StatusSystem system, string sign, StatusInstance instance)
    {
        if (this == null || system == null) return;

        StatusDefinition def = instance.Definition;
        string source = instance.Source != null ? instance.Source.transform.root.name : "none";
        string time = instance.IsPermanent ? "permanent" : $"{instance.Remaining:0.0}s";

        var line = new StringBuilder();
        line.Append($"[StatusTrace] {Owner(system)}  {sign} {def.displayName} x{instance.Stacks}  {time}  from {source}");

        foreach (StatContribution c in def.contributions)
            line.Append($"  {c.statId} {c.amountPerStack * instance.Stacks:+0.#;-0.#}");

        Blackboard blackboard = Brain(system)?.GetModule<BlackboardSystem>()?.Blackboard;
        foreach (StatusFlag flag in def.blackboardFlags)
        {
            bool held = blackboard != null && blackboard.GetBool(new BlackboardKey(flag.fact).hash);
            line.Append($"  {flag.fact}={(held ? "on" : "off")}");
        }

        line.Append("  | ").Append(Watched(system));
        Debug.Log(line.ToString());
    }

    private string Watched(StatusSystem system)
    {
        StatSystem stats = Brain(system)?.Stats;
        if (stats == null) return "no stats";

        var text = new StringBuilder();
        foreach (string id in watchedStats)
        {
            if (!stats.HasStat(id)) continue;
            text.Append($"{id}={stats.GetValue(id):0.#} ");
        }
        return text.ToString();
    }

    private static ControllerBrain Brain(StatusSystem system) => system.GetComponentInParent<ControllerBrain>();

    private static string Owner(StatusSystem system) => system.transform.root.name;
}
#endif
