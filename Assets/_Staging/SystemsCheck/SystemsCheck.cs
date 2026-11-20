#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only, temporary: logs what the 13 Nov builds do in play so the test pass can be read from Editor.log.
// Delete the SystemsCheck folder once the tests pass. Every line starts [Check].
//
// Logged as it happens: feat grants, item facts flipping, every stat change (old -> new), stamina and health jumps of
// a whole point or more (regen trickle is skipped), blocks (cost, posture, blockstun), parry windows, flanked hits,
// hits landed, and statuses on anyone.
// Home dumps a snapshot of the player: feats, facts, the watched stats and the pools.

namespace CrabSystem
{
    public class SystemsCheck : MonoBehaviour
    {
        [SerializeField] private string[] facts =
        {
            "WieldingLight", "WieldingMedium", "WieldingHeavy", "WieldingEdge", "WieldingReach", "WieldingBreak",
            "ArmourLight", "ArmourMedium", "ArmourHeavy", "WearingArmour", "HasShield", "IsUnarmed", "NoParry"
        };

        [Tooltip("Stats the Home snapshot prints. Changes to any stat are logged regardless.")]
        [SerializeField] private string[] snapshotStats =
        {
            "cmb.bonus_damage", "cmb.initiative", "cmb.parry_window", "cmb.block_stamina",
            "cmb.block_advantage", "cmb.guard_pressure", "cmb.strike_arc",
            "atr.arm", "atr.arm_dice", "atr.arm_def", "equip.load",
            "def.deflection", "def.avoidance", "def.toughness",
            "mov.run_speed", "mov.sprint_speed", "character.max_health"
        };

        private ControllerBrain player;
        private StatSystem playerStats;
        private ResourceSystem playerResources;
        private FeatModule playerFeats;
        private Blackboard playerBlackboard;
        private readonly Dictionary<int, string> factNames = new();
        private readonly HashSet<ControllerBrain> watched = new();
        private readonly Dictionary<string, float> lastPool = new();
        private float nextScan;

        private void Awake()
        {
            foreach (string fact in facts) factNames[BlackboardKey.Hash(fact)] = fact;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScan) Scan();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[Key.Home].wasPressedThisFrame) Snapshot();
        }

        private void Scan()
        {
            nextScan = Time.unscaledTime + 1f;
            foreach (ControllerBrain brain in FindObjectsByType<ControllerBrain>())
            {
                if (watched.Add(brain)) Watch(brain);
            }
        }

        private void Watch(ControllerBrain brain)
        {
            string who = brain.transform.root.name;

            StatusSystem statuses = brain.GetModule<StatusSystem>();
            if (statuses != null) statuses.OnStatusApplied += s => Log($"{who} status + {s.Id} ({s.Remaining:0.0}s, x{s.Stacks})");
            if (statuses != null) statuses.OnStatusRemoved += s => Log($"{who} status - {s.Id}");

            ResourceSystem resources = brain.GetModule<ResourceSystem>();
            if (resources != null) resources.OnResourceChanged += (def, value) => Pool(who, def, value);

            if (!brain.IsPlayer) return;
            WatchPlayer(brain, resources);
        }

        private void WatchPlayer(ControllerBrain brain, ResourceSystem resources)
        {
            player = brain;
            playerResources = resources;
            playerStats = brain.GetModule<StatSystem>();
            playerFeats = brain.GetComponentInChildren<FeatModule>();
            playerBlackboard = brain.Blackboard;
            Log($"watching player {brain.transform.root.name}");

            if (playerStats != null) playerStats.OnStatChanged += (id, from, to) => Log($"stat {id}: {from:0.##} -> {to:0.##}");
            if (playerFeats != null) playerFeats.Changed += () => Log($"feats now: {FeatList()}");
            if (playerBlackboard != null) playerBlackboard.OnBoolChanged += Fact;

            GuardModule guard = brain.GetModule<GuardModule>();
            if (guard != null) guard.OnGuardedHit += (cost, posture, stun) => Log($"BLOCK stamina -{cost}, posture +{posture:0.#}, blockstun {stun}f (block_stamina dial {Stat("cmb.block_stamina")}, block_advantage {Stat("cmb.block_advantage")}, deflection {Stat("def.deflection")})");
            if (guard != null) guard.OnParryWindowOpened += frames => Log($"PARRY window {frames}f (parry_window dial {Stat("cmb.parry_window")})");
            if (guard != null) guard.OnGuardFlanked += angle => Log($"FLANKED {angle:0}° off facing (strike_arc {Stat("cmb.strike_arc")})");

            AbilitySystem abilities = brain.GetModule<AbilitySystem>();
            if (abilities != null) abilities.OnHitLanded += (ability, target) => Log($"HIT {ability?.abilityId} -> {(target != null ? target.transform.root.name : "none")} (bonus_damage {Stat("cmb.bonus_damage")})");
        }

        private void Fact(int key, bool value)
        {
            if (factNames.TryGetValue(key, out string fact)) Log($"fact {fact} = {value}");
        }

        private void Pool(string who, ResourceDefinition def, float value)
        {
            if (def == null || def.resourceId == "regeneration") return;

            string key = $"{who}/{def.resourceId}";
            float last = lastPool.TryGetValue(key, out float stored) ? stored : value;
            lastPool[key] = value;

            float delta = value - last;
            if (Mathf.Abs(delta) >= 1f) Log($"{who} {def.resourceId} {delta:+0.#;-0.#} -> {value:0.#}");
        }

        private void Snapshot()
        {
            if (player == null)
            {
                Log("no player yet");
                return;
            }

            var text = new StringBuilder($"SNAPSHOT {player.transform.root.name}\n  feats: {FeatList()}\n  facts:");
            foreach (string fact in facts)
            {
                if (playerBlackboard != null && playerBlackboard.GetBool(BlackboardKey.Hash(fact))) text.Append($" {fact}");
            }

            text.Append("\n  stats:");
            foreach (string id in snapshotStats) text.Append($" {id}={Stat(id)}");

            if (playerResources != null)
                text.Append($"\n  health {playerResources.GetCurrentHealth():0}/{playerResources.GetMaxHealth():0}");
            Log(text.ToString());
        }

        private string FeatList()
        {
            if (playerFeats == null || playerFeats.Grants.Count == 0) return "none";

            var ids = new List<string>();
            foreach (FeatGrant grant in playerFeats.Grants) ids.Add(grant.feat != null ? grant.feat.featId : "?");
            return string.Join(", ", ids);
        }

        private string Stat(string id) => playerStats != null && playerStats.HasStat(id) ? playerStats.GetValue(id).ToString("0.##") : "n/a";

        private static void Log(string message) => Debug.Log($"[Check] {message}");
    }
}
#endif
