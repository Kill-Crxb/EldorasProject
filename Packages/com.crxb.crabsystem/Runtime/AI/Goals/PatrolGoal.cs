using UnityEngine;

// Walk a route with no target: the Patrol Offsets on the creature's AIControlSource, in metres from where
// it started, in order and looped, pausing at each. Drops out the moment it sees someone. Give it a
// higher base weight than Wander. Phase 0 is pausing, phase 1 walking.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Goal_Patrol", menuName = "AI/GOAP/Goals/Patrol")]
    public class PatrolGoal : GOAPGoal
    {
        public float strength = 0.4f;

        [Tooltip("Seconds stood at each point.")]
        public float pause = 1.5f;

        public float arrive = 0.6f;

        [Tooltip("Give up on a point after this long (something in the way) and take the next.")]
        public float legTimeout = 10f;

        public override bool CanExecute(GOAPContext ctx)
        {
            return ctx.aiControl != null && ctx.target == null && ctx.aiControl.PatrolOffsets.Count > 0;
        }

        public override float CalculateWeight(GOAPContext ctx) => 1f;

        public override bool IsComplete(GOAPContext ctx) => ctx.target != null;

        public override void OnStart(GOAPContext ctx)
        {
            ctx.aiControl.GoalPhase = 1;
            ctx.aiControl.GoalUntil = ctx.Now + legTimeout;
        }

        public override void Execute(GOAPContext ctx)
        {
            AIControlSource control = ctx.aiControl;

            if (control.GoalPhase == 0)
            {
                control.Stop();
                if (ctx.Now < control.GoalUntil) return;

                control.PatrolIndex = (control.PatrolIndex + 1) % control.PatrolOffsets.Count;
                control.GoalPhase = 1;
                control.GoalUntil = ctx.Now + legTimeout;
                return;
            }

            Vector3 point = control.Home + control.PatrolOffsets[control.PatrolIndex % control.PatrolOffsets.Count];
            control.Face(point - ctx.self.position);

            if (!GoalSteer.ToPoint(ctx, point, arrive, strength) && ctx.Now < control.GoalUntil) return;

            control.GoalPhase = 0;
            control.GoalUntil = ctx.Now + pause;
        }

        public override void OnEnd(GOAPContext ctx)
        {
            ctx.aiControl?.Release();
        }
    }
}
