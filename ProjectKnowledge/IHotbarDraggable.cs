/// <summary>
/// Implemented by anything that can be dragged onto a hotbar slot.
/// ActionBarSlotView.OnDrop checks for this interface.
/// Ability and Item are mutually exclusive — only one should be set per drag.
/// </summary>
public interface IHotbarDraggable
{
    AbilityDefinition Ability { get; } // null if dragging an item
    ItemInstance      Item    { get; } // null if dragging an ability
}
