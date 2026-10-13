using System.Collections.Generic;
using UnityEngine;

// Spawns the floating combat text (GI-73). A hit shows what happened to it, read from the target's
// DamageSystem.LastHit: glancing (small, grey), counter, advantage, the keyword dice that added to it
// ("+4 Cruelty"), what armour absorbed, blocked / parried / evaded. Statuses landing on anyone show their name and
// stacks ("Sundered ×2"), "Resisted" when immunity, resistance or diminishing returns refused them, "Saved" when a
// saving throw weakened them; hit states (tag hitstate) don't,
// the reaction already shows them.
//
// Setup: on Manager_Brain, with the DamageNumber prefab assigned. Call sites: DamageEffect, ProjectilePayload.

namespace CrabSystem
{
    public class DamageNumberManager : MonoBehaviour
    {
        [SerializeField] private DamageNumber damageNumberPrefab;

        [Header("Combat text")]
        [SerializeField] private Color fullColor = Color.white;
        [SerializeField] private Color glancingColor = new Color(0.65f, 0.65f, 0.65f);
        [SerializeField] private Color counterColor = new Color(1f, 0.6f, 0.2f);
        [SerializeField] private Color soakColor = new Color(0.6f, 0.75f, 0.9f);
        [SerializeField] private Color guardColor = new Color(0.7f, 0.8f, 1f, 0.75f);
        [SerializeField] private Color parryColor = new Color(1f, 0.85f, 0.3f, 0.85f);
        [SerializeField] private Color evadeColor = new Color(0.5f, 1f, 1f, 0.75f);
        [SerializeField] private float glancingScale = 0.75f;
        [SerializeField] private float counterScale = 1.25f;
        [Tooltip("Blocked / Parried / Evaded — kept small so the words don't shout over the fight.")]
        [SerializeField] private float wordScale = 0.55f;

        [Header("Statuses")]
        [SerializeField] private bool showStatuses = true;
        [SerializeField] private Color harmfulColor = new Color(0.8f, 0.5f, 1f, 0.8f);
        [SerializeField] private Color beneficialColor = new Color(0.5f, 1f, 0.6f, 0.8f);
        [SerializeField] private Color resistedColor = new Color(1f, 1f, 0.5f, 0.8f);
        [SerializeField] private float statusScale = 0.5f;
        [Tooltip("Status text sits this far above the damage numbers.")]
        [SerializeField] private float statusHeight = 0.4f;
        [Tooltip("Seconds between looks for new characters to follow.")]
        [SerializeField] private float rescanInterval = 1f;

        private static DamageNumberManager instance;

        private readonly HashSet<StatusSystem> tracked = new();
        private float nextScan;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void Update()
        {
            if (!showStatuses || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + rescanInterval;

            tracked.RemoveWhere(s => s == null);
            foreach (StatusSystem statuses in FindObjectsByType<StatusSystem>(FindObjectsSortMode.None))
            {
                if (!tracked.Add(statuses)) continue;

                StatusSystem bearer = statuses;
                bearer.OnStatusApplied += status => ShowStatus(bearer, status.Definition, status.Stacks);
                bearer.OnStacksChanged += status => ShowStatus(bearer, status.Definition, status.Stacks);
                bearer.OnStatusResisted += definition => ShowWord(bearer, definition, "Resisted");
                bearer.OnStatusSaved += definition => ShowWord(bearer, definition, "Saved");
            }
        }

        // A number with no story — a breakable prop, or a caller that has no DamageSystem.
        public static void Spawn(float damage, Vector3 worldPos)
        {
            if (instance == null) return;
            instance.Show(Mathf.RoundToInt(damage).ToString(), worldPos, 1f, instance.fullColor);
        }

        public static void Spawn(DamageSystem target, float applied, Vector3 worldPos)
        {
            if (instance == null) return;
            if (target == null)
            {
                Spawn(applied, worldPos);
                return;
            }

            instance.ShowHit(target.LastHit, applied, worldPos);
        }

        private void ShowHit(HitResolution hit, float applied, Vector3 pos)
        {
            if (hit.grade == HitGrade.Evaded) { Show("Evaded", pos, wordScale, evadeColor); return; }
            if (hit.grade == HitGrade.Parried) { Show("Parried", pos, wordScale, parryColor); return; }
            if (hit.grade == HitGrade.Blocked) { Show("Blocked", pos, wordScale, guardColor); return; }

            bool glancing = hit.grade == HitGrade.Glancing;
            float scale = glancing ? glancingScale : hit.counter ? counterScale : 1f;
            Color color = glancing ? glancingColor : hit.counter ? counterColor : fullColor;

            string text = Mathf.RoundToInt(applied).ToString();
            if (hit.advantage) text = $"<size=55%>ADV</size> {text}";
            if (hit.counter) text += " <size=55%>COUNTER</size>";
            if (glancing) text += " <size=55%>glance</size>";
            if (hit.soak > 0f) text += $" <size=50%><color=#{ColorUtility.ToHtmlStringRGB(soakColor)}>-{Mathf.RoundToInt(hit.soak)}</color></size>";
            if (hit.bonus > 0f) text += $"\n<size=55%>+{Mathf.RoundToInt(hit.bonus)} {hit.bonusSource}</size>";

            Show(text, pos, scale, color);
        }

        private void ShowStatus(StatusSystem bearer, StatusDefinition status, int stacks)
        {
            if (this == null || bearer == null || status == null || status.HasTag("hitstate")) return;

            string name = string.IsNullOrEmpty(status.displayName) ? status.id : status.displayName;
            string text = stacks > 1 ? $"{name} ×{stacks}" : name;
            Show(text, StatusPoint(bearer), statusScale, status.beneficial ? beneficialColor : harmfulColor);
        }

        // "Resisted X" when immunity, resistance or diminishing returns refused it; "Saved X" when a save weakened it.
        private void ShowWord(StatusSystem bearer, StatusDefinition status, string word)
        {
            if (this == null || bearer == null || status == null) return;

            string name = string.IsNullOrEmpty(status.displayName) ? status.id : status.displayName;
            Show($"{word} {name}", StatusPoint(bearer), statusScale, resistedColor);
        }

        private Vector3 StatusPoint(StatusSystem bearer)
        {
            ControllerBrain brain = bearer.GetComponentInParent<ControllerBrain>();
            VFXSystem vfx = brain != null ? brain.GetModule<VFXSystem>() : null;
            Vector3 overhead = vfx != null ? vfx.GetAnchorPosition(VFXAnchor.Overhead) : bearer.transform.root.position + Vector3.up * 1.8f;
            return overhead + Vector3.up * statusHeight;
        }

        private void Show(string text, Vector3 pos, float scale, Color color)
        {
            if (damageNumberPrefab == null) return;

            DamageNumber number = Instantiate(damageNumberPrefab);
            number.Init(text, pos, scale, color);
        }
    }
}
