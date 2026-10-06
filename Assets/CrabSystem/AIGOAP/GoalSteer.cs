using UnityEngine;

// The moves every monster goal is made of. Goals call these and keep only their own decisions.
public static class GoalSteer
{
    // Close to `stop` metres from the target, easing off over `slow` metres before it. Movement is
    // momentum-first, so full input right up to the stop point would carry the body past it.
    public static void Approach(GOAPContext ctx, float stop, float slow, float strength)
    {
        float gap = ctx.distanceToTarget - stop;
        if (gap <= 0f)
        {
            ctx.aiControl.Stop();
            return;
        }
        ctx.aiControl.Steer(ctx.toTarget, strength * Mathf.Clamp01(gap / Mathf.Max(0.01f, slow)));
    }

    public static void Away(GOAPContext ctx, float strength)
    {
        ctx.aiControl.Steer(-ctx.toTarget, strength);
    }

    // Sideways around the target. Sign 1 is anticlockwise seen from above.
    public static void Orbit(GOAPContext ctx, float sign, float strength)
    {
        ctx.aiControl.Steer(Vector3.Cross(Vector3.up, ctx.toTarget) * sign, strength);
    }

    // Walk to a point. True once there.
    public static bool ToPoint(GOAPContext ctx, Vector3 point, float arrive, float strength)
    {
        Vector3 to = point - ctx.self.position;
        to.y = 0f;
        if (to.magnitude <= arrive)
        {
            ctx.aiControl.Stop();
            return true;
        }
        ctx.aiControl.Steer(to, strength * Mathf.Clamp01(to.magnitude / Mathf.Max(0.01f, arrive * 2f)));
        return false;
    }

    // Flips GoalSign every `period` seconds, so strafing changes direction.
    public static void SwapSign(GOAPContext ctx, float period)
    {
        AIControlSource control = ctx.aiControl;
        if (Time.time < control.GoalUntil) return;
        control.GoalSign = -control.GoalSign;
        control.GoalUntil = Time.time + period;
    }

    public static float RandomSign() => Random.value < 0.5f ? -1f : 1f;

    public static bool Ready(GOAPContext ctx, AbilityDefinition ability)
    {
        return ability != null && ctx.abilityModule != null && ctx.abilityModule.CanUseAbility(ability.abilityId);
    }

    // Uses the ability if it can, and stamps the attack time goals pace themselves by.
    public static bool TryUse(GOAPContext ctx, AbilityDefinition ability)
    {
        if (!Ready(ctx, ability)) return false;
        ctx.abilityModule.UseAbility(ability.abilityId);
        ctx.aiControl.LastAttackTime = Time.time;
        return true;
    }

    public static bool Facing(GOAPContext ctx, float degrees) => ctx.angleToTarget <= degrees;

    public static bool Rested(GOAPContext ctx, float interval) => Time.time - ctx.aiControl.LastAttackTime >= interval;

    public static float HealthFraction(GOAPContext ctx) => ctx.healthModule != null ? ctx.healthModule.GetHealthPercentage() : 1f;

    public static bool HasRole(GOAPContext ctx, AIRole role) => ctx.aiControl != null && ctx.aiControl.AbilityFor(role) != null;
}
