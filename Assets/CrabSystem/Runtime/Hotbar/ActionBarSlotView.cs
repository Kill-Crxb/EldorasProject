using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace CrabSystem
{
    public class ActionBarSlotView : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        [Header("Visuals (auto-created if null)")]
        [SerializeField] private Image baseIconImage;
        [SerializeField] private Image overrideIconImage;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Image overrideTimerRing;
        [SerializeField] private Image overrideBorder;
        [SerializeField] private TextMeshProUGUI slotNumberText;

        [Header("Colors")]
        [SerializeField] private Color emptySlotColor = new Color(0.12f, 0.12f, 0.12f, 0.90f);
        [SerializeField] private Color assignedSlotColor = new Color(0.20f, 0.20f, 0.20f, 0.95f);
        [SerializeField] private Color cooldownTint = new Color(0f, 0f, 0f, 0.55f);

        [Header("Tells")]
        [Tooltip("GI-49: the slot dips to this scale on a press and springs back.")]
        [SerializeField] private float pressScale = 0.88f;
        [SerializeField] private float pressSeconds = 0.12f;
        [Tooltip("GI-25: while a press is held for the first free frame the slot pulses this much.")]
        [SerializeField] private float bufferedPulse = 0.06f;
        [SerializeField] private float bufferedPulseSpeed = 18f;
        [Tooltip("GI-26: a white flash when the ability comes off cooldown or a proc lands on the slot.")]
        [SerializeField] private Color readyFlashColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private float readyFlashSeconds = 0.25f;

        private static readonly Color[] s_borderColors =
        {
            new Color(0.937f, 0.624f, 0.153f),
            new Color(0.114f, 0.620f, 0.459f),
            new Color(0.216f, 0.541f, 0.867f),
            new Color(0.886f, 0.294f, 0.290f),
            new Color(0.388f, 0.600f, 0.133f),
        };

        private string barId;
        private int slotIndex;
        private ControllerBrain playerBrain;
        private HotbarSystem hotbarSystem;
        private SlotTransformationSystem transformSystem;
        private AbilitySystem abilitySystem;

        private Image readyFlash;
        private float pressedAt = -99f;
        private float flashedAt = -99f;
        private bool wasCooling;
        private bool hadOverride;

        void Awake() => EnsureComponents();

        void OnDestroy()
        {
            if (hotbarSystem != null) hotbarSystem.OnSlotPressed -= HandlePressed;
        }

        private void HandlePressed(string pressedBar, int pressedIndex)
        {
            if (pressedBar == barId && pressedIndex == slotIndex) pressedAt = Time.unscaledTime;
        }

        public void Setup(string barId, int index,
                          HotbarSystem hotbar,
                          SlotTransformationSystem transforms,
                          string keybindLabel = "")
        {
            this.barId = barId;
            this.slotIndex = index;
            if (this.hotbarSystem != null) this.hotbarSystem.OnSlotPressed -= HandlePressed;
            this.hotbarSystem = hotbar;
            this.transformSystem = transforms;
            if (hotbar != null) hotbar.OnSlotPressed += HandlePressed;

            playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
            abilitySystem = playerBrain?.GetModule<AbilitySystem>();

            if (slotNumberText != null)
            {
                bool hasLabel = !string.IsNullOrEmpty(keybindLabel);
                slotNumberText.text = hasLabel ? keybindLabel : (index + 1).ToString();
                slotNumberText.alpha = hasLabel ? 1f : 0f;
            }
        }

        public void Refresh()
        {
            if (hotbarSystem == null) return;

            var slot = hotbarSystem.GetSlot(barId, slotIndex);
            var baseAbility = hotbarSystem.ResolveSlotAbility(slot);
            bool hasBase = baseAbility != null;
            bool hasOverride = transformSystem != null && transformSystem.HasOverride(barId, slotIndex);
            var effectiveAbility = transformSystem?.GetEffectiveAbility(barId, slotIndex) ?? baseAbility;

            GetComponent<Image>().color = hasBase ? assignedSlotColor : emptySlotColor;
            UpdateIconImage(baseIconImage, baseAbility, hasBase && !hasOverride);
            UpdateIconImage(overrideIconImage, effectiveAbility, hasOverride && effectiveAbility?.icon != null);
            UpdateOverrideBorder(hasOverride, transformSystem?.GetActiveOverride(barId, slotIndex));

            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
            if (overrideTimerRing != null) overrideTimerRing.enabled = false;
        }

        void Update()
        {
            if (abilitySystem == null || transformSystem == null || hotbarSystem == null) return;

            var eff = transformSystem.GetEffectiveAbility(barId, slotIndex);
            if (cooldownOverlay != null && eff != null)
            {
                float remaining = abilitySystem.GetAbilityCooldownRemaining(eff.abilityId);
                float total = abilitySystem.GetAbilityMaxCooldown(eff.abilityId);
                cooldownOverlay.fillAmount = total > 0f ? remaining / total : 0f;

                bool cooling = remaining > 0f;
                if (wasCooling && !cooling) flashedAt = Time.unscaledTime;
                wasCooling = cooling;
            }

            bool hasOverride = transformSystem.HasOverride(barId, slotIndex);
            if (hasOverride && !hadOverride) flashedAt = Time.unscaledTime;
            hadOverride = hasOverride;

            UpdateTells();

            if (overrideTimerRing != null)
            {
                var ov = transformSystem.GetActiveOverride(barId, slotIndex);
                if (ov != null && ov.expiresAt > 0f)
                {
                    float total = ov.expiresAt - ov.appliedAt;
                    float elapsed = Time.time - ov.appliedAt;
                    overrideTimerRing.fillAmount = total > 0f ? 1f - (elapsed / total) : 0f;
                    overrideTimerRing.enabled = true;
                }
                else
                {
                    overrideTimerRing.enabled = false;
                }
            }
        }

        // Press dip, buffered pulse and the ready flash, all on the slot's own scale and one overlay.
        private void UpdateTells()
        {
            float sincePress = Time.unscaledTime - pressedAt;
            float dip = sincePress < pressSeconds ? Mathf.Lerp(pressScale, 1f, sincePress / pressSeconds) : 1f;
            float pulse = hotbarSystem.IsBuffered(barId, slotIndex) ? 1f + bufferedPulse * Mathf.Abs(Mathf.Sin(Time.unscaledTime * bufferedPulseSpeed)) : 1f;
            transform.localScale = Vector3.one * (dip * pulse);

            if (readyFlash == null) return;
            float sinceFlash = Time.unscaledTime - flashedAt;
            bool flashing = sinceFlash < readyFlashSeconds;
            readyFlash.enabled = flashing;
            if (!flashing) return;

            Color color = readyFlashColor;
            color.a *= 1f - sinceFlash / readyFlashSeconds;
            readyFlash.color = color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                hotbarSystem?.ClearSlot(barId, slotIndex);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null || hotbarSystem == null) return;

            GameObject draggedGO = eventData.pointerDrag;
            var draggable = draggedGO.GetComponent<IHotbarDraggable>();

            if (draggable == null)
                return;

            if (draggable.Ability != null)
            {
                hotbarSystem.AssignSlot(barId, slotIndex, draggable.Ability);
                GameEvents.SaveRequested();
                Refresh();
                return;
            }

            if (draggable.Item != null)
            {
                hotbarSystem.AssignItemSlot(barId, slotIndex, draggable.Item.instanceId);
                GameEvents.SaveRequested();
                Refresh();
            }
        }

        private void UpdateIconImage(Image img, AbilityDefinition ability, bool show)
        {
            if (img == null) return;
            img.sprite = show ? ability?.icon : null;
            img.enabled = show && ability?.icon != null;

            if (show)
            {
                var c = img.color;
                c.a = 1f;
                img.color = c;
            }
        }

        private void UpdateOverrideBorder(bool hasOverride, SlotOverride ov)
        {
            if (overrideBorder == null) return;
            overrideBorder.enabled = hasOverride;

            if (hasOverride && ov != null)
            {
                int idx = (int)ov.type;
                overrideBorder.color = s_borderColors[Mathf.Clamp(idx, 0, s_borderColors.Length - 1)];
            }
        }

        private void EnsureComponents()
        {
            var rootImg = GetComponent<Image>();
            if (rootImg == null)
            {
                rootImg = gameObject.AddComponent<Image>();
                rootImg.color = emptySlotColor;
            }
            rootImg.raycastTarget = true;

            baseIconImage ??= CreateChildImage("BaseIcon", new Vector2(4, 4), new Vector2(-4, -4));
            overrideIconImage ??= CreateChildImage("OverrideIcon", new Vector2(4, 4), new Vector2(-4, -4));
            cooldownOverlay ??= CreateRadialImage("Cooldown", cooldownTint);
            overrideTimerRing ??= CreateRadialImage("OverrideTimer", new Color(1f, 1f, 1f, 0.6f));
            overrideBorder ??= CreateChildImage("OverrideBorder", Vector2.zero, Vector2.zero);
            readyFlash = CreateChildImage("ReadyFlash", Vector2.zero, Vector2.zero);

            if (overrideBorder != null)
                overrideBorder.enabled = false;
        }

        private Image CreateChildImage(string goName, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        private Image CreateRadialImage(string goName, Color color)
        {
            var img = CreateChildImage(goName, Vector2.zero, Vector2.zero);
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0f;
            img.enabled = false;
            return img;
        }
    }
}
