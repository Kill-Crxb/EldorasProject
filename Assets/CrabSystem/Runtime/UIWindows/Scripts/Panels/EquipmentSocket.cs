using UnityEngine;

/// <summary>
/// One authored equipment socket. Tag it with a slot id in the inspector and it wires
/// itself to the player's EquipmentSystem — accepting drops, showing the equipped item
/// and unequipping on right-click, all handled by the EquipmentItemIcon it drives.
/// Equipping goes straight through EquipmentSystem, which already owns persistence.
/// </summary>
public class EquipmentSocket : MonoBehaviour
{
    #region Inspector

    [Header("Slot")]
    [Tooltip("Slot id this socket represents, e.g. 'helmet', 'bodyarmor', 'mainwep'.")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string slotId;

    [Header("Visual")]
    [Tooltip("Icon that shows the equipped item. Found in children if left empty.")]
    [SerializeField] private EquipmentItemIcon icon;

    [Tooltip("Shown while the socket is empty.")]
    [SerializeField] private Sprite emptyIcon;

    #endregion

    public string SlotId => slotId;
    public EquipmentSlotDefinition Slot { get; private set; }

    private EquipmentSystem equipment;

    /// <summary>Called by EquipmentPanelView once the player is known.</summary>
    public void Bind(EquipmentSystem system)
    {
        Unbind();

        if (system == null) return;

        if (string.IsNullOrEmpty(slotId))
        {
            Debug.LogWarning($"[EquipmentSocket] {name} has no slot id");
            return;
        }

        Slot = system.GetSlotDefinition(slotId);

        if (Slot == null)
        {
            Debug.LogWarning($"[EquipmentSocket] {name}: no slot definition for '{slotId}'. " +
                             $"Available: {string.Join(", ", system.GetSlotIds())}");
            return;
        }

        equipment = system;

        if (!ResolveIcon()) return;

        icon.Initialize(new EquipmentSlotConfig { slotDefinition = Slot, emptySlotIcon = emptyIcon }, system);
        equipment.OnEquipmentChanged += HandleEquipmentChanged;

        Refresh();
    }

    public void Unbind()
    {
        if (equipment == null) return;

        equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        equipment = null;
    }

    private void OnDestroy() => Unbind();

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        if (slot == null || Slot == null) return;
        if (slot.slotId != Slot.slotId) return;

        icon.SetItem(item);
    }

    private void Refresh()
    {
        icon.SetItem(equipment.GetEquippedItem(Slot));
    }

    /// <summary>
    /// Uses an authored icon if there is one, otherwise builds it. The icon must be its own
    /// child so it layers over the socket art instead of replacing the background sprite.
    /// </summary>
    private bool ResolveIcon()
    {
        if (icon == null) icon = GetComponentInChildren<EquipmentItemIcon>(true);
        if (icon == null) icon = CreateIcon();

        return icon != null;
    }

    private EquipmentItemIcon CreateIcon()
    {
        var holder = new GameObject("ItemIcon", typeof(RectTransform));
        holder.transform.SetParent(transform, false);

        var rect = (RectTransform)holder.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return holder.AddComponent<EquipmentItemIcon>();
    }
}
