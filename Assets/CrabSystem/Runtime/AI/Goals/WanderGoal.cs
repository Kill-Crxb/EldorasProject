using UnityEngine;

// Idle life with no target: stroll to a random point near home, wait, pick another.
// Needs no target, so it runs while the creature has seen nobody and drops out the moment it does.
[CreateAssetMenu(fileName = "Goal_Wander", menuName = "AI/GOAP/Goals/Wander")]
public class WanderGoal : GOAPGoal
{
    [Tooltip("Metres from where the creature started.")]
    public float radius = 5f;

    [Tooltip("Stick push while strolling, 0–1.")]
    public float strength = 0.35f;

    public float minPause = 1.5f;
    public float maxPause = 4f;

    [Tooltip("Give up on a point after this long (something in the way).")]
    public float legTimeout = 6f;

    public override bool CanExecute(GOAPContext ctx) => ctx.aiControl != null && ctx.target == null;

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target != null;

    public override void OnStart(GOAPContext ctx)
    {
        Pause(ctx.aiControl);
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;

        if (control.GoalPhase == 0)
        {
            control.Stop();
            if (Time.time < control.GoalUntil) return;

            Vector2 offset = Random.insideUnitCircle * radius;
            control.GoalPoint = control.Home + new Vector3(offset.x, 0f, offset.y);
            control.GoalUntil = Time.time + legTimeout;
            control.GoalPhase = 1;
            return;
        }

        Vector3 to = control.GoalPoint - ctx.self.position;
        control.Face(to);

        if (GoalSteer.ToPoint(ctx, control.GoalPoint, 0.5f, strength) || Time.time > control.GoalUntil)
            Pause(control);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    void Pause(AIControlSource control)
    {
        control.GoalPhase = 0;
        control.GoalUntil = Time.time + Random.Range(minPause, maxPause);
    }
}
