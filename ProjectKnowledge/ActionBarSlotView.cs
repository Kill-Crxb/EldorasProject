using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Per-button view inside an action bar.
///
/// Visual layers (all optional — auto-created if missing in the prefab):
///   baseIconImage     — base ability icon, dimmed to 40% alpha while override is active
///   overrideIconImage — override ability icon, hidden when no override
///   cooldownOverlay   — Radial360 fill tracking effective ability cooldown (60Hz)
///   overrideTimerRing — Radial360 fill tracking override expiry window
///   overrideBorder    — colour-coded ring whose tint signals TransformationType
///   slotNumberText    — slot index (1-based); alpha=0 in prefab until keybinds exist
///
/// Never calls HotbarSystem directly for "what to show" — always routes through
/// SlotTransformationSystem.GetEffectiveAbility (single query point rule).
/// </summary>
public class ActionBarSlotView : MonoBehaviour,
    IPointerClickHandler,
    IDropHandler
{
    [Header("Visuals (auto-created if null)")]
    [SerializeField] private Image            baseIconImage;
    [SerializeField] private Image            overrideIconImage;
    [SerializeField] private Image            cooldownOverlay;
    [SerializeField] private Image            overrideTimerRing;
    [SerializeField] private Image            overrideBorder;
    [SerializeField] private TextMeshProUGUI  slotNumberText;

    [Header("Colors")]
    [SerializeField] private Color emptySlotColor    = new Color(0.12f, 0.12f, 0.12f, 0.90f);
    [SerializeField] private Color assignedSlotColor = new Color(0.20f, 0.20f, 0.20f, 0.95f);
    [SerializeField] private Color cooldownTint      = new Color(0f,    0f,    0f,    0.55f);

    // Override border colours keyed by TransformationType
    private static readonly Color[] s_borderColors =
    {
        new Color(0.937f, 0.624f, 0.153f), // HitProc           — amber  #EF9F27
        new Color(0.114f, 0.620f, 0.459f), // SequencePrereq    — teal   #1D9E75
        new Color(0.216f, 0.541f, 0.867f), // BuffStatus        — blue   #378ADD
        new Color(0.886f, 0.294f, 0.290f), // ResourceThreshold — red    #E24B4A
        new Color(0.388f, 0.600f, 0.133f), // TargetCondition   — green  #639922
    };

    // ── State ────────────────────────────────────────────────────────────

    private string barId;
    private int    slotIndex;
    private HotbarSystem            hotbarSystem;
    private SlotTransformationSystem transformSystem;
    private AbilitySystem           abilitySystem;  // cached in Setup — never queried per-frame via FindFirstObjectByType

    // ── Init ─────────────────────────────────────────────────────────────

    void Awake() => EnsureComponents();

    /// <summary>Called by ActionBarView after spawning. Caches all needed refs.</summary>
    public void Setup(string barId, int index,
                      HotbarSystem hotbar,
                      SlotTransformationSystem transforms)
    {
        this.barId          = barId;
        this.slotIndex      = index;
        this.hotbarSystem   = hotbar;
        this.transformSystem = transforms;

        // Resolve AbilitySystem once from the same brain that owns the hotbar
        var playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        abilitySystem = playerBrain?.GetModule<AbilitySystem>();

        if (slotNumberText != null)
        {
            slotNumberText.text  = (index + 1).ToString();
            slotNumberText.alpha = 0f; // hidden until keybinds are wired
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    public void Refresh()
    {
        if (hotbarSystem == null) return;

        var slot        = hotbarSystem.GetSlot(barId, slotIndex);
        var baseAbility = hotbarSystem.ResolveSlotAbility(slot);
        bool hasBase    = baseAbility != null;

        bool hasOverride        = transformSystem != null && transformSystem.HasOverride(barId, slotIndex);
        AbilityDefinition eff   = transformSystem != null
            ? transformSystem.GetEffectiveAbility(barId, slotIndex)
            : baseAbility;

        // Background
        GetComponent<Image>().color = hasBase ? assignedSlotColor : emptySlotColor;

        // Base icon — dimmed while override active
        if (baseIconImage != null)
        {
            baseIconImage.sprite  = baseAbility?.icon;
            baseIconImage.enabled = hasBase;
            var c = baseIconImage.color;
            c.a = hasOverride ? 0.4f : 1f;
            baseIconImage.color = c;
        }

        // Override icon
        if (overrideIconImage != null)
        {
            overrideIconImage.sprite  = hasOverride ? eff?.icon : null;
            overrideIconImage.enabled = hasOverride && eff?.icon != null;
        }

        // Override border colour
        if (overrideBorder != null)
        {
            overrideBorder.enabled = hasOverride;
            if (hasOverride)
            {
                var ov  = transformSystem.GetActiveOverride(barId, slotIndex);
                int idx = ov != null ? (int)ov.type : 0;
                overrideBorder.color = s_borderColors[Mathf.Clamp(idx, 0, s_borderColors.Length - 1)];
            }
        }

        // Reset cooldown fill (Update drives the actual animation)
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (overrideTimerRing != null) overrideTimerRing.enabled = false;
    }

    // ── Update ────────────────────────────────────────────────────────────

    void Update()
    {
        if (abilitySystem == null || transformSystem == null || hotbarSystem == null) return;

        // Cooldown — effective ability (override or base)
        var eff = transformSystem.GetEffectiveAbility(barId, slotIndex);
        if (cooldownOverlay != null && eff != null)
        {
            float remaining = abilitySystem.GetAbilityCooldownRemaining(eff.abilityId);
            float total     = abilitySystem.GetAbilityMaxCooldown(eff.abilityId);
            cooldownOverlay.fillAmount = total > 0f ? remaining / total : 0f;
        }
        else if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
        }

        // Override timer ring
        if (overrideTimerRing != null)
        {
            var ov = transformSystem.GetActiveOverride(barId, slotIndex);
            if (ov != null && ov.expiresAt > 0f)
            {
                float total   = ov.expiresAt - ov.appliedAt;
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

    // ── Input ─────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            hotbarSystem?.ClearSlot(barId, slotIndex);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (hotbarSystem == null) return;

        var draggable = eventData.pointerDrag?.GetComponent<IHotbarDraggable>();
        if (draggable == null) return;

        if (draggable.Ability != null)
            hotbarSystem.AssignSlot(barId, slotIndex, draggable.Ability);
        else if (draggable.Item != null)
            hotbarSystem.AssignItemSlot(barId, slotIndex, draggable.Item);
    }

    // ── Auto-create visuals ───────────────────────────────────────────────

    private void EnsureComponents()
    {
        // Root background image
        if (GetComponent<Image>() == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = emptySlotColor;
        }

        baseIconImage    ??= CreateChildImage("BaseIcon",    new Vector2(4, 4), new Vector2(-4, -4));
        overrideIconImage ??= CreateChildImage("OverrideIcon", new Vector2(4, 4), new Vector2(-4, -4));

        cooldownOverlay ??= CreateRadialImage("Cooldown", cooldownTint);
        overrideTimerRing ??= CreateRadialImage("OverrideTimer", new Color(1f, 1f, 1f, 0.6f));

        if (overrideBorder == null)
        {
            overrideBorder = CreateChildImage("OverrideBorder", Vector2.zero, Vector2.zero);
            overrideBorder.enabled = false;
        }
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
        img.color      = color;
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillAmount = 0f;
        img.enabled    = false;
        return img;
    }
}
