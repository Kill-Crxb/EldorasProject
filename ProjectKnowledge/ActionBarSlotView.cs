using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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

    void Awake() => EnsureComponents();

    public void Setup(string barId, int index,
                      HotbarSystem hotbar,
                      SlotTransformationSystem transforms,
                      string keybindLabel = "")
    {
        this.barId = barId;
        this.slotIndex = index;
        this.hotbarSystem = hotbar;
        this.transformSystem = transforms;

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
        }

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