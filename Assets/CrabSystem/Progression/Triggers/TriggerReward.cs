using UnityEngine;

namespace NinjaGame.Progression
{
    // "On a perfect dodge, regain 3 stamina", "on kill, Dash Thrust is ready 5 s sooner", "on hit, 1 in 4: your
    // light attack becomes Shadow Riposte for 3 s, once": an event, its filters and chance, and the effects it fires.
    // Held by the character's TriggerModule while the reward is.
    [CreateAssetMenu(fileName = "Reward_Trigger", menuName = "NinjaGame/Rewards/Trigger")]
    public class TriggerReward : Reward
    {
        [Header("When")]
        [SerializeField] private TriggerEvent trigger = TriggerEvent.HitLanded;
        [Tooltip("HitLanded / AbilityUsed / Kill: only for the abilities this filter picks.")]
        [SerializeField] private bool filterAbility;
        [SerializeField] private AbilityFilter ability;
        [Tooltip("StatusGained: only this status. Empty = any.")]
        [SerializeField] private StatusDefinition status;
        [Tooltip("Fires on a d20 of this or higher. 1 = always, 11 = half the time.")]
        [Range(1, 21)] [SerializeField] private int dc = 1;
        [Tooltip("Seconds before it can fire again. 0 = every time.")]
        [SerializeField] private float internalCooldown;

        [Header("Then")]
        [SerializeField] private ResourceDefinition resource;
        [SerializeField] private float resourceAmount;
        [SerializeField] private StatusDefinition applyStatus;
        [Tooltip("Apply the status to the other fighter (the one hit, killed, or who hit you) rather than to you.")]
        [SerializeField] private bool statusOnTarget;
        [Tooltip("Abilities whose cooldowns run forward.")]
        [SerializeField] private AbilityFilter cooldownFilter;
        [SerializeField] private float cooldownSeconds;
        [Tooltip("Single-use ability shown on a slot for a while; the slot goes back once it's used or the time runs out.")]
        [SerializeField] private AbilityDefinition proc;
        [Tooltip("Which slot it takes over: the first whose ability this filter picks (tag basic = the light attack).")]
        [SerializeField] private AbilityFilter procSlot;
        [SerializeField] private float procSeconds = 3f;

        [Tooltip("Multiply the resource and cooldown amounts by the talent's rank.")]
        [SerializeField] private bool perRank = true;

        public override void Apply(RewardContext context)
        {
            TriggerModule triggers = context.Brain != null ? context.Brain.GetModule<TriggerModule>() : null;
            if (triggers == null) return;

            float scale = perRank ? Mathf.Max(1f, context.Scale) : 1f;
            triggers.Set(context.SourceKey, new TalentTrigger
            {
                Event = trigger,
                FilterAbility = filterAbility,
                Ability = ability,
                Status = status,
                Dc = dc,
                InternalCooldown = internalCooldown,
                Resource = resource,
                ResourceAmount = resourceAmount * scale,
                ApplyStatus = applyStatus,
                StatusOnTarget = statusOnTarget,
                CooldownFilter = cooldownFilter,
                CooldownSeconds = cooldownSeconds * scale,
                Proc = proc,
                ProcSlot = procSlot,
                ProcSeconds = procSeconds
            });
        }

        public override void Remove(RewardContext context)
        {
            TriggerModule triggers = context.Brain != null ? context.Brain.GetModule<TriggerModule>() : null;
            if (triggers != null) triggers.Remove(context.SourceKey);
        }

        public override string Summarise()
        {
            string when = $"On {trigger}";
            if (filterAbility) when += $" ({ability.Describe()})";
            if (dc > 1) when += $", {21 - dc} in 20";

            if (proc != null) return $"{when}: {procSlot.Describe()} becomes {proc.abilityName} for {procSeconds:0.#} s";
            if (resource != null && resourceAmount > 0f) return $"{when}: +{resourceAmount:0.#} {resource.displayName}";
            if (applyStatus != null) return $"{when}: apply {applyStatus.displayName}";
            return $"{when}: {cooldownFilter.Describe()} cooldown −{cooldownSeconds:0.#} s";
        }
    }
}
