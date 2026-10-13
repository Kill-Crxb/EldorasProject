using UnityEngine;

// Marks a string field (or each element of a string list) as an id picked from an asset,
// not typed. The inspector shows a dropdown of the ids that exist, flags a missing one in red,
// and Tools > CrabSystem > Validate Ids lists every field pointing at an id that doesn't exist.
// Inspector only: no runtime cost, no data change.
public enum IdKind
{
    Fact,
    Stat,
    Ability,
    Item,
    Resource,
    Status,
    Faction,
    EquipmentSlot,
    Archetype,
    Model,
    Scene,
    AnimatorParam
}

public class IdRefAttribute : PropertyAttribute
{
    public readonly IdKind Kind;

    public IdRefAttribute(IdKind kind)
    {
        Kind = kind;
    }
}
