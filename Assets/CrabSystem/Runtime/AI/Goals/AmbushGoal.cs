using UnityEngine;

// Lie in wait. Holds perfectly still while the target is beyond Spring Range, then springs: marks the
// creature Engaged and hands over to its fighting goals (Lunge, Chase & Melee…), which do the rushing.
// Engaged clears when the creature loses its target, so it can lie in wait again.
// Give it a higher base weight than the fighting goals so it wins while the target is far, and leave
// Wander out of the pool, or the creature strolls instead of hiding.
[CreateAssetMenu(fileName = "Goal_Ambush", menuName = "AI/GOAP/Goals/Ambush")]
public class AmbushGoal : GOAPGoal
{
    [Tooltip("Spring when the target comes inside this distance.")]
    public float springRange = 5f;

    [Tooltip("Turn to watch the target while waiting. Off reads as unaware.")]
    public bool watchTarget;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && ctx.aiControl != null && !ctx.aiControl.Engaged;

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.Engaged;

    public override void Execute(GOAPContext ctx)
    {
        ctx.aiControl.Stop();
        if (watchTarget) ctx.aiControl.Face(ctx.toTarget);

        if (ctx.distanceToTarget > springRange) return;

        ctx.aiControl.Face(ctx.toTarget);
        ctx.aiControl.Engaged = true;
    }
}
