using UnityEngine;

// Hit states — Combat_Framework.md §5, roadmap CF2.
// The move says which state its hit causes; the damage that landed says how long. A hit the armour
// shield swallows whole causes none: armour trades blows (Crxb, 8 Oct). A hit state is a
// status raising CannotAct, so AbilitySystem's hard-control cancel does the interrupting.
// Combat_Framework §3.1: a guarded hit puts the defender in blockstun, never a hit state. A hit
// that gets round the guard (flanked), or a guard broken by the hit, does apply one (B29).
public partial class AbilityDefinition
{
    const float MinHitStateScale = 0.5f;
    const float MaxHitStateScale = 1.5f;

    public void ApplyHitState(ControllerBrain target, ControllerBrain caster, float applied)
    {
        if (hit.onHit == HitState.None || hit.onHitFrames <= 0) return;
        if (target == null || applied <= 0f) return;
        if (target.Health != null && !target.Health.IsAlive()) return;
        if (target.Damage != null && target.Damage.LastHitGuarded) return;

        ICombatantState targetState = target.GetProvider<ICombatantState>();
        if (targetState != null && targetState.IsArmored) return;

        StatusDefinition status = LoadHitState(hit.onHit);
        if (status == null) return;

        float average = AverageDamage(caster);
        float scale = average > 0f ? Mathf.Clamp(applied / average, MinHitStateScale, MaxHitStateScale) : 1f;

        target.GetModule<StatusSystem>()?.Apply(status, caster, hit.onHitFrames * scale / 60f);
    }

    float AverageDamage(ControllerBrain caster)
    {
        if (caster == null || caster.Damage == null || damageEffects == null) return 0f;

        float total = 0f;
        for (int i = 0; i < damageEffects.Count; i++)
        {
            if (damageEffects[i] != null) total += damageEffects[i].AverageDamage(caster.Damage);
        }
        return total;
    }

    public static StatusDefinition LoadHitState(HitState state)
    {
        string path = HitStatePath(state);
        if (path == null) return null;

        StatusDefinition status = Resources.Load<StatusDefinition>(path);
        if (status == null) Debug.LogError($"[AbilityDefinition] No hit-state status at Resources/{path}");
        return status;
    }

    static string HitStatePath(HitState state) => state switch
    {
        HitState.Flinch => "Statuses/HitStates/Status_Flinched",
        HitState.Stagger => "Statuses/HitStates/Status_Staggered",
        HitState.Knockdown => "Statuses/HitStates/Status_Prone",
        HitState.Launch => "Statuses/HitStates/Status_Launched",
        HitState.GuardBreak => "Statuses/HitStates/Status_GuardBroken",
        _ => null
    };
}
