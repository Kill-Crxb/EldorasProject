// Layer names as constants so callers stop spelling them out. A typo in a string here silently
// resolves to layer -1 and the write is skipped with no error — which is the failure mode that
// hid the jump bug for a month.
//
// HumanoidAnimator_v2 layers (Animator_Audit.md). Action layers rest at weight 1 and show only
// while they play something — AnimationLayerController's rest rule.
public static class AnimationLayerNames
{
    public const string Locomotion = "Locomotion";
    public const string MovementActions = "Movement Actions";
    public const string Actions = "Actions";
    public const string ActionsUpper = "Actions Upper";
    public const string Reactions = "Reactions";
}
