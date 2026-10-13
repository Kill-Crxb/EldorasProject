using UnityEngine;

// After an attack, what to do with the space it made — Dark Souls' "get well space". Odds out of 100;
// whatever is left over after the first three is a short wait. DS1 Hollows: 60 / 10 / 10 / 20.
[System.Serializable]
public class AfterAct
{
    [Range(0, 100)] public int nothing = 60;
    [Range(0, 100)] public int backAndStrafe = 10;
    [Range(0, 100)] public int back = 10;
}

// The moves every monster goal is made of. Goals call these and keep only their own decisions.
//
// Spacing phases: a goal hands control to these for a moment (GoalPhase 1–4, timed by GoalUntil) and
// carries on with its own logic once RunSpacing returns false. Goals keep phase 0 for themselves.
public static class GoalSteer
{
    public const int PhaseWait = 1;
    public const int PhaseBack = 2;
    public const int PhaseBackAndStrafe = 3;
    public const int PhaseStrafe = 4;
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

    public static bool Roll(int odds) => Random.Range(0, 100) < odds;

    // Busy with a move (swinging, casting, recovering).
    public static bool Busy(GOAPContext ctx)
    {
        AbilitySystem abilities = ctx.brain.GetModule<AbilitySystem>();
        return abilities != null && abilities.IsExecuting;
    }

    // Walk when close, run when far — DS1 approaches walk unless the target is well past reach, so
    // creatures shuffle in and re-decide often instead of sprinting in a line.
    public static float Gait(GOAPContext ctx, float stop, float runBeyond, float walkStrength)
    {
        return ctx.distanceToTarget > stop + runBeyond ? 1f : walkStrength;
    }

    // Roll what to do after an attack. Call it the moment the attack is pressed.
    public static void RollAfterAct(GOAPContext ctx, AfterAct odds)
    {
        int roll = Random.Range(0, 100);
        ctx.aiControl.GoalSign = RandomSign();

        if (roll < odds.nothing) return;
        roll -= odds.nothing;

        if (roll < odds.backAndStrafe)
            BeginSpacing(ctx, PhaseBackAndStrafe, Random.Range(2f, 3f));
        else if (roll < odds.backAndStrafe + odds.back)
            BeginSpacing(ctx, PhaseBack, Random.Range(1f, 1.5f));
        else
            BeginSpacing(ctx, PhaseWait, Random.Range(0.5f, 1f));
    }

    // Now and then, instead of pressing in, pause or strafe a 30–45° arc for a moment — the milling
    // about that makes low-level DS1 enemies feel alive. Rolled at most once per `interval` seconds.
    public static bool Hesitate(GOAPContext ctx, int odds, float interval)
    {
        AIControlSource control = ctx.aiControl;
        if (Time.time < control.NextDecisionAt) return false;
        control.NextDecisionAt = Time.time + interval;
        if (!Roll(odds)) return false;

        control.GoalSign = RandomSign();
        if (Roll(50))
            BeginSpacing(ctx, PhaseWait, Random.Range(0.5f, 1.5f));
        else
            BeginSpacing(ctx, PhaseStrafe, Random.Range(1f, 2.5f));
        return true;
    }

    public static void BeginSpacing(GOAPContext ctx, int phase, float seconds)
    {
        ctx.aiControl.GoalPhase = phase;
        ctx.aiControl.GoalUntil = Time.time + seconds;
    }

    // Runs a spacing phase. True while one is running. Its clock only starts once any move in
    // progress has finished, so an after-act begins when the swing does, not partway through it.
    public static bool RunSpacing(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        if (control.GoalPhase < PhaseWait || control.GoalPhase > PhaseStrafe) return false;

        if (Busy(ctx))
        {
            control.GoalUntil += Time.deltaTime;
            return true;
        }

        if (Time.time >= control.GoalUntil)
        {
            control.GoalPhase = 0;
            return false;
        }

        control.Face(ctx.toTarget);

        if (control.GoalPhase == PhaseWait)
            control.Stop();
        else if (control.GoalPhase == PhaseBack)
            Away(ctx, 0.6f);
        else if (control.GoalPhase == PhaseBackAndStrafe && ctx.distanceToTarget < 3f)
            Away(ctx, 0.6f);
        else
            Orbit(ctx, control.GoalSign, 0.5f);

        return true;
    }
}
