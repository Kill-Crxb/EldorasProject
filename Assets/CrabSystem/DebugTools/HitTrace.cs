#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Editor-only. Drop one on any object in the scene. Logs every resolved hit on every entity —
// the d20 against Defense, full or glancing, the damage carried into soak, the soak, and what
// reached health — plus every kill with its killer. Roadmap P9 / tracker CX5.
//
//   [HitTrace] Porphi → Hvy_Dummy  Melee Physical  d20 14 +4 = 18 vs 17 FULL
//              9 +3 expl(1) = 12 − soak 7 → 5   hp 55/60
public class HitTrace : MonoBehaviour
{
    [SerializeField] private float rescanInterval = 1f;
    [SerializeField] private bool logTicks = false;

    private readonly HashSet<DamageSystem> tracked = new();
    private float nextScan;

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanInterval;

        foreach (DamageSystem damage in FindObjectsByType<DamageSystem>(FindObjectsSortMode.None))
        {
            if (!tracked.Add(damage)) continue;

            DamageSystem d = damage;
            d.OnHitResolved += (packet, hit) => LogHit(d, packet, hit);
            d.OnKill += victim => LogKill(d, victim);
        }
    }

    private void LogHit(DamageSystem target, CombatDamagePacket packet, HitResolution hit)
    {
        if (this == null || target == null) return;
        if (packet.source == DamageSource.Tick && !logTicks) return;

        string attacker = packet.attacker != null ? packet.attacker.root.name : "none";
        string head = $"[HitTrace] {attacker} → {target.transform.root.name}  {packet.source} {packet.damageType}";

        Debug.Log($"{head}  {Roll(hit)}\n           {Damage(packet, hit)}   {Health(target)}");
    }

    private void LogKill(DamageSystem killer, ControllerBrain victim)
    {
        if (this == null || killer == null) return;

        string victimName = victim != null ? victim.transform.root.name : "none";
        Debug.Log($"[HitTrace] {killer.transform.root.name} killed {victimName}");
    }

    private static string Roll(HitResolution hit)
    {
        if (hit.grade == HitGrade.Unrolled) return "unrolled";

        float total = hit.roll + hit.accuracy;
        string grade = hit.grade == HitGrade.Full ? "FULL" : "glancing";
        return $"d20 {hit.roll} {hit.accuracy:+0;-0} = {total:0} vs {hit.defense:0} {grade}";
    }

    private static string Damage(CombatDamagePacket packet, HitResolution hit)
    {
        string carried = $"{packet.finalDamage:0.#}";
        if (hit.grade == HitGrade.Full && packet.explosions > 0)
            carried += $" +{packet.explosionDamage:0.#} expl({packet.explosions})";
        if (hit.grade == HitGrade.Glancing)
            carried += " halved";

        return $"{carried} = {hit.rolled:0.#} − soak {hit.soak:0.#} → {hit.applied:0.#}";
    }

    private static string Health(DamageSystem target)
    {
        IHealthProvider health = target.Brain != null ? target.Brain.Health : null;
        if (health == null) return "";
        return $"hp {health.GetCurrentHealth():0}/{health.GetMaxHealth():0}";
    }
}
#endif
